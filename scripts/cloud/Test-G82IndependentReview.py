#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path

from g82_common import read_json, sha256_file, validate_schema, write_json
from g82_governance import REVIEW_NAMESPACE, evaluate_review_semantics, verify_ssh_signature


def evaluate(
    review_path: Path,
    final_verdict_path: Path,
    evidence_index_path: Path,
    manifest_path: Path,
    archive_receipt_path: Path,
    *,
    signature_ok: bool,
) -> tuple[dict, bool]:
    review = read_json(review_path)
    final = read_json(final_verdict_path)
    index = read_json(evidence_index_path)
    archive = read_json(archive_receipt_path)
    manifest = read_json(manifest_path)
    validate_schema(review, "g8-2-independent-review.schema.json")
    validate_schema(final, "g8-2-final-verdict.schema.json")
    validate_schema(index, "g8-2-evidence-index.schema.json")
    validate_schema(archive, "g8-2-archive-receipt.schema.json")
    validate_schema(manifest, "g8-1-release-manifest.schema.json")

    semantic = evaluate_review_semantics(review)
    manifest_sha = sha256_file(manifest_path)
    bindings = (
        review["qualification_id"] == final["qualification_id"] == index["qualification_id"] == archive["qualification_id"]
        and review["candidate_commit"] == final["source_commit"]
        and review["candidate_manifest_sha256"] == manifest_sha
        and final["candidate_manifest_sha256"] == manifest_sha
        and index["candidate_manifest_sha256"] == manifest_sha
        and archive["candidate_manifest_sha256"] == manifest_sha
        and review["evidence_index_sha256"] == sha256_file(evidence_index_path)
        and review["final_qualification_verdict_sha256"] == sha256_file(final_verdict_path)
        and review["archive_receipt_sha256"] == sha256_file(archive_receipt_path)
    )
    checks = {
        "qualification_passed": final["status"] == "G82_FINAL_QUALIFICATION_PASSED",
        "archive_passed": archive["status"] == "passed",
        "bindings": bindings,
        **semantic,
        "signature": signature_ok
        and review["signature_namespace"] == REVIEW_NAMESPACE,
    }
    passed = all(checks.values())
    result = {
        "schema_version": 2,
        "phase": "G8.2",
        "qualification_id": review["qualification_id"],
        "status": "G82_INDEPENDENT_REVIEW_ACCEPTED" if passed else "G82_BLOCKED_PENDING_INDEPENDENT_REVIEW",
        "checks": checks,
        "review_sha256": sha256_file(review_path),
        "reviewer_identity": review["reviewer_identity"],
        "decision": review["decision"],
        "candidate_manifest_sha256": manifest_sha,
        "final_qualification_verdict_sha256": sha256_file(final_verdict_path),
        "archive_receipt_sha256": sha256_file(archive_receipt_path),
        "production_authorized": False,
        "production_deployed": False,
    }
    validate_schema(result, "g8-2-independent-review-verdict.schema.json")
    return result, passed


def main() -> int:
    parser = argparse.ArgumentParser()
    for name in [
        "review",
        "signature",
        "allowed-signers",
        "final-verdict",
        "evidence-index",
        "candidate-manifest",
        "archive-receipt",
        "output",
    ]:
        parser.add_argument("--" + name, required=True)
    args = parser.parse_args()
    review = read_json(args.review)
    try:
        validate_schema(review, "g8-2-independent-review.schema.json")
        signature_ok, signature_detail = verify_ssh_signature(
            args.review,
            args.signature,
            args.allowed_signers,
            review.get("reviewer_identity", ""),
            REVIEW_NAMESPACE,
        )
    except Exception as exc:
        signature_ok, signature_detail = False, str(exc)
    result, passed = evaluate(
        Path(args.review),
        Path(args.final_verdict),
        Path(args.evidence_index),
        Path(args.candidate_manifest),
        Path(args.archive_receipt),
        signature_ok=signature_ok,
    )
    result["signature_detail"] = signature_detail[-1000:]
    # signature_detail is intentionally not in the strict persisted contract.
    output_document = dict(result)
    output_document.pop("signature_detail", None)
    write_json(args.output, output_document)
    print(output_document["status"])
    return 0 if passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
