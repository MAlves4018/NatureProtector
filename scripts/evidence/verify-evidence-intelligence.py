#!/usr/bin/env python3
"""Verify a Phase 10 evidence-intelligence package."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
from pathlib import Path
from typing import Any

REQUIRED_FILES = {
    "phase10-summary.json",
    "phase10-summary.md",
    "evidence-index.json",
    "evidence-index.csv",
    "integrity-audit.json",
    "integrity-audit.csv",
    "phase-scorecard.json",
    "phase-scorecard.csv",
    "claim-lineage.json",
    "claim-lineage.csv",
    "figure-inventory.json",
    "figure-inventory.csv",
    "report-asset-audit.json",
    "report-asset-audit.csv",
    "report-area-coverage.json",
    "report-area-coverage.csv",
    "evidence-gap-register.json",
    "evidence-gap-register.csv",
    "evidence-quality-scorecard.csv",
    "report-ready/evidence-at-a-glance.md",
    "report-ready/figures/evidence-quality-scorecard.svg",
    "report-ready/figures/phase-coverage.svg",
    "report-ready/figures/claim-lineage.dot",
    "report-ready/tables/evidence-quality-scorecard.md",
    "report-ready/tables/phase-coverage.md",
    "report-ready/tables/claim-lineage.md",
    "report-ready/tables/evidence-gaps.md",
    "report-ready/tables/report-area-coverage.md",
    "report-ready/phase10-report-asset-manifest.json",
    "SHA256SUMS.txt",
}
SECRET_PATTERNS = [
    re.compile(r"(?i)(password|passwd|pwd|token|secret|api[_-]?key)\s*[:=]\s*[^\s<]{8,}"),
    re.compile(r"postgres(?:ql)?://[^\s]+", re.I),
    re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_json(path: Path, default: Any = None) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return default


def verify_manifest(root: Path) -> list[str]:
    issues: list[str] = []
    manifest = root / "SHA256SUMS.txt"
    for line_number, line in enumerate(manifest.read_text(encoding="utf-8").splitlines(), start=1):
        if not line.strip():
            continue
        match = re.match(r"^([0-9a-f]{64})\s+\*?(.*)$", line.strip(), re.I)
        if not match:
            issues.append(f"invalid hash line {line_number}")
            continue
        expected, raw = match.group(1).lower(), match.group(2).strip()
        target = (root / raw).resolve()
        try:
            target.relative_to(root.resolve())
        except ValueError:
            issues.append(f"hash path escapes package: {raw}")
            continue
        if not target.is_file():
            issues.append(f"hashed file missing: {raw}")
        elif sha256(target) != expected:
            issues.append(f"hash mismatch: {raw}")
    return issues


def scan_secrets(root: Path) -> list[str]:
    findings: list[str] = []
    for path in root.rglob("*"):
        if not path.is_file() or path.suffix.lower() in {".png", ".jpg", ".jpeg", ".webp", ".zip"}:
            continue
        if path.stat().st_size > 4 * 1024 * 1024:
            continue
        try:
            text = path.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        for pattern in SECRET_PATTERNS:
            if pattern.search(text):
                findings.append(path.relative_to(root).as_posix())
                break
    return findings


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence_root", type=Path)
    parser.add_argument("--require-ready", action="store_true")
    args = parser.parse_args()
    root = args.evidence_root.resolve()
    issues: list[str] = []

    for relative in sorted(REQUIRED_FILES):
        if not (root / relative).is_file():
            issues.append(f"required file missing: {relative}")

    summary = read_json(root / "phase10-summary.json", {})
    if not isinstance(summary, dict):
        issues.append("phase10-summary.json is invalid")
    else:
        if summary.get("status") not in {"READY_TO_SHARE", "SHARE_WITH_CAVEATS", "NEEDS_REVISION"}:
            issues.append(f"invalid phase10 status: {summary.get('status')}")
        score = summary.get("overallScore")
        if not isinstance(score, (int, float)) or not (0 <= score <= 100):
            issues.append("overallScore must be between 0 and 100")
        counts = summary.get("counts", {})
        if int(counts.get("artifacts", 0)) <= 0:
            issues.append("no artifacts were indexed")
        if args.require_ready and summary.get("status") != "READY_TO_SHARE":
            issues.append(f"package is not READY_TO_SHARE: {summary.get('status')}")

    integrity = read_json(root / "integrity-audit.json", [])
    if not isinstance(integrity, list):
        issues.append("integrity-audit.json is invalid")
    else:
        bad = [row for row in integrity if row.get("status") in {"MISMATCH", "PATH_ESCAPE", "INVALID_MANIFEST_LINE"}]
        if bad:
            issues.append(f"source integrity audit contains {len(bad)} material failures")

    claims = read_json(root / "claim-lineage.json", [])
    if not isinstance(claims, list):
        issues.append("claim-lineage.json is invalid")
    else:
        missing_sources = [row for row in claims if not row.get("sourceExists")]
        if missing_sources:
            issues.append(f"{len(missing_sources)} claims reference missing source files")

    figures = read_json(root / "figure-inventory.json", [])
    if not isinstance(figures, list):
        issues.append("figure-inventory.json is invalid")

    scorecard_csv = root / "evidence-quality-scorecard.csv"
    if scorecard_csv.is_file():
        with scorecard_csv.open(encoding="utf-8", newline="") as handle:
            rows = list(csv.DictReader(handle))
        dimensions = {row.get("dimension") for row in rows}
        expected = {"integrity", "completeness", "traceability", "reproducibility", "presentation"}
        if dimensions != expected:
            issues.append(f"scorecard dimensions differ: {sorted(dimensions)}")

    if (root / "SHA256SUMS.txt").is_file():
        issues.extend(verify_manifest(root))
    findings = scan_secrets(root)
    if findings:
        issues.append("possible secret material in: " + ", ".join(findings[:10]))

    if issues:
        print("PHASE_10_VERIFY_STATUS=FAIL")
        for issue in issues:
            print(f"- {issue}")
        return 1
    print("PHASE_10_VERIFY_STATUS=PASS")
    print(f"PHASE_10_VERIFY_ROOT={root}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
