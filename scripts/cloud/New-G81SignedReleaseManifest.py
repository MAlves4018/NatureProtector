#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path


REQUIRED_IMAGES = {
    "backoffice-api",
    "prevention",
    "simulator",
    "postgres-migrations",
    "postgres-bootstrap",
    "frontend",
    "functional-smoke",
    "rabbitmq",
    "otel-collector",
    "distributed-probe",
    "cloud-deploy-verifier",
}

DIGEST_RE = re.compile(r"^sha256:[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
ISSUER = "https://token.actions.githubusercontent.com"
REPOSITORY = "MAlves4018/NatureProtector"
REGISTRY_PREFIX = (
    "europe-southwest1-docker.pkg.dev/"
    "np-platform-migkxl-20260624/"
    "natureprotector/"
)


def load_json(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001
        raise SystemExit(f"json:{path}:{exc}") from exc


def image_map(document: dict) -> dict:
    images = document.get("images")
    if not isinstance(images, dict):
        raise SystemExit("images:missing-or-not-object")
    return images


def validate(
    evidence: dict,
    signatures: dict,
    *,
    expected_identity: str,
) -> dict[str, dict]:
    errors: list[str] = []
    evidence_images = image_map(evidence)
    signature_images = image_map(signatures)

    evidence_names = set(evidence_images)
    signature_names = set(signature_images)
    if evidence_names != REQUIRED_IMAGES:
        errors.append(f"evidence-images-mismatch:{','.join(sorted(evidence_names ^ REQUIRED_IMAGES))}")
    if signature_names != REQUIRED_IMAGES:
        errors.append(f"signature-images-mismatch:{','.join(sorted(signature_names ^ REQUIRED_IMAGES))}")

    seen_references: set[str] = set()
    signed_images: dict[str, dict] = {}
    for name in sorted(REQUIRED_IMAGES):
        item = evidence_images.get(name, {})
        signature = signature_images.get(name, {})
        reference = item.get("reference")
        digest = item.get("digest")

        if not isinstance(reference, str) or "@" not in reference:
            errors.append(f"reference-not-digest-bound:{name}")
            continue
        if not reference.startswith(REGISTRY_PREFIX):
            errors.append(f"reference-outside-approved-registry:{name}")
        if not isinstance(digest, str) or not DIGEST_RE.fullmatch(digest):
            errors.append(f"invalid-digest:{name}")
        elif not reference.endswith(digest):
            errors.append(f"reference-digest-mismatch:{name}")
        name_part = reference.split("@", 1)[0]
        if ":git-" in reference or name_part.rsplit("/", 1)[-1].count(":") > 0:
            errors.append(f"tag-used-as-release-reference:{name}")
        if reference in seen_references:
            errors.append(f"duplicate-reference:{name}")
        seen_references.add(reference)

        if int(item.get("critical", -1)) != 0:
            errors.append(f"critical-not-zero:{name}")
        if int(item.get("high", -1)) != 0:
            errors.append(f"high-not-zero:{name}")
        if item.get("sbom_verified") is not True:
            errors.append(f"sbom-not-verified:{name}")
        if item.get("provenance_verified") is not True:
            errors.append(f"provenance-not-verified:{name}")

        if signature.get("reference") != reference:
            errors.append(f"signature-reference-mismatch:{name}")
        if signature.get("digest") != digest:
            errors.append(f"signature-digest-mismatch:{name}")
        if signature.get("signature_exists") is not True:
            errors.append(f"signature-missing:{name}")
        if signature.get("signature_verified") is not True:
            errors.append(f"signature-not-verified:{name}")
        if signature.get("certificate_oidc_issuer") != ISSUER:
            errors.append(f"issuer-mismatch:{name}")
        if signature.get("certificate_identity") != expected_identity:
            errors.append(f"identity-mismatch:{name}")

        signed_images[name] = {
            "reference": reference,
            "digest": digest,
            "signature_verified": True,
            "high": 0,
            "critical": 0,
            "sbom_verified": True,
            "provenance_verified": True,
        }

    if errors:
        print(json.dumps({"status": "failed", "errors": errors}, indent=2))
        raise SystemExit(1)

    return signed_images


def main() -> int:
    parser = argparse.ArgumentParser(description="Create a signed G8.1 manifest from verified digest evidence.")
    parser.add_argument("--images-evidence", required=True, type=Path)
    parser.add_argument("--signature-results", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--build-run-id", required=True, type=int)
    parser.add_argument("--platform-project", required=True)
    parser.add_argument("--engineering-run-id", required=True, type=int)
    parser.add_argument("--security-run-id", required=True, type=int)
    parser.add_argument("--policy-run-id", required=True, type=int)
    parser.add_argument("--expected-identity", required=True)
    args = parser.parse_args()

    errors: list[str] = []
    if args.repository != REPOSITORY:
        errors.append("repository-mismatch")
    if not COMMIT_RE.fullmatch(args.source_commit):
        errors.append("invalid-source-commit")
    expected_identity = (
        f"https://github.com/{args.repository}/.github/workflows/"
        f"gcp-g8-1-release.yml@refs/heads/master"
    )
    if args.expected_identity != expected_identity:
        errors.append("expected-identity-not-canonical")
    if args.platform_project != "np-platform-migkxl-20260624":
        errors.append("platform-project-mismatch")
    if errors:
        print(json.dumps({"status": "failed", "errors": errors}, indent=2))
        return 1

    signed_images = validate(
        load_json(args.images_evidence),
        load_json(args.signature_results),
        expected_identity=args.expected_identity,
    )

    manifest = {
        "schema_version": 1,
        "repository": args.repository,
        "source_commit": args.source_commit,
        "build_run_id": args.build_run_id,
        "generated_at": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "images": signed_images,
        "quality_gates": [
            {"name": "Engineering foundations", "run_id": args.engineering_run_id, "conclusion": "success"},
            {"name": "Security", "run_id": args.security_run_id, "conclusion": "success"},
            {"name": "G8.1 cloud production policy", "run_id": args.policy_run_id, "conclusion": "success"},
        ],
        "delivery": {
            "platform_project": args.platform_project,
            "region": "europe-southwest1",
            "pipelines": [
                "natureprotector-api",
                "natureprotector-frontend",
                "natureprotector-prevention",
            ],
        },
        "production_authorized": False,
        "production_deployed": False,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"status": "passed", "manifest": str(args.output), "images": len(signed_images)}, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
