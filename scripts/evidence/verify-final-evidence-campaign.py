#!/usr/bin/env python3
from __future__ import annotations
import argparse
import csv
import hashlib
import json
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))
from proof_contracts import REQUIRED_CAMPAIGNS, validate_case_tree


def digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for c in iter(lambda: f.read(1024 * 1024), b""):
            h.update(c)
    return h.hexdigest()


def verify_exact_hashes(root: Path) -> list[str]:
    errors = []
    mf = root / "hashes.sha256"
    if not mf.is_file():
        return ["Root hashes.sha256 is missing."]
    listed = {}
    for line in mf.read_text().splitlines():
        try:
            expected, rel = line.split("  ", 1)
        except ValueError:
            errors.append("Malformed hash line")
            continue
        listed[rel] = expected
    observed = {p.relative_to(root).as_posix() for p in root.rglob("*") if p.is_file() and p != mf}
    if observed != set(listed):
        for rel in sorted(observed - set(listed)):
            errors.append(f"Unlisted extra file: {rel}")
        for rel in sorted(set(listed) - observed):
            errors.append(f"Missing hashed file: {rel}")
    for rel, expected in listed.items():
        p = root / rel
        if p.is_file() and digest(p) != expected:
            errors.append(f"Hash mismatch: {rel}")
    return errors


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("portfolio", type=Path)
    ap.add_argument("--require-live", action="store_true")
    a = ap.parse_args()
    root = a.portfolio.resolve()
    errors = verify_exact_hashes(root)
    for n in ("manifest.json", "verdict.json", "REPORT_EVIDENCE_MATRIX.csv"):
        if not (root / n).is_file():
            errors.append(f"Missing {n}")
    if errors:
        print("\n".join(errors))
        return 1
    manifest = json.loads((root / "manifest.json").read_text())
    verdict = json.loads((root / "verdict.json").read_text())
    cases = manifest.get("cases") or []
    campaigns = {x.get("campaignId") for x in cases}
    if campaigns != REQUIRED_CAMPAIGNS:
        errors.append(f"Campaign coverage mismatch: {sorted(campaigns)}")
    ids = set()
    for case in cases:
        key = (case.get("campaignId"), case.get("caseId"))
        if key in ids:
            errors.append(f"Duplicate case: {key}")
        ids.add(key)
        case_dir = root / case.get("artifact", "")
        errors += [f"{key}: {e}" for e in validate_case_tree(case_dir, case.get("kind"))]
        v = json.loads((case_dir / "verdict.json").read_text()) if (case_dir / "verdict.json").is_file() else {}
        claim = (
            json.loads((case_dir / "tables/claim-assertions.json").read_text())
            if (case_dir / "tables/claim-assertions.json").is_file()
            else {}
        )
        if manifest.get("mode") == "live" and (v.get("status") != "PASS" or not claim.get("passed")):
            errors.append(f"{key}: live claim verdict is not PASS")
    with (root / "REPORT_EVIDENCE_MATRIX.csv").open(newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    if not rows:
        errors.append("Evidence matrix is empty.")
    if a.require_live:
        if (
            manifest.get("mode") != "live"
            or verdict.get("status") != "REPORT_EVIDENCE_PORTFOLIO_READY"
            or not verdict.get("live")
        ):
            errors.append("Live gate not satisfied.")
    if errors:
        print("\n".join(errors))
        return 1
    print(verdict.get("status"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
