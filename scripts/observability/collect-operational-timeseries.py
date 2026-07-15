#!/usr/bin/env python3
"""Convert a correlated runtime snapshot into operational InfluxDB 3 series."""
from __future__ import annotations
import argparse
import json
import os
import sys
import urllib.request
import time
from pathlib import Path
from typing import Any

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "scripts/common"))
from np_line_protocol import point, write_lines  # noqa: E402


def load_snapshot(path: Path | None, url: str | None, token: str | None) -> dict[str, Any]:
    if path:
        return json.loads(path.read_text(encoding="utf-8"))
    if not url:
        raise ValueError("Either --fixture or --url is required.")
    headers = {"Accept": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=30) as response:
        value = json.loads(response.read().decode("utf-8"))
        if isinstance(value, dict) and "operationId" in value and "operation" not in value:
            from datetime import datetime, timezone
            return {"capturedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"), "operation": value}
        return value


def normalize(value: dict[str, Any]) -> list[str]:
    captured = value["capturedAtUtc"]
    operation = value.get("operation", {})
    accounting = operation.get("accounting", {})
    tags = {"state": operation.get("state"), "provider_state": operation.get("providerState")}
    lines = [point("run_progress", tags, {"operation_id": operation.get("operationId") or "", "simulation_run_id": operation.get("simulationRunId") or "", "expected": int(accounting.get("expectedObservations", 0)), "accepted": int(accounting.get("acceptedObservations", 0)), "pending": int(accounting.get("pendingInbox", 0)), "processing": int(accounting.get("processingInbox", 0)), "retry_pending": int(accounting.get("retryPendingInbox", 0)), "settled": bool(accounting.get("settled", False))}, captured)]
    rabbit = value.get("rabbitmq")
    if rabbit:
        lines.append(point("rabbitmq_queue", {"queue": rabbit.get("queue", "unknown")}, {"messages_ready": int(rabbit.get("messagesReady", 0)), "messages_unacknowledged": int(rabbit.get("messagesUnacknowledged", 0)), "publish_rate": float(rabbit.get("publishRate", 0)), "deliver_rate": float(rabbit.get("deliverRate", 0)), "consumer_count": int(rabbit.get("consumerCount", 0))}, captured))
    scaling = value.get("scaling")
    if scaling:
        lines.append(point("autoscaling_decision", {"reason": scaling.get("reason", "unknown")}, {"current_replicas": int(scaling.get("currentReplicas", 0)), "desired_replicas": int(scaling.get("desiredReplicas", 0)), "retry_pending": int(scaling.get("retryPending", 0)), "unsettled_cycles": int(scaling.get("unsettledCycles", 0))}, captured))
    health = value.get("health")
    if health:
        lines.append(point("service_health", {"service": health.get("service", "unknown"), "status": health.get("status", "Unknown")}, {"latency_ms": float(health.get("latencyMs", 0))}, captured))
    return lines


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--fixture", type=Path)
    parser.add_argument("--url")
    parser.add_argument("--token", default=os.getenv("NATUREPROTECTOR_RUNTIME_BEARER_TOKEN"))
    parser.add_argument("--output", type=Path, default=REPO / "artifacts/observability/operational.lineprotocol")
    parser.add_argument("--write-influx", action="store_true")
    parser.add_argument("--interval-seconds", type=int, default=0)
    args = parser.parse_args()
    while True:
        lines = normalize(load_snapshot(args.fixture, args.url, args.token))
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text("\n".join(lines) + "\n", encoding="utf-8")
        if args.write_influx:
            write_lines(os.environ["INFLUXDB_URL"], os.environ.get("INFLUXDB_DATABASE", "np_telemetry"), os.environ["INFLUXDB_TOKEN"], lines)
        print(json.dumps({"points": len(lines), "output": str(args.output)}))
        if args.interval_seconds <= 0 or args.fixture:
            return 0
        time.sleep(args.interval_seconds)

if __name__ == "__main__":
    raise SystemExit(main())
