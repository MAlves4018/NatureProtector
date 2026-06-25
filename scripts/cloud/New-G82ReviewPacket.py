#!/usr/bin/env python3
from __future__ import annotations

import argparse
import shutil
from pathlib import Path

from g82_common import read_json, sha256_file, validate_schema, write_json


def main() -> int:
    parser = argparse.ArgumentParser()
    for name in [
        "qualification-bundle",
        "final-verdict",
        "evidence-index",
        "candidate-manifest",
        "archive-receipt",
        "output-directory",
    ]:
        parser.add_argument("--" + name, required=True)
    args = parser.parse_args()
    final = read_json(args.final_verdict)
    index = read_json(args.evidence_index)
    archive = read_json(args.archive_receipt)
    manifest = read_json(args.candidate_manifest)
    validate_schema(final, "g8-2-final-verdict.schema.json")
    validate_schema(index, "g8-2-evidence-index.schema.json")
    validate_schema(archive, "g8-2-archive-receipt.schema.json")
    validate_schema(manifest, "g8-1-release-manifest.schema.json")
    if final["status"] != "G82_FINAL_QUALIFICATION_PASSED":
        raise SystemExit("cannot create review packet before final qualification passes")
    output = Path(args.output_directory)
    output.mkdir(parents=True, exist_ok=True)
    sources = {
        "qualification_bundle": Path(args.qualification_bundle),
        "final_verdict": Path(args.final_verdict),
        "evidence_index": Path(args.evidence_index),
        "candidate_manifest": Path(args.candidate_manifest),
        "archive_receipt": Path(args.archive_receipt),
    }
    copied = {}
    for key, source in sources.items():
        destination = output / source.name
        shutil.copy2(source, destination)
        copied[key] = {
            "name": destination.name,
            "sha256": sha256_file(destination),
            "size_bytes": destination.stat().st_size,
        }
    metadata = {
        "schema_version": 2,
        "phase": "G8.2",
        "qualification_id": final["qualification_id"],
        "status": "independent_review_required",
        "source_commit": final["source_commit"],
        "candidate_manifest_sha256": sha256_file(args.candidate_manifest),
        "files": copied,
        "production_authorized": False,
        "production_deployed": False,
    }
    write_json(output / "review-packet.json", metadata)
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
