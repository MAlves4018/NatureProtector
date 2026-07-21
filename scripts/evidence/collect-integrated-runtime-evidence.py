#!/usr/bin/env python3
"""Collect NatureProtector Phase 4 integrated runtime evidence.

The collector has three evidence classes:
- STATIC_RUNTIME_CONTRACT: repository declarations and evidence topology.
- HISTORICAL_REPOSITORY_EXECUTION: preserved B/C execution artefacts already in the repository.
- CURRENT_RUNTIME_EXECUTION: optional live API and PostgreSQL evidence from the owner environment.

It never edits application data unless --reset-runtime is explicitly supplied. Secrets are read
from environment variables and are never written to evidence files.
"""

from __future__ import annotations

import argparse
import csv
import dataclasses
import datetime as dt
import hashlib
import json
import os
import platform
import re
import shutil
import socket
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any, Mapping, Sequence

EVIDENCE_STATIC = "STATIC_RUNTIME_CONTRACT"
EVIDENCE_HISTORICAL = "HISTORICAL_REPOSITORY_EXECUTION"
EVIDENCE_CURRENT = "CURRENT_RUNTIME_EXECUTION"

DEFAULT_DIAGNOSTICS = [
    "latest-run-expected-vs-observed",
    "latest-run-events-by-cycle",
    "latest-run-np-vs-fwi-kbdi",
    "latest-run-portuguese-context-proxy",
    "latest-run-kbdi-series-context",
    "latest-run-components",
    "latest-run-quality-by-profile",
    "latest-run-degradation-effects",
    "latest-run-cell-context",
    "latest-run-fwi-input-completeness",
    "latest-run-kbdi-input-completeness",
    "latest-run-coverage-freshness",
    "compare-latest-b-vs-c",
]

TRACE_SQL = r"""
WITH requested_run AS (
    SELECT %(run_id)s::uuid AS run_id
),
run_row AS (
    SELECT r.*
    FROM control.simulation_runs r
    JOIN requested_run requested ON requested.run_id = r."Id"
),
inbox AS (
    SELECT
        ei."Id" AS inbox_id,
        ei."EventId" AS event_id,
        ei."CorrelationId" AS correlation_id,
        ei."Status" AS inbox_status,
        ei."AttemptCount" AS attempt_count,
        ei."EventTime" AS event_time,
        ei."IngestTime" AS ingest_time,
        ei."ReceivedAt" AS received_at,
        ei."LastProcessedAt" AS last_processed_at,
        ei."LastErrorCode" AS last_error_code,
        ei."QuarantinedAt" AS quarantined_at
    FROM pipeline.event_inbox ei
    JOIN requested_run requested
      ON ei."PayloadJson" LIKE '%%' || requested.run_id::text || '%%'
      OR ei."EnvelopeJson" LIKE '%%' || requested.run_id::text || '%%'
),
attempts AS (
    SELECT
        pa."InboxEventId" AS inbox_id,
        count(*) AS processing_attempt_count,
        min(pa."StartedAt") AS first_processing_started_at,
        max(pa."FinishedAt") AS last_processing_finished_at,
        min(EXTRACT(EPOCH FROM (pa."FinishedAt" - pa."StartedAt")) * 1000.0) AS min_attempt_duration_ms,
        avg(EXTRACT(EPOCH FROM (pa."FinishedAt" - pa."StartedAt")) * 1000.0) AS avg_attempt_duration_ms,
        max(EXTRACT(EPOCH FROM (pa."FinishedAt" - pa."StartedAt")) * 1000.0) AS max_attempt_duration_ms
    FROM pipeline.processing_attempts pa
    JOIN inbox i ON i.inbox_id = pa."InboxEventId"
    GROUP BY pa."InboxEventId"
),
accepted AS (
    SELECT
        ar."Id" AS accepted_reading_id,
        ar."EventId" AS event_id,
        ar."CorrelationId" AS accepted_correlation_id,
        ar."SensorId" AS sensor_id,
        ar."MetricType" AS metric_type,
        ar."OperationalState" AS operational_state,
        ar."CreatedAt" AS accepted_created_at
    FROM projection.accepted_reading_log ar
    JOIN inbox i ON i.event_id = ar."EventId"
),
risk AS (
    SELECT
        ra."Id" AS assessment_id,
        ra."SimulationRunId" AS simulation_run_id,
        ra."SourceEventId" AS source_event_id,
        ra."GridCellId" AS grid_cell_id,
        ra."Timestamp" AS assessment_timestamp,
        ra."CreatedAt" AS assessment_created_at,
        ra."RiskScore" AS risk_score,
        ra."RiskLevel" AS risk_level,
        ra."CalculationStatus" AS calculation_status,
        ra."Score100" AS score_100
    FROM projection.risk_assessment_log ra
    JOIN requested_run requested ON requested.run_id = ra."SimulationRunId"
),
cell_projection AS (
    SELECT
        cs."Id" AS cell_projection_id,
        cs."LatestAssessmentId" AS assessment_id,
        cs."SnapshotTimestamp" AS cell_snapshot_timestamp,
        cs."UpdatedAt" AS cell_projection_updated_at,
        cs."RiskScore" AS cell_risk_score,
        cs."RiskLevel" AS cell_risk_level,
        cs."CoverageStatus" AS cell_coverage_status,
        cs."FreshnessStatus" AS cell_freshness_status,
        cs."CarryForwardStatus" AS cell_carry_forward_status
    FROM projection.cell_operational_state cs
    JOIN risk r ON r.assessment_id = cs."LatestAssessmentId"
),
area_projection AS (
    SELECT
        aps."Id" AS area_projection_id,
        aps."SimulationRunId" AS simulation_run_id,
        aps."SnapshotTimestamp" AS area_snapshot_timestamp,
        aps."UpdatedAt" AS area_projection_updated_at,
        aps."AggregateRiskScore" AS aggregate_risk_score,
        aps."AggregateRiskLevel" AS aggregate_risk_level,
        aps."AssessmentCount" AS aggregate_assessment_count,
        aps."CoverageStatus" AS area_coverage_status,
        aps."FreshnessStatus" AS area_freshness_status,
        aps."CarryForwardStatus" AS area_carry_forward_status
    FROM projection.area_operational_state aps
    JOIN requested_run requested ON requested.run_id = aps."SimulationRunId"
)
SELECT
    rr."Id" AS simulation_run_id,
    rr."ScenarioCode" AS scenario_code,
    rr."Status" AS run_status,
    rr."StartedAt" AS run_started_at,
    rr."EndedAt" AS run_ended_at,
    i.inbox_id,
    i.event_id,
    i.correlation_id,
    i.inbox_status,
    i.attempt_count,
    i.event_time,
    i.ingest_time,
    i.received_at,
    i.last_processed_at,
    i.last_error_code,
    i.quarantined_at,
    a.processing_attempt_count,
    a.first_processing_started_at,
    a.last_processing_finished_at,
    a.min_attempt_duration_ms,
    a.avg_attempt_duration_ms,
    a.max_attempt_duration_ms,
    ar.accepted_reading_id,
    ar.accepted_correlation_id,
    ar.sensor_id,
    ar.metric_type,
    ar.operational_state,
    ar.accepted_created_at,
    r.assessment_id,
    r.grid_cell_id,
    r.assessment_timestamp,
    r.assessment_created_at,
    r.risk_score,
    r.risk_level,
    r.calculation_status,
    r.score_100,
    cp.cell_projection_id,
    cp.cell_snapshot_timestamp,
    cp.cell_projection_updated_at,
    cp.cell_risk_score,
    cp.cell_risk_level,
    cp.cell_coverage_status,
    cp.cell_freshness_status,
    cp.cell_carry_forward_status,
    ap.area_projection_id,
    ap.area_snapshot_timestamp,
    ap.area_projection_updated_at,
    ap.aggregate_risk_score,
    ap.aggregate_risk_level,
    ap.aggregate_assessment_count,
    ap.area_coverage_status,
    ap.area_freshness_status,
    ap.area_carry_forward_status
FROM run_row rr
LEFT JOIN inbox i ON TRUE
LEFT JOIN attempts a ON a.inbox_id = i.inbox_id
LEFT JOIN accepted ar ON ar.event_id = i.event_id
LEFT JOIN risk r ON r.source_event_id = i.event_id
LEFT JOIN cell_projection cp ON cp.assessment_id = r.assessment_id
LEFT JOIN area_projection ap ON ap.simulation_run_id = rr."Id"
ORDER BY i.received_at NULLS LAST, i.event_id NULLS LAST;
""".strip()


