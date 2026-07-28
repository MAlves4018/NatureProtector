#!/usr/bin/env python3
"""Collect Phase 6 reliability and recovery evidence for NatureProtector.

Default mode is static and non-invasive. It inventories retry, quarantine,
idempotency, lease recovery, controlled fault cases, read-only audit packs,
telemetry and tests. Optional P3 execution is guarded by the dedicated safe
runner and is allowed only in Development/Evidence after explicit acknowledgement.
Existing PostgreSQL audit outputs can be ingested and independently summarized.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import platform
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Sequence

SCRIPT_VERSION = "1.0.0"
STATIC_CLASS = "STATIC_RELIABILITY_CONTRACT"
CURRENT_CLASS = "CURRENT_CONTROLLED_VALIDATION_EXECUTION"
AUDIT_CLASS = "CURRENT_POSTGRES_READ_ONLY_AUDIT"
HISTORICAL_CLASS = "HISTORICAL_REPOSITORY_EVIDENCE"


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def compact_utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace") if path.exists() else ""


def safe_rel(path: Path, root: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve())


def normalize(value: Any) -> Any:
    if value is None:
        return ""
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (list, tuple, set)):
        return "; ".join(str(item) for item in value)
    if isinstance(value, dict):
        return json.dumps(value, ensure_ascii=False, sort_keys=True)
    return value


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_csv(path: Path, rows: Sequence[dict[str, Any]], fieldnames: Sequence[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow({field: normalize(row.get(field)) for field in fieldnames})


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def parse_int(text: str, pattern: str, default: int = 0) -> int:
    match = re.search(pattern, text, re.S)
    return int(match.group(1)) if match else default


def parse_string_array(text: str, pattern: str) -> list[int]:
    match = re.search(pattern, text, re.S)
    if not match:
        return []
    return [int(value) for value in re.findall(r"\d+", match.group(1))]


def collect_retry_policy(repo: Path) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    options_path = repo / "src/NatureProtector.Prevention.Host/Configuration/PreventionHostOptions.cs"
    settings_path = repo / "src/NatureProtector.Prevention.Host/appsettings.json"
    options_text = read_text(options_path)
    settings = json.loads(read_text(settings_path))
    cfg = settings.get("PreventionHost", {})
    max_attempts = int(
        cfg.get("MaxProcessingAttempts", parse_int(options_text, r"MaxProcessingAttempts\s*\{[^}]+\}\s*=\s*(\d+)", 3))
    )
    delays = [
        int(value)
        for value in cfg.get(
            "RetryDelaySeconds", parse_string_array(options_text, r"RetryDelaySeconds[^=]+=\s*\[([^]]+)\]")
        )
    ]
    polling = int(cfg.get("RetryPollingIntervalSeconds", 5))
    lease = int(
        cfg.get(
            "ProcessingLeaseTimeoutSeconds",
            parse_int(options_text, r"ProcessingLeaseTimeoutSeconds[^=]+=\s*(\d+)", 300),
        )
    )
    prefetch = int(cfg.get("ConsumerPrefetchCount", 1))
    rows: list[dict[str, Any]] = []
    for attempt in range(1, max_attempts + 1):
        retry_allowed = attempt < max_attempts
        delay = delays[min(attempt - 1, len(delays) - 1)] if retry_allowed and delays else 0
        rows.append(
            {
                "attemptNumber": attempt,
                "retryAllowedForRetryableFailure": retry_allowed,
                "configuredDelaySeconds": delay if retry_allowed else "",
                "nextDisposition": "RetryPending" if retry_allowed else "Quarantined(retries_exhausted)",
                "pollingIntervalSeconds": polling,
                "evidenceClass": STATIC_CLASS,
                "source": safe_rel(settings_path, repo),
            }
        )
    summary = {
        "maxProcessingAttempts": max_attempts,
        "retryDelaySeconds": delays,
        "retryPollingIntervalSeconds": polling,
        "processingLeaseTimeoutSeconds": lease,
        "consumerPrefetchCount": prefetch,
        "minimumConfiguredDelayBeforeAttempt3Seconds": sum(delays[: max(0, max_attempts - 1)]),
        "note": "Observed retry elapsed time can exceed configured delays because polling and scheduling add latency.",
    }
    return rows, summary


def extract_method_block(text: str, method_name: str) -> str:
    start = text.find(f"{method_name}()")
    if start < 0:
        return ""
    next_method = text.find("public static IReadOnlyList<ValidationFaultCase>", start + 10)
    return text[start : next_method if next_method >= 0 else len(text)]


def parse_fault_constants(repo: Path) -> dict[str, str]:
    text = read_text(
        repo / "src/NatureProtector.Simulator.Host/ControlledValidation/ControlledValidationFaultCaseIds.cs"
    )
    return dict(re.findall(r'public const string (\w+)\s*=\s*"([^"]+)";', text))


def parse_fault_method(repo: Path, method: str, phase: str) -> list[dict[str, Any]]:
    path = repo / "src/NatureProtector.Simulator.Host/ControlledValidation/ControlledValidationScenarioManifest.cs"
    text = read_text(path)
    block = extract_method_block(text, method)
    constants = parse_fault_constants(repo)
    rows: list[dict[str, Any]] = []
    pattern = re.compile(
        r"new\(\s*ControlledValidationFaultCaseIds\.(\w+),\s*"
        r"ControlledValidationFaultLayer\.(\w+),\s*"
        r"ControlledValidationExpectedOutcome\.(\w+),\s*"
        r'(?:(null)|"([^"]*)"),\s*'
        r'"([^"]*)"(.*?)\)\s*,?',
        re.S,
    )
    for match in pattern.finditer(block):
        tail = match.group(7)

        def optional_int(name: str, default: int | None) -> int | None:
            found = re.search(rf"{name}:\s*(\d+)", tail)
            return int(found.group(1)) if found else default

        profile = re.search(r'valueProfile:\s*"([^"]+)"', tail)
        expected_events = optional_int("expectedEvents", 1)
        expected_published = optional_int("expectedPublishedEvents", expected_events)
        expected_gap = optional_int("expectedCoverageGap", 0)
        rows.append(
            {
                "phase": phase,
                "faultCaseId": constants.get(match.group(1), match.group(1)),
                "faultLayer": match.group(2),
                "expectedOutcome": match.group(3),
                "expectedReasonCode": "" if match.group(4) else match.group(5),
                "expectedEvents": expected_events,
                "expectedPublishedEvents": expected_published,
                "expectedCoverageGap": expected_gap,
                "valueProfile": profile.group(1) if profile else "",
                "executionPolicy": "executable",
                "description": match.group(6),
                "evidenceClass": STATIC_CLASS,
                "source": safe_rel(path, repo),
            }
        )
    return rows


def collect_fault_cases(repo: Path) -> list[dict[str, Any]]:
    rows = []
    rows.extend(parse_fault_method(repo, "CreateDefaultP0FaultCases", "P0"))
    rows.extend(parse_fault_method(repo, "CreateDefaultP1FaultCases", "P1"))
    rows.extend(parse_fault_method(repo, "CreateDefaultP2FaultCases", "P2/P2Extended"))
    rows.extend(parse_fault_method(repo, "CreateDefaultP3NegativePipelineFaultCases", "P3NegativePipeline"))
    query_p2 = repo / "tools/data-audit/postgres/10_controlled_validation_p2.sql"
    query_p3 = repo / "tools/data-audit/postgres/11_controlled_validation_p3_negative_pipeline.sql"
    rows.append(
        {
            "phase": "P2Extended",
            "faultCaseId": "P2_TEMPORAL_OUT_OF_ORDER",
            "faultLayer": "TemporalQuality",
            "expectedOutcome": "Blocked",
            "expectedReasonCode": "blocked_ambiguous_temporal_semantics",
            "expectedEvents": 0,
            "expectedPublishedEvents": 0,
            "expectedCoverageGap": 0,
            "valueProfile": "",
            "executionPolicy": "blocked_not_implemented",
            "description": "Out-of-order temporal semantics remain explicitly blocked in the P2 query pack.",
            "evidenceClass": STATIC_CLASS,
            "source": safe_rel(query_p2, repo),
        }
    )
    for case_id, reason, description in (
        ("P3_QUARANTINE_SENSOR_INACTIVE", "sensor_inactive", "Blocked until a safe inactive-sensor fixture exists."),
        (
            "P3_QUARANTINE_SENSOR_AREA_MISMATCH",
            "sensor_area_mismatch",
            "Blocked until a safe second-area fixture exists.",
        ),
    ):
        rows.append(
            {
                "phase": "P3NegativePipeline",
                "faultCaseId": case_id,
                "faultLayer": "Processing",
                "expectedOutcome": "Quarantined",
                "expectedReasonCode": reason,
                "expectedEvents": 0,
                "expectedPublishedEvents": 0,
                "expectedCoverageGap": 0,
                "valueProfile": "",
                "executionPolicy": "blocked_needs_fixture",
                "description": description,
                "evidenceClass": STATIC_CLASS,
                "source": safe_rel(query_p3, repo),
            }
        )
    return rows


def collect_state_machine(repo: Path) -> list[dict[str, Any]]:
    source = "src/NatureProtector.Prevention.Host/Processing/PostgresReadingEventInbox.cs"
    return [
        {
            "fromState": "<not persisted>",
            "trigger": "valid unique envelope",
            "toState": "Processing",
            "attemptOutcome": "Started",
            "safetyProperty": "EventId uniqueness and a processing lease are persisted before risk processing.",
            "source": source,
            "evidenceClass": STATIC_CLASS,
        },
        {
            "fromState": "Processing",
            "trigger": "pipeline completes",
            "toState": "Processed",
            "attemptOutcome": "Succeeded",
            "safetyProperty": "Only the current started lease may complete the event.",
            "source": source,
            "evidenceClass": STATIC_CLASS,
        },
        {
            "fromState": "Processing",
            "trigger": "retryable failure and attempts remain",
            "toState": "RetryPending",
            "attemptOutcome": "RetryScheduled",
            "safetyProperty": "NextAttemptNotBefore is persisted with the classified error.",
            "source": source,
            "evidenceClass": STATIC_CLASS,
        },
        {
            "fromState": "RetryPending",
            "trigger": "retry due and worker acquires it",
            "toState": "Processing",
            "attemptOutcome": "Started(new attempt)",
            "safetyProperty": "Attempt number increments and processing is reused through the same service.",
            "source": source,
            "evidenceClass": STATIC_CLASS,
        },
        {
            "fromState": "Processing",
            "trigger": "permanent failure",
            "toState": "Quarantined",
            "attemptOutcome": "Quarantined",
            "safetyProperty": "No automatic retry is scheduled for permanent failures.",
            "source": source,
            "evidenceClass": STATIC_CLASS,
        },
        {
            "fromState": "Processing",
            "trigger": "retryable failure at MaxProcessingAttempts",
            "toState": "Quarantined",
            "attemptOutcome": "Quarantined",
            "safetyProperty": "Final code is retries_exhausted.",
            "source": source,
            "evidenceClass": STATIC_CLASS,
        },
        {
            "fromState": "RetryPending/Processing",
            "trigger": "persisted retry envelope cannot be deserialized",
            "toState": "Quarantined",
            "attemptOutcome": "Quarantined",
            "safetyProperty": "Malformed retry payload is isolated as invalid_retry_payload.",
            "source": source,
            "evidenceClass": STATIC_CLASS,
        },
        {
            "fromState": "Processing(stale lease)",
            "trigger": "lease exceeds ProcessingLeaseTimeoutSeconds",
            "toState": "Processing(new lease)",
            "attemptOutcome": "old attempt Failed(processing_lease_expired)",
            "safetyProperty": "Late completion/retry/quarantine from the expired lease is ignored.",
            "source": source,
            "evidenceClass": STATIC_CLASS,
        },
        {
            "fromState": "Existing EventId",
            "trigger": "identical payload replay",
            "toState": "unchanged",
            "attemptOutcome": "no additional effective processing",
            "safetyProperty": "Nominal duplicate is idempotent.",
            "source": source,
            "evidenceClass": STATIC_CLASS,
        },
        {
            "fromState": "Existing EventId",
            "trigger": "divergent duplicate payload",
            "toState": "unchanged + rejected record",
            "attemptOutcome": "rejected duplicate_payload_mismatch",
            "safetyProperty": "Divergent duplicate does not overwrite the original event.",
            "source": source,
            "evidenceClass": STATIC_CLASS,
        },
    ]


def collect_failure_classification(repo: Path) -> list[dict[str, Any]]:
    source = "src/NatureProtector.Prevention.Host/Processing/DefaultProcessingFailureClassifier.cs"
    rows = [
        (
            "ControlledValidationProcessingFaultException",
            "fault-provided",
            "fault-provided",
            "Controlled test-only injection",
        ),
        ("TimeoutException", "Transient", "timeout", "retryable"),
        ("HttpRequestException", "Transient", "http_request_failed", "retryable"),
        ("IOException", "Transient", "io_failed", "retryable"),
        ("ArgumentException", "Permanent", "invalid_argument", "quarantine"),
        ("FormatException", "Permanent", "invalid_format", "quarantine"),
        ("InvalidDataException", "Permanent", "invalid_data", "quarantine"),
        ("NotSupportedException", "Permanent", "not_supported", "quarantine"),
        (
            "OperationCanceledException",
            "Transient",
            "operation_cancelled",
            "retryable unless host cancellation path handles it first",
        ),
        ("DbUpdateException without PostgreSQL detail", "Transient", "db_update_failed", "retryable"),
        ("PostgreSQL foreign-key violation", "Permanent", "db_foreign_key_violation", "quarantine"),
        ("PostgreSQL unique violation", "Permanent", "db_unique_violation", "quarantine"),
        ("PostgreSQL check violation", "Permanent", "db_check_violation", "quarantine"),
        ("PostgreSQL not-null violation", "Permanent", "db_not_null_violation", "quarantine"),
        ("PostgreSQL serialization failure", "Transient", "db_serialization_failure", "retryable"),
        ("PostgreSQL deadlock detected", "Transient", "db_deadlock_detected", "retryable"),
        ("PostgreSQL lock not available", "Transient", "db_lock_not_available", "retryable"),
        ("PostgreSQL SQLSTATE 08*", "Transient", "db_connection_failed", "retryable"),
        ("PostgreSQL SQLSTATE 22*", "Permanent", "db_data_exception", "quarantine"),
        ("PostgreSQL SQLSTATE 23*", "Permanent", "db_integrity_constraint_violation", "quarantine"),
        ("Other exception", "Unknown", "processing_failed", "retryable until attempts are exhausted"),
    ]
    return [
        {"match": a, "kind": b, "errorCode": c, "policy": d, "source": source, "evidenceClass": STATIC_CLASS}
        for a, b, c, d in rows
    ]


def collect_telemetry(repo: Path) -> list[dict[str, Any]]:
    path = repo / "src/NatureProtector.Shared.Observability/Observability/HostTelemetry.cs"
    text = read_text(path)
    names = {
        "ReceivedEvents",
        "ValidatedEvents",
        "RejectedEvents",
        "AckedEvents",
        "ProcessedEvents",
        "RetryScheduledEvents",
        "QuarantinedEvents",
        "RetryPickedEvents",
        "InboxStoreDurationMs",
        "ProcessingDurationMs",
    }
    rows = []
    pattern = re.compile(
        r"public static readonly (Counter|Histogram)<[^>]+> (\w+) = Meter\.Create(?:Counter|Histogram)<[^>]+>\(\"([^\"]+)\"(?:, unit: \"([^\"]+)\")?"
    )
    for kind, symbol, metric, unit in pattern.findall(text):
        if symbol in names:
            rows.append(
                {
                    "symbol": symbol,
                    "instrumentType": kind,
                    "metricName": metric,
                    "unit": unit,
                    "source": safe_rel(path, repo),
                    "evidenceClass": STATIC_CLASS,
                }
            )
    return rows


def collect_tests(repo: Path) -> list[dict[str, Any]]:
    roots = [
        repo / "tests/NatureProtector.Prevention.Host.Tests",
        repo / "tests/NatureProtector.Simulator.Host.Tests/ControlledValidation",
        repo / "tests/NatureProtector.IntegrationTests/Flow",
    ]
    keywords = re.compile(r"retry|quarant|inbox|processing|controlledvalidation|rabbitmq|publishedruntime", re.I)
    rows: list[dict[str, Any]] = []
    for root in roots:
        if not root.exists():
            continue
        for path in sorted(root.rglob("*.cs")):
            if not keywords.search(path.name):
                continue
            text = read_text(path)
            facts = len(re.findall(r"\[Fact(?:\([^]]*\))?\]", text))
            theories = len(re.findall(r"\[Theory(?:\([^]]*\))?\]", text))
            inline = len(re.findall(r"\[InlineData", text))
            if facts + theories == 0:
                continue
            rows.append(
                {
                    "testFile": safe_rel(path, repo),
                    "facts": facts,
                    "theories": theories,
                    "inlineData": inline,
                    "declaredTestMethods": facts + theories,
                    "executionStatus": "NOT_EXECUTED_IN_PHASE_6_ANALYSIS_ENVIRONMENT",
                    "evidenceClass": STATIC_CLASS,
                }
            )
    return rows


def collect_query_packs(repo: Path) -> list[dict[str, Any]]:
    rows = []
    for number, phase, filename in (
        (8, "P0", "08_controlled_validation_p0.sql"),
        (9, "P1", "09_controlled_validation_p1.sql"),
        (10, "P2/P2Extended", "10_controlled_validation_p2.sql"),
        (11, "P3NegativePipeline", "11_controlled_validation_p3_negative_pipeline.sql"),
    ):
        path = repo / "tools/data-audit/postgres" / filename
        text = read_text(path)
        outputs = re.findall(r"^\\set\s+\w+\s+:out_dir\s+'?/([^'\s]+)'?", text, re.M)
        forbidden = []
        for label, pattern in (
            ("insert", r"\binsert\s+into\b"),
            ("update", r"\bupdate\s+"),
            ("delete", r"\bdelete\s+from\b"),
            ("truncate", r"\btruncate\b"),
            ("drop", r"\bdrop\s+(?:table|schema|database)\b"),
        ):
            if re.search(pattern, text, re.I):
                forbidden.append(label)
        rows.append(
            {
                "pack": number,
                "phase": phase,
                "file": safe_rel(path, repo),
                "lineCount": len(text.splitlines()),
                "outputCount": len(outputs),
                "outputs": outputs,
                "readOnlyStaticCheck": not forbidden,
                "forbiddenStatements": forbidden,
                "evidenceClass": STATIC_CLASS,
            }
        )
    return rows


def probe_json(url: str, token: str | None, timeout: int = 5) -> dict[str, Any]:
    headers = {"Accept": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    req = urllib.request.Request(url=url, headers=headers, method="GET")
    started = time.perf_counter()
    try:
        with urllib.request.urlopen(req, timeout=timeout) as response:
            raw = response.read().decode("utf-8", errors="replace")
            try:
                body = json.loads(raw) if raw else {}
            except json.JSONDecodeError:
                body = {"raw": raw[:2000]}
            return {
                "status": "PASS",
                "httpStatus": response.status,
                "durationSeconds": round(time.perf_counter() - started, 3),
                "body": body,
            }
    except urllib.error.HTTPError as exc:
        return {
            "status": "BLOCKED_HTTP",
            "httpStatus": exc.code,
            "durationSeconds": round(time.perf_counter() - started, 3),
        }
    except (urllib.error.URLError, TimeoutError, OSError) as exc:
        return {
            "status": "BLOCKED_API_UNAVAILABLE",
            "durationSeconds": round(time.perf_counter() - started, 3),
            "error": str(exc),
        }


def find_audit_root(value: Path) -> Path | None:
    if (value / "p3_expected_vs_observed.csv").exists():
        return value
    if (value / "postgres" / "p3_expected_vs_observed.csv").exists():
        return value / "postgres"
    for candidate in value.rglob("p3_expected_vs_observed.csv"):
        return candidate.parent
    return None


def read_csv_rows(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        return []
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def number(row: dict[str, str], key: str) -> int:
    try:
        return int(row.get(key, "0") or "0")
    except ValueError:
        return 0


def ingest_audit(audit_directory: Path | None, output: Path) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    if audit_directory is None:
        status = {"status": "NOT_PROVIDED", "evidenceClass": AUDIT_CLASS}
        write_json(output / "execution/postgres-audit-status.json", status)
        return status, []
    root = find_audit_root(audit_directory)
    if root is None:
        status = {
            "status": "FAIL_REQUIRED_FILES_MISSING",
            "providedDirectory": str(audit_directory),
            "evidenceClass": AUDIT_CLASS,
        }
        write_json(output / "execution/postgres-audit-status.json", status)
        return status, []
    expected = read_csv_rows(root / "p3_expected_vs_observed.csv")
    retries = read_csv_rows(root / "p3_retry_paths_by_fault_case.csv")
    unexpected_projection = read_csv_rows(root / "p3_unexpected_accepted_or_risk.csv")
    blocked = read_csv_rows(root / "p3_blocked_or_skipped_cases.csv")
    statuses = Counter(row.get("status", "") for row in expected)
    allowed = {"matched", "matched_with_setup_projection", "blocked_needs_fixture"}
    executable = [row for row in expected if row.get("execution_policy") != "blocked_needs_fixture"]
    matched = [row for row in executable if row.get("status") in {"matched", "matched_with_setup_projection"}]
    retry_bad = [row for row in retries if row.get("status") != "matched"]
    audit_pass = (
        len(expected) == 12
        and len(executable) == 10
        and len(matched) == 10
        and len(blocked) == 2
        and not unexpected_projection
        and not retry_bad
        and all(row.get("status") in allowed for row in expected)
    )
    case_rows: list[dict[str, Any]] = []
    for row in expected:
        case_rows.append({**row, "evidenceClass": AUDIT_CLASS, "sourceDirectory": str(root)})
    status = {
        "status": "PASS" if audit_pass else "FAIL",
        "auditRoot": str(root),
        "expectedCaseRows": len(expected),
        "executableCases": len(executable),
        "matchedExecutableCases": len(matched),
        "blockedCases": len(blocked),
        "unexpectedPositiveProjectionRows": len(unexpected_projection),
        "retryPathRows": len(retries),
        "retryPathUnexpectedRows": len(retry_bad),
        "statusCounts": dict(statuses),
        "totals": {
            "expectedPublishedEvents": sum(number(row, "expected_published_events") for row in expected),
            "inboxEvents": sum(number(row, "inbox_events") for row in expected),
            "rejectedEvents": sum(number(row, "rejected_events") for row in expected),
            "quarantinedEvents": sum(number(row, "quarantined_events") for row in expected),
            "acceptedReadings": sum(number(row, "accepted_readings") for row in expected),
            "riskAssessments": sum(number(row, "risk_assessments") for row in expected),
            "processingAttempts": sum(number(row, "processing_attempts") for row in expected),
            "retryScheduledAttempts": sum(number(row, "retry_scheduled_attempts") for row in expected),
        },
        "evidenceClass": AUDIT_CLASS,
    }
    write_json(output / "execution/postgres-audit-status.json", status)
    write_json(output / "execution/p3-case-results.json", case_rows)
    if case_rows:
        fields = list(case_rows[0].keys())
        write_csv(output / "execution/p3-case-results.csv", case_rows, fields)
    return status, case_rows


def run_p3(
    repo: Path, output: Path, api_base_url: str, run_label: str | None, timeout: int, acknowledge: bool
) -> dict[str, Any]:
    runner = repo / "scripts/reliability/run-controlled-validation-p3.py"
    run_label = (
        run_label or f"controlled-validation-p3-negative-pipeline-{datetime.now(timezone.utc):%Y%m%d-%H%M%S}-phase6"
    )
    command = [
        sys.executable,
        str(runner),
        "--api-base-url",
        api_base_url,
        "--run-label",
        run_label,
        "--timeout-seconds",
        str(timeout),
        "--output",
        str(output / "execution/p3-run"),
        "--execute",
    ]
    if acknowledge:
        command.append("--acknowledge-non-production")
    started = time.perf_counter()
    process = subprocess.run(command, cwd=repo, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    (output / "execution").mkdir(parents=True, exist_ok=True)
    (output / "execution/p3-runner.stdout.txt").write_text(process.stdout, encoding="utf-8")
    (output / "execution/p3-runner.stderr.txt").write_text(process.stderr, encoding="utf-8")
    status_path = output / "execution/p3-run/status.json"
    status = json.loads(read_text(status_path)) if status_path.exists() else {"status": "FAIL_STATUS_MISSING"}
    status.update(
        {
            "exitCode": process.returncode,
            "durationSeconds": round(time.perf_counter() - started, 3),
            "evidenceClass": CURRENT_CLASS,
        }
    )
    write_json(output / "execution/p3-execution-status.json", status)
    return status


def build_findings(
    repo: Path, retry_summary: dict[str, Any], fault_cases: list[dict[str, Any]]
) -> list[dict[str, Any]]:
    p3_exec = [
        row for row in fault_cases if row["phase"] == "P3NegativePipeline" and row["executionPolicy"] == "executable"
    ]
    p3_blocked = [
        row for row in fault_cases if row["phase"] == "P3NegativePipeline" and row["executionPolicy"] != "executable"
    ]
    return [
        {
            "id": "REL-001",
            "severity": "info",
            "finding": f"P3 defines {len(p3_exec)} executable fault cases and {len(p3_blocked)} blocked fixture-dependent cases; one duplicate-mismatch case emits two messages, giving 11 messages.",
            "impact": "Case count and message count must not be conflated.",
            "nextAction": "Present both values and preserve blocked cases as blocked.",
            "evidenceClass": STATIC_CLASS,
        },
        {
            "id": "REL-002",
            "severity": "high",
            "finding": "The P3 API endpoint does not execute query pack 11 even when RunAuditAfterCompletion is requested; its response keeps AuditRequired=true.",
            "impact": "A completed simulator process alone is insufficient to prove expected outcomes.",
            "nextAction": "Run tools/data-audit/run-postgres-audit.ps1 with the exact run label and ingest the CSV outputs.",
            "evidenceClass": STATIC_CLASS,
        },
        {
            "id": "REL-003",
            "severity": "medium",
            "finding": f"Retry policy allows {retry_summary['maxProcessingAttempts']} attempts with configured delays {retry_summary['retryDelaySeconds']} seconds and polling every {retry_summary['retryPollingIntervalSeconds']} seconds.",
            "impact": "Configured delay is not the same as observed recovery time.",
            "nextAction": "Report observed timestamps and polling overhead separately.",
            "evidenceClass": STATIC_CLASS,
        },
        {
            "id": "REL-004",
            "severity": "medium",
            "finding": "Expired processing-lease recovery and malformed retry quarantine exist and are covered by tests, but are not part of the current P3 runtime campaign.",
            "impact": "P3 cannot by itself prove crash/lease recovery.",
            "nextAction": "Add a bounded lease-expiry campaign after P3 is closed.",
            "evidenceClass": STATIC_CLASS,
        },
        {
            "id": "REL-005",
            "severity": "high",
            "finding": "P3 injects application-processing faults; it does not stop RabbitMQ, PostgreSQL or InfluxDB and does not restart the Prevention Host.",
            "impact": "Infrastructure outage recovery, backlog drain and service restart claims remain unproved.",
            "nextAction": "Use separate owner-controlled outage drills with data reconciliation.",
            "evidenceClass": STATIC_CLASS,
        },
        {
            "id": "REL-006",
            "severity": "medium",
            "finding": "P2_TEMPORAL_OUT_OF_ORDER remains blocked_not_implemented because the temporal semantics are ambiguous.",
            "impact": "Out-of-order handling must not be claimed from delayed-reading evidence.",
            "nextAction": "Define authority and ordering semantics before implementing the case.",
            "evidenceClass": STATIC_CLASS,
        },
        {
            "id": "REL-007",
            "severity": "medium",
            "finding": "The API has no general command for quarantine replay or manual retry maintenance.",
            "impact": "Operational recovery is currently isolation-oriented, not a complete operator recovery workflow.",
            "nextAction": "Document manual database/runbook boundaries or implement an audited replay command later.",
            "evidenceClass": STATIC_CLASS,
        },
    ]


def write_hash_manifest(root: Path) -> int:
    manifest = root / "SHA256SUMS.txt"
    files = [path for path in root.rglob("*") if path.is_file() and path != manifest]
    lines = [f"{sha256(path)}  {path.relative_to(root).as_posix()}" for path in sorted(files)]
    manifest.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return len(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--baseline-id", required=True)
    parser.add_argument("--run-id")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--api-base-url", default="http://localhost:5254")
    parser.add_argument("--execute-p3", action="store_true")
    parser.add_argument("--acknowledge-non-production", action="store_true")
    parser.add_argument("--p3-run-label")
    parser.add_argument("--timeout-seconds", type=int, default=300)
    parser.add_argument("--audit-directory", type=Path)
    parser.add_argument("--require-p3", action="store_true")
    parser.add_argument("--require-audit", action="store_true")
    parser.add_argument(
        "--no-latest-pointer",
        action="store_true",
        help="Do not update artifacts/report-evidence/<baseline>/06-reliability/LATEST.txt.",
    )
    args = parser.parse_args()

    repo = args.repo.resolve()
    run_id = args.run_id or compact_utc_now()
    output = (
        args.output.resolve()
        if args.output
        else repo / "artifacts/report-evidence" / args.baseline_id / "06-reliability" / run_id
    )
    output.mkdir(parents=True, exist_ok=True)
    for name in ("static", "execution", "report-ready"):
        (output / name).mkdir(parents=True, exist_ok=True)

    environment = {
        "generatedAtUtc": utc_now(),
        "baselineId": args.baseline_id,
        "phase6RunId": run_id,
        "scriptVersion": SCRIPT_VERSION,
        "platform": platform.platform(),
        "python": sys.version.split()[0],
        "repo": str(repo),
        "apiBaseUrl": args.api_base_url,
        "dotnetAvailable": shutil.which("dotnet") is not None,
        "dockerAvailable": shutil.which("docker") is not None,
        "powershellAvailable": shutil.which("pwsh") is not None or shutil.which("powershell") is not None,
        "psqlAvailable": shutil.which("psql") is not None,
    }
    write_json(output / "environment.json", environment)

    retry_rows, retry_summary = collect_retry_policy(repo)
    fault_cases = collect_fault_cases(repo)
    states = collect_state_machine(repo)
    classifications = collect_failure_classification(repo)
    telemetry = collect_telemetry(repo)
    tests = collect_tests(repo)
    query_packs = collect_query_packs(repo)
    findings = build_findings(repo, retry_summary, fault_cases)

    datasets = [
        (
            "retry-policy",
            retry_rows,
            [
                "attemptNumber",
                "retryAllowedForRetryableFailure",
                "configuredDelaySeconds",
                "nextDisposition",
                "pollingIntervalSeconds",
                "evidenceClass",
                "source",
            ],
        ),
        (
            "fault-case-catalog",
            fault_cases,
            [
                "phase",
                "faultCaseId",
                "faultLayer",
                "expectedOutcome",
                "expectedReasonCode",
                "expectedEvents",
                "expectedPublishedEvents",
                "expectedCoverageGap",
                "valueProfile",
                "executionPolicy",
                "description",
                "evidenceClass",
                "source",
            ],
        ),
        (
            "inbox-state-machine",
            states,
            ["fromState", "trigger", "toState", "attemptOutcome", "safetyProperty", "source", "evidenceClass"],
        ),
        (
            "failure-classification",
            classifications,
            ["match", "kind", "errorCode", "policy", "source", "evidenceClass"],
        ),
        (
            "reliability-telemetry",
            telemetry,
            ["symbol", "instrumentType", "metricName", "unit", "source", "evidenceClass"],
        ),
        (
            "reliability-test-inventory",
            tests,
            ["testFile", "facts", "theories", "inlineData", "declaredTestMethods", "executionStatus", "evidenceClass"],
        ),
        (
            "query-pack-catalog",
            query_packs,
            [
                "pack",
                "phase",
                "file",
                "lineCount",
                "outputCount",
                "outputs",
                "readOnlyStaticCheck",
                "forbiddenStatements",
                "evidenceClass",
            ],
        ),
        ("reliability-findings", findings, ["id", "severity", "finding", "impact", "nextAction", "evidenceClass"]),
    ]
    for name, rows, fields in datasets:
        write_json(output / f"static/{name}.json", rows)
        write_csv(output / f"static/{name}.csv", rows, fields)
    write_json(output / "static/retry-policy-summary.json", retry_summary)

    token = os.environ.get("NP_RELIABILITY_AUTH_TOKEN")
    health = probe_json(args.api_base_url.rstrip("/") + "/health", None)
    availability = probe_json(args.api_base_url.rstrip("/") + "/api/dev/controlled-validation/p3", token)
    write_json(output / "execution/environment-health-probe.json", health)
    write_json(output / "execution/p3-availability-probe.json", availability)

    if args.execute_p3:
        execution = run_p3(
            repo, output, args.api_base_url, args.p3_run_label, args.timeout_seconds, args.acknowledge_non_production
        )
    else:
        execution = {"status": "NOT_REQUESTED", "evidenceClass": CURRENT_CLASS}
        write_json(output / "execution/p3-execution-status.json", execution)

    audit_status, case_results = ingest_audit(args.audit_directory, output)

    static_pass = (
        len([r for r in fault_cases if r["phase"] == "P3NegativePipeline" and r["executionPolicy"] == "executable"])
        == 10
        and len(
            [
                r
                for r in fault_cases
                if r["phase"] == "P3NegativePipeline" and r["executionPolicy"] == "blocked_needs_fixture"
            ]
        )
        == 2
        and all(row["readOnlyStaticCheck"] for row in query_packs)
        and retry_summary["maxProcessingAttempts"] == 3
        and retry_summary["retryDelaySeconds"] == [5, 30]
    )
    current_status = (
        "PASS"
        if execution.get("status") == "PASS_AUDIT_REQUIRED" and audit_status.get("status") == "PASS"
        else (
            "PARTIAL_AUDIT_REQUIRED"
            if execution.get("status") == "PASS_AUDIT_REQUIRED"
            else execution.get("status", "NOT_REQUESTED")
        )
    )
    if static_pass and current_status == "PASS":
        phase_status = "PASS"
    elif static_pass:
        phase_status = "PARTIAL_PASS_BLOCKED_OR_NOT_EXECUTED"
    else:
        phase_status = "FAIL"

    counts = {
        "faultCasesTotal": len(fault_cases),
        "p0ExecutableCases": len(
            [r for r in fault_cases if r["phase"] == "P0" and r["executionPolicy"] == "executable"]
        ),
        "p1ExecutableCases": len(
            [r for r in fault_cases if r["phase"] == "P1" and r["executionPolicy"] == "executable"]
        ),
        "p2ExecutableCases": len(
            [r for r in fault_cases if r["phase"] == "P2/P2Extended" and r["executionPolicy"] == "executable"]
        ),
        "p2BlockedCases": len(
            [r for r in fault_cases if r["phase"] == "P2Extended" and r["executionPolicy"] != "executable"]
        ),
        "p3ExecutableCases": len(
            [r for r in fault_cases if r["phase"] == "P3NegativePipeline" and r["executionPolicy"] == "executable"]
        ),
        "p3BlockedCases": len(
            [r for r in fault_cases if r["phase"] == "P3NegativePipeline" and r["executionPolicy"] != "executable"]
        ),
        "stateTransitions": len(states),
        "failureClassificationRules": len(classifications),
        "reliabilityTelemetryInstruments": len(telemetry),
        "reliabilityTestFiles": len(tests),
        "declaredReliabilityTestMethods": sum(int(r["declaredTestMethods"]) for r in tests),
        "queryPacks": len(query_packs),
        "queryPackOutputs": sum(int(r["outputCount"]) for r in query_packs),
        "findings": len(findings),
    }
    capability_rows = [
        {
            "capability": "Retry policy and state transitions",
            "staticStatus": "PASS" if static_pass else "FAIL",
            "currentExecutionStatus": current_status,
            "claimCeiling": "Implementation and test inventory only until current P3 plus PostgreSQL audit pass.",
        },
        {
            "capability": "P3 controlled negative pipeline",
            "staticStatus": "PASS",
            "currentExecutionStatus": execution.get("status"),
            "claimCeiling": "Development/Evidence controlled execution; not production resilience.",
        },
        {
            "capability": "P3 PostgreSQL outcome audit",
            "staticStatus": "PASS_READ_ONLY_PACK",
            "currentExecutionStatus": audit_status.get("status"),
            "claimCeiling": "Run-specific outcomes only when exact run_label is used.",
        },
        {
            "capability": "Lease-expiry recovery",
            "staticStatus": "IMPLEMENTED_AND_TESTED_STATICALLY",
            "currentExecutionStatus": "NOT_IN_P3_CAMPAIGN",
            "claimCeiling": "No current runtime recovery-time result.",
        },
        {
            "capability": "RabbitMQ/PostgreSQL/Influx outage recovery",
            "staticStatus": "NOT_COVERED_BY_P3",
            "currentExecutionStatus": "NOT_EXECUTED",
            "claimCeiling": "No outage recovery or backlog-drain claim.",
        },
        {
            "capability": "Quarantine replay/manual recovery",
            "staticStatus": "NO_GENERAL_API_COMMAND",
            "currentExecutionStatus": "NOT_AVAILABLE",
            "claimCeiling": "Isolation is implemented; operator replay workflow is not proved.",
        },
    ]
    write_csv(
        output / "report-ready/reliability-capability-summary.csv",
        capability_rows,
        ["capability", "staticStatus", "currentExecutionStatus", "claimCeiling"],
    )
    write_json(output / "report-ready/reliability-capability-summary.json", capability_rows)

    summary = {
        "phase": 6,
        "baselineId": args.baseline_id,
        "runId": run_id,
        "phaseStatus": phase_status,
        "staticContractStatus": "PASS" if static_pass else "FAIL",
        "healthProbeStatus": health.get("status"),
        "availabilityProbeStatus": availability.get("status"),
        "p3ExecutionStatus": execution.get("status"),
        "postgresAuditStatus": audit_status.get("status"),
        "currentReliabilityStatus": current_status,
        "counts": counts,
        "retryPolicy": retry_summary,
        "audit": audit_status,
        "claimBoundaries": [
            "Static retry/quarantine implementation does not prove current runtime reliability.",
            "A P3 process result is incomplete until query pack 11 passes for the exact run label.",
            "P3 processing-fault injection does not prove RabbitMQ, PostgreSQL or InfluxDB outage recovery.",
            "Blocked fixture cases must remain blocked and cannot be promoted by inference.",
            "Configured retry delays are not observed recovery time.",
            "No event-loss rate may be claimed without complete run-specific reconciliation.",
        ],
    }
    write_json(output / "phase6-summary.json", summary)
    md = (
        [
            "# NatureProtector — Phase 6 reliability evidence",
            "",
            f"- Baseline: `{args.baseline_id}`",
            f"- Run: `{run_id}`",
            f"- Status: **{phase_status}**",
            f"- Static contract: **{summary['staticContractStatus']}**",
            f"- P3 execution: **{summary['p3ExecutionStatus']}**",
            f"- PostgreSQL audit: **{summary['postgresAuditStatus']}**",
            "",
            "## Static counts",
            "",
            f"- Fault cases: {counts['faultCasesTotal']} total; P3 has {counts['p3ExecutableCases']} executable and {counts['p3BlockedCases']} blocked.",
            f"- State transitions: {counts['stateTransitions']}.",
            f"- Failure-classification rules: {counts['failureClassificationRules']}.",
            f"- Reliability telemetry instruments: {counts['reliabilityTelemetryInstruments']}.",
            f"- Relevant test files/methods: {counts['reliabilityTestFiles']} / {counts['declaredReliabilityTestMethods']}.",
            f"- Read-only query packs/outputs: {counts['queryPacks']} / {counts['queryPackOutputs']}.",
            "",
            "## Claim ceiling",
            "",
        ]
        + [f"- {item}" for item in summary["claimBoundaries"]]
        + [""]
    )
    (output / "phase6-summary.md").write_text("\n".join(md), encoding="utf-8")

    hashed = write_hash_manifest(output)
    summary["hashedEvidenceFiles"] = hashed
    write_json(output / "phase6-summary.json", summary)
    hashed = write_hash_manifest(output)

    if not args.no_latest_pointer:
        latest_dir = repo / "artifacts/report-evidence" / args.baseline_id / "06-reliability"
        latest_dir.mkdir(parents=True, exist_ok=True)
        (latest_dir / "LATEST.txt").write_text(str(output) + "\n", encoding="utf-8")

    print(f"PHASE_6_STATUS={phase_status}")
    print(f"STATIC_RELIABILITY_CONTRACT_STATUS={summary['staticContractStatus']}")
    print(f"P3_AVAILABILITY_PROBE_STATUS={summary['availabilityProbeStatus']}")
    print(f"P3_EXECUTION_STATUS={summary['p3ExecutionStatus']}")
    print(f"POSTGRES_AUDIT_STATUS={summary['postgresAuditStatus']}")
    print(f"EVIDENCE_ROOT={output}")
    print(f"HASHED_FILE_COUNT={hashed}")

    if args.require_p3 and current_status not in {"PASS", "PARTIAL_AUDIT_REQUIRED"}:
        return 2
    if args.require_audit and audit_status.get("status") != "PASS":
        return 2
    return 0 if static_pass else 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"PHASE_6_COLLECTION=FAIL: {exc}", file=sys.stderr)
        raise
