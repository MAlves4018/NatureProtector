#!/usr/bin/env python3
"""Validate configuration authority while preserving the canonical deployment snapshot.

Deployment files from the current canonical repository are verified by SHA-256 and are
not rewritten to match older maintainability snapshots.
"""

from __future__ import annotations
import argparse
import hashlib
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any

AUTHORITY_MANIFEST = Path("docs/implementation/maintainability/canonical-deployment-authority-2026-06-28.json")
PROJECT_REFERENCE_EXCLUDED_PREFIXES = (
    "docs/RepositorioDocumental/",
    ".nuget/",
    ".testbin/",
    "artifacts/",
    "BenchmarkDotNet.Artifacts/",
)
PROJECT_REFERENCE_EXCLUDED_PARTS = {
    "bin",
    "obj",
    "node_modules",
}


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def package_versions(root: Path) -> tuple[dict[str, str], list[str]]:
    path = root / "Directory.Packages.props"
    errors = []
    if not path.is_file():
        return {}, ["missing:Directory.Packages.props"]
    try:
        doc = ET.parse(path)
    except ET.ParseError as exc:
        return {}, [f"invalid-xml:Directory.Packages.props:{exc}"]
    versions = {}
    for el in doc.findall(".//PackageVersion"):
        name = el.attrib.get("Include", "")
        version = el.attrib.get("Version", "")
        if not name or not version:
            errors.append("invalid-package-version-entry")
            continue
        if name in versions:
            errors.append(f"duplicate-package-version:{name}")
        versions[name] = version
    return versions, errors


def project_package_references(root: Path) -> tuple[set[str], list[str]]:
    refs = set()
    errors = []
    for path in sorted(root.rglob("*.csproj")):
        rel = path.relative_to(root).as_posix()
        if rel.startswith(PROJECT_REFERENCE_EXCLUDED_PREFIXES) or PROJECT_REFERENCE_EXCLUDED_PARTS.intersection(path.parts):
            continue
        try:
            doc = ET.parse(path)
        except ET.ParseError as exc:
            errors.append(f"invalid-xml:{rel}:{exc}")
            continue
        for el in doc.findall(".//PackageReference"):
            name = el.attrib.get("Include", "")
            if not name:
                errors.append(f"package-reference-without-include:{rel}")
                continue
            refs.add(name)
            if "Version" in el.attrib or el.find("Version") is not None:
                errors.append(f"project-package-version-declared:{rel}:{name}")
    return refs, errors


def validate_repository(root: Path) -> dict[str, Any]:
    root = root.resolve()
    errors = []
    checks = 0

    def check(ok: bool, code: str):
        nonlocal checks
        checks += 1
        if not ok:
            errors.append(code)

    registry_path = root / "config/configuration-authorities.json"
    check(registry_path.is_file(), "missing:config/configuration-authorities.json")
    registry = read_json(registry_path) if registry_path.is_file() else {"authorities": []}
    check(registry.get("schema_version") == 2, "configuration-authority-schema-not-v2")
    ids = set()
    for authority in registry.get("authorities", []):
        aid = str(authority.get("id", ""))
        rel = str(authority.get("path", ""))
        check(bool(aid), "authority-without-id")
        check(aid not in ids, f"duplicate-authority-id:{aid}")
        ids.add(aid)
        check(bool(rel), f"authority-without-path:{aid}")
        if rel:
            check((root / rel).is_file(), f"missing-authority:{aid}:{rel}")

    manifest_path = root / AUTHORITY_MANIFEST
    check(manifest_path.is_file(), f"missing:{AUTHORITY_MANIFEST.as_posix()}")
    manifest = read_json(manifest_path) if manifest_path.is_file() else {"protected_hashes": {}, "semantic_merges": {}}
    protected = manifest.get("protected_hashes", {})
    check(manifest.get("protected_file_count") == len(protected), "deployment-protected-count-mismatch")
    deployment_snapshot_drift = []
    for rel, expected in protected.items():
        path = root / rel
        check(path.is_file(), f"deployment-authority-missing:{rel}")
        if path.is_file() and sha256(path) != expected:
            deployment_snapshot_drift.append({"path": rel, "expected": expected, "actual": sha256(path)})

    for rel, policy in manifest.get("semantic_merges", {}).items():
        path = root / rel
        check(path.is_file(), f"semantic-merge-missing:{rel}")
        text = path.read_text(encoding="utf-8-sig") if path.is_file() else ""
        for marker in policy.get("required_deployment_markers", []):
            check(marker in text, f"semantic-merge-deployment-marker-missing:{rel}:{marker}")
        for marker in policy.get("required_tooling_markers", []):
            check(marker in text, f"semantic-merge-tooling-marker-missing:{rel}:{marker}")

    versions, version_errors = package_versions(root)
    errors.extend(version_errors)
    checks += 1
    refs, ref_errors = project_package_references(root)
    errors.extend(ref_errors)
    checks += 1
    check(bool(versions), "central-package-list-empty")
    check(refs == set(versions), "central-package-set-mismatch")
    policy_path = root / "config/dependencies/dotnet-package-policy.json"
    check(policy_path.is_file(), "missing:config/dependencies/dotnet-package-policy.json")
    if policy_path.is_file():
        policy = read_json(policy_path)
        check(policy.get("package_version_authority") == "Directory.Packages.props", "dotnet-policy-authority-invalid")
        check(policy.get("target_framework") == "net9.0", "dotnet-policy-framework-invalid")

    for rel in (
        "deploy/environments/common.json",
        "deploy/environments/staging.json",
        "deploy/environments/production.json",
    ):
        path = root / rel
        check(path.is_file(), f"missing:{rel}")
        if path.is_file():
            try:
                read_json(path)
                check(True, f"json-valid:{rel}")
            except Exception:
                check(False, f"invalid-json:{rel}")

    return {
        "phase": "CONFIGURATION_AUTHORITY_VALIDATION_V2",
        "status": "PASS" if not errors else "FAIL",
        "checks_total": checks,
        "checks_failed": len(errors),
        "errors": sorted(errors),
        "cloud_mutation": False,
        "repository_mutation": False,
        "metrics": {
            "authorities": len(registry.get("authorities", [])),
            "historical_deployment_snapshot_files": len(protected),
            "deployment_snapshot_drift": len(deployment_snapshot_drift),
            "central_package_versions": len(versions),
            "project_package_references": len(refs),
        },
        "deployment_snapshot_observations": deployment_snapshot_drift,
    }


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    p.add_argument("--output", type=Path)
    a = p.parse_args()
    result = validate_repository(a.repo)
    rendered = json.dumps(result, indent=2, sort_keys=True) + "\n"
    if a.output:
        a.output.parent.mkdir(parents=True, exist_ok=True)
        a.output.write_text(rendered, encoding="utf-8")
    print(rendered, end="")
    return 0 if result["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