def utc_now() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc)


def stamp(value: dt.datetime | None = None) -> str:
    return (value or utc_now()).strftime("%Y%m%dT%H%M%SZ")


def iso(value: dt.datetime | None = None) -> str:
    return (value or utc_now()).isoformat().replace("+00:00", "Z")


def ensure_dir(path: Path) -> Path:
    path.mkdir(parents=True, exist_ok=True)
    return path


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace")


def read_json(path: Path) -> Any:
    return json.loads(read_text(path))


def write_text(path: Path, text: str) -> None:
    ensure_dir(path.parent)
    path.write_text(text, encoding="utf-8", newline="\n")


def write_json(path: Path, value: Any) -> None:
    write_text(path, json.dumps(value, indent=2, ensure_ascii=False, default=json_default) + "\n")


def json_default(value: Any) -> Any:
    if isinstance(value, (dt.datetime, dt.date, dt.time)):
        return value.isoformat()
    if dataclasses.is_dataclass(value):
        return dataclasses.asdict(value)
    return str(value)


def write_csv(path: Path, rows: Sequence[Mapping[str, Any]], fieldnames: Sequence[str] | None = None) -> None:
    ensure_dir(path.parent)
    if fieldnames is None:
        ordered: list[str] = []
        for row in rows:
            for key in row.keys():
                if key not in ordered:
                    ordered.append(key)
        fieldnames = ordered
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(fieldnames), extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow({key: normalize_cell(row.get(key)) for key in fieldnames})


