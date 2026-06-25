#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path

from g82_common import (
    assert_commit,
    assert_qualification_id,
    parse_datetime,
    percentile_from_histograms,
    read_json,
    sha256_file,
    utc_now,
    validate_schema,
    write_json,
    iso_utc,
)

REQUIRED_SINGLETONS = {
    "pilot-1",
    "pilot-2",
    "pilot-3",
    "soak-start",
    "soak-finish",
    "capacity",
    "security-rotation",
    "incident-drill",
    "collect-audit",
    "cost-observation",
    "second-operator",
    "rollback-drill",
    "teardown-rehearsal",
}


def verify_record_files(record_path: Path, record: dict) -> None:
    root = record_path.parent.resolve()
    for reference in record["evidence_files"]:
        path = (root / reference["path"]).resolve()
        path.relative_to(root)
        if path.is_symlink() or not path.is_file():
            raise ValueError(f"missing/non-regular referenced evidence: {path}")
        if sha256_file(path) != reference["sha256"]:
            raise ValueError(f"referenced evidence digest mismatch: {path}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--actions-root", required=True)
    parser.add_argument("--candidate-manifest", required=True)
    parser.add_argument("--qualification-id", required=True)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    actions_root = Path(args.actions_root).resolve()
    qualification_id = assert_qualification_id(args.qualification_id)
    source_commit = assert_commit(args.source_commit)
    manifest_path = Path(args.candidate_manifest).resolve()
    manifest = read_json(manifest_path)
    validate_schema(manifest, "g8-1-release-manifest.schema.json")
    manifest_commit = manifest.get("source_commit") or manifest.get("commit")
    if manifest_commit != source_commit:
        raise SystemExit("candidate manifest does not bind to the requested source commit")
    manifest_sha = sha256_file(manifest_path)

    record_paths = sorted(actions_root.rglob("action-record.json"))
    if not record_paths:
        raise SystemExit("no action-record.json files found")

    by_action: dict[str, list[tuple[Path, dict]]] = {}
    run_ids: set[int] = set()
    for record_path in record_paths:
        if record_path.is_symlink():
            raise SystemExit(f"action record may not be a symlink: {record_path}")
        record = read_json(record_path)
        validate_schema(record, "g8-2-action-record.schema.json")
        if record["qualification_id"] != qualification_id:
            raise SystemExit(f"qualification id mismatch: {record_path}")
        if record["source_commit"] != source_commit:
            raise SystemExit(f"source commit mismatch: {record_path}")
        if record["candidate_manifest_sha256"] != manifest_sha:
            raise SystemExit(f"candidate manifest mismatch: {record_path}")
        started = parse_datetime(record["started_at"], field=f"{record_path}:started_at")
        finished = parse_datetime(record["finished_at"], field=f"{record_path}:finished_at")
        if started > finished:
            raise SystemExit(f"negative action duration: {record_path}")
        if finished > utc_now() and (finished - utc_now()).total_seconds() > 300:
            raise SystemExit(f"future action timestamp: {record_path}")
        run_id = record["workflow"]["run_id"]
        if run_id in run_ids:
            raise SystemExit(f"workflow run id reused across action records: {run_id}")
        run_ids.add(run_id)
        verify_record_files(record_path, record)
        by_action.setdefault(record["action"], []).append((record_path, record))

    missing = sorted(action for action in REQUIRED_SINGLETONS if action not in by_action)
    duplicated = sorted(action for action in REQUIRED_SINGLETONS if len(by_action.get(action, [])) != 1)
    if missing:
        raise SystemExit(f"required qualification actions missing: {', '.join(missing)}")
    if duplicated:
        raise SystemExit(f"singleton qualification actions duplicated: {', '.join(duplicated)}")

    def one(action: str) -> tuple[Path, dict]:
        return by_action[action][0]

    pilots: list[dict] = []
    pilot_dates: set[str] = set()
    execution_ids: set[str] = set()
    simulation_ids: set[str] = set()
    runtime_records: list[dict] = []
    for action in ("pilot-1", "pilot-2", "pilot-3"):
        _, record = one(action)
        started = parse_datetime(record["started_at"], field=f"{action}.started_at")
        execution_id = record["subject"]["execution_id"]
        simulation_id = record["subject"]["simulation_run_id"]
        if execution_id in execution_ids:
            raise SystemExit("pilot execution IDs must be unique")
        if simulation_id in simulation_ids:
            raise SystemExit("pilot SimulationRunIds must be unique")
        execution_ids.add(execution_id)
        simulation_ids.add(simulation_id)
        pilot_dates.add(started.date().isoformat())
        pilots.append(
            {
                "profile": action,
                "execution_id": execution_id,
                "simulation_run_id": simulation_id,
                "started_at": record["started_at"],
                "finished_at": record["finished_at"],
                "status": "passed",
            }
        )
        runtime_records.append(record)
    if len(pilot_dates) != 3:
        raise SystemExit("the three pilots must execute on three distinct UTC dates")

    _, soak_start = one("soak-start")
    _, soak_finish = one("soak-finish")
    if soak_start["subject"]["execution_id"] != soak_finish["subject"]["execution_id"]:
        raise SystemExit("soak start and finish must bind to the same execution")
    timestamps = sorted(
        parse_datetime(value, field="soak.sample_timestamps")
        for value in soak_finish["measurements"]["sample_timestamps"]
    )
    if len(set(timestamps)) != len(timestamps):
        raise SystemExit("soak sample timestamps must be unique")
    gaps = [
        (current - previous).total_seconds() / 60
        for previous, current in zip(timestamps, timestamps[1:])
    ]
    continuous_hours = (timestamps[-1] - timestamps[0]).total_seconds() / 3600
    maximum_gap = max(gaps) if gaps else 0.0
    start_claim = parse_datetime(
        soak_start["measurements"]["sample_started_at"], field="soak-start.sample_started_at"
    )
    if abs((timestamps[0] - start_claim).total_seconds()) > 300:
        raise SystemExit("soak sample series does not start within five minutes of soak-start")
    runtime_records.append(soak_finish)

    produced = processed = failed = loss = 0
    availability_window = unavailable = 0.0
    histograms: list[dict] = []
    for record in runtime_records:
        measurements = record["measurements"]
        p = int(measurements["produced_events"])
        ok = int(measurements["processed_events"])
        bad = int(measurements["failed_events"])
        missing_events = int(measurements["confirmed_message_loss"])
        if ok + bad + missing_events != p:
            raise SystemExit(
                f"event accounting invariant failed for {record['action']}: "
                f"processed + failed + loss must equal produced"
            )
        window = float(measurements["availability_window_seconds"])
        down = float(measurements["unavailable_seconds"])
        if down > window:
            raise SystemExit(f"unavailable time exceeds observation window: {record['action']}")
        produced += p
        processed += ok
        failed += bad
        loss += missing_events
        availability_window += window
        unavailable += down
        histograms.append(measurements["latency_histogram"])

    availability_ratio = (
        1.0 - unavailable / availability_window if availability_window > 0 else 0.0
    )
    processing_success_ratio = processed / produced if produced > 0 else 0.0
    p95 = percentile_from_histograms(histograms, 0.95)
    p99 = percentile_from_histograms(histograms, 0.99)

    _, capacity = one("capacity")
    required_peak = float(capacity["measurements"]["required_peak_eps"])
    measured_eps = float(capacity["measurements"]["measured_sustainable_eps"])
    headroom = measured_eps / required_peak

    _, incident = one("incident-drill")
    _, security_rotation = one("security-rotation")
    _, audit = one("collect-audit")
    _, cost = one("cost-observation")
    _, second_operator = one("second-operator")
    _, rollback = one("rollback-drill")
    _, teardown = one("teardown-rehearsal")

    executor_identity = one("pilot-1")[1]["workflow"]["actor"]
    second_identity = second_operator["measurements"]["second_operator_identity"]
    if second_identity == executor_identity:
        raise SystemExit("second operator must differ from the qualification executor")

    action_records = []
    for record_path in record_paths:
        record = read_json(record_path)
        action_records.append(
            {
                "action": record["action"],
                "workflow_run_id": record["workflow"]["run_id"],
                "sha256": sha256_file(record_path),
                "relative_path": record_path.relative_to(actions_root).as_posix(),
            }
        )

    summary = {
        "schema_version": 2,
        "phase": "G8.2",
        "qualification_id": qualification_id,
        "source_commit": source_commit,
        "candidate_manifest_sha256": manifest_sha,
        "generated_at": iso_utc(),
        "action_records": action_records,
        "pilot_runs": pilots,
        "soak": {
            "started_at": timestamps[0].isoformat().replace("+00:00", "Z"),
            "finished_at": timestamps[-1].isoformat().replace("+00:00", "Z"),
            "continuous_hours": round(continuous_hours, 6),
            "maximum_gap_minutes": round(maximum_gap, 6),
            "sample_count": len(timestamps),
        },
        "runtime": {
            "availability_ratio": round(availability_ratio, 9),
            "processing_success_ratio": round(processing_success_ratio, 9),
            "p95_seconds": p95,
            "p99_seconds": p99,
            "confirmed_message_loss": loss,
            "headroom_multiplier": round(headroom, 6),
            "produced_events": produced,
            "processed_events": processed,
            "failed_events": failed,
        },
        "dr": {
            "regional_failover_seconds": incident["measurements"]["regional_failover_seconds"],
            "pitr_rpo_seconds": incident["measurements"]["pitr_rpo_seconds"],
            "pitr_restore_seconds": incident["measurements"]["pitr_restore_seconds"],
            "cross_region_promotion_passed": incident["measurements"]["cross_region_promotion_passed"],
            "return_to_primary_passed": incident["measurements"]["return_to_primary_passed"],
        },
        "security": {
            "open_high_findings": audit["measurements"]["open_high_findings"],
            "open_critical_findings": audit["measurements"]["open_critical_findings"],
            "data_access_audit_logs_enabled": audit["measurements"]["data_access_audit_logs_enabled"],
            "artifact_attestations_verified": audit["measurements"]["artifact_attestations_verified"],
            "credential_rotation_passed": security_rotation["measurements"]["credential_rotation_passed"],
            "certificate_rotation_passed": security_rotation["measurements"]["certificate_rotation_passed"],
        },
        "cost": {
            "observation_days": cost["measurements"]["observation_days"],
            "observed_cost_eur": cost["measurements"]["observed_cost_eur"],
            "forecast_monthly_eur": cost["measurements"]["forecast_monthly_eur"],
            "approved_monthly_eur": cost["measurements"]["approved_monthly_eur"],
            "monthly_cost_approved": cost["measurements"]["monthly_cost_approved"],
        },
        "operability": {
            "second_operator_identity": second_identity,
            "second_operator_runbook": "passed",
            "incident_drill": "passed" if incident["measurements"]["incident_drill_passed"] else "failed",
            "rollback_proved": rollback["measurements"]["rollback_proved"],
            "cleanup_rehearsal_proved": teardown["measurements"]["cleanup_rehearsal_proved"],
            "resources_remaining": teardown["measurements"]["resources_remaining"],
            "environment_recreated": teardown["measurements"]["environment_recreated"],
        },
        "production_authorized": False,
        "production_deployed": False,
    }
    validate_schema(summary, "g8-2-qualification-summary.schema.json")
    write_json(args.output, summary)
    print(args.output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
