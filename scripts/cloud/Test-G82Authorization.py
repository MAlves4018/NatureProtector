#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path

from g82_common import read_json, sha256_file, validate_schema, write_json
from g82_governance import AUTHORIZATION_NAMESPACE, evaluate_authorization_semantics, verify_ssh_signature


def evaluate(
    request_path: Path,
    decision_path: Path,
    final_path: Path,
    index_path: Path,
    archive_path: Path,
    review_path: Path,
    review_verdict_path: Path,
    manifest_path: Path,
    *,
    signature_ok: bool,
) -> tuple[dict, bool]:
    request = read_json(request_path)
    decision = read_json(decision_path)
    final = read_json(final_path)
    index = read_json(index_path)
    archive = read_json(archive_path)
    review = read_json(review_path)
    review_verdict = read_json(review_verdict_path)
    manifest = read_json(manifest_path)
    validate_schema(request, "g8-2-authorization-request.schema.json")
    validate_schema(decision, "g8-2-authorization-decision.schema.json")
    validate_schema(final, "g8-2-final-verdict.schema.json")
    validate_schema(index, "g8-2-evidence-index.schema.json")
    validate_schema(archive, "g8-2-archive-receipt.schema.json")
    validate_schema(review, "g8-2-independent-review.schema.json")
    validate_schema(review_verdict, "g8-2-independent-review-verdict.schema.json")
    validate_schema(manifest, "g8-1-release-manifest.schema.json")

    expected = {
        "authorization_request_sha256": sha256_file(request_path),
        "candidate_manifest_sha256": sha256_file(manifest_path),
        "evidence_index_sha256": sha256_file(index_path),
        "final_qualification_verdict_sha256": sha256_file(final_path),
        "archive_receipt_sha256": sha256_file(archive_path),
        "independent_review_sha256": sha256_file(review_path),
        "independent_review_verdict_sha256": sha256_file(review_verdict_path),
    }
    binding_fields = all(decision.get(key) == value for key, value in expected.items())
    request_bindings = (
        request["candidate_manifest_sha256"] == expected["candidate_manifest_sha256"]
        and request["evidence_index_sha256"] == expected["evidence_index_sha256"]
        and request["final_qualification_verdict_sha256"] == expected["final_qualification_verdict_sha256"]
        and request["archive_receipt_sha256"] == expected["archive_receipt_sha256"]
        and request["independent_review_sha256"] == expected["independent_review_sha256"]
        and request["independent_review_verdict_sha256"] == expected["independent_review_verdict_sha256"]
    )
    common_binding = (
        decision["qualification_id"] == request["qualification_id"] == final["qualification_id"] == index["qualification_id"] == archive["qualification_id"]
        and decision["source_commit"] == request["source_commit"] == manifest["source_commit"]
        and decision["environment"] == request["environment"]
        and decision["scope"] == request["scope"]
        and decision["independent_reviewer_identity"] == request["reviewer_identity"]
        and decision["executor_identity"] == request["executor_identity"]
    )
    semantic = evaluate_authorization_semantics(request, decision)
    checks = {
        "upstream_passed": final["status"] == "G82_FINAL_QUALIFICATION_PASSED"
        and review_verdict["status"] == "G82_INDEPENDENT_REVIEW_ACCEPTED"
        and archive["status"] == "passed",
        "request_not_authorized": request["production_authorized"] is False
        and request["production_deployed"] is False,
        "file_bindings": binding_fields and request_bindings,
        "semantic_bindings": common_binding,
        **semantic,
        "signature": signature_ok
        and decision["signature_namespace"] == AUTHORIZATION_NAMESPACE,
    }
    passed = all(checks.values())
    result = {
        "schema_version": 2,
        "phase": "G8.2",
        "qualification_id": request["qualification_id"],
        "status": "G82_PRODUCTION_AUTHORIZATION_VERIFIED" if passed else "G82_BLOCKED_PENDING_SIGNED_AUTHORIZATION",
        "checks": checks,
        "authorization_request_sha256": sha256_file(request_path),
        "authorization_decision_sha256": sha256_file(decision_path),
        "authorizer_identity": decision["authorizer_identity"],
        "scope": decision["scope"],
        "expires_at": decision["expires_at"],
        "production_authorized": passed,
        "production_deployed": False,
        "next_phase_required": "controlled-production-launch",
    }
    validate_schema(result, "g8-2-authorization-verification.schema.json")
    return result, passed


def main() -> int:
    parser = argparse.ArgumentParser()
    for name in [
        "request",
        "decision",
        "signature",
        "allowed-signers",
        "final-verdict",
        "evidence-index",
        "archive-receipt",
        "independent-review",
        "independent-review-verdict",
        "candidate-manifest",
        "output",
    ]:
        parser.add_argument("--" + name, required=True)
    args = parser.parse_args()
    decision = read_json(args.decision)
    try:
        validate_schema(decision, "g8-2-authorization-decision.schema.json")
        signature_ok, signature_detail = verify_ssh_signature(
            args.decision,
            args.signature,
            args.allowed_signers,
            decision.get("authorizer_identity", ""),
            AUTHORIZATION_NAMESPACE,
        )
    except Exception as exc:
        signature_ok, signature_detail = False, str(exc)
    result, passed = evaluate(
        Path(args.request),
        Path(args.decision),
        Path(args.final_verdict),
        Path(args.evidence_index),
        Path(args.archive_receipt),
        Path(args.independent_review),
        Path(args.independent_review_verdict),
        Path(args.candidate_manifest),
        signature_ok=signature_ok,
    )
    write_json(args.output, result)
    print(result["status"])
    if not signature_ok:
        print(signature_detail[-1000:])
    return 0 if passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
