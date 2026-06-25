#!/usr/bin/env python3
from __future__ import annotations

import argparse
import copy
import json
import os
import shutil
import subprocess
import sys
import tempfile
from datetime import datetime, timedelta, timezone
from pathlib import Path

from g82_common import read_json, sha256_file, validate_schema, write_json
from g82_governance import evaluate_authorization_semantics, evaluate_review_semantics

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "scripts" / "cloud"
MANIFEST = ROOT / "infra" / "gcp" / "contracts" / "g8-1-release-manifest.example.json"
COMMIT = "0" * 40
QID = "g82-adversarial-fixture"


def iso(day: int, hour: int = 0) -> str:
    return datetime(2026, 6, day, hour, tzinfo=timezone.utc).isoformat().replace("+00:00", "Z")


def histogram() -> dict:
    return {
        "unit": "seconds",
        "count": 100,
        "sum": 250.0,
        "buckets": [
            {"le": 1.0, "cumulative_count": 50},
            {"le": 10.0, "cumulative_count": 95},
            {"le": 20.0, "cumulative_count": 99},
            {"le": 60.0, "cumulative_count": 100},
        ],
    }


def runtime_measurements() -> dict:
    return {
        "produced_events": 100,
        "processed_events": 100,
        "failed_events": 0,
        "confirmed_message_loss": 0,
        "availability_window_seconds": 3600,
        "unavailable_seconds": 0,
        "latency_histogram": histogram(),
    }


def make_record(root: Path, action: str, run_id: int, started: str, finished: str, subject: dict, measurements: dict, manifest_sha: str) -> Path:
    directory = root / f"run-{run_id}" / action
    directory.mkdir(parents=True, exist_ok=True)
    raw = directory / "raw-measurement.json"
    write_json(raw, {"action": action, "fixture": True})
    record = {
        "schema_version": 2,
        "phase": "G8.2",
        "qualification_id": QID,
        "action": action,
        "repository": "MAlves4018/NatureProtector",
        "repository_id": "123456789",
        "source_commit": COMMIT,
        "candidate_manifest_sha256": manifest_sha,
        "workflow": {
            "run_id": run_id,
            "run_attempt": 1,
            "name": "G8.2 runtime qualification",
            "path": ".github/workflows/gcp-g8-2-runtime-qualification.yml",
            "event": "workflow_dispatch",
            "environment": "staging",
            "actor": "executor@example.test",
        },
        "status": "passed",
        "started_at": started,
        "finished_at": finished,
        "producer": {
            "script": "scripts/cloud/Test-G82Adversarial.py",
            "algorithm": "synthetic-positive-fixture",
            "version": 2,
        },
        "subject": subject,
        "measurements": measurements,
        "evidence_files": [
            {
                "path": raw.name,
                "sha256": sha256_file(raw),
                "role": "raw-measurement",
            }
        ],
        "production_authorized": False,
        "production_deployed": False,
    }
    validate_schema(record, "g8-2-action-record.schema.json")
    path = directory / "action-record.json"
    write_json(path, record)
    return path


