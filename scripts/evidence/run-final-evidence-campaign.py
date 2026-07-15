#!/usr/bin/env python3
"""Build a run-scoped, hashable NatureProtector final evidence portfolio.

Modes:
- plan: produce a reviewed execution plan without contacting services.
- synthetic: exercise the complete artifact pipeline with explicitly synthetic data.
- live: call the runtime API and optional reviewed commands.

Synthetic artifacts can validate tooling but can never satisfy the live report gate.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import shutil
import subprocess
import time
import urllib.error
import urllib.request
import urllib.parse
import base64
import uuid
from datetime import datetime, timezone
from pathlib import Path
import sys
SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))
from typing import Any

from proof_contracts import claim_assertions, validate_case_tree

TERMINAL_STATES = {"SystemCompleted", "Failed", "Cancelled", "TimedOut", "Orphaned", "Rejected"}
SUCCESS_STATES = {"SystemCompleted"}


def utc_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def utc_stamp() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def write_csv(path: Path, rows: list[dict[str, Any]], fields: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)


def digest(path: Path) -> str:
    value = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            value.update(chunk)
    return value.hexdigest()


def write_hashes(root: Path) -> int:
    target = root / "hashes.sha256"
    files = sorted(path for path in root.rglob("*") if path.is_file() and path != target)
    target.write_text(
        "\n".join(f"{digest(path)}  {path.relative_to(root).as_posix()}" for path in files) + "\n",
        encoding="utf-8",
    )
    return len(files)


class ApiClient:
    def __init__(self, base_url: str, token: str | None, timeout: float = 30.0):
        self.base_url = base_url.rstrip("/")
        self.token = token
        self.timeout = timeout

    def request(self, method: str, path: str, payload: dict[str, Any] | None = None, authenticated: bool = True) -> dict[str, Any]:
        body = json.dumps(payload).encode("utf-8") if payload is not None else None
        headers = {"Accept": "application/json"}
        if body is not None:
            headers["Content-Type"] = "application/json"
        if authenticated and self.token:
            headers["Authorization"] = f"Bearer {self.token}"
        request = urllib.request.Request(self.base_url + path, data=body, headers=headers, method=method)
        try:
            with urllib.request.urlopen(request, timeout=self.timeout) as response:
                content = response.read().decode("utf-8")
                return json.loads(content) if content else {}
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode("utf-8", errors="replace")
            raise RuntimeError(f"HTTP {exc.code} for {path}: {detail[:500]}") from exc

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


def load_config(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    ids = [item["id"] for item in value.get("campaigns", [])]
    if ids != ["E1", "E2", "E3", "E4", "E5", "E6"]:
        raise ValueError("Final campaign must define E1-E6 exactly and in order.")
    return value


def fetch_json(url: str, token: str | None = None, basic: tuple[str, str] | None = None) -> Any:
    headers = {"Accept": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    elif basic:
        headers["Authorization"] = "Basic " + base64.b64encode(f"{basic[0]}:{basic[1]}".encode()).decode()
    with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=30) as response:
        raw = response.read().decode("utf-8")
        return json.loads(raw) if raw else {}


def collect_domain_evidence(case_dir: Path, operation_id: str, run_id: str) -> None:
    rabbit_url = os.getenv("RABBITMQ_MANAGEMENT_URL", "http://localhost:15672").rstrip("/")
    rabbit_user = os.getenv("RABBITMQ_DEFAULT_USER") or os.getenv("RABBITMQ_MANAGEMENT_USER")
    rabbit_password = os.getenv("RABBITMQ_DEFAULT_PASS") or os.getenv("RABBITMQ_MANAGEMENT_PASSWORD")
    if not rabbit_user or not rabbit_password:
        raise RuntimeError("RabbitMQ management credentials are required for live evidence.")
    queue = urllib.parse.quote("np.ingestion.readings", safe="")
    rabbit = fetch_json(f"{rabbit_url}/api/queues/%2F/{queue}", basic=(rabbit_user, rabbit_password))
    if not isinstance(rabbit, dict) or "messages" not in rabbit:
        raise RuntimeError("RabbitMQ queue metrics are incomplete.")
    write_json(case_dir / "rabbitmq" / "queue-metrics.json", rabbit)

    influx_url = os.getenv("INFLUXDB_URL", "http://localhost:8181").rstrip("/")
    influx_token = os.getenv("INFLUXDB_TOKEN")
    influx_db = os.getenv("INFLUXDB_DATABASE", "np_telemetry")
    if not influx_token:
        raise RuntimeError("INFLUXDB_TOKEN is required for live evidence.")
    query = f"SELECT * FROM accepted_readings WHERE simulation_run_id = '{run_id}' ORDER BY time DESC LIMIT 100"
    params = urllib.parse.urlencode({"db": influx_db, "q": query, "format": "json"})
    influx = fetch_json(f"{influx_url}/api/v3/query_sql?{params}", token=influx_token)
    if not influx:
        raise RuntimeError("Influx run-scoped query returned no evidence.")
    write_json(case_dir / "influx" / "run-query.json", influx)

    grafana_url = os.getenv("GRAFANA_URL", "http://localhost:3000").rstrip("/")
    grafana_token = os.getenv("GRAFANA_SERVICE_ACCOUNT_TOKEN")
    grafana_user = os.getenv("GRAFANA_ADMIN_USER")
    grafana_password = os.getenv("GRAFANA_ADMIN_PASSWORD")
    if not grafana_token and not (grafana_user and grafana_password):
        raise RuntimeError("Grafana service token or admin credentials are required for live evidence.")
    dashboards = fetch_json(f"{grafana_url}/api/search?tag=natureprotector", token=grafana_token, basic=(grafana_user, grafana_password) if grafana_user and grafana_password else None)
    if not isinstance(dashboards, list) or len(dashboards) < 5:
        raise RuntimeError("Grafana dashboard inventory does not contain the required portfolio.")
    write_json(case_dir / "grafana" / "dashboard-inventory.json", dashboards)


def run_api_case(client: ApiClient, case: dict[str, Any], case_dir: Path, poll_seconds: float) -> dict[str, Any]:
    reset = client.request("POST", "/api/control/runtime/reset", {"scope":"runtime-only","confirm":"RESET_RUNTIME_STATE","dryRun":False,"requireExternalStores":True,"reconcileTerminalOrphans":True})
    write_json(case_dir / "configuration" / "systemic-reset.json", reset)
    if str(reset.get("status", "")).lower() not in {"completed", "success", "passed"}:
        raise RuntimeError(f"Systemic reset did not complete: {reset}")
    payload = {
        "areaCode": case["areaCode"],
        "scenarioCode": case["scenarioCode"],
        "sensorCount": case.get("sensorCount"),
        "numberOfCycles": case.get("numberOfCycles"),
        "intervalSeconds": case.get("intervalSeconds"),
        "seed": case.get("seed"),
        "degradationProfiles": case.get("degradationProfiles", []),
        "collectEvidence": bool(case.get("collectEvidence", True)),
        "waitForCompletion": False,
        "timeoutSeconds": int(case.get("timeoutSeconds", 600)),
        "runLabel": f"final-evidence-{case['id']}",
    }
    write_json(case_dir / "configuration" / "request.json", payload)
    accepted = client.request("POST", "/api/control/runtime/runs", payload)
    write_json(case_dir / "acceptance.json", accepted)
    operation_id = accepted.get("operationId")
    if not operation_id:
        raise RuntimeError(f"Case {case['id']} did not return operationId.")
    deadline = time.monotonic() + int(case.get("timeoutSeconds", 600)) + 180
    history: list[dict[str, Any]] = []
    operation: dict[str, Any] = {}
    while time.monotonic() < deadline:
        operation = client.request("GET", f"/api/control/runtime/operations/{operation_id}")
        history.append({"capturedAtUtc": utc_iso(), "state": operation.get("state"), "providerState": operation.get("providerState"), "runState": operation.get("runState"), "processingState": operation.get("processingState")})
        if operation.get("state") in TERMINAL_STATES:
            break
        time.sleep(poll_seconds)
    write_json(case_dir / "operation.json", operation)
    write_json(case_dir / "logs" / "operation-history.json", history)
    run_id = operation.get("simulationRunId")
    run = audit = timings = None
    if run_id:
        run = client.request("GET", f"/api/control/runtime/runs/{run_id}")
        audit = client.request("GET", f"/api/control/runtime/runs/{run_id}/audit")
        timings = client.request("GET", f"/api/control/runtime/runs/{run_id}/timings")
        write_json(case_dir / "database" / "run.json", run)
        write_json(case_dir / "database" / "audit.json", audit)
        write_json(case_dir / "metrics" / "timings.json", timings)
    accounting = operation.get("accounting") or {}
    assertions = claim_assertions(case["campaignId"], case, operation, run, audit, timings)
    write_json(case_dir / "tables" / "claim-assertions.json", assertions)
    collect_domain_evidence(case_dir, operation_id, str(run_id))
    passed = operation.get("state") in SUCCESS_STATES and bool(accounting.get("settled")) and bool(run_id) and assertions["passed"]
    return {
        "caseId": case["id"],
        "kind": "api-run",
        "status": "PASS" if passed else "FAIL",
        "operationId": operation_id,
        "simulationRunId": run_id,
        "terminalState": operation.get("state"),
        "settled": accounting.get("settled"),
        "limitations": [],
    }


def run_command_case(case: dict[str, Any], case_dir: Path, repo: Path, allow_commands: bool) -> dict[str, Any]:
    command = [str(value) for value in case["command"]]
    write_json(case_dir / "configuration" / "command.json", {"command": command})
    if not allow_commands:
        return {"caseId": case["id"], "kind": "command", "status": "BLOCKED", "operationId": None, "simulationRunId": None, "terminalState": None, "settled": None, "limitations": ["Command execution was not authorized."]}
    executable = shutil.which(command[0])
    if not executable:
        return {"caseId": case["id"], "kind": "command", "status": "BLOCKED", "operationId": None, "simulationRunId": None, "terminalState": None, "settled": None, "limitations": [f"Executable {command[0]} is unavailable."]}
    process = subprocess.run(command, cwd=repo, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False, timeout=3600)
    (case_dir / "logs").mkdir(parents=True, exist_ok=True)
    (case_dir / "logs" / "stdout.txt").write_text(process.stdout or "", encoding="utf-8")
    (case_dir / "logs" / "stderr.txt").write_text(process.stderr or "NO_STDERR_CAPTURED\n", encoding="utf-8")
    assertions = claim_assertions(case["campaignId"], case, None, None, None, None, process.stdout or "")
    write_json(case_dir / "tables" / "claim-assertions.json", assertions)
    passed = process.returncode == 0 and assertions["passed"]
    return {"caseId": case["id"], "kind": "command", "status": "PASS" if passed else "FAIL", "operationId": None, "simulationRunId": None, "terminalState": None, "settled": None, "exitCode": process.returncode, "limitations": assertions["errors"]}


def synthetic_case(campaign_id: str, case: dict[str, Any], case_dir: Path) -> dict[str, Any]:
    operation_id = str(uuid.uuid5(uuid.NAMESPACE_URL, f"natureprotector:{campaign_id}:{case['id']}:operation")) if case["kind"] == "api-run" else None
    run_id = str(uuid.uuid5(uuid.NAMESPACE_URL, f"natureprotector:{campaign_id}:{case['id']}:run")) if case["kind"] == "api-run" else None
    write_json(case_dir / "configuration" / "synthetic.json", {"synthetic": True, "case": case})
    write_json(case_dir / "logs" / "synthetic.json", {"notice": "Synthetic artifact pipeline test; not runtime evidence."})
    if case["kind"] == "api-run":
        write_json(case_dir / "acceptance.json", {"operationId": operation_id, "synthetic": True})
        write_json(case_dir / "operation.json", {"operationId": operation_id, "simulationRunId": run_id, "state": "SystemCompleted", "accounting": {"settled": True}})
        for rel in ("database/run.json","database/audit.json","metrics/timings.json","rabbitmq/queue-metrics.json","influx/run-query.json","grafana/dashboard-inventory.json"):
            write_json(case_dir / rel, {"synthetic": True})
    else:
        write_json(case_dir / "configuration" / "command.json", {"synthetic": True, "command": case.get("command")})
        (case_dir / "logs" / "stdout.txt").write_text("SYNTHETIC PASS\n", encoding="utf-8")
        (case_dir / "logs" / "stderr.txt").write_text("synthetic\n", encoding="utf-8")
    write_json(case_dir / "tables" / "claim-assertions.json", {"passed": False, "synthetic": True, "errors": ["Synthetic evidence cannot prove claims."]})
    return {"caseId": case["id"], "kind": case["kind"], "status": "SYNTHETIC_PASS", "operationId": operation_id, "simulationRunId": run_id, "terminalState": "SystemCompleted" if run_id else None, "settled": True if run_id else None, "limitations": ["Synthetic mode cannot support report claims."]}


def build_case_dir(root: Path, campaign_id: str, case_id: str) -> Path:
    case_dir = root / campaign_id / case_id
    for subdir in ("configuration", "logs", "metrics", "database", "rabbitmq", "influx", "grafana", "screenshots", "tables"):
        (case_dir / subdir).mkdir(parents=True, exist_ok=True)
    return case_dir


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--config", type=Path, default=Path("config/evidence/final-evidence-campaign.json"))
    parser.add_argument("--output-root", type=Path, default=Path("artifacts/report-evidence/final-campaign"))
    parser.add_argument("--mode", choices=("plan", "synthetic", "live"), default="plan")
    parser.add_argument("--api-base-url", default=os.getenv("NATUREPROTECTOR_API_BASE_URL", "http://localhost:5254"))
    parser.add_argument("--bearer-token", default=os.getenv("NATUREPROTECTOR_RUNTIME_BEARER_TOKEN"))
    parser.add_argument("--username", default=os.getenv("NATUREPROTECTOR_USERNAME"))
    parser.add_argument("--password", default=os.getenv("NATUREPROTECTOR_PASSWORD"))
    parser.add_argument("--allow-commands", action="store_true")
    parser.add_argument("--poll-seconds", type=float, default=2.0)
    args = parser.parse_args()
    repo = args.repo.resolve()
    config_path = args.config if args.config.is_absolute() else repo / args.config
    config = load_config(config_path)
    portfolio = (args.output_root if args.output_root.is_absolute() else repo / args.output_root) / f"{utc_stamp()}-{args.mode}"
    portfolio.mkdir(parents=True, exist_ok=False)
    client = ApiClient(args.api_base_url, args.bearer_token)
    if args.mode == "live" and not client.token:
        if not args.username or not args.password:
            raise RuntimeError("Live mode requires --bearer-token or --username/--password.")
        client.login(args.username, args.password)
        os.environ.setdefault("NP_PERFORMANCE_USERNAME", args.username)
        os.environ.setdefault("NP_PERFORMANCE_PASSWORD", args.password)
    if args.mode == "live":
        client.request("GET", "/api/control/runtime/operations/current")
    case_results: list[dict[str, Any]] = []
    evidence_rows: list[dict[str, Any]] = []
    for campaign in config["campaigns"]:
        campaign_results: list[dict[str, Any]] = []
        for configured_case in campaign["cases"]:
            case = {**configured_case, "campaignId": campaign["id"]}
            case_dir = build_case_dir(portfolio, campaign["id"], case["id"])
            try:
                if args.mode == "plan":
                    result = {"caseId": case["id"], "kind": case["kind"], "status": "PLANNED", "operationId": None, "simulationRunId": None, "terminalState": None, "settled": None, "limitations": ["Plan mode did not execute the case."]}
                    write_json(case_dir / "configuration" / "plan.json", case)
                elif args.mode == "synthetic":
                    result = synthetic_case(campaign["id"], case, case_dir)
                elif case["kind"] == "api-run":
                    result = run_api_case(client, case, case_dir, args.poll_seconds)
                else:
                    result = run_command_case(case, case_dir, repo, args.allow_commands)
            except Exception as exc:  # preserve evidence of failed collection
                result = {"caseId": case["id"], "kind": case["kind"], "status": "ERROR", "operationId": None, "simulationRunId": None, "terminalState": None, "settled": None, "limitations": [str(exc)]}
                (case_dir / "logs" / "collector-error.txt").write_text(str(exc) + "\n", encoding="utf-8")
            result["campaignId"] = campaign["id"]
            result["artifact"] = case_dir.relative_to(portfolio).as_posix()
            write_json(case_dir / "verdict.json", result)
            write_hashes(case_dir)
            if args.mode == "live":
                structural_errors = validate_case_tree(case_dir, case["kind"])
                if structural_errors:
                    result["status"] = "FAIL"
                    result.setdefault("limitations", []).extend(structural_errors)
                    write_json(case_dir / "verdict.json", result)
            write_hashes(case_dir)
            campaign_results.append(result)
            case_results.append(result)
            for claim in campaign["claims"]:
                evidence_rows.append({
                    "requirement_id": f"{campaign['id']}-{case['id']}",
                    "claim": claim,
                    "campaign": campaign["id"],
                    "run_id": result.get("simulationRunId") or "",
                    "operation_id": result.get("operationId") or "",
                    "artifact": result["artifact"],
                    "metric_or_query": "run-scoped operation, audit and timings" if case["kind"] == "api-run" else "reviewed command output",
                    "result": result["status"],
                    "report_chapter": ",".join(str(value) for value in campaign["reportChapters"]),
                    "figure_or_table": "TBD after live collection",
                    "limitations": "; ".join(result.get("limitations", [])),
                })
        campaign_statuses = {item["status"] for item in campaign_results}
        if args.mode == "live" and campaign_statuses == {"PASS"}:
            status = "PASS"
        elif args.mode == "synthetic" and campaign_statuses == {"SYNTHETIC_PASS"}:
            status = "SYNTHETIC_PASS"
        elif args.mode == "plan" and campaign_statuses == {"PLANNED"}:
            status = "PLANNED"
        elif "FAIL" in campaign_statuses or "ERROR" in campaign_statuses:
            status = "FAIL"
        else:
            status = "BLOCKED"
        write_json(portfolio / campaign["id"] / "manifest.json", {"schemaVersion": 1, "campaignId": campaign["id"], "title": campaign["title"], "generatedAtUtc": utc_iso(), "mode": args.mode, "cases": campaign_results, "status": status})
    live_pass = args.mode == "live" and case_results and all(item["status"] == "PASS" for item in case_results)
    status = "REPORT_EVIDENCE_PORTFOLIO_READY" if live_pass else ("SYNTHETIC_PORTFOLIO_PASS" if args.mode == "synthetic" and all(item["status"] == "SYNTHETIC_PASS" for item in case_results) else "PLAN_READY" if args.mode == "plan" else "REPORT_EVIDENCE_PORTFOLIO_NOT_READY")
    write_csv(portfolio / "REPORT_EVIDENCE_MATRIX.csv", evidence_rows, ["requirement_id", "claim", "campaign", "run_id", "operation_id", "artifact", "metric_or_query", "result", "report_chapter", "figure_or_table", "limitations"])
    write_json(portfolio / "manifest.json", {"schemaVersion": 1, "generatedAtUtc": utc_iso(), "mode": args.mode, "config": str(config_path.relative_to(repo)), "commit": os.getenv("GITHUB_SHA") or os.getenv("NP_COMMIT") or "unavailable", "environment": os.getenv("NP_ENVIRONMENT", "local"), "status": status, "cases": case_results})
    write_json(portfolio / "verdict.json", {"status": status, "live": live_pass, "synthetic": args.mode == "synthetic", "caseCount": len(case_results), "passed": sum(item["status"] in {"PASS", "SYNTHETIC_PASS", "PLANNED"} for item in case_results), "failed": sum(item["status"] in {"FAIL", "ERROR"} for item in case_results), "limitations": [] if live_pass else ["Only a fully live E1-E6 execution can satisfy REPORT_EVIDENCE_PORTFOLIO_READY."]})
    write_hashes(portfolio)
    print(portfolio)
    print(status)
    return 0 if status in {"REPORT_EVIDENCE_PORTFOLIO_READY", "SYNTHETIC_PORTFOLIO_PASS", "PLAN_READY"} else 1


if __name__ == "__main__":
    raise SystemExit(main())
