#!/usr/bin/env python3
"""Verify a NatureProtector report-evidence campaign output."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path

ALLOWED_STATUSES = {"PASS", "PASS_PARTIAL_REPORT_PACKAGE", "PLAN_ONLY", "PARTIAL", "FAIL", "BLOCKED_SAFETY"}
SECRET_PATTERNS = [
    re.compile(r"(?i)(password|bearer|token|connectionstring)\s*[:=]\s*[^<\s][^\s]*"),
    re.compile(r"postgres(?:ql)?://[^\s:@/]+:[^\s@/]+@", re.I),
]


def digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("campaign_root", type=Path)
    parser.add_argument("--require-pass", action="store_true")
    args = parser.parse_args()
    root = args.campaign_root.resolve()
    required = [
        root / "preflight.json",
        root / "execution-plan.json",
        root / "campaign-summary.json",
        root / "campaign-summary.md",
        root / "step-results.csv",
        root / "SHA256SUMS.txt",
    ]
    missing = [str(p) for p in required if not p.exists()]
    if missing:
        raise SystemExit("Missing required files: " + ", ".join(missing))

    summary = json.loads((root / "campaign-summary.json").read_text(encoding="utf-8"))
    status = summary.get("status")
    if status not in ALLOWED_STATUSES:
        raise SystemExit(f"Invalid campaign status: {status}")
    if args.require_pass and status not in {"PASS", "PASS_PARTIAL_REPORT_PACKAGE"}:
        raise SystemExit(f"Campaign is not PASS: {status}")

    lines = (root / "SHA256SUMS.txt").read_text(encoding="utf-8").splitlines()
    verified = 0
    for line in lines:
        if not line.strip():
            continue
        expected, relative = line.split("  ", 1)
        path = root / relative
        if not path.is_file():
            raise SystemExit(f"Missing hashed file: {relative}")
        actual = digest(path)
        if actual != expected:
            raise SystemExit(f"SHA-256 mismatch: {relative}")
        verified += 1

    scanned = 0
    for path in root.rglob("*"):
        if not path.is_file() or path.name == "SHA256SUMS.txt" or path.stat().st_size > 5_000_000:
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        scanned += 1
        sanitized = text.replace("<redacted>", "")
        for pattern in SECRET_PATTERNS:
            if pattern.search(sanitized):
                raise SystemExit(f"Possible secret material in {path.relative_to(root)}")

    selected = [row for row in summary.get("steps", []) if row.get("selected")]
    if status in {"PASS", "PASS_PARTIAL_REPORT_PACKAGE"} and (
        not selected
        or any(
            row.get("status") not in {"PASS", "PASS_COMPLETE_REPORT_PACKAGE", "PASS_PARTIAL_REPORT_PACKAGE", "PASS_EXPLORATORY_VALIDATION", "PASS_GAP_CLOSURE_READY", "PASS_EVIDENCE_COMPLETE", "PLAN_READY_EVIDENCE_INCOMPLETE", "PASS_WITH_LIMITATIONS"}
            for row in selected
        )
    ):
        raise SystemExit("PASS campaign contains a selected non-PASS step")
    if status == "PLAN_ONLY" and any(
        row.get("status") not in {"PLANNED", "NOT_SELECTED"} for row in summary.get("steps", [])
    ):
        raise SystemExit("PLAN_ONLY campaign contains an executed/invalid step status")

    print("PHASE_8_VERIFICATION=PASS")
    print(f"VERIFIED_CAMPAIGN_STATUS={status}")
    print(f"VERIFIED_HASHED_FILES={verified}")
    print(f"SECRET_SCAN_FILES={scanned}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
