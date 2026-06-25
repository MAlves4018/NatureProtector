#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path

from g82_common import (
    iso_utc,
    read_json,
    sha256_file,
    validate_schema,
    write_json,
)


def pre_archive(summary_path: Path, index_path: Path, manifest_path: Path, policy_path: Path) -> tuple[dict, bool]:
    summary = read_json(summary_path)
    index = read_json(index_path)
    manifest = read_json(manifest_path)
    policy = read_json(policy_path)
    validate_schema(summary, "g8-2-qualification-summary.schema.json")
    validate_schema(index, "g8-2-evidence-index.schema.json")
    validate_schema(manifest, "g8-1-release-manifest.schema.json")

    manifest_sha = sha256_file(manifest_path)
    runtime = summary["runtime"]
    soak = summary["soak"]
    dr = summary["dr"]
    security = summary["security"]
    cost = summary["cost"]
    operability = summary["operability"]
    checks = {
        "bindings": summary["qualification_id"] == index["qualification_id"]
        and summary["source_commit"] == index["source_commit"]
        and summary["candidate_manifest_sha256"] == manifest_sha
        and index["candidate_manifest_sha256"] == manifest_sha
        and (manifest.get("source_commit") or manifest.get("commit")) == summary["source_commit"],
        "evidence_index_closed_world": index["file_count"] == len(index["files"]),
        "pilots": len(summary["pilot_runs"]) == policy["pilots"]["count"]
        and len({item["execution_id"] for item in summary["pilot_runs"]}) == policy["pilots"]["count"]
        and len({item["simulation_run_id"] for item in summary["pilot_runs"]}) == policy["pilots"]["count"]
        and len({item["started_at"][:10] for item in summary["pilot_runs"]}) == policy["pilots"]["count"],
        "soak": soak["continuous_hours"] >= policy["soak"]["minimum_continuous_hours"]
        and soak["maximum_gap_minutes"] <= policy["soak"]["maximum_unaccounted_gap_minutes"],
        "runtime": runtime["availability_ratio"] >= policy["runtime"]["availability_ratio_min"]
        and runtime["processing_success_ratio"] >= policy["runtime"]["processing_success_ratio_min"]
        and runtime["p95_seconds"] <= policy["runtime"]["p95_seconds_max"]
        and runtime["p99_seconds"] <= policy["runtime"]["p99_seconds_max"]
        and runtime["confirmed_message_loss"] <= policy["runtime"]["confirmed_message_loss_max"]
        and runtime["headroom_multiplier"] >= policy["runtime"]["headroom_multiplier_min"],
        "dr": dr["regional_failover_seconds"] <= policy["dr"]["regional_failover_seconds_max"]
        and dr["pitr_rpo_seconds"] <= policy["dr"]["pitr_rpo_seconds_max"]
        and dr["pitr_restore_seconds"] <= policy["dr"]["pitr_restore_seconds_max"]
        and dr["cross_region_promotion_passed"] is True
        and dr["return_to_primary_passed"] is True,
        "security": security["open_high_findings"] <= policy["security"]["open_high_max"]
        and security["open_critical_findings"] <= policy["security"]["open_critical_max"]
        and security["data_access_audit_logs_enabled"] is True
        and security["artifact_attestations_verified"] is True
        and security["credential_rotation_passed"] is True
        and security["certificate_rotation_passed"] is True,
        "cost": cost["observation_days"] >= policy["cost"]["observation_days_min"]
        and cost["monthly_cost_approved"] is True
        and cost["approved_monthly_eur"] >= cost["forecast_monthly_eur"],
        "operability": operability["second_operator_runbook"] == "passed"
        and operability["incident_drill"] == "passed"
        and operability["rollback_proved"] is True
        and operability["cleanup_rehearsal_proved"] is True
        and operability["resources_remaining"] == 0
        and operability["environment_recreated"] is True,
        "not_authorized_or_deployed": summary["production_authorized"] is False
        and summary["production_deployed"] is False
        and index["production_authorized"] is False
        and index["production_deployed"] is False,
    }
    passed = all(checks.values())
    result = {
        "schema_version": 2,
        "phase": "G8.2",
        "qualification_id": summary["qualification_id"],
        "generated_at": iso_utc(),
        "status": "G82_PRE_ARCHIVE_QUALIFICATION_PASSED" if passed else "G82_BLOCKED_PENDING_QUALIFICATION",
        "checks": checks,
        "source_commit": summary["source_commit"],
        "candidate_manifest_sha256": manifest_sha,
        "evidence_index_sha256": sha256_file(index_path),
        "qualification_summary_sha256": sha256_file(summary_path),
        "production_authorized": False,
        "production_deployed": False,
    }
    validate_schema(result, "g8-2-pre-archive-verdict.schema.json")
    return result, passed


