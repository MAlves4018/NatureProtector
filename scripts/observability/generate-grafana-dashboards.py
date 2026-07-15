#!/usr/bin/env python3
"""Generate the five canonical NatureProtector Grafana dashboards."""
from __future__ import annotations
import argparse
import json
from pathlib import Path

POSTGRES = {"type": "postgres", "uid": "natureprotector-postgres"}
INFLUX = {"type": "yesoreyeram-infinity-datasource", "uid": "natureprotector-infinity-json"}


def panel(identifier, title, query, y, datasource=POSTGRES, panel_type="table"):
    target = {"refId": "A", "datasource": datasource}
    if datasource["type"] == "postgres":
        target.update({"format": "table", "rawQuery": True, "rawSql": query})
    else:
        target.update({"type": "json", "source": "url", "format": "table", "url": "/api/v3/query_sql", "url_options": {"method": "GET", "params": [{"key": "db", "value": "np_telemetry"}, {"key": "format", "value": "json"}, {"key": "q", "value": query}]}})
    return {"id": identifier, "title": title, "type": panel_type, "datasource": datasource, "gridPos": {"h": 9, "w": 12, "x": 0 if identifier % 2 else 12, "y": y}, "fieldConfig": {"defaults": {}, "overrides": []}, "options": {"showHeader": True}, "targets": [target]}


def dashboard(uid, title, panels):
    return {"uid": uid, "title": title, "tags": ["natureprotector", "provisioned"], "schemaVersion": 41, "version": 1, "editable": True, "refresh": "10s", "time": {"from": "now-6h", "to": "now"}, "annotations": {"list": []}, "templating": {"list": []}, "panels": panels}


