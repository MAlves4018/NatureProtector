#!/usr/bin/env python3
"""Execute and verify long-running NatureProtector simulation operations.

Every case is correlated by OperationId and SimulationRunId. The runner never
uses /runs/latest and never treats producer completion as system completion.
The matrix can express successful, timed-out and validation-rejected cases;
an expected rejection is evidence, not a runner crash.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable

TERMINAL_STATES = {"SystemCompleted", "Failed", "TimedOut", "Cancelled", "Orphaned", "Rejected"}
SUCCESS_TERMINAL = "SystemCompleted"


class ApiError(RuntimeError):
    """HTTP failure with enough structured detail for expected rejection cases."""

    def __init__(self, status_code: int, method: str, path: str, body: str):
        super().__init__(f"HTTP {status_code} {method} {path}: {body[:2000]}")
        self.status_code = status_code
        self.method = method
        self.path = path
        self.body = body


class InfrastructurePreconditionError(RuntimeError):
    """A deterministic external-store precondition failed before the run matrix."""


def utc_now() -> datetime:
    return datetime.now(timezone.utc)


def utc_iso(value: datetime | None = None) -> str:
    return (value or utc_now()).isoformat().replace("+00:00", "Z")


def parse_timestamp(value: str | None) -> datetime | None:
    if not value:
        return None
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def derive_termination_reason(operation: dict[str, Any]) -> str:
    outcome = str(operation.get("terminalOutcome") or operation.get("state") or "")
    failure_code = str(operation.get("failureCode") or "").lower()
    if outcome == "SystemCompleted":
        return "CompletedNormally"
    if outcome == "Cancelled":
        return "UserCancelled"
    if outcome == "TimedOut":
        return "ConfiguredTimeout"
    if "exit_nonzero" in failure_code or "process_exit" in failure_code:
        return "ProcessExitedNonZero"
    if outcome == "Rejected":
        return "RequestRejected"
    if outcome == "Failed":
        return "ProviderFailure"
    if outcome == "Orphaned":
        return "OrphanReconciled"
    return "Unknown"


def observed_wall_seconds(operation: dict[str, Any]) -> float | None:
    started = parse_timestamp(operation.get("startedAt")) or parse_timestamp(operation.get("acceptedAt"))
    finished = (
        parse_timestamp(operation.get("systemCompletedAt"))
        or parse_timestamp(operation.get("finishedAt"))
        or parse_timestamp(operation.get("updatedAt"))
    )
    if not started or not finished:
        return None
    return max(0.0, (finished - started).total_seconds())


def expected_terminal_outcomes(case: dict[str, Any]) -> list[str]:
    values = case.get("expectedTerminalOutcomes")
    if isinstance(values, list) and values:
        return [str(value) for value in values]
    value = case.get("expectedOutcome")
    return [str(value)] if value else [SUCCESS_TERMINAL]


def evaluate_case(case: dict[str, Any], operation: dict[str, Any]) -> dict[str, Any]:
    duration = observed_wall_seconds(operation)
    reason = derive_termination_reason(operation)
    expected_minimum = float(case.get("expectedMinimumWallSeconds", 0))
    terminal = str(operation.get("terminalOutcome") or operation.get("state") or "")
    expected_outcomes = expected_terminal_outcomes(case)
    accounting = operation.get("accounting") or {}
    failures: list[str] = []

    if terminal not in expected_outcomes:
        failures.append(f"terminal outcome is {terminal!r}, expected one of {expected_outcomes!r}")
    if reason == "Unknown":
        failures.append("termination reason is Unknown")

    is_rejection_case = "Rejected" in expected_outcomes
    require_duration = bool(case.get("requireDuration", not is_rejection_case))
    if require_duration:
        if duration is None:
            failures.append("observed wall duration is unavailable")
        elif duration < expected_minimum:
            failures.append(f"observed duration {duration:.3f}s is below minimum {expected_minimum:.3f}s")
        if expected_minimum > 75 and duration is not None and 55 <= duration <= 75:
            failures.append("run terminated in the historical one-minute cutoff window")

    require_settlement = bool(case.get("requireSettlement", SUCCESS_TERMINAL in expected_outcomes))
    if require_settlement:
        if not bool(accounting.get("settled")):
            failures.append("run-scoped pipeline accounting is not settled")
        if int(accounting.get("pendingInbox", 0)) != 0:
            failures.append("pending inbox is not zero")
        if int(accounting.get("processingInbox", 0)) != 0:
            failures.append("processing inbox is not zero")
        if int(accounting.get("retryPendingInbox", 0)) != 0:
            failures.append("retry-pending inbox is not zero")
        expected = int(accounting.get("expectedObservations", 0))
        accepted = int(accounting.get("acceptedObservations", 0))
        processed = int(accounting.get("processedInbox", 0))
        quarantined = int(accounting.get("quarantinedInbox", 0))
        if expected <= 0 or accepted != expected:
            failures.append(f"expected/accepted mismatch: {expected}/{accepted}")
        if quarantined != 0:
            failures.append(f"quarantined inbox is {quarantined}, expected zero")
        if processed != accepted:
            failures.append(f"processed/accepted mismatch: {processed}/{accepted}")

    return {
        "caseId": case["id"],
        "status": "PASS" if not failures else "FAIL",
        "terminationReason": reason,
        "terminalOutcome": terminal,
        "expectedTerminalOutcomes": expected_outcomes,
        "observedWallSeconds": None if duration is None else round(duration, 3),
        "expectedMinimumWallSeconds": expected_minimum,
        "operationId": operation.get("operationId"),
        "simulationRunId": operation.get("simulationRunId"),
        "httpStatus": operation.get("httpStatus"),
        "accounting": accounting,
        "failures": failures,
    }


class ApiClient:
    def __init__(self, base_url: str, token: str | None, timeout: float = 30.0):
        self.base_url = base_url.rstrip("/")
        self.token = token
        self.timeout = timeout

    def request(
        self,
        method: str,
        path: str,
        body: dict[str, Any] | None = None,
        authenticated: bool = True,
        timeout_seconds: float | None = None,
    ) -> Any:
        headers = {"Accept": "application/json"}
        evidence_run_id = os.getenv("NP_EVIDENCE_RUN_ID", "").strip()
        if evidence_run_id:
            headers["X-NP-Evidence-Run-Id"] = evidence_run_id
        data = None
        if body is not None:
            headers["Content-Type"] = "application/json"
            data = json.dumps(body).encode("utf-8")
        if authenticated and self.token:
            headers["Authorization"] = f"Bearer {self.token}"
        request = urllib.request.Request(self.base_url + path, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(request, timeout=timeout_seconds or self.timeout) as response:
                raw = response.read().decode("utf-8")
                return json.loads(raw) if raw else None
        except urllib.error.HTTPError as error:
            raw = error.read().decode("utf-8", errors="replace")
            raise ApiError(error.code, method, path, raw) from error

    def login(self, username: str, password: str) -> None:
        result = self.request(
            "POST",
            "/api/users-roles/login",
            {"usernameOrEmail": username, "password": password},
            authenticated=False,
        )
        token = (result or {}).get("token")
        if not token:
            raise RuntimeError("Login returned no token.")
        self.token = str(token)


def load_matrix(path: Path) -> list[dict[str, Any]]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    cases = payload.get("cases")
    if not isinstance(cases, list) or not cases:
        raise ValueError("Long-run proof matrix must contain at least one case.")
    required = {
        "id",
        "numberOfCycles",
        "intervalSeconds",
        "collectEvidence",
        "waitForCompletion",
        "timeoutSeconds",
    }
    for case in cases:
        missing = sorted(required - set(case))
        if missing:
            raise ValueError(f"Case {case.get('id', '<unknown>')} is missing: {missing}")
        case.setdefault("expectedMinimumWallSeconds", 0)
        outcomes = expected_terminal_outcomes(case)
        unknown = sorted(set(outcomes) - TERMINAL_STATES)
        if unknown:
            raise ValueError(f"Case {case['id']} has unknown expected outcomes: {unknown}")
    return cases


def poll_operation(client: ApiClient, operation_id: str, poll_seconds: float, max_wait_seconds: float) -> dict[str, Any]:
    deadline = time.monotonic() + max_wait_seconds
    last: dict[str, Any] | None = None
    while time.monotonic() < deadline:
        last = client.request("GET", f"/api/control/runtime/operations/{operation_id}")
        state = str((last or {}).get("terminalOutcome") or (last or {}).get("state") or "")
        if state in TERMINAL_STATES:
            return last
        time.sleep(poll_seconds)
    raise TimeoutError(f"Operation {operation_id} did not become terminal within {max_wait_seconds}s. Last={last}")


def wait_for_pipeline_settlement(
    client: ApiClient,
    operation: dict[str, Any],
    poll_seconds: float,
    max_wait_seconds: float,
) -> dict[str, Any]:
    operation_id = operation.get("operationId")
    terminal = str(operation.get("terminalOutcome") or operation.get("state") or "")
    if not operation_id or terminal != SUCCESS_TERMINAL:
        return operation

    deadline = time.monotonic() + max(0.0, max_wait_seconds)
    last = operation
    while True:
        accounting = last.get("accounting") or {}
        expected = int(accounting.get("expectedObservations", 0))
        accepted = int(accounting.get("acceptedObservations", 0))
        processed = int(accounting.get("processedInbox", 0))
        quarantined = int(accounting.get("quarantinedInbox", 0))
        queues_empty = (
            int(accounting.get("pendingInbox", 0)) == 0
            and int(accounting.get("processingInbox", 0)) == 0
            and int(accounting.get("retryPendingInbox", 0)) == 0
        )
        complete_expected_coverage = expected <= 0 or accepted >= expected
        drained_accounting = processed + quarantined == accepted
        if (
            bool(accounting.get("settled"))
            and queues_empty
            and complete_expected_coverage
            and drained_accounting
        ):
            return last
        if time.monotonic() >= deadline:
            return last
        time.sleep(max(0.5, poll_seconds))
        last = client.request("GET", f"/api/control/runtime/operations/{operation_id}")


def reset_runtime(client: ApiClient, *, dry_run: bool = False) -> dict[str, Any]:
    return client.request(
        "POST",
        "/api/control/runtime/reset",
        {
            "scope": "runtime-only",
            "confirm": "RESET_RUNTIME_STATE",
            "dryRun": dry_run,
            "requireExternalStores": True,
            "reconcileTerminalOrphans": True,
        },
    )


def reset_error_is_infrastructure_precondition(error: ApiError) -> bool:
    body = error.body.lower()
    return error.status_code == 400 and (
        "configuration is incomplete" in body
        or "configured rabbitmq and influxdb" in body
        or '"store":"influxdb"' in body and '"status":"unavailable"' in body
        or "influxdb" in body and "unavailable" in body
    )


def reset_error_is_transient_busy(error: ApiError) -> bool:
    body = error.body.lower()
    if error.status_code != 400 or reset_error_is_infrastructure_precondition(error):
        return False
    return (
        "unacknowledged" in body
        or '"status":"busy"' in body
        or "active runtime" in body
        or "pending inbox" in body
        or "processing inbox" in body
        or "retry-pending" in body
        or "requires quiescent" in body
    )


def reset_runtime_when_quiescent(
    client: ApiClient,
    poll_seconds: float,
    max_wait_seconds: float,
    *,
    dry_run: bool = False,
) -> dict[str, Any]:
    deadline = time.monotonic() + max_wait_seconds

    while True:
        try:
            return reset_runtime(client, dry_run=dry_run)
        except ApiError as error:
            if reset_error_is_infrastructure_precondition(error):
                raise InfrastructurePreconditionError(str(error)) from error
            if not reset_error_is_transient_busy(error):
                raise
            if time.monotonic() >= deadline:
                raise TimeoutError(
                    f"Runtime did not become quiescent within {max_wait_seconds}s. "
                    f"Last reset response: {error}"
                ) from error
            time.sleep(max(0.5, poll_seconds))


def write_failed_preflight(output: Path, started: str, error: Exception, args: argparse.Namespace) -> None:
    result = {
        "status": "FAIL",
        "errorType": type(error).__name__,
        "error": str(error),
        "capturedAtUtc": utc_iso(),
    }
    write_json(output / "preflight-reset.json", result)
    summary = {
        "schemaVersion": 2,
        "status": "FAIL",
        "legacyStatus": "LONG_RUN_STABILITY_FAIL",
        "startedAtUtc": started,
        "finishedAtUtc": utc_iso(),
        "baseUrl": args.base_url,
        "matrix": str(args.matrix),
        "areaCode": args.area_code,
        "scenarioCode": args.scenario_code,
        "seed": args.seed,
        "infrastructurePreconditionStatus": "FAIL",
        "cases": [],
        "failures": [result],
    }
    write_json(output / "summary.json", summary)
    write_csv(output / "matrix.csv", [])
    write_csv(output / "LONG_RUN_TERMINATION_MATRIX.csv", [])
    write_dynamic_csv(
        output / "timeline.csv",
        [],
        ["caseId", "event", "timestamp", "operationId", "simulationRunId", "state"],
    )
    write_dynamic_csv(
        output / "process-observations.csv",
        [],
        [
            "caseId",
            "operationId",
            "simulationRunId",
            "processId",
            "providerExitCode",
            "producerState",
            "pipelineState",
            "terminalOutcome",
        ],
    )
    write_hashes(output)


def rejected_operation(case: dict[str, Any], error: ApiError, started_at: str) -> tuple[dict[str, Any], dict[str, Any]]:
    body: dict[str, Any] | str
    try:
        parsed = json.loads(error.body)
        body = parsed if isinstance(parsed, dict) else error.body[:2000]
    except json.JSONDecodeError:
        body = error.body[:2000]
    now = utc_iso()
    acceptance = {"status": "Rejected", "httpStatus": error.status_code, "response": body}
    operation = {
        "state": "Rejected",
        "terminalOutcome": "Rejected",
        "failureCode": f"http_{error.status_code}",
        "failureMessage": body,
        "httpStatus": error.status_code,
        "acceptedAt": started_at,
        "finishedAt": now,
        "updatedAt": now,
        "accounting": {},
        "caseId": case["id"],
    }
    return acceptance, operation


def execute_case(
    client: ApiClient,
    case: dict[str, Any],
    area_code: str,
    scenario_code: str,
    seed: int,
    poll_seconds: float,
    settlement_grace_seconds: int,
    reset_before: bool,
) -> tuple[dict[str, Any], dict[str, Any]]:
    if reset_before:
        reset_runtime_when_quiescent(
            client,
            poll_seconds=poll_seconds,
            max_wait_seconds=max(30.0, float(settlement_grace_seconds)),
        )
    payload = {
        "areaCode": area_code,
        "scenarioCode": scenario_code,
        "numberOfCycles": int(case["numberOfCycles"]),
        "intervalSeconds": int(case["intervalSeconds"]),
        "seed": seed,
        "degradationProfile": str(case.get("degradationProfile") or "none"),
        "collectEvidence": bool(case["collectEvidence"]),
        "waitForCompletion": bool(case["waitForCompletion"]),
        "timeoutSeconds": int(case["timeoutSeconds"]),
        "runLabel": str(case["id"]),
    }
    started_at = utc_iso()
    nominal = max(1, int(case["numberOfCycles"])) * max(1, int(case["intervalSeconds"]))
    request_timeout = float(case.get("requestTimeoutSeconds") or client.timeout)
    if bool(case["waitForCompletion"]):
        request_timeout = max(request_timeout, nominal + settlement_grace_seconds + 60)
    try:
        accepted = client.request(
            "POST",
            "/api/control/runtime/runs",
            payload,
            timeout_seconds=request_timeout,
        )
    except ApiError as error:
        if "Rejected" in expected_terminal_outcomes(case) and 400 <= error.status_code < 500:
            return rejected_operation(case, error, started_at)
        raise

    operation_id = (accepted or {}).get("operationId")
    terminal = str((accepted or {}).get("terminalOutcome") or (accepted or {}).get("state") or "")
    if not operation_id:
        if terminal in TERMINAL_STATES:
            return accepted, accepted
        raise RuntimeError(f"Run start did not return OperationId: {accepted}")
    if terminal in TERMINAL_STATES:
        operation = wait_for_pipeline_settlement(
            client,
            accepted,
            poll_seconds=poll_seconds,
            max_wait_seconds=float(settlement_grace_seconds),
        )
        return accepted, operation

    predicted = max(int(case["timeoutSeconds"]), nominal)
    operation = poll_operation(
        client,
        str(operation_id),
        poll_seconds=poll_seconds,
        max_wait_seconds=predicted + settlement_grace_seconds + 60,
    )
    operation = wait_for_pipeline_settlement(
        client,
        operation,
        poll_seconds=poll_seconds,
        max_wait_seconds=float(settlement_grace_seconds),
    )
    return accepted, operation


def write_csv(path: Path, rows: Iterable[dict[str, Any]]) -> None:
    rows = list(rows)
    fieldnames = [
        "caseId",
        "status",
        "operationId",
        "simulationRunId",
        "terminalOutcome",
        "terminationReason",
        "observedWallSeconds",
        "expectedMinimumWallSeconds",
        "expectedTerminalOutcomes",
        "httpStatus",
        "failures",
    ]
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            writer.writerow(
                {field: row.get(field) for field in fieldnames[:-2]}
                | {
                    "httpStatus": row.get("httpStatus"),
                    "failures": " | ".join(row.get("failures") or []),
                    "expectedTerminalOutcomes": " | ".join(row.get("expectedTerminalOutcomes") or []),
                }
            )


def timeline_rows(case: dict[str, Any], operation: dict[str, Any]) -> list[dict[str, Any]]:
    mapping = [
        ("request_started", "requestStartedAt"),
        ("accepted", "acceptedAt"),
        ("provider_started", "startedAt"),
        ("producer_completed", "producerCompletedAt"),
        ("system_completed", "systemCompletedAt"),
        ("finished", "finishedAt"),
        ("updated", "updatedAt"),
    ]
    rows = []
    for event, field in mapping:
        value = operation.get(field)
        if value:
            rows.append(
                {
                    "caseId": case["id"],
                    "event": event,
                    "timestamp": value,
                    "operationId": operation.get("operationId"),
                    "simulationRunId": operation.get("simulationRunId"),
                    "state": operation.get("terminalOutcome") or operation.get("state"),
                }
            )
    return rows


def write_dynamic_csv(path: Path, rows: Iterable[dict[str, Any]], fieldnames: list[str]) -> None:
    rows = list(rows)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            writer.writerow({name: row.get(name) for name in fieldnames})


def write_hashes(root: Path) -> None:
    target = root / "hashes.sha256"
    files = sorted(path for path in root.rglob("*") if path.is_file() and path != target)
    target.write_text(
        "\n".join(f"{sha256(path)}  {path.relative_to(root).as_posix()}" for path in files) + "\n",
        encoding="utf-8",
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", default="http://localhost:5254")
    parser.add_argument("--matrix", type=Path, default=Path("config/runtime/long-run-proof-matrix.json"))
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--area-code", default="proenca-a-nova")
    parser.add_argument("--scenario-code", default="scenario_b")
    parser.add_argument("--seed", type=int, default=20260714)
    parser.add_argument("--poll-seconds", type=float, default=2.0)
    parser.add_argument("--settlement-grace-seconds", type=int, default=180)
    parser.add_argument("--bearer-token-env", default="NATUREPROTECTOR_RUNTIME_BEARER_TOKEN")
    parser.add_argument("--username-env", default="NATUREPROTECTOR_RUNTIME_USERNAME")
    parser.add_argument("--password-env", default="NATUREPROTECTOR_RUNTIME_PASSWORD")
    parser.add_argument("--no-reset", action="store_true")
    parser.add_argument("--case", action="append", dest="case_ids")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    (output / "termination-manifests").mkdir(parents=True, exist_ok=True)
    (output / "logs").mkdir(parents=True, exist_ok=True)
    matrix = load_matrix(args.matrix.resolve())
    if args.case_ids:
        selected = set(args.case_ids)
        matrix = [case for case in matrix if case["id"] in selected]
        missing = sorted(selected - {case["id"] for case in matrix})
        if missing:
            raise ValueError(f"Unknown case IDs: {missing}")

    bearer_token = os.getenv(args.bearer_token_env)
    username = os.getenv(args.username_env)
    password = os.getenv(args.password_env)
    client = ApiClient(args.base_url, None)

    if username and password:
        client.login(username, password)
    elif bearer_token:
        client.token = bearer_token
    else:
        raise RuntimeError(
            f"Set {args.bearer_token_env} or both {args.username_env}/{args.password_env}."
        )

    results: list[dict[str, Any]] = []
    timelines: list[dict[str, Any]] = []
    process_observations: list[dict[str, Any]] = []
    started = utc_iso()
    if not args.no_reset:
        try:
            preflight = reset_runtime_when_quiescent(
                client,
                poll_seconds=args.poll_seconds,
                max_wait_seconds=min(30.0, max(5.0, float(args.settlement_grace_seconds))),
                dry_run=True,
            )
            write_json(output / "preflight-reset.json", {"status": "PASS", "response": preflight})
        except Exception as error:
            write_failed_preflight(output, started, error, args)
            print(json.dumps(json.loads((output / "summary.json").read_text(encoding="utf-8")), indent=2))
            return 1
    for case in matrix:
        case_root = output / str(case["id"])
        case_root.mkdir(parents=True, exist_ok=True)
        accepted: dict[str, Any] = {}
        operation: dict[str, Any] = {}
        try:
            accepted, operation = execute_case(
                client,
                case,
                args.area_code,
                args.scenario_code,
                args.seed,
                args.poll_seconds,
                args.settlement_grace_seconds,
                reset_before=not args.no_reset,
            )
            result = evaluate_case(case, operation)
            run_id = operation.get("simulationRunId")
            if run_id:
                write_json(case_root / "run.json", client.request("GET", f"/api/control/runtime/runs/{run_id}"))
                write_json(case_root / "audit.json", client.request("GET", f"/api/control/runtime/runs/{run_id}/audit"))
                write_json(case_root / "timings.json", client.request("GET", f"/api/control/runtime/runs/{run_id}/timings"))
        except Exception as error:  # Continue the matrix and preserve a per-case failure artifact.
            result = {
                "caseId": case["id"],
                "status": "FAIL",
                "terminationReason": "RunnerFailure",
                "terminalOutcome": operation.get("terminalOutcome") or operation.get("state"),
                "expectedTerminalOutcomes": expected_terminal_outcomes(case),
                "observedWallSeconds": observed_wall_seconds(operation),
                "expectedMinimumWallSeconds": float(case.get("expectedMinimumWallSeconds", 0)),
                "operationId": operation.get("operationId"),
                "simulationRunId": operation.get("simulationRunId"),
                "httpStatus": getattr(error, "status_code", None),
                "accounting": operation.get("accounting") or {},
                "failures": [f"{type(error).__name__}: {error}"],
            }
            (output / "logs" / f"{case['id']}.error.log").write_text(str(error) + "\n", encoding="utf-8")

        write_json(case_root / "request.json", {"case": case, "areaCode": args.area_code, "scenarioCode": args.scenario_code})
        write_json(case_root / "acceptance.json", accepted)
        write_json(case_root / "operation.json", operation)
        results.append(result)
        timelines.extend(timeline_rows(case, operation))
        process_observations.append(
            {
                "caseId": case["id"],
                "operationId": operation.get("operationId"),
                "simulationRunId": operation.get("simulationRunId"),
                "processId": operation.get("processId") or operation.get("providerProcessId"),
                "providerExitCode": operation.get("providerExitCode"),
                "producerState": operation.get("producerState"),
                "pipelineState": operation.get("pipelineState"),
                "terminalOutcome": operation.get("terminalOutcome") or operation.get("state"),
            }
        )
        write_json(case_root / "verdict.json", result)
        write_json(output / "termination-manifests" / f"{case['id']}.json", result)
        write_hashes(case_root)

    status = "PASS" if results and all(row["status"] == "PASS" for row in results) else "FAIL"
    summary = {
        "schemaVersion": 2,
        "status": status,
        "legacyStatus": "LONG_RUN_STABILITY_PASS" if status == "PASS" else "LONG_RUN_STABILITY_FAIL",
        "startedAtUtc": started,
        "finishedAtUtc": utc_iso(),
        "baseUrl": args.base_url,
        "matrix": str(args.matrix),
        "areaCode": args.area_code,
        "scenarioCode": args.scenario_code,
        "seed": args.seed,
        "cases": results,
    }
    write_json(output / "summary.json", summary)
    write_csv(output / "matrix.csv", results)
    write_csv(output / "LONG_RUN_TERMINATION_MATRIX.csv", results)
    write_dynamic_csv(
        output / "timeline.csv",
        timelines,
        ["caseId", "event", "timestamp", "operationId", "simulationRunId", "state"],
    )
    write_dynamic_csv(
        output / "process-observations.csv",
        process_observations,
        [
            "caseId",
            "operationId",
            "simulationRunId",
            "processId",
            "providerExitCode",
            "producerState",
            "pipelineState",
            "terminalOutcome",
        ],
    )
    write_hashes(output)
    print(json.dumps(summary, indent=2))
    return 0 if status == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