def build_fixture(base: Path) -> tuple[Path, Path]:
    manifest = base / "candidate-manifest.json"
    shutil.copy2(MANIFEST, manifest)
    manifest_sha = sha256_file(manifest)
    actions = base / "actions"
    actions.mkdir()
    run = 1000
    for index, action in enumerate(("pilot-1", "pilot-2", "pilot-3"), start=1):
        run += 1
        make_record(
            actions,
            action,
            run,
            iso(index, 1),
            iso(index, 2),
            {"execution_id": f"execution-{index}", "simulation_run_id": f"00000000-0000-4000-8000-{index:012d}"},
            runtime_measurements(),
            manifest_sha,
        )
    run += 1
    make_record(actions, "soak-start", run, iso(4), iso(4), {"execution_id": "soak-execution"}, {"sample_started_at": iso(4)}, manifest_sha)
    samples = []
    start = datetime(2026, 6, 4, tzinfo=timezone.utc)
    for offset in range(0, 72 * 60 + 1, 5):
        samples.append((start + timedelta(minutes=offset)).isoformat().replace("+00:00", "Z"))
    run += 1
    soak = runtime_measurements()
    soak["sample_timestamps"] = samples
    make_record(actions, "soak-finish", run, iso(4), iso(7), {"execution_id": "soak-execution"}, soak, manifest_sha)
    run += 1
    make_record(actions, "capacity", run, iso(7, 1), iso(7, 2), {}, {"required_peak_eps": 50, "measured_sustainable_eps": 120, "backlog_peak": 1000, "drain_seconds": 120}, manifest_sha)
    run += 1
    make_record(actions, "security-rotation", run, iso(7, 3), iso(7, 4), {}, {"credential_rotation_passed": True, "certificate_rotation_passed": True}, manifest_sha)
    run += 1
    make_record(actions, "incident-drill", run, iso(7, 5), iso(7, 6), {}, {"regional_failover_seconds": 120, "pitr_rpo_seconds": 120, "pitr_restore_seconds": 900, "cross_region_promotion_passed": True, "return_to_primary_passed": True, "incident_drill_passed": True}, manifest_sha)
    run += 1
    make_record(actions, "collect-audit", run, iso(7, 7), iso(7, 8), {}, {"data_access_audit_logs_enabled": True, "artifact_attestations_verified": True, "open_high_findings": 0, "open_critical_findings": 0}, manifest_sha)
    run += 1
    make_record(actions, "cost-observation", run, iso(7, 9), iso(7, 10), {}, {"observation_days": 7, "observed_cost_eur": 20, "forecast_monthly_eur": 85, "approved_monthly_eur": 100, "monthly_cost_approved": True}, manifest_sha)
    run += 1
    make_record(actions, "second-operator", run, iso(7, 11), iso(7, 12), {}, {"second_operator_identity": "operator2@example.test", "runbook_passed": True}, manifest_sha)
    run += 1
    make_record(actions, "rollback-drill", run, iso(7, 13), iso(7, 14), {}, {"rollback_proved": True, "restored_release_digest": "sha256:" + "1" * 64}, manifest_sha)
    run += 1
    make_record(actions, "teardown-rehearsal", run, iso(7, 15), iso(7, 16), {}, {"cleanup_rehearsal_proved": True, "resources_remaining": 0, "environment_recreated": True}, manifest_sha)
    return actions, manifest


