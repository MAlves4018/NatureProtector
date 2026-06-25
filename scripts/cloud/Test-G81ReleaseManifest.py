#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker

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


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate a G8.1 immutable release manifest.")
    parser.add_argument("manifest", type=Path)
    parser.add_argument(
        "--schema",
        type=Path,
        default=Path(__file__).resolve().parents[2]
        / "infra/gcp/contracts/g8-1-release-manifest.schema.json",
    )
    args = parser.parse_args()

    errors: list[str] = []
    try:
        manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
        schema = json.loads(args.schema.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001
        print(json.dumps({"status": "failed", "errors": [f"json:{exc}"]}, indent=2))
        return 1

    validator = Draft202012Validator(schema, format_checker=FormatChecker())
    errors.extend(
        f"schema:{'/'.join(map(str, error.absolute_path))}:{error.message}"
        for error in sorted(validator.iter_errors(manifest), key=lambda item: list(item.absolute_path))
    )

    images = manifest.get("images", {})
    missing = REQUIRED_IMAGES - set(images)
    extra = set(images) - REQUIRED_IMAGES
    if missing:
        errors.append(f"images-missing:{','.join(sorted(missing))}")
    if extra:
        errors.append(f"images-extra:{','.join(sorted(extra))}")

    for name, item in images.items():
        reference = item.get("reference", "")
        digest = item.get("digest", "")
        if "@" not in reference or not reference.endswith(digest):
            errors.append(f"image-reference-not-bound-to-digest:{name}")
        if ":git-" in reference:
            errors.append(f"mutable-tag-used-as-release-reference:{name}")

    gate_names = {gate.get("name") for gate in manifest.get("quality_gates", [])}
    for required in {"Engineering foundations", "Security", "G8.1 cloud production policy"}:
        if required not in gate_names:
            errors.append(f"quality-gate-missing:{required}")

    if manifest.get("production_authorized") is not False:
        errors.append("production-authorized-must-remain-false")
    if manifest.get("production_deployed") is not False:
        errors.append("production-deployed-must-remain-false")

    result = {
        "phase": "G8.1",
        "status": "passed" if not errors else "failed",
        "manifest": str(args.manifest),
        "images": len(images),
        "errors": errors,
        "production_authorized": False,
        "production_deployed": False,
    }
    print(json.dumps(result, indent=2))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
