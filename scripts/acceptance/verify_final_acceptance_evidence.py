#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
from pathlib import Path

BEARER_RE = re.compile(r"(?i)\bbearer\s+([A-Za-z0-9._~+/=-]{20,})")
ASSIGNMENT_RE = re.compile(
    r"""(?ix)
    \b(?:password|token|secret)\b
    \s*[=:]\s*
    ["']?
    ([A-Za-z0-9._~+/=-]{12,})
    """
)
NON_SECRET_VALUES = {
    "authorization",
    "bearertoken",
    "development-only",
    "example",
    "missing",
    "none",
    "null",
    "password",
    "placeholder",
    "redacted",
    "token",
    "undefined",
    "unknown",
}
NON_SECRET_PREFIXES = (
    "process.env.",
    "import.meta.env.",
    "os.environ.",
    "environment.",
    "$env:",
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_json(path: Path, errors: list[str]) -> dict:
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception as exc:  # pragma: no cover - defensive reporting
        errors.append(f"Invalid JSON {path.name}: {exc}")
        return {}
    if not isinstance(value, dict):
        errors.append(f"Expected object in {path.name}")
        return {}
    return value


def verify_hash_manifest(root: Path, errors: list[str]) -> None:
    manifest = root / "hashes.sha256"
    if not manifest.is_file():
        errors.append("Missing hashes.sha256")
        return

    entries: dict[str, str] = {}
    for number, raw in enumerate(manifest.read_text(encoding="utf-8-sig").splitlines(), start=1):
        if not raw.strip():
            continue
        match = re.fullmatch(r"([a-fA-F0-9]{64})\s{2}(.+)", raw)
        if not match:
            errors.append(f"Invalid hashes.sha256 line {number}")
            continue
        expected, relative = match.groups()
        normalized = relative.replace("\\", "/")
        candidate = (root / normalized).resolve()
        try:
            candidate.relative_to(root)
        except ValueError:
            errors.append(f"Unsafe hash path: {relative}")
            continue
        if normalized in entries:
            errors.append(f"Duplicate hash entry: {normalized}")
            continue
        entries[normalized] = expected.lower()
        if not candidate.is_file():
            errors.append(f"Hashed file is missing: {normalized}")
        elif sha256(candidate) != expected.lower():
            errors.append(f"Hash mismatch: {normalized}")

    root_manifest = (root / "hashes.sha256").resolve()
    actual = {
        path.relative_to(root).as_posix()
        for path in root.rglob("*")
        if path.is_file() and path.resolve() != root_manifest
    }
    missing = sorted(actual - set(entries))
    orphan = sorted(set(entries) - actual)
    if missing:
        errors.append("Files missing from hashes.sha256: " + ", ".join(missing))
    if orphan:
        errors.append("Hash entries without files: " + ", ".join(orphan))


def potential_secret(text: str) -> str | None:
    if BEARER_RE.search(text):
        return "bearer credential"
    for match in ASSIGNMENT_RE.finditer(text):
        candidate = match.group(1)
        lowered = candidate.lower()
        normalized = re.sub(r"[^a-z0-9]", "", lowered)
        if normalized in {re.sub(r"[^a-z0-9]", "", item) for item in NON_SECRET_VALUES}:
            continue
        if lowered.startswith(NON_SECRET_PREFIXES):
            continue
        return "credential assignment"
    return None


def verify_evidence_manifest(root: Path, errors: list[str]) -> None:
    manifest = root / "evidence-manifest.csv"
    if not manifest.is_file():
        errors.append("Missing evidence-manifest.csv")
        return
    with manifest.open(encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))
    if not rows:
        errors.append("evidence-manifest.csv is empty")
        return
    for row in rows:
        relative = str(row.get("path", "")).replace("\\", "/")
        candidate = (root / relative).resolve()
        try:
            candidate.relative_to(root)
        except ValueError:
            errors.append(f"Unsafe evidence path: {relative}")
            continue
        if not candidate.is_file():
            errors.append(f"Evidence file is missing: {relative}")
            continue
        expected = str(row.get("sha256", "")).lower()
        if not re.fullmatch(r"[a-f0-9]{64}", expected):
            errors.append(f"Invalid evidence hash: {relative}")
        elif sha256(candidate) != expected:
            errors.append(f"Evidence manifest hash mismatch: {relative}")
        try:
            expected_size = int(str(row.get("sizeBytes", "")))
        except ValueError:
            errors.append(f"Invalid evidence size: {relative}")
        else:
            if candidate.stat().st_size != expected_size:
                errors.append(f"Evidence manifest size mismatch: {relative}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("acceptance_root", type=Path)
    parser.add_argument(
        "--config",
        type=Path,
        default=Path("config/acceptance/final-acceptance.json"),
    )
    parser.add_argument("--result", type=Path)
    parser.add_argument("--expected-commit")
    parser.add_argument("--expected-source-fingerprint")
    args = parser.parse_args()

    root = args.acceptance_root.resolve()
    config_path = args.config.resolve()
    errors: list[str] = []
    if not root.is_dir():
        errors.append(f"Acceptance root does not exist: {root}")
    if not config_path.is_file():
        errors.append(f"Acceptance config does not exist: {config_path}")

    config = load_json(config_path, errors) if config_path.is_file() else {}
    summary_path = root / "summary.json"
    environment_path = root / "environment.json"
    run_spec_path = root / "run-spec.json"
    for required in (
        summary_path,
        environment_path,
        run_spec_path,
        root / "SUMMARY.md",
        root / "tests.csv",
        root / "commands.csv",
        root / "blockers.csv",
    ):
        if not required.is_file():
            errors.append(f"Missing {required.name}")

    summary = load_json(summary_path, errors) if summary_path.is_file() else {}
    run_spec = load_json(run_spec_path, errors) if run_spec_path.is_file() else {}
    environment = load_json(environment_path, errors) if environment_path.is_file() else {}

    expected_stages = list(config.get("profiles", {}).get("Full", {}).get("stages", []))
    actual_stages = [str(row.get("id", "")) for row in summary.get("stages", []) if isinstance(row, dict)]
    if summary.get("profile") != "Full":
        errors.append("Acceptance profile is not Full")
    if summary.get("status") != "PASS":
        errors.append(f"Acceptance status is not PASS: {summary.get('status')}")
    if run_spec.get("profile") != "Full":
        errors.append("run-spec profile is not Full")
    if run_spec.get("planOnly") is not False:
        errors.append("run-spec is plan-only")
    if run_spec.get("executeControlledValidationP3") is not True:
        errors.append("Controlled validation P3 was not enabled")
    if run_spec.get("acknowledgeNonProduction") is not True:
        errors.append("Non-production acknowledgement is absent")
    if not run_spec.get("p3AuthenticationConfigured"):
        errors.append("P3 authentication was not configured")
    if actual_stages != expected_stages:
        errors.append("Executed Full stage sequence differs from the versioned contract")

    selected = int(summary.get("selectedStageCount", -1) or -1)
    executed = int(summary.get("executedStageCount", -1) or -1)
    passed = int(summary.get("passedStageCount", -1) or -1)
    if not expected_stages or selected != len(expected_stages):
        errors.append("Selected stage count does not match the Full contract")
    if executed != selected or passed != selected:
        errors.append("Not every selected stage executed and passed")
    for field in (
        "failedStageCount",
        "blockedStageCount",
        "harnessErrorStageCount",
        "notSelectedStageCount",
    ):
        if int(summary.get(field, -1) or 0) != 0:
            errors.append(f"Non-zero {field}")
    bad_stages = [
        str(row.get("id", ""))
        for row in summary.get("stages", [])
        if not isinstance(row, dict) or row.get("status") != "PASS" or int(row.get("exitCode", -1)) != 0
    ]
    if bad_stages:
        errors.append("Non-passing stages: " + ", ".join(bad_stages))

    if not environment.get("gitCommit"):
        errors.append("Acceptance evidence does not identify a git commit")
    if environment.get("gitSourceClean") is not True:
        errors.append("Acceptance evidence was not produced from a clean Git source")
    if not environment.get("sourceFingerprint"):
        errors.append("Acceptance evidence has no source fingerprint")
    if args.expected_commit and environment.get("gitCommit") != args.expected_commit:
        errors.append("Acceptance commit differs from the delivery source commit")
    if args.expected_source_fingerprint and environment.get("sourceFingerprint") != args.expected_source_fingerprint:
        errors.append("Acceptance source fingerprint differs from the delivery source fingerprint")

    if root.is_dir():
        verify_evidence_manifest(root, errors)
        verify_hash_manifest(root, errors)
        extensions = {".json", ".csv", ".md", ".txt", ".log"}
        for path in root.rglob("*"):
            if path.is_file() and path.suffix.lower() in extensions:
                finding = potential_secret(path.read_text(encoding="utf-8", errors="replace"))
                if finding:
                    errors.append(f"Potential secret material ({finding}) in {path.relative_to(root).as_posix()}")

    result = {
        "schemaVersion": 1,
        "status": "PASS" if not errors else "FAIL",
        "acceptanceRoot": str(root),
        "profile": summary.get("profile"),
        "acceptanceRunId": summary.get("runId"),
        "gitCommit": environment.get("gitCommit"),
        "sourceFingerprint": environment.get("sourceFingerprint"),
        "sourceClean": environment.get("gitSourceClean"),
        "verifiedStageCount": len(actual_stages),
        "errors": errors,
    }
    if args.result:
        args.result.parent.mkdir(parents=True, exist_ok=True)
        args.result.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