def final_verdict(pre_path: Path, archive_path: Path, summary_path: Path, index_path: Path, manifest_path: Path) -> tuple[dict, bool]:
    pre = read_json(pre_path)
    archive = read_json(archive_path)
    summary = read_json(summary_path)
    index = read_json(index_path)
    validate_schema(pre, "g8-2-pre-archive-verdict.schema.json")
    validate_schema(archive, "g8-2-archive-receipt.schema.json")
    validate_schema(summary, "g8-2-qualification-summary.schema.json")
    validate_schema(index, "g8-2-evidence-index.schema.json")
    manifest_sha = sha256_file(manifest_path)
    checks = {
        "pre_archive_passed": pre["status"] == "G82_PRE_ARCHIVE_QUALIFICATION_PASSED",
        "archive_passed": archive["status"] == "passed",
        "qualification_binding": pre["qualification_id"] == archive["qualification_id"] == summary["qualification_id"] == index["qualification_id"],
        "manifest_binding": pre["candidate_manifest_sha256"] == archive["candidate_manifest_sha256"] == index["candidate_manifest_sha256"] == manifest_sha,
        "index_binding": archive["evidence_index_sha256"] == sha256_file(index_path)
        and pre["evidence_index_sha256"] == sha256_file(index_path),
        "pre_verdict_binding": archive["pre_archive_verdict_sha256"] == sha256_file(pre_path),
        "archive_controls": archive["retention_days"] >= 365
        and archive["versioning_enabled"] is True
        and archive["public_access_prevention"] is True
        and len(archive["objects"]) >= 4,
        "not_deployed": archive["production_deployed"] is False,
    }
    passed = all(checks.values())
    result = {
        "schema_version": 2,
        "phase": "G8.2",
        "qualification_id": summary["qualification_id"],
        "generated_at": iso_utc(),
        "status": "G82_FINAL_QUALIFICATION_PASSED" if passed else "G82_BLOCKED_PENDING_ARCHIVE",
        "checks": checks,
        "source_commit": summary["source_commit"],
        "candidate_manifest_sha256": manifest_sha,
        "evidence_index_sha256": sha256_file(index_path),
        "qualification_summary_sha256": sha256_file(summary_path),
        "pre_archive_verdict_sha256": sha256_file(pre_path),
        "archive_receipt_sha256": sha256_file(archive_path),
        "production_authorized": False,
        "production_deployed": False,
    }
    validate_schema(result, "g8-2-final-verdict.schema.json")
    return result, passed


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", required=True, choices=["pre-archive", "final"])
    parser.add_argument("--summary", required=True)
    parser.add_argument("--evidence-index", required=True)
    parser.add_argument("--candidate-manifest", required=True)
    parser.add_argument("--policy", default="infra/gcp/qualification/g8-2-qualification-plan.json")
    parser.add_argument("--pre-archive-verdict")
    parser.add_argument("--archive-receipt")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    if args.stage == "pre-archive":
        result, passed = pre_archive(
            Path(args.summary), Path(args.evidence_index), Path(args.candidate_manifest), Path(args.policy)
        )
    else:
        if not args.pre_archive_verdict or not args.archive_receipt:
            raise SystemExit("final stage requires --pre-archive-verdict and --archive-receipt")
        result, passed = final_verdict(
            Path(args.pre_archive_verdict),
            Path(args.archive_receipt),
            Path(args.summary),
            Path(args.evidence_index),
            Path(args.candidate_manifest),
        )
    write_json(args.output, result)
    print(result["status"])
    return 0 if passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