def definitions():
    return {
        "natureprotector-run-lifecycle.json": dashboard("np-run-lifecycle", "NatureProtector — Run & Lifecycle", [
            panel(1, "Recent operations", 'SELECT accepted_at AS "time", execution_id AS operation_id, simulation_run_id, state, provider_state, run_state, processing_state, terminal_outcome FROM control.runtime_orchestrator_executions ORDER BY accepted_at DESC LIMIT 50', 0),
            panel(2, "Requested vs observed duration", 'SELECT started_at AS "time", simulation_run_id, EXTRACT(EPOCH FROM (COALESCE(system_completed_at, NOW()) - started_at)) AS observed_seconds, terminal_outcome FROM control.runtime_orchestrator_executions WHERE started_at IS NOT NULL ORDER BY started_at DESC LIMIT 50', 0),
            panel(3, "Run accounting", 'SELECT "CreatedAt" AS "time", "Id" AS simulation_run_id, "NumberOfCycles" AS number_of_cycles, "IntervalSeconds" AS interval_seconds, "Status" AS status, "EndedAt" AS ended_at FROM control.simulation_runs ORDER BY "CreatedAt" DESC LIMIT 50', 9),
            panel(4, "Termination outcomes", 'SELECT COALESCE(terminal_outcome, state) AS outcome, COUNT(*) AS operations FROM control.runtime_orchestrator_executions GROUP BY 1 ORDER BY 2 DESC', 9),
        ]),
        "natureprotector-pipeline-rabbitmq.json": dashboard("np-pipeline-rabbitmq", "NatureProtector — Pipeline & RabbitMQ", [
            panel(1, "Queue metrics", 'SELECT time, queue, messages_ready, messages_unacknowledged, publish_rate, deliver_rate, consumer_count FROM rabbitmq_queue WHERE time >= now() - INTERVAL \'6 hours\' ORDER BY time DESC', 0, INFLUX),
            panel(2, "Inbox outcomes", 'SELECT "ReceivedAt" AS "time", "Status" AS status, COUNT(*) AS events FROM pipeline.event_inbox WHERE "ReceivedAt" >= NOW() - INTERVAL \'6 hours\' GROUP BY 1,2 ORDER BY 1 DESC', 0),
            panel(3, "Processing latency", 'SELECT "StartedAt" AS "time", "Stage" AS stage, "Outcome" AS outcome, EXTRACT(EPOCH FROM ("FinishedAt"-"StartedAt"))*1000 AS duration_ms FROM pipeline.processing_attempts WHERE "FinishedAt" IS NOT NULL ORDER BY "StartedAt" DESC LIMIT 500', 9),
            panel(4, "Retries and quarantine", 'SELECT "Status" AS status, COUNT(*) AS events FROM pipeline.event_inbox WHERE "Status" IN (3,5,6) GROUP BY "Status"', 9),
        ]),
        "natureprotector-risk-temporal.json": dashboard("np-risk-temporal", "NatureProtector — Risk & Temporal", [
            panel(1, "Area cycle snapshots", 'SELECT "SnapshotTimestamp" AS "time", "SimulationRunId" AS simulation_run_id, "CycleIndex" AS cycle_index, "ExpectedCount" AS expected_count, "ObservedCount" AS observed_count, "MissingCount" AS missing_count, "BlockedCount" AS blocked_count, "AggregateRiskScore" AS aggregate_risk_score, "AggregateRiskLevel" AS aggregate_risk_level, "AlertOutcome" AS alert_outcome FROM projection.area_cycle_snapshot ORDER BY "SnapshotTimestamp" DESC LIMIT 500', 0),
            panel(2, "Cycle settlement coverage", 'SELECT "UpdatedAt" AS "time", "SimulationRunId" AS simulation_run_id, "CycleIndex" AS cycle_index, "Status" AS status, "IsOperational" AS is_operational, "FinalizationReason" AS finalization_reason FROM projection.cycle_settlement ORDER BY "UpdatedAt" DESC LIMIT 500', 0),
            panel(3, "Risk assessment trajectory", 'SELECT "CreatedAt" AS "time", "SimulationRunId" AS simulation_run_id, "AreaId" AS area_id, "SensorId" AS sensor_id, "RiskScore" AS risk_score, "RiskLevel" AS risk_level FROM projection.risk_assessment_log ORDER BY "CreatedAt" DESC LIMIT 500', 9),
            panel(4, "Missing and blocked counts", 'SELECT "SnapshotTimestamp" AS "time", "SimulationRunId" AS simulation_run_id, "CycleIndex" AS cycle_index, "MissingCount" AS missing_count, "BlockedCount" AS blocked_count FROM projection.area_cycle_snapshot ORDER BY "SnapshotTimestamp" DESC LIMIT 500', 9),
        ]),
        "natureprotector-autoscaling.json": dashboard("np-autoscaling", "NatureProtector — Autoscaling", [
            panel(1, "Replica decisions", 'SELECT time, reason, current_replicas, desired_replicas, retry_pending, unsettled_cycles FROM autoscaling_decision WHERE time >= now() - INTERVAL \'6 hours\' ORDER BY time DESC', 0, INFLUX),
            panel(2, "Backlog per replica", 'SELECT q.time, q.messages_ready, a.current_replicas, q.messages_ready / CASE WHEN a.current_replicas = 0 THEN 1 ELSE a.current_replicas END AS backlog_per_replica FROM rabbitmq_queue q CROSS JOIN (SELECT current_replicas FROM autoscaling_decision ORDER BY time DESC LIMIT 1) a WHERE q.time >= now() - INTERVAL \'6 hours\' ORDER BY q.time DESC', 0, INFLUX),
            panel(3, "Run progress guardrails", 'SELECT time, operation_id, state, expected, accepted, pending, processing, retry_pending, settled FROM run_progress WHERE time >= now() - INTERVAL \'6 hours\' ORDER BY time DESC', 9, INFLUX),
            panel(4, "Service health", 'SELECT time, service, status, latency_ms FROM service_health WHERE time >= now() - INTERVAL \'6 hours\' ORDER BY time DESC', 9, INFLUX),
        ]),
        "natureprotector-external-data.json": dashboard("np-external-data", "NatureProtector — External Data", [
            panel(1, "Latest IPMA observations", 'SELECT time, station_id, metric, value, unit, station_name, observed_at FROM external_observation WHERE provider = \'IPMA\' AND time >= now() - INTERVAL \'24 hours\' ORDER BY time DESC LIMIT 1000', 0, INFLUX),
            panel(2, "Provider freshness", 'SELECT station_id, metric, MAX(time) AS latest_observation FROM external_observation WHERE provider = \'IPMA\' GROUP BY station_id, metric ORDER BY latest_observation DESC', 0, INFLUX),
            panel(3, "External observations by metric", 'SELECT metric, COUNT(*) AS observations FROM external_observation WHERE provider = \'IPMA\' AND time >= now() - INTERVAL \'24 hours\' GROUP BY metric', 9, INFLUX),
            panel(4, "External-data coverage", 'SELECT metric, COUNT(DISTINCT station_id) AS stations, COUNT(*) AS points FROM external_observation WHERE provider = \'IPMA\' AND time >= now() - INTERVAL \'24 hours\' GROUP BY metric', 9, INFLUX),
        ]),
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", type=Path, default=Path("infra/grafana/dashboards"))
    args = parser.parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    for name, value in definitions().items():
        (args.output_dir / name).write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(len(definitions()))
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
