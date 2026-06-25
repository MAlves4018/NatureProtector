#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path

from g82_common import iso_utc, read_json, sha256_file, validate_schema, write_json


def main() -> int:
    parser = argparse.ArgumentParser()
    for name in [
        "final-verdict",
        "evidence-index",
        "archive-receipt",
        "independent-review",
        "independent-review-verdict",
        "candidate-manifest",
        "output",
    ]:
        parser.add_argument("--" + name, required=True)
    parser.add_argument("--scope", default="controlled-production-launch-single-region")
    parser.add_argument("--validity-hours", type=int, default=24)
    args = parser.parse_args()
    if not 1 <= args.validity_hours <= 168:
        raise SystemExit("validity must be 1..168 hours")

    final = read_json(args.final_verdict)
    index = read_json(args.evidence_index)
    archive = read_json(args.archive_receipt)
    review = read_json(args.independent_review)
    review_verdict = read_json(args.independent_review_verdict)
    manifest = read_json(args.candidate_manifest)
    validate_schema(final, "g8-2-final-verdict.schema.json")
    validate_schema(index, "g8-2-evidence-index.schema.json")
    validate_schema(archive, "g8-2-archive-receipt.schema.json")
    validate_schema(review, "g8-2-independent-review.schema.json")
    validate_schema(review_verdict, "g8-2-independent-review-verdict.schema.json")
    validate_schema(manifest, "g8-1-release-manifest.schema.json")
    if final["status"] != "G82_FINAL_QUALIFICATION_PASSED":
        raise SystemExit("final qualification has not passed")
    if review_verdict["status"] != "G82_INDEPENDENT_REVIEW_ACCEPTED":
        raise SystemExit("independent review has not passed")

    manifest_sha = sha256_file(args.candidate_manifest)
    if not (
        final["candidate_manifest_sha256"] == manifest_sha
        and index["candidate_manifest_sha256"] == manifest_sha
        and archive["candidate_manifest_sha256"] == manifest_sha
        and review["candidate_manifest_sha256"] == manifest_sha
        and review_verdict["candidate_manifest_sha256"] == manifest_sha
    ):
        raise SystemExit("upstream candidate manifest bindings do not match")
    qualification_id = final["qualification_id"]
    if len({qualification_id, index["qualification_id"], archive["qualification_id"], review["qualification_id"], review_verdict["qualification_id"]}) != 1:
        raise SystemExit("upstream qualification IDs do not match")

    output = {
        "schema_version": 2,
        "phase": "G8.2",
        "qualification_id": qualification_id,
        "created_at": iso_utc(),
        "source_commit": manifest["source_commit"],
        "build_run_id": manifest["build_run_id"],
        "candidate_manifest_sha256": manifest_sha,
        "evidence_index_sha256": sha256_file(args.evidence_index),
        "final_qualification_verdict_sha256": sha256_file(args.final_verdict),
        "archive_receipt_sha256": sha256_file(args.archive_receipt),
        "independent_review_sha256": sha256_file(args.independent_review),
        "independent_review_verdict_sha256": sha256_file(args.independent_review_verdict),
        "reviewer_identity": review_verdict["reviewer_identity"],
        "executor_identity": review["executor_identity"],
        "environment": "production",
        "scope": args.scope,
        "requested_validity_hours": args.validity_hours,
        "production_authorized": False,
        "production_deployed": False,
    }
    validate_schema(output, "g8-2-authorization-request.schema.json")
    write_json(args.output, output)
    print(args.output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
