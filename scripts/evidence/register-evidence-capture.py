#!/usr/bin/env python3
"""Register a manual screenshot or exported visual with reproducible metadata."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import shutil
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def utc_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def safe_id(value: str) -> str:
    normalized = re.sub(r"[^A-Za-z0-9._-]+", "-", value.strip()).strip("-.")
    if not normalized:
        raise ValueError("capture id is empty after normalization")
    return normalized


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_json(path: Path, default: Any) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return default


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", type=Path, required=True)
    parser.add_argument("--evidence-root", type=Path, required=True)
    parser.add_argument("--capture-id")
    parser.add_argument("--title", required=True)
    parser.add_argument("--purpose", required=True)
    parser.add_argument("--chapter-target", required=True)
    parser.add_argument("--baseline-id", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--source-page", required=True)
    parser.add_argument("--captured-at-utc", default=utc_iso())
    parser.add_argument("--scenario", default="")
    parser.add_argument("--simulation-run-id", default="")
    parser.add_argument("--cycle-start", type=int)
    parser.add_argument("--cycle-end", type=int)
    parser.add_argument("--filters", default="")
    parser.add_argument("--redactions", default="")
    parser.add_argument("--interpretation", default="")
    parser.add_argument("--limitations", default="")
    args = parser.parse_args()

    image = args.image.resolve()
    if not image.is_file():
        raise SystemExit(f"Image not found: {image}")
    if image.suffix.lower() not in {".png", ".jpg", ".jpeg", ".webp", ".svg"}:
        raise SystemExit("Supported capture formats: PNG, JPG, JPEG, WEBP and SVG")
    capture_id = safe_id(args.capture_id or f"CAP-{args.run_id}-{image.stem}")
    root = args.evidence_root.resolve()
    capture_root = root / "manual-captures" / capture_id
    if capture_root.exists():
        raise SystemExit(f"Capture already exists: {capture_root}")
    capture_root.mkdir(parents=True)
    target = capture_root / f"capture{image.suffix.lower()}"
    shutil.copy2(image, target)
    digest = sha256(target)
    metadata = {
        "captureId": capture_id,
        "title": args.title,
        "purpose": args.purpose,
        "chapterTarget": args.chapter_target,
        "baselineId": args.baseline_id,
        "runId": args.run_id,
        "capturedAtUtc": args.captured_at_utc,
        "sourcePage": args.source_page,
        "scenario": args.scenario,
        "simulationRunId": args.simulation_run_id,
        "cycleStart": args.cycle_start,
        "cycleEnd": args.cycle_end,
        "filters": args.filters,
        "redactions": args.redactions,
        "interpretation": args.interpretation,
        "limitations": args.limitations,
        "sourceFilename": image.name,
        "registeredFilename": target.name,
        "sha256": digest,
        "sizeBytes": target.stat().st_size,
        "registeredAtUtc": utc_iso(),
    }
    (capture_root / "metadata.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (capture_root / "SHA256SUMS.txt").write_text(f"{digest}  {target.name}\n{sha256(capture_root / 'metadata.json')}  metadata.json\n", encoding="utf-8")

    register_json = root / "manual-captures" / "capture-register.json"
    register = read_json(register_json, [])
    if not isinstance(register, list):
        register = []
    register.append({**metadata, "path": capture_root.relative_to(root).as_posix()})
    register.sort(key=lambda row: (row.get("capturedAtUtc", ""), row.get("captureId", "")))
    register_json.write_text(json.dumps(register, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    with (root / "manual-captures" / "capture-register.csv").open("w", encoding="utf-8", newline="") as handle:
        fields = ["captureId", "title", "purpose", "chapterTarget", "baselineId", "runId", "capturedAtUtc", "sourcePage", "scenario", "simulationRunId", "cycleStart", "cycleEnd", "filters", "redactions", "interpretation", "limitations", "registeredFilename", "sha256", "sizeBytes", "path"]
        writer = csv.DictWriter(handle, fieldnames=fields, extrasaction="ignore")
        writer.writeheader(); writer.writerows(register)
    print(f"EVIDENCE_CAPTURE_ID={capture_id}")
    print(f"EVIDENCE_CAPTURE_ROOT={capture_root}")
    print(f"EVIDENCE_CAPTURE_SHA256={digest}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