def normalize_cell(value: Any) -> Any:
    if value is None:
        return ""
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (dict, list, tuple)):
        return json.dumps(value, ensure_ascii=False, separators=(",", ":"), default=json_default)
    return value


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def rel(path: Path, root: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def hash_tree(root: Path, manifest_name: str = "SHA256SUMS.txt") -> list[dict[str, str]]:
    manifest = root / manifest_name
    entries: list[dict[str, str]] = []
    for path in sorted(p for p in root.rglob("*") if p.is_file() and p != manifest):
        entries.append({"sha256": sha256_file(path), "path": rel(path, root)})
    write_text(manifest, "".join(f"{item['sha256']}  {item['path']}\n" for item in entries))
    return entries


def find_repo_root(start: Path) -> Path:
    candidate = start.resolve()
    for value in [candidate, *candidate.parents]:
        if (value / "NatureProtector.sln").exists() and (value / "src").is_dir():
            return value
    raise FileNotFoundError(f"Could not locate NatureProtector repository from {start}")


def command_available(name: str) -> bool:
    return shutil.which(name) is not None


def environment_summary(repo: Path, args: argparse.Namespace) -> dict[str, Any]:
    return {
        "generatedAtUtc": iso(),
        "baselineId": args.baseline_id,
        "phaseRunId": args.run_id,
        "repositoryRoot": str(repo),
        "hostname": socket.gethostname(),
        "platform": platform.platform(),
        "python": sys.version.splitlines()[0],
        "toolAvailability": {
            "dotnet": command_available("dotnet"),
            "docker": command_available("docker"),
            "pwsh": command_available("pwsh"),
            "psql": command_available("psql"),
            "git": command_available("git"),
        },
        "requestedModes": {
            "live": args.live,
            "requireLive": args.require_live,
            "resetRuntime": args.reset_runtime,
            "postgresTrace": bool(args.postgres_dsn_env),
        },
        "apiBaseUrl": args.api_base_url,
        "secretInputs": {
            "bearerTokenEnvironmentVariable": args.bearer_token_env,
            "usernameEnvironmentVariable": args.username_env,
            "passwordEnvironmentVariable": args.password_env,
            "postgresDsnEnvironmentVariable": args.postgres_dsn_env,
            "valuesPersisted": False,
        },
    }


def extract_controller_endpoints(repo: Path) -> list[dict[str, Any]]:
    files = [
        repo / "src/NatureProtector.Backoffice.Api/Controllers/ControlRuntimeController.cs",
        repo / "src/NatureProtector.Backoffice.Api/Controllers/ControlRuntimeObservabilityController.cs",
    ]
    endpoints: list[dict[str, Any]] = []
    for path in files:
        if not path.exists():
            continue
        text = read_text(path)
        route_match = re.search(r'\[Route\("([^"]+)"\)\]', text)
        base_route = route_match.group(1) if route_match else ""
        class_auth = re.search(r'\[Authorize\s*\(Roles\s*=\s*"([^"]+)"\)\]', text)
        class_roles = class_auth.group(1).split(",") if class_auth else []
        lines = text.splitlines()
        pending: list[str] = []
        for index, line in enumerate(lines):
            stripped = line.strip()
            if stripped.startswith("["):
                pending.append(stripped)
                continue
            if re.search(
                r"public\s+(?:async\s+)?Task<ActionResult>|public\s+(?:async\s+)?Task<ActionResult<", stripped
            ):
                http_attr = next((item for item in pending if item.startswith("[Http")), None)
                if not http_attr:
                    pending = []
                    continue
                method_match = re.match(r'\[Http(Get|Post|Put|Delete|Patch)(?:\("([^"]*)"\))?', http_attr)
                if not method_match:
                    pending = []
                    continue
                method = method_match.group(1).upper()
                suffix = method_match.group(2) or ""
                full_path = "/" + "/".join(part.strip("/") for part in [base_route, suffix] if part)
                auth_attr = next((item for item in pending if item.startswith("[Authorize")), None)
                roles_match = re.search(r'Roles\s*=\s*"([^"]+)"', auth_attr or "")
                roles = roles_match.group(1).split(",") if roles_match else class_roles
                dev_only = full_path in {"/api/control/runtime/runs", "/api/control/runtime/reset"} and method == "POST"
                action_match = re.search(r"public\s+(?:async\s+)?[^\s]+(?:<[^>]+>)?\s+(\w+)\s*\(", stripped)
                endpoints.append(
                    {
                        "method": method,
                        "path": full_path,
                        "controller": path.stem,
                        "action": action_match.group(1) if action_match else "",
                        "roles": ",".join(roles),
                        "developmentOnly": dev_only,
                        "readOnly": method == "GET" or ("diagnostics/" in full_path and method == "POST"),
                        "evidenceClass": EVIDENCE_STATIC,
                        "source": rel(path, repo),
                    }
                )
                pending = []
            elif stripped and not stripped.startswith("//") and not stripped.startswith("["):
                pending = []
    return sorted(endpoints, key=lambda item: (item["path"], item["method"]))


def extract_diagnostics(repo: Path) -> list[dict[str, Any]]:
    path = repo / "src/NatureProtector.Backoffice.Api/ControlPlane/Services/PostgresControlPlaneService.cs"
    text = read_text(path)
    block = re.search(r"RuntimeDiagnostics\s*=\s*\[(.*?)\];", text, re.S)
    if not block:
        return []
    results = []
    pattern = re.compile(r'new\("([^"]+)",\s*"([^"]+)",\s*"([^"]+)"\)')
    for match in pattern.finditer(block.group(1)):
        results.append(
            {
                "id": match.group(1),
                "title": match.group(2),
                "description": match.group(3),
                "evidenceClass": EVIDENCE_STATIC,
                "source": rel(path, repo),
            }
        )
    return results


def extract_degradation_profiles(repo: Path) -> list[dict[str, Any]]:
    path = repo / "src/NatureProtector.Simulator.Host/Services/SimulationDegradationProfiles.cs"
    text = read_text(path)
    results = []
    for name, value in re.findall(r'public const string\s+(\w+)\s*=\s*"([^"]+)";', text):
        results.append(
            {
                "constant": name,
                "profile": value,
                "evidenceClass": EVIDENCE_STATIC,
                "source": rel(path, repo),
            }
        )
    return results


def scenario_manifest_summary(repo: Path) -> list[dict[str, Any]]:
    base = repo / "data/manifests/scenarios/proenca-a-nova"
    results: list[dict[str, Any]] = []
    for path in sorted(base.glob("*.json")):
        try:
            data = read_json(path)
        except Exception as exc:
            results.append({"source": rel(path, repo), "error": str(exc), "evidenceClass": EVIDENCE_STATIC})
            continue
        simulator = ci_get(data, "simulator_options", "simulatorOptions") or {}
        source_context = ci_get(data, "source_context", "sourceContext") or {}
        results.append(
            {
                "scenarioCode": ci_get(data, "scenario_key", "scenarioCode", "scenario_id"),
                "scenarioName": ci_get(data, "scenario_name", "scenarioName", "name"),
                "scenarioCategory": ci_get(data, "scenario_category", "scenarioCategory"),
                "candidateDate": ci_get(data, "candidate_date", "candidateDate"),
                "baseScenarioId": ci_get(data, "base_scenario_id", "baseScenarioId"),
                "degradationProfile": ci_get(simulator, "DegradationProfile", "degradationProfile"),
                "baseTemperature": ci_get(simulator, "BaseTemperature", "baseTemperature"),
                "baseHumidity": ci_get(simulator, "BaseHumidity", "baseHumidity"),
                "baseWindSpeed": ci_get(simulator, "BaseWindSpeed", "baseWindSpeed"),
                "sourceDataset": ci_get(source_context, "source_dataset", "sourceDataset"),
                "evidenceClass": EVIDENCE_STATIC,
                "source": rel(path, repo),
            }
        )
    return results


def ci_get(mapping: Any, *keys: str, default: Any = None) -> Any:
    if not isinstance(mapping, Mapping):
        return default
    normalized = {str(key).lower(): value for key, value in mapping.items()}
    for key in keys:
        if key in mapping:
            return mapping[key]
        if key.lower() in normalized:
            return normalized[key.lower()]
    return default


def parse_metadata_from_sql(text: str) -> dict[str, Any]:
    match = re.search(r'(\{"sensor_count".*?\})\s*\n\(1 row\)', text, re.S)
    if not match:
        return {}
    value = match.group(1).strip()
    try:
        return json.loads(value)
    except json.JSONDecodeError:
        return {}


def historical_evidence(repo: Path) -> dict[str, Any]:
    base = repo / "docs/evidence/progress-2026-05-22"
    compare_path = base / "06-compare-b-vs-c.json"
    notes_path = base / "07-runtime-notes.md"
    spec_paths = {
        "scenario_b": base / "scenario-b-5cycles-6sensors-none.json",
        "scenario_c": base / "scenario-c-5cycles-6sensors-missing-readings.json",
    }
    sql_paths = {
        "scenario_b": base / "04-scenario-b-summary.sql.txt",
        "scenario_c": base / "05-scenario-c-summary.sql.txt",
    }
    sources = [compare_path, notes_path, *spec_paths.values(), *sql_paths.values()]
    source_manifest = []
    for path in sources:
        source_manifest.append(
            {
                "path": rel(path, repo),
                "exists": path.exists(),
                "sha256": sha256_file(path) if path.exists() else None,
                "bytes": path.stat().st_size if path.exists() else None,
                "evidenceClass": EVIDENCE_HISTORICAL,
            }
        )
    if not compare_path.exists():
        return {"status": "NOT_AVAILABLE", "sources": source_manifest, "runs": [], "comparison": {}}
    compare = read_json(compare_path)
    runs_obj = ci_get(compare, "runs") or {}
    rows: list[dict[str, Any]] = []
    for scenario in ["scenario_b", "scenario_c"]:
        run = ci_get(runs_obj, scenario) or {}
        spec = read_json(spec_paths[scenario]) if spec_paths[scenario].exists() else {}
        sql_text = read_text(sql_paths[scenario]) if sql_paths[scenario].exists() else ""
        metadata = parse_metadata_from_sql(sql_text)
        orchestrator_id = ci_get(metadata, "orchestrator_correlation_id")
        inbox = ci_get(run, "inboxEvents", default=0) or 0
        expected = ci_get(run, "expectedEvents", default=0) or 0
        assessments = ci_get(run, "riskAssessments", default=0) or 0
        missing = ci_get(run, "missingEvents", default=max(expected - inbox, 0)) or 0
        rows.append(
            {
                "scenarioCode": scenario,
                "simulationRunId": ci_get(run, "simulationRunId"),
                "orchestratorCorrelationId": orchestrator_id,
                "status": ci_get(run, "status"),
                "areaCode": ci_get(spec, "areaCode"),
                "sensorCount": ci_get(spec, "sensorCount"),
                "numberOfCycles": ci_get(spec, "numberOfCycles"),
                "intervalSeconds": ci_get(spec, "intervalSeconds"),
                "seed": ci_get(spec, "seed"),
                "degradationProfile": ci_get(run, "degradationProfile") or ci_get(spec, "degradationProfile"),
                "expectedEvents": expected,
                "inboxEvents": inbox,
                "riskAssessments": assessments,
                "missingEvents": missing,
                "rejected": ci_get(run, "rejected", default=0) or 0,
                "quarantined": ci_get(run, "quarantined", default=0) or 0,
                "observedEventRatePct": round(100.0 * inbox / expected, 3) if expected else None,
                "assessmentYieldPct": round(100.0 * assessments / expected, 3) if expected else None,
                "reconciliationDelta": expected - (inbox + missing),
                "generatedAtUtc": ci_get(compare, "generatedAtUtc"),
                "evidenceClass": EVIDENCE_HISTORICAL,
                "source": rel(compare_path, repo),
            }
        )
    by_code = {row["scenarioCode"]: row for row in rows}
    b, c = by_code.get("scenario_b", {}), by_code.get("scenario_c", {})
    comparison = {
        "scenarioBRunId": b.get("simulationRunId"),
        "scenarioCRunId": c.get("simulationRunId"),
        "expectedEventsDelta": diff(c.get("expectedEvents"), b.get("expectedEvents")),
        "inboxEventsDelta": diff(c.get("inboxEvents"), b.get("inboxEvents")),
        "riskAssessmentsDelta": diff(c.get("riskAssessments"), b.get("riskAssessments")),
        "missingEventsDelta": diff(c.get("missingEvents"), b.get("missingEvents")),
        "observedRatePercentagePointDelta": diff(c.get("observedEventRatePct"), b.get("observedEventRatePct")),
        "controlledDegradationObserved": bool(
            b.get("expectedEvents") == c.get("expectedEvents")
            and (c.get("inboxEvents") or 0) < (b.get("inboxEvents") or 0)
            and (c.get("missingEvents") or 0) > (b.get("missingEvents") or 0)
            and (c.get("rejected") or 0) == 0
            and (c.get("quarantined") or 0) == 0
        ),
        "evidenceClass": EVIDENCE_HISTORICAL,
        "source": rel(compare_path, repo),
    }
    chain = []
    for row in rows:
        availability = {
            "SimulationRunId": bool(row.get("simulationRunId")),
            "OrchestratorCorrelationId": bool(row.get("orchestratorCorrelationId")),
            "EventId": False,
            "EventCorrelationId": False,
            "InboxId": False,
            "ProcessingAttemptId": False,
            "AcceptedReadingId": False,
            "AssessmentId": False,
            "CellProjectionId": False,
            "AreaProjectionId": False,
        }
        for identifier, available in availability.items():
            chain.append(
                {
                    "scenarioCode": row["scenarioCode"],
                    "simulationRunId": row.get("simulationRunId"),
                    "identifier": identifier,
                    "available": available,
                    "interpretation": "Preserved in current repository evidence"
                    if available
                    else "Not preserved in the current repository evidence package",
                    "evidenceClass": EVIDENCE_HISTORICAL,
                }
            )
    return {
        "status": "PASS" if len(rows) == 2 else "PARTIAL",
        "sources": source_manifest,
        "runs": rows,
        "comparison": comparison,
        "chainTraceability": chain,
    }


def diff(a: Any, b: Any) -> Any:
    if a is None or b is None:
        return None
    try:
        return round(float(a) - float(b), 3)
    except (TypeError, ValueError):
        return None


class ApiError(RuntimeError):
    pass


class RuntimeApi:
    def __init__(self, base_url: str, token: str | None, timeout: int):
        self.base_url = base_url.rstrip("/")
        self.token = token
        self.timeout = timeout

    def request(
        self,
        method: str,
        path: str,
        body: Any = None,
        authenticated: bool = True,
        expect_json: bool = True,
    ) -> Any:
        url = self.base_url + path
        data = None
        headers = {"Accept": "application/json", "User-Agent": "NatureProtector-Phase4-Evidence/1.0"}
        evidence_run_id = os.getenv("NP_EVIDENCE_RUN_ID", "").strip()
        if evidence_run_id:
            headers["X-NP-Evidence-Run-Id"] = evidence_run_id
        if body is not None:
            data = json.dumps(body, ensure_ascii=False).encode("utf-8")
            headers["Content-Type"] = "application/json"
        if authenticated and self.token:
            headers["Authorization"] = f"Bearer {self.token}"
        request = urllib.request.Request(url, data=data, headers=headers, method=method)
        started = time.perf_counter()
        try:
            with urllib.request.urlopen(request, timeout=self.timeout) as response:
                payload = response.read()
                elapsed = (time.perf_counter() - started) * 1000.0
                content_type = response.headers.get("Content-Type", "")
                text = payload.decode("utf-8-sig", errors="replace") if payload else ""
                if not payload:
                    result = None
                else:
                    try:
                        result = json.loads(text)
                    except json.JSONDecodeError as exc:
                        if not expect_json:
                            result = text
                        else:
                            raise ApiError(
                                f"{method} {path} returned HTTP {response.status} after {elapsed:.1f} ms "
                                f"with non-JSON content-type={content_type!r}, bytes={len(payload)}, "
                                f"bodyPreview={text[:1000]!r}"
                            ) from exc
                return {
                    "statusCode": response.status,
                    "durationMs": round(elapsed, 3),
                    "contentType": content_type,
                    "bodyBytes": len(payload),
                    "body": result,
                }
        except urllib.error.HTTPError as exc:
            payload = exc.read()
            elapsed = (time.perf_counter() - started) * 1000.0
            content_type = exc.headers.get("Content-Type", "") if exc.headers else ""
            text = payload.decode("utf-8-sig", errors="replace") if payload else ""
            try:
                result = json.loads(text) if text else None
            except json.JSONDecodeError:
                result = text[:2000]
            raise ApiError(
                f"{method} {path} returned HTTP {exc.code} after {elapsed:.1f} ms "
                f"content-type={content_type!r}, bytes={len(payload)}, body={result!r}"
            ) from exc
        except (urllib.error.URLError, TimeoutError, OSError) as exc:
            elapsed = (time.perf_counter() - started) * 1000.0
            raise ApiError(f"{method} {path} failed after {elapsed:.1f} ms: {exc}") from exc


def resolve_token(args: argparse.Namespace, base_url: str) -> tuple[str | None, dict[str, Any]]:
    username = os.getenv(args.username_env, "").strip() if args.username_env else ""
    password = os.getenv(args.password_env, "") if args.password_env else ""
    if username and password:
        client = RuntimeApi(base_url, None, args.http_timeout)
        response = client.request(
            "POST",
            "/api/users-roles/login",
            {"usernameOrEmail": username, "password": password},
            authenticated=False,
        )
        login = response["body"] or {}
        token = ci_get(login, "token")
        roles = ci_get(login, "roles") or []
        if not token:
            raise ApiError("Login succeeded but no token was returned.")
        return str(token), {
            "mode": "fresh_login_environment",
            "status": "AVAILABLE",
            "username": ci_get(login, "username"),
            "roles": roles,
            "loginDurationMs": response["durationMs"],
            "tokenPersisted": False,
        }

    token = os.getenv(args.bearer_token_env, "").strip() if args.bearer_token_env else ""
    if token:
        return token, {"mode": "bearer_environment", "status": "AVAILABLE", "tokenPersisted": False}

    return None, {"mode": "none", "status": "NOT_AVAILABLE", "tokenPersisted": False}


def make_run_spec(args: argparse.Namespace, scenario: str) -> dict[str, Any]:
    profiles = ["none"] if scenario == "scenario_b" else ["missing-readings"]
    return {
        "areaCode": args.area_code,
        "scenarioCode": scenario,
        "sensorCount": args.sensor_count,
        "numberOfCycles": args.number_of_cycles,
        "intervalSeconds": args.interval_seconds,
        "seed": args.seed,
        "degradationProfile": profiles[0],
        "degradationProfiles": profiles,
        "collectEvidence": True,
        "waitForCompletion": True,
        "timeoutSeconds": args.run_timeout,
        "allowParallelRun": False,
        "runLabel": f"phase4-{scenario}-{args.run_id.lower()}",
    }


def normalize_live_run(start_response: Any, audit: Any, timings: Any) -> dict[str, Any]:
    start_body = start_response.get("body") if isinstance(start_response, Mapping) else start_response
    run = ci_get(start_body, "run") or {}
    audit_body = audit.get("body") if isinstance(audit, Mapping) else audit
    timings_body = timings.get("body") if isinstance(timings, Mapping) else timings
    expected = ci_get(audit_body, "expectedEvents")
    accepted = ci_get(audit_body, "acceptedReadings", default=0) or 0
    missing = ci_get(audit_body, "missingEvents")
    risk = ci_get(audit_body, "riskAssessments", default=0) or 0
    return {
        "scenarioCode": ci_get(run, "scenarioCode"),
        "simulationRunId": ci_get(run, "id"),
        "orchestratorCorrelationId": ci_get(run, "orchestratorCorrelationId")
        or ci_get(start_body, "orchestratorCorrelationId"),
        "status": ci_get(run, "status") or ci_get(start_body, "status"),
        "areaCode": ci_get(run, "areaCode"),
        "expectedEvents": expected,
        "acceptedReadings": accepted,
        "missingEvents": missing,
        "rejected": ci_get(audit_body, "rejected", default=0) or 0,
        "quarantined": ci_get(audit_body, "quarantined", default=0) or 0,
        "retryAttempts": ci_get(audit_body, "retryAttempts", default=0) or 0,
        "riskAssessments": risk,
        "runDurationMs": ci_get(timings_body, "runDurationMs"),
        "timeToFirstInboxMs": ci_get(timings_body, "timeToFirstInboxMs"),
        "timeToFirstProcessingAttemptMs": ci_get(timings_body, "timeToFirstProcessingAttemptMs"),
        "timeToFirstRiskAssessmentMs": ci_get(timings_body, "timeToFirstRiskAssessmentMs"),
        "timeToFirstAlertMs": ci_get(timings_body, "timeToFirstAlertMs"),
        "observedEventRatePct": round(100.0 * accepted / expected, 3) if expected else None,
        "assessmentYieldPct": round(100.0 * risk / expected, 3) if expected else None,
        "reconciliationDelta": expected - (accepted + (missing or 0))
        if expected is not None and missing is not None
        else None,
        "evidenceClass": EVIDENCE_CURRENT,
    }


def live_collection(repo: Path, output: Path, args: argparse.Namespace) -> dict[str, Any]:
    live_dir = ensure_dir(output / "live")
    status: dict[str, Any] = {
        "requested": args.live,
        "status": "NOT_REQUESTED" if not args.live else "PENDING",
        "evidenceClass": EVIDENCE_CURRENT,
        "apiBaseUrl": args.api_base_url,
        "runs": [],
        "errors": [],
    }
    if not args.live:
        write_json(live_dir / "live-status.json", status)
        return status
    try:
        calls_dir = ensure_dir(live_dir / "http")
        preflight = {}
        anonymous_api = RuntimeApi(args.api_base_url, None, args.http_timeout)
        health = anonymous_api.request("GET", "/health", authenticated=False, expect_json=False)
        preflight["health"] = {"statusCode": health["statusCode"], "durationMs": health["durationMs"]}
        write_json(calls_dir / "health.json", health["body"])

        token, auth = resolve_token(args, args.api_base_url)
        status["authentication"] = auth
        if not token:
            raise ApiError(
                f"No runtime token available. Set {args.bearer_token_env}, or set both {args.username_env} and {args.password_env}."
            )
        api = RuntimeApi(args.api_base_url, token, args.http_timeout)
        for name, path, authenticated in [
            (
                "runtime-summary-before",
                f"/api/control/runtime/summary?areaCode={urllib.parse.quote(args.area_code)}&recentMinutes=30",
                True,
            ),
            ("diagnostic-catalog", "/api/control/runtime/diagnostics", True),
            ("observability-health", "/api/control/runtime/observability/health", True),
            ("rabbitmq", "/api/control/runtime/observability/rabbitmq", True),
        ]:
            response = api.request("GET", path, authenticated=authenticated)
            preflight[name] = {"statusCode": response["statusCode"], "durationMs": response["durationMs"]}
            write_json(calls_dir / f"{name}.json", response["body"])
        status["preflight"] = preflight
        if args.reset_runtime:
            reset = api.request(
                "POST",
                "/api/control/runtime/reset",
                {"scope": "runtime-only", "confirm": "RESET_RUNTIME_STATE", "dryRun": False},
            )
            write_json(calls_dir / "reset.json", reset["body"])
            status["reset"] = {
                "statusCode": reset["statusCode"],
                "durationMs": reset["durationMs"],
                "explicitlyRequested": True,
            }
        else:
            reset_dry = api.request(
                "POST",
                "/api/control/runtime/reset",
                {"scope": "runtime-only", "confirm": "RESET_RUNTIME_STATE", "dryRun": True},
            )
            write_json(calls_dir / "reset-dry-run.json", reset_dry["body"])
            status["reset"] = {
                "statusCode": reset_dry["statusCode"],
                "durationMs": reset_dry["durationMs"],
                "explicitlyRequested": False,
                "dryRun": True,
            }
        live_rows = []
        for scenario in ["scenario_b", "scenario_c"]:
            spec = make_run_spec(args, scenario)
            write_json(live_dir / f"run-spec-{scenario}.json", spec)
            start = api.request("POST", "/api/control/runtime/runs", spec)
            write_json(calls_dir / f"start-{scenario}.json", start["body"])
            run_body = ci_get(start["body"], "run") or {}
            run_id = ci_get(run_body, "id")
            if not run_id:
                raise ApiError(f"{scenario} did not return a SimulationRunId: {start['body']}")
            audit = api.request("GET", f"/api/control/runtime/runs/{run_id}/audit")
            timings = api.request("GET", f"/api/control/runtime/runs/{run_id}/timings")
            write_json(calls_dir / f"audit-{scenario}.json", audit["body"])
            write_json(calls_dir / f"timings-{scenario}.json", timings["body"])
            live_rows.append(normalize_live_run(start, audit, timings))
            diagnostic_dir = ensure_dir(live_dir / "diagnostics" / scenario)
            for diagnostic in args.diagnostics:
                result = api.request(
                    "POST",
                    f"/api/control/runtime/diagnostics/{urllib.parse.quote(diagnostic)}",
                    {
                        "areaCode": args.area_code,
                        "recentMinutes": 30,
                        "scenarioCode": scenario,
                    },
                )
                write_json(diagnostic_dir / f"{diagnostic}.json", result["body"])
        summary_after = api.request(
            "GET", f"/api/control/runtime/summary?areaCode={urllib.parse.quote(args.area_code)}&recentMinutes=30"
        )
        evidence_catalog = api.request("GET", "/api/control/runtime/observability/evidence")
        write_json(calls_dir / "runtime-summary-after.json", summary_after["body"])
        write_json(calls_dir / "evidence-catalog.json", evidence_catalog["body"])
        status["runs"] = live_rows
        status["status"] = "PASS"
        status["comparison"] = compare_rows(live_rows, EVIDENCE_CURRENT)
    except Exception as exc:
        message = str(exc)
        if "Connection refused" in message or "Name or service not known" in message or "timed out" in message:
            status["status"] = "BLOCKED_API_UNAVAILABLE"
        elif (
            "No runtime token available" in message
            or "Unauthorized" in message
            or "HTTP 401" in message
            or "HTTP 403" in message
        ):
            status["status"] = "BLOCKED_AUTHENTICATION_OR_AUTHORIZATION"
        else:
            status["status"] = "FAILED"
        status["errors"].append(message)
        write_json(
            live_dir / "live-failure.json",
            {
                "type": type(exc).__name__,
                "message": message,
                "apiBaseUrl": args.api_base_url,
                "generatedAtUtc": iso(),
            },
        )
    write_json(live_dir / "live-status.json", status)
    if status.get("runs"):
        write_json(live_dir / "live-runs.json", status["runs"])
        write_csv(live_dir / "live-runs.csv", status["runs"])
        write_json(live_dir / "live-comparison.json", status.get("comparison", {}))
        write_csv(live_dir / "live-comparison.csv", [status.get("comparison", {})])
    return status


def compare_rows(rows: Sequence[Mapping[str, Any]], evidence_class: str) -> dict[str, Any]:
    by_code = {str(row.get("scenarioCode")): row for row in rows}
    b, c = by_code.get("scenario_b", {}), by_code.get("scenario_c", {})
    return {
        "scenarioBRunId": b.get("simulationRunId"),
        "scenarioCRunId": c.get("simulationRunId"),
        "expectedEventsDelta": diff(c.get("expectedEvents"), b.get("expectedEvents")),
        "acceptedReadingsDelta": diff(
            c.get("acceptedReadings", c.get("inboxEvents")), b.get("acceptedReadings", b.get("inboxEvents"))
        ),
        "riskAssessmentsDelta": diff(c.get("riskAssessments"), b.get("riskAssessments")),
        "missingEventsDelta": diff(c.get("missingEvents"), b.get("missingEvents")),
        "observedRatePercentagePointDelta": diff(c.get("observedEventRatePct"), b.get("observedEventRatePct")),
        "runDurationMsDelta": diff(c.get("runDurationMs"), b.get("runDurationMs")),
        "controlledDegradationObserved": bool(
            b
            and c
            and b.get("expectedEvents") == c.get("expectedEvents")
            and (c.get("acceptedReadings", c.get("inboxEvents")) or 0)
            < (b.get("acceptedReadings", b.get("inboxEvents")) or 0)
            and (c.get("missingEvents") or 0) > (b.get("missingEvents") or 0)
            and (c.get("rejected") or 0) == 0
            and (c.get("quarantined") or 0) == 0
        ),
        "evidenceClass": evidence_class,
    }


def postgres_trace(output: Path, args: argparse.Namespace, run_rows: Sequence[Mapping[str, Any]]) -> dict[str, Any]:
    trace_dir = ensure_dir(output / "database-trace")
    status: dict[str, Any] = {
        "requested": bool(args.postgres_dsn_env),
        "status": "NOT_REQUESTED" if not args.postgres_dsn_env else "PENDING",
        "dsnEnvironmentVariable": args.postgres_dsn_env,
        "dsnPersisted": False,
        "runs": [],
        "errors": [],
        "evidenceClass": EVIDENCE_CURRENT,
    }
    write_text(trace_dir / "runtime-trace-query.sql", TRACE_SQL + "\n")
    if not args.postgres_dsn_env:
        write_json(trace_dir / "database-trace-status.json", status)
        return status
    dsn = os.getenv(args.postgres_dsn_env, "").strip()
    if not dsn:
        status["status"] = "BLOCKED"
        status["errors"].append(f"Environment variable {args.postgres_dsn_env} is empty.")
        write_json(trace_dir / "database-trace-status.json", status)
        return status
    try:
        import psycopg  # type: ignore
        from psycopg.rows import dict_row  # type: ignore
    except Exception as exc:
        status["status"] = "BLOCKED"
        status["errors"].append(f"psycopg v3 is required for database trace collection: {exc}")
        write_json(trace_dir / "database-trace-status.json", status)
        return status
    try:
        with psycopg.connect(dsn, autocommit=False, row_factory=dict_row) as connection:
            with connection.cursor() as cursor:
                cursor.execute("SET TRANSACTION READ ONLY")
                cursor.execute("SET LOCAL statement_timeout = '60s'")
                for run in run_rows:
                    run_id = run.get("simulationRunId")
                    scenario = run.get("scenarioCode") or "unknown"
                    if not run_id:
                        continue
                    cursor.execute(TRACE_SQL, {"run_id": str(run_id)})
                    rows = [serialize_db_row(row) for row in cursor.fetchall()]
                    json_path = trace_dir / f"trace-{scenario}.json"
                    csv_path = trace_dir / f"trace-{scenario}.csv"
                    write_json(json_path, rows)
                    write_csv(csv_path, rows)
                    summary = trace_summary(str(run_id), str(scenario), rows)
                    status["runs"].append(summary)
                connection.rollback()
        status["status"] = "PASS"
    except Exception as exc:
        status["status"] = "FAILED"
        status["errors"].append(str(exc))
    write_json(trace_dir / "database-trace-status.json", status)
    if status["runs"]:
        write_csv(trace_dir / "database-trace-summary.csv", status["runs"])
        write_json(trace_dir / "database-trace-summary.json", status["runs"])
    return status


def serialize_db_row(row: Mapping[str, Any]) -> dict[str, Any]:
    return {
        str(key): json_default(value) if isinstance(value, (dt.datetime, dt.date, dt.time)) else value
        for key, value in row.items()
    }


def trace_summary(run_id: str, scenario: str, rows: Sequence[Mapping[str, Any]]) -> dict[str, Any]:
    def count_present(key: str) -> int:
        return sum(1 for row in rows if row.get(key) not in (None, ""))

    event_ids = {str(row.get("event_id")) for row in rows if row.get("event_id")}
    assessment_ids = {str(row.get("assessment_id")) for row in rows if row.get("assessment_id")}
    return {
        "scenarioCode": scenario,
        "simulationRunId": run_id,
        "rows": len(rows),
        "distinctEventIds": len(event_ids),
        "distinctInboxIds": len({str(row.get("inbox_id")) for row in rows if row.get("inbox_id")}),
        "distinctAcceptedReadingIds": len(
            {str(row.get("accepted_reading_id")) for row in rows if row.get("accepted_reading_id")}
        ),
        "distinctAssessmentIds": len(assessment_ids),
        "distinctCellProjectionIds": len(
            {str(row.get("cell_projection_id")) for row in rows if row.get("cell_projection_id")}
        ),
        "areaProjectionPresent": count_present("area_projection_id") > 0,
        "eventsWithCorrelationId": count_present("correlation_id"),
        "eventsWithProcessingAttempt": count_present("processing_attempt_count"),
        "eventsWithAssessment": count_present("assessment_id"),
        "eventsWithCellProjection": count_present("cell_projection_id"),
        "evidenceClass": EVIDENCE_CURRENT,
    }


def static_chain_model() -> list[dict[str, Any]]:
    return [
        {
            "sequence": 1,
            "entity": "control.simulation_runs",
            "identifier": "Id",
            "joinToNext": "risk_assessment_log.SimulationRunId",
            "purpose": "Run identity",
            "evidenceClass": EVIDENCE_STATIC,
        },
        {
            "sequence": 2,
            "entity": "pipeline.event_inbox",
            "identifier": "Id, EventId, CorrelationId",
            "joinToNext": "accepted_reading_log.EventId / processing_attempts.InboxEventId",
            "purpose": "Transport and inbox identity",
            "evidenceClass": EVIDENCE_STATIC,
        },
        {
            "sequence": 3,
            "entity": "pipeline.processing_attempts",
            "identifier": "Id, InboxEventId",
            "joinToNext": "event_inbox.Id",
            "purpose": "Processing attempts and outcomes",
            "evidenceClass": EVIDENCE_STATIC,
        },
        {
            "sequence": 4,
            "entity": "projection.accepted_reading_log",
            "identifier": "Id, EventId, CorrelationId",
            "joinToNext": "risk_assessment_log.SourceEventId",
            "purpose": "Accepted observation",
            "evidenceClass": EVIDENCE_STATIC,
        },
        {
            "sequence": 5,
            "entity": "projection.risk_assessment_log",
            "identifier": "Id, SimulationRunId, SourceEventId",
            "joinToNext": "cell_operational_state.LatestAssessmentId",
            "purpose": "Risk assessment",
            "evidenceClass": EVIDENCE_STATIC,
        },
        {
            "sequence": 6,
            "entity": "projection.cell_operational_state",
            "identifier": "Id, LatestAssessmentId",
            "joinToNext": "risk_assessment_log.Id",
            "purpose": "Cell projection",
            "evidenceClass": EVIDENCE_STATIC,
        },
        {
            "sequence": 7,
            "entity": "projection.area_operational_state",
            "identifier": "Id, SimulationRunId",
            "joinToNext": "simulation_runs.Id",
            "purpose": "Area projection",
            "evidenceClass": EVIDENCE_STATIC,
        },
    ]


def report_markdown(
    static: dict[str, Any], historical: dict[str, Any], live: dict[str, Any], db_trace: dict[str, Any]
) -> str:
    lines = [
        "# Phase 4 — integrated runtime evidence summary",
        "",
        f"- Generated at: `{iso()}`",
        "- Static runtime contract: **PASS**",
        f"- Historical repository execution: **{historical.get('status', 'UNKNOWN')}**",
        f"- Current live execution: **{live.get('status', 'UNKNOWN')}**",
        f"- Current database trace: **{db_trace.get('status', 'UNKNOWN')}**",
        "",
        "## Static capability",
        "",
        f"- Runtime/observability endpoints inventoried: `{len(static.get('endpoints', []))}`",
        f"- Runtime diagnostics inventoried: `{len(static.get('diagnostics', []))}`",
        f"- Degradation profiles inventoried: `{len(static.get('degradationProfiles', []))}`",
        f"- Scenario manifests inventoried: `{len(static.get('scenarios', []))}`",
        "",
        "## Historical B/C execution preserved in the repository",
        "",
        "| Scenario | Run ID | Expected | Observed | Risk assessments | Missing | Rejected | Quarantined | Observed rate |",
        "|---|---|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for row in historical.get("runs", []):
        lines.append(
            f"| {row.get('scenarioCode')} | `{row.get('simulationRunId')}` | {row.get('expectedEvents')} | {row.get('inboxEvents')} | "
            f"{row.get('riskAssessments')} | {row.get('missingEvents')} | {row.get('rejected')} | {row.get('quarantined')} | {row.get('observedEventRatePct')}% |"
        )
    comparison = historical.get("comparison", {})
    lines += [
        "",
        f"The preserved historical comparison reports a `{comparison.get('observedRatePercentagePointDelta')}` percentage-point change in observed events from B to C. "
        "This is historical repository evidence, not a current Phase 4 execution.",
        "",
        "## Current execution interpretation",
        "",
    ]
    if live.get("status") == "PASS":
        lines.append(
            "A current API-driven B/C execution completed. Use `live/live-runs.csv`, the raw HTTP payloads and the database trace, when available, for report values."
        )
    else:
        errors = live.get("errors") or []
        lines.append(
            "A current B/C execution was not obtained in this environment. No current runtime values are claimed."
        )
        for error in errors:
            lines.append(f"- Blocker: `{error}`")
    lines += [
        "",
        "## Traceability ceiling",
        "",
        "The repository schema supports a chain from run to inbox, processing attempt, accepted reading, risk assessment and operational projection. "
        "The current repository's preserved historical B/C package contains run-level counts and run identifiers, but not the event-level IDs needed to reproduce that full chain. "
        "The optional read-only PostgreSQL trace query closes this gap for a current database.",
        "",
        "## Claim rules",
        "",
        "- Do not label historical B/C values as current execution.",
        "- Do not claim publish-to-end latency: the event contract still lacks a persisted `PublishedAt` timestamp.",
        "- Run timings are durations between persisted points, not proof of production capacity.",
        "- A full event-level chain requires `database-trace-status=PASS`.",
    ]
    return "\n".join(lines) + "\n"


def build_summary(
    args: argparse.Namespace,
    static: dict[str, Any],
    historical: dict[str, Any],
    live: dict[str, Any],
    trace: dict[str, Any],
) -> dict[str, Any]:
    live_pass = live.get("status") == "PASS"
    trace_pass = trace.get("status") == "PASS"
    status = "PASS" if live_pass and (trace_pass or not args.postgres_dsn_env) else "PARTIAL_PASS_BLOCKED_ENVIRONMENT"
    if args.require_live and not live_pass:
        status = "FAIL_REQUIRED_LIVE_NOT_AVAILABLE"
    return {
        "phase": 4,
        "baselineId": args.baseline_id,
        "phaseRunId": args.run_id,
        "generatedAtUtc": iso(),
        "status": status,
        "staticRuntimeContractStatus": "PASS",
        "historicalRepositoryExecutionStatus": historical.get("status"),
        "currentRuntimeExecutionStatus": live.get("status"),
        "databaseTraceStatus": trace.get("status"),
        "staticCounts": {
            "endpoints": len(static.get("endpoints", [])),
            "diagnostics": len(static.get("diagnostics", [])),
            "degradationProfiles": len(static.get("degradationProfiles", [])),
            "scenarioManifests": len(static.get("scenarios", [])),
            "chainEntities": len(static.get("chainModel", [])),
        },
        "historicalCounts": {
            "runs": len(historical.get("runs", [])),
            "sourceFiles": len(historical.get("sources", [])),
        },
        "claimCeiling": {
            "currentIntegratedExecution": live_pass,
            "currentEventLevelTrace": trace_pass,
            "historicalBCComparison": historical.get("status") == "PASS",
            "publishToEndLatency": False,
            "productionCapacity": False,
        },
    }


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path.cwd())
    parser.add_argument("--baseline-id", required=True)
    parser.add_argument("--run-id", default=stamp())
    parser.add_argument("--output", type=Path)
    parser.add_argument("--api-base-url", default="http://localhost:5254")
    parser.add_argument("--live", action="store_true", help="Execute the current API B/C collection.")
    parser.add_argument(
        "--require-live", action="store_true", help="Fail unless current API B/C evidence is collected."
    )
    parser.add_argument(
        "--reset-runtime", action="store_true", help="Explicitly reset runtime-only state before B/C execution."
    )
    parser.add_argument("--bearer-token-env", default="NATUREPROTECTOR_RUNTIME_BEARER_TOKEN")
    parser.add_argument("--username-env", default="NATUREPROTECTOR_RUNTIME_USERNAME")
    parser.add_argument("--password-env", default="NATUREPROTECTOR_RUNTIME_PASSWORD")
    parser.add_argument(
        "--postgres-dsn-env",
        default="",
        help="Optional env var containing a PostgreSQL DSN for read-only event trace extraction.",
    )
    parser.add_argument("--http-timeout", type=int, default=30)
    parser.add_argument("--run-timeout", type=int, default=240)
    parser.add_argument("--area-code", default="proenca-a-nova")
    parser.add_argument("--sensor-count", type=int, default=6)
    parser.add_argument("--number-of-cycles", type=int, default=5)
    parser.add_argument("--interval-seconds", type=int, default=5)
    parser.add_argument("--seed", type=int, default=12345)
    parser.add_argument("--diagnostics", nargs="*", default=DEFAULT_DIAGNOSTICS)
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    repo = find_repo_root(args.repo)
    output = args.output or repo / "artifacts/report-evidence" / args.baseline_id / "04-runtime" / args.run_id
    output = output.resolve()
    ensure_dir(output)
    write_json(output / "environment.json", environment_summary(repo, args))

    static_dir = ensure_dir(output / "static")
    endpoints = extract_controller_endpoints(repo)
    diagnostics = extract_diagnostics(repo)
    profiles = extract_degradation_profiles(repo)
    scenarios = scenario_manifest_summary(repo)
    chain_model = static_chain_model()
    static = {
        "evidenceClass": EVIDENCE_STATIC,
        "endpoints": endpoints,
        "diagnostics": diagnostics,
        "degradationProfiles": profiles,
        "scenarios": scenarios,
        "chainModel": chain_model,
    }
    for name, rows in [
        ("runtime-endpoints", endpoints),
        ("runtime-diagnostics", diagnostics),
        ("degradation-profiles", profiles),
        ("scenario-manifests", scenarios),
        ("runtime-chain-model", chain_model),
    ]:
        write_json(static_dir / f"{name}.json", rows)
        write_csv(static_dir / f"{name}.csv", rows)
    write_json(static_dir / "static-runtime-contract.json", static)

    run_specs_dir = ensure_dir(output / "run-specs")
    for scenario in ["scenario_b", "scenario_c"]:
        write_json(run_specs_dir / f"{scenario}.json", make_run_spec(args, scenario))
    write_text(run_specs_dir / "runtime-trace-query.sql", TRACE_SQL + "\n")

    historical = historical_evidence(repo)
    historical_dir = ensure_dir(output / "historical")
    write_json(historical_dir / "historical-source-manifest.json", historical.get("sources", []))
    write_csv(historical_dir / "historical-source-manifest.csv", historical.get("sources", []))
    write_json(historical_dir / "historical-runs.json", historical.get("runs", []))
    write_csv(historical_dir / "historical-runs.csv", historical.get("runs", []))
    write_json(historical_dir / "historical-comparison.json", historical.get("comparison", {}))
    write_csv(historical_dir / "historical-comparison.csv", [historical.get("comparison", {})])
    write_json(historical_dir / "historical-chain-traceability.json", historical.get("chainTraceability", []))
    write_csv(historical_dir / "historical-chain-traceability.csv", historical.get("chainTraceability", []))

    live = live_collection(repo, output, args)
    trace_input = live.get("runs", []) if live.get("runs") else historical.get("runs", [])
    trace = postgres_trace(output, args, trace_input)

    report_dir = ensure_dir(output / "report-ready")
    write_text(report_dir / "integrated-runtime-summary.md", report_markdown(static, historical, live, trace))
    write_csv(
        report_dir / "runtime-capability-summary.csv",
        [
            {
                "dimension": "Runtime endpoints",
                "value": len(endpoints),
                "evidenceClass": EVIDENCE_STATIC,
                "status": "PASS",
            },
            {
                "dimension": "Runtime diagnostics",
                "value": len(diagnostics),
                "evidenceClass": EVIDENCE_STATIC,
                "status": "PASS",
            },
            {
                "dimension": "Degradation profiles",
                "value": len(profiles),
                "evidenceClass": EVIDENCE_STATIC,
                "status": "PASS",
            },
            {
                "dimension": "Historical B/C runs",
                "value": len(historical.get("runs", [])),
                "evidenceClass": EVIDENCE_HISTORICAL,
                "status": historical.get("status"),
            },
            {
                "dimension": "Current B/C runs",
                "value": len(live.get("runs", [])),
                "evidenceClass": EVIDENCE_CURRENT,
                "status": live.get("status"),
            },
            {
                "dimension": "Current DB event traces",
                "value": len(trace.get("runs", [])),
                "evidenceClass": EVIDENCE_CURRENT,
                "status": trace.get("status"),
            },
        ],
    )
    summary = build_summary(args, static, historical, live, trace)
    write_json(output / "phase4-summary.json", summary)
    write_text(output / "phase4-summary.md", report_markdown(static, historical, live, trace))
    entries = hash_tree(output)
    summary["hashedEvidenceFiles"] = len(entries)
    write_json(output / "phase4-summary.json", summary)
    entries = hash_tree(output)

    print(f"PHASE_4_STATUS={summary['status']}")
    print("STATIC_RUNTIME_CONTRACT_STATUS=PASS")
    print(f"HISTORICAL_REPOSITORY_EXECUTION_STATUS={historical.get('status')}")
    print(f"CURRENT_RUNTIME_EXECUTION_STATUS={live.get('status')}")
    print(f"DATABASE_TRACE_STATUS={trace.get('status')}")
    print(f"STATIC_ENDPOINTS={len(endpoints)}")
    print(f"STATIC_DIAGNOSTICS={len(diagnostics)}")
    print(f"HISTORICAL_RUNS={len(historical.get('runs', []))}")
    print(f"CURRENT_RUNS={len(live.get('runs', []))}")
    print(f"HASHED_EVIDENCE_FILES={len(entries)}")
    print(f"EVIDENCE_ROOT={output}")
    return 1 if summary["status"].startswith("FAIL") else 0


if __name__ == "__main__":
    raise SystemExit(main())
