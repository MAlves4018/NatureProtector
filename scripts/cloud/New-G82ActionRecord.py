#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path

from g82_common import (
    assert_commit,
    assert_qualification_id,
    iso_utc,
    normalize_relative_path,
    parse_datetime,
    read_json,
    sha256_file,
    utc_now,
    validate_schema,
    write_json,
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--action", required=True)
    parser.add_argument("--qualification-id", required=True)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--candidate-manifest", required=True)
    parser.add_argument("--repository", default="MAlves4018/NatureProtector")
    parser.add_argument("--repository-id", required=True)
    parser.add_argument("--workflow-run-id", required=True, type=int)
    parser.add_argument("--workflow-run-attempt", required=True, type=int)
    parser.add_argument("--actor", required=True)
    parser.add_argument("--measurement", required=True)
    parser.add_argument("--evidence-root", required=True)
    parser.add_argument("--producer-script", required=True)
    parser.add_argument("--producer-algorithm", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    evidence_root = Path(args.evidence_root).resolve()
    measurement_path = Path(args.measurement).resolve()
    measurement_path.relative_to(evidence_root)
    if measurement_path.is_symlink() or not measurement_path.is_file():
        raise SystemExit("measurement must be a regular file inside the evidence root")
    measurement = read_json(measurement_path)
    for required in ("started_at", "finished_at", "subject", "measurements", "evidence_files"):
        if required not in measurement:
            raise SystemExit(f"measurement is missing {required}")

    started = parse_datetime(measurement["started_at"], field="measurement.started_at")
    finished = parse_datetime(measurement["finished_at"], field="measurement.finished_at")
    if started > finished:
        raise SystemExit("measurement finished_at precedes started_at")
    if finished > utc_now().replace(microsecond=0) and (finished - utc_now()).total_seconds() > 300:
        raise SystemExit("measurement finished_at is unreasonably in the future")

    references: list[dict] = []
    seen: set[str] = set()
    for item in measurement["evidence_files"]:
        if not isinstance(item, dict) or set(item) != {"path", "role"}:
            raise SystemExit("each measurement evidence_files entry requires path and role")
        relative = normalize_relative_path(item["path"])
        if relative in seen:
            raise SystemExit(f"duplicate evidence reference: {relative}")
        seen.add(relative)
        path = (evidence_root / relative).resolve()
        path.relative_to(evidence_root)
        if path.is_symlink() or not path.is_file():
            raise SystemExit(f"evidence reference is not a regular file: {relative}")
        references.append({"path": relative, "sha256": sha256_file(path), "role": item["role"]})

    measurement_relative = measurement_path.relative_to(evidence_root).as_posix()
    if measurement_relative not in seen:
        references.append(
            {
                "path": measurement_relative,
                "sha256": sha256_file(measurement_path),
                "role": "raw-measurement",
            }
        )

    manifest_path = Path(args.candidate_manifest).resolve()
    manifest = read_json(manifest_path)
    manifest_commit = manifest.get("source_commit") or manifest.get("commit")
    source_commit = assert_commit(args.source_commit)
    if manifest_commit != source_commit:
        raise SystemExit("candidate manifest source commit does not match action source commit")

    document = {
        "schema_version": 2,
        "phase": "G8.2",
        "qualification_id": assert_qualification_id(args.qualification_id),
        "action": args.action,
        "repository": args.repository,
        "repository_id": str(args.repository_id),
        "source_commit": source_commit,
        "candidate_manifest_sha256": sha256_file(manifest_path),
        "workflow": {
            "run_id": args.workflow_run_id,
            "run_attempt": args.workflow_run_attempt,
            "name": "G8.2 runtime qualification",
            "path": ".github/workflows/gcp-g8-2-runtime-qualification.yml",
            "event": "workflow_dispatch",
            "environment": "staging",
            "actor": args.actor,
        },
        "status": "passed",
        "started_at": measurement["started_at"],
        "finished_at": measurement["finished_at"],
        "producer": {
            "script": args.producer_script,
            "algorithm": args.producer_algorithm,
            "version": 2,
        },
        "subject": measurement["subject"],
        "measurements": measurement["measurements"],
        "evidence_files": sorted(references, key=lambda item: item["path"]),
        "production_authorized": False,
        "production_deployed": False,
    }
    validate_schema(document, "g8-2-action-record.schema.json")
    write_json(args.output, document)
    print(args.output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