def run(command: list[str], expected: int = 0) -> subprocess.CompletedProcess[str]:
    process = subprocess.run(command, cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if process.returncode != expected:
        raise AssertionError(f"unexpected exit {process.returncode}, expected {expected}: {' '.join(command)}\n{process.stdout}")
    return process


def cloud_script(name: str, *args: object) -> list[str]:
    script = SCRIPTS / name
    script_args = [str(arg) for arg in args]
    child_paths = [str(SCRIPTS), *[path for path in sys.path if path]]
    code = (
        "import runpy, sys; "
        f"[sys.path.insert(0, path) for path in reversed({child_paths!r})]; "
        f"sys.argv = [{str(script)!r}] + {script_args!r}; "
        f"runpy.run_path({str(script)!r}, run_name='__main__')"
    )
    return [sys.executable, "-c", code]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository-root", type=Path, default=ROOT)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if args.repository_root.resolve() != ROOT.resolve():
        raise SystemExit(f"repository root mismatch: expected {ROOT}, got {args.repository_root.resolve()}")
    results: list[dict] = []
    def check(name: str, function) -> None:
        try:
            function()
            results.append({"name": name, "passed": True})
        except Exception as exc:
            results.append({"name": name, "passed": False, "detail": str(exc)})

    with tempfile.TemporaryDirectory(prefix="g82-adversarial-") as temporary:
        base = Path(temporary)
        actions, manifest = build_fixture(base)
        summary = base / "summary.json"
        check("positive-derived-summary", lambda: run(cloud_script("Aggregate-G82Qualification.py", "--actions-root", actions, "--candidate-manifest", manifest, "--qualification-id", QID, "--source-commit", COMMIT, "--output", summary)))
        evidence_index = base / "index.json"
        check("positive-closed-index", lambda: run(cloud_script("New-G82EvidenceIndex.py", "--root", actions, "--output", evidence_index, "--qualification-id", QID, "--source-commit", COMMIT, "--candidate-manifest-sha256", sha256_file(manifest))))
        check("positive-index-verification", lambda: run(cloud_script("Verify-G82EvidenceIndex.py", "--root", actions, "--index", evidence_index)))
        pre = base / "pre.json"
        check("positive-pre-archive-verdict", lambda: run(cloud_script("Test-G82QualificationEvidence.py", "--stage", "pre-archive", "--summary", summary, "--evidence-index", evidence_index, "--candidate-manifest", manifest, "--output", pre)))

        def extra_file_rejected():
            extra = actions / "unindexed.txt"
            extra.write_text("tamper", encoding="utf-8")
            run(cloud_script("Verify-G82EvidenceIndex.py", "--root", actions, "--index", evidence_index), expected=1)
            extra.unlink()
        check("extra-file-rejected", extra_file_rejected)

        def duplicate_execution_rejected():
            record = next(actions.rglob("pilot-2/action-record.json"))
            value = read_json(record)
            pilot1 = read_json(next(actions.rglob("pilot-1/action-record.json")))
            value["subject"]["execution_id"] = pilot1["subject"]["execution_id"]
            write_json(record, value)
            run(cloud_script("Aggregate-G82Qualification.py", "--actions-root", actions, "--candidate-manifest", manifest, "--qualification-id", QID, "--source-commit", COMMIT, "--output", base / "bad-summary.json"), expected=1)
            value["subject"]["execution_id"] = "execution-2"
            write_json(record, value)
        check("duplicate-pilot-execution-rejected", duplicate_execution_rejected)

        def uppercase_severity_rejected():
            review = {
                "schema_version": 2, "phase": "G8.2", "qualification_id": QID, "status": "SIGNED", "review_id": "r1",
                "reviewer_identity": "reviewer", "executor_identity": "executor", "authorizer_identity": "authorizer", "candidate_commit": COMMIT,
                "candidate_manifest_sha256": "0"*64, "evidence_index_sha256": "0"*64, "final_qualification_verdict_sha256": "0"*64, "archive_receipt_sha256": "0"*64,
                "started_at": iso(7), "completed_at": iso(7,1), "decision": "ACCEPT_WITH_CONDITIONS",
                "findings": [{"id":"f1","severity":"HIGH","status":"open","description":"x","owner":"o","resolution":"pending"}],
                "conditions": [], "signature_namespace":"natureprotector-g82-independent-review", "production_authorized":False, "production_deployed":False
            }
            try:
                validate_schema(review, "g8-2-independent-review.schema.json")
            except ValueError:
                return
            raise AssertionError("uppercase severity was accepted")
        check("uppercase-finding-severity-rejected", uppercase_severity_rejected)

        def conditions_required():
            review = {
                "status":"SIGNED","decision":"ACCEPT_WITH_CONDITIONS","reviewer_identity":"r","executor_identity":"e","authorizer_identity":"a",
                "started_at":iso(7),"completed_at":iso(7,1),"findings":[],"conditions":[],"production_authorized":False,"production_deployed":False
            }
            assert evaluate_review_semantics(review)["decision_semantics"] is False
        check("accept-with-conditions-requires-conditions", conditions_required)

        def same_identity_rejected():
            request={"requested_validity_hours":24}
            now=datetime.now(timezone.utc)
            decision={"status":"SIGNED","decision":"GO","production_authorized":True,"production_deployed":False,"rollback_owner":"owner","authorizer_identity":"same","independent_reviewer_identity":"same","executor_identity":"executor","conditions":[],"issued_at":now.isoformat(),"expires_at":(now+timedelta(hours=1)).isoformat()}
            assert evaluate_authorization_semantics(request, decision)["independence"] is False
        check("authorization-separation-of-duties", same_identity_rejected)

        def expired_rejected():
            request={"requested_validity_hours":24}
            now=datetime.now(timezone.utc)
            decision={"status":"SIGNED","decision":"GO","production_authorized":True,"production_deployed":False,"rollback_owner":"owner","authorizer_identity":"a","independent_reviewer_identity":"r","executor_identity":"e","conditions":[],"issued_at":(now-timedelta(hours=2)).isoformat(),"expires_at":(now-timedelta(hours=1)).isoformat()}
            assert evaluate_authorization_semantics(request, decision)["validity"] is False
        check("expired-authorization-rejected", expired_rejected)

        def symlink_rejected():
            if not hasattr(os, "symlink"):
                return
            target = actions / "target.txt"; target.write_text("x", encoding="utf-8")
            link = actions / "link.txt"
            try:
                try:
                    os.symlink(target, link)
                except OSError as exc:
                    if getattr(exc, "winerror", None) == 1314:
                        return
                    raise
                run(cloud_script("New-G82EvidenceIndex.py", "--root", actions, "--output", base / "symlink-index.json", "--qualification-id", QID, "--source-commit", COMMIT, "--candidate-manifest-sha256", sha256_file(manifest)), expected=1)
            finally:
                if link.exists() or link.is_symlink(): link.unlink()
                target.unlink()
        check("symlink-evidence-rejected", symlink_rejected)

    failed=[item for item in results if not item["passed"]]
    output={"schema_version":2,"phase":"G8.2","status":"PASS" if not failed else "FAIL","checks_total":len(results),"checks_passed":len(results)-len(failed),"checks_failed":len(failed),"checks":results,"production_authorized":False,"production_deployed":False}
    rendered = json.dumps(output, indent=2)
    print(rendered)
    if args.output is not None:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered + "\n", encoding="utf-8")
    return 0 if not failed else 1


if __name__ == "__main__":
    raise SystemExit(main())
