#!/usr/bin/env python3
"""Execute and verify long-running NatureProtector simulation operations.

Every case is correlated by OperationId and SimulationRunId.  The runner never
uses /runs/latest and never treats producer completion as system completion.
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
    if outcome in {"Failed", "Rejected"}:
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


def evaluate_case(case: dict[str, Any], operation: dict[str, Any]) -> dict[str, Any]:
    duration = observed_wall_seconds(operation)
    reason = derive_termination_reason(operation)
    expected_minimum = float(case["expectedMinimumWallSeconds"])
    terminal = operation.get("terminalOutcome") or operation.get("state")
    accounting = operation.get("accounting") or {}
    failures: list[str] = []
    if terminal != SUCCESS_TERMINAL:
        failures.append(f"terminal outcome is {terminal!r}, expected {SUCCESS_TERMINAL!r}")
    if reason == "Unknown":
        failures.append("termination reason is Unknown")
    if duration is None:
        failures.append("observed wall duration is unavailable")
    elif duration < expected_minimum:
        failures.append(f"observed duration {duration:.3f}s is below minimum {expected_minimum:.3f}s")
    if case["expectedMinimumWallSeconds"] > 75 and duration is not None and 55 <= duration <= 75:
        failures.append("run terminated in the historical one-minute cutoff window")
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
        "observedWallSeconds": None if duration is None else round(duration, 3),
        "expectedMinimumWallSeconds": expected_minimum,
        "operationId": operation.get("operationId"),
        "simulationRunId": operation.get("simulationRunId"),
        "accounting": accounting,
        "failures": failures,
    }


class ApiClient:
    def __init__(self, base_url: str, token: str | None, timeout: float = 30.0):
        self.base_url = base_url.rstrip("/")
        self.token = token
        self.timeout = timeout

    def request(self, method: str, path: str, body: dict[str, Any] | None = None, authenticated: bool = True) -> Any:
        headers = {"Accept": "application/json"}
        data = None
        if body is not None:
            headers["Content-Type"] = "application/json"
            data = json.dumps(body).encode("utf-8")
        if authenticated and self.token:
            headers["Authorization"] = f"Bearer {self.token}"
        request = urllib.request.Request(self.base_url + path, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(request, timeout=self.timeout) as response:
                raw = response.read().decode("utf-8")
                return json.loads(raw) if raw else None
        except urllib.error.HTTPError as error:
            raw = error.read().decode("utf-8", errors="replace")
            raise RuntimeError(f"HTTP {error.code} {method} {path}: {raw[:2000]}") from error

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
        "expectedMinimumWallSeconds",
    }
    for case in cases:
        missing = sorted(required - set(case))
        if missing:
            raise ValueError(f"Case {case.get('id', '<unknown>')} is missing: {missing}")
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


def reset_runtime(client: ApiClient) -> dict[str, Any]:
    return client.request(
        "POST",
        "/api/control/runtime/reset",
        {
            "scope": "runtime-only",
            "confirm": "RESET_RUNTIME_STATE",
            "dryRun": False,
            "requireExternalStores": True,
            "reconcileTerminalOrphans": True,
        },
    )


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
        reset_runtime(client)
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
    accepted = client.request("POST", "/api/control/runtime/runs", payload)
    operation_id = (accepted or {}).get("operationId")
    if not operation_id:
        raise RuntimeError(f"Run start did not return OperationId: {accepted}")
    predicted = max(
        int(case["timeoutSeconds"]),
        max(1, int(case["numberOfCycles"])) * max(1, int(case["intervalSeconds"])),
    )
    operation = poll_operation(
        client,
        str(operation_id),
        poll_seconds=poll_seconds,
        max_wait_seconds=predicted + settlement_grace_seconds + 60,
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
        "failures",
    ]
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            writer.writerow({field: row.get(field) for field in fieldnames[:-1]} | {"failures": " | ".join(row.get("failures") or [])})


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
    matrix = load_matrix(args.matrix.resolve())
    if args.case_ids:
        selected = set(args.case_ids)
        matrix = [case for case in matrix if case["id"] in selected]
        missing = sorted(selected - {case["id"] for case in matrix})
        if missing:
            raise ValueError(f"Unknown case IDs: {missing}")

    client = ApiClient(args.base_url, os.getenv(args.bearer_token_env))
    if not client.token:
        username = os.getenv(args.username_env)
        password = os.getenv(args.password_env)
        if not username or not password:
            raise RuntimeError(
                f"Set {args.bearer_token_env} or both {args.username_env}/{args.password_env}."
            )
        client.login(username, password)

    results: list[dict[str, Any]] = []
    started = utc_iso()
    for case in matrix:
        case_root = output / str(case["id"])
        case_root.mkdir(parents=True, exist_ok=True)
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
        write_json(case_root / "request.json", {"case": case, "areaCode": args.area_code, "scenarioCode": args.scenario_code})
        write_json(case_root / "acceptance.json", accepted)
        write_json(case_root / "operation.json", operation)
        run_id = operation.get("simulationRunId")
        if run_id:
            write_json(case_root / "run.json", client.request("GET", f"/api/control/runtime/runs/{run_id}"))
            write_json(case_root / "audit.json", client.request("GET", f"/api/control/runtime/runs/{run_id}/audit"))
            write_json(case_root / "timings.json", client.request("GET", f"/api/control/runtime/runs/{run_id}/timings"))
        result = evaluate_case(case, operation)
        results.append(result)
        write_json(case_root / "verdict.json", result)
        write_hashes(case_root)

    status = "LONG_RUN_STABILITY_PASS" if results and all(row["status"] == "PASS" for row in results) else "LONG_RUN_STABILITY_FAIL"
    summary = {
        "schemaVersion": 1,
        "status": status,
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
    write_csv(output / "LONG_RUN_TERMINATION_MATRIX.csv", results)
    write_hashes(output)
    print(json.dumps(summary, indent=2))
    return 0 if status == "LONG_RUN_STABILITY_PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
