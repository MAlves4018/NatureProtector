#!/usr/bin/env python3
"""Verify NatureProtector Phase 5 performance evidence."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any, Sequence


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def fail(message: str) -> None:
    raise RuntimeError(message)


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence_root", type=Path)
    parser.add_argument("--require-http", action="store_true")
    parser.add_argument("--require-microbenchmarks", action="store_true")
    parser.add_argument("--require-system", action="store_true")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    root = args.evidence_root.resolve()
    if not root.is_dir():
        fail(f"Evidence root does not exist: {root}")
    manifest = root / "SHA256SUMS.txt"
    if not manifest.exists():
        fail("SHA256SUMS.txt is missing")
    entries: list[tuple[str, str]] = []
    for line in manifest.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        expected, relative = line.split("  ", 1)
        entries.append((expected, relative))
    if not entries:
        fail("SHA256SUMS.txt is empty")
    for expected, relative in entries:
        path = root / relative
        if not path.is_file():
            fail(f"Hashed file is missing: {relative}")
        actual = sha256(path)
        if actual != expected:
            fail(f"SHA-256 mismatch for {relative}: expected {expected}, got {actual}")

    summary = read_json(root / "phase5-summary.json")
    if summary.get("phase") != 5:
        fail("phase5-summary.json does not identify Phase 5")
    if summary.get("staticPerformanceContractStatus") != "PASS":
        fail("Static performance contract did not pass")
    counts = summary.get("staticCounts") or {}
    expected_counts = {
        "benchmarkCases": 12,
        "benchmarkMethods": 4,
        "benchmarkBatchSizes": 3,
        "benchmarkProfiles": 3,
        "systemProfiles": 4,
        "readinessProbes": 11,
        "httpProfiles": 4,
        "findings": 6,
    }
    for name, expected in expected_counts.items():
        actual = int(counts.get(name, -1))
        if actual != expected:
            fail(f"Unexpected {name}: expected {expected}, got {actual}")
    if int(counts.get("performanceTelemetryInstruments", 0)) < 20:
        fail("Performance telemetry inventory is unexpectedly small")

    benchmark_rows = list(csv.DictReader((root / "static/microbenchmark-catalog.csv").open(encoding="utf-8-sig")))
    batch_sizes = {int(row["batchSize"]) for row in benchmark_rows}
    if batch_sizes != {32, 512, 4096}:
        fail(f"Unexpected benchmark batch sizes: {sorted(batch_sizes)}")
    benchmark_methods = {(row["benchmarkClass"], row["method"]) for row in benchmark_rows}
    if len(benchmark_methods) != 4:
        fail("Expected four benchmark methods")

    system_rows = list(csv.DictReader((root / "static/system-workload-profiles.csv").open(encoding="utf-8-sig")))
    by_profile = {row["profile"]: row for row in system_rows}
    if set(by_profile) != {"Calibration", "B0", "B1", "B2"}:
        fail(f"Unexpected system profiles: {sorted(by_profile)}")
    if int(by_profile["B1"]["expectedEventsPerRun"]) != 30 or int(by_profile["B1"]["expectedEventsCampaign"]) != 60:
        fail("B1 system profile does not match the repository contract")
    if int(by_profile["B2"]["expectedEventsPerRun"]) != 60 or int(by_profile["B2"]["expectedEventsCampaign"]) != 60:
        fail("B2 system profile does not match the repository contract")

    execution_statuses = {
        "http": read_json(root / "execution/http-status.json").get("status"),
        "microbenchmarks": read_json(root / "execution/microbenchmarks-status.json").get("status"),
        "system": read_json(root / "execution/system-status.json").get("status"),
    }
    result_counts = summary.get("currentResultCounts") or {}
    if execution_statuses["http"] != "PASS" and int(result_counts.get("httpRows", 0)) != 0:
        fail("HTTP result rows exist without a passing current execution")
    if execution_statuses["microbenchmarks"] != "PASS" and int(result_counts.get("microbenchmarkRows", 0)) != 0:
        fail("Microbenchmark result rows exist without a passing current execution")
    if execution_statuses["system"] != "PASS" and int(result_counts.get("systemRows", 0)) != 0:
        fail("System result rows exist without a passing current execution")
    if args.require_http and execution_statuses["http"] != "PASS":
        fail(f"Current HTTP evidence is required but status is {execution_statuses['http']}")
    if args.require_microbenchmarks and execution_statuses["microbenchmarks"] != "PASS":
        fail(f"Current microbenchmark evidence is required but status is {execution_statuses['microbenchmarks']}")
    if args.require_system and execution_statuses["system"] != "PASS":
        fail(f"Current system evidence is required but status is {execution_statuses['system']}")

    claims = "\n".join(str(item) for item in summary.get("claimBoundaries", []))
    for required in ("Microbenchmarks do not prove distributed throughput", "PublishedAt is not persisted"):
        if required not in claims:
            fail(f"Required claim boundary is missing: {required}")

    secret_patterns = [
        re.compile(r"\beyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b"),
        re.compile(r"(?i)(password|passwd|pwd)\s*[=:]\s*[^\s\"']{4,}"),
        re.compile(r"(?i)authorization\s*:\s*bearer\s+[A-Za-z0-9._-]{10,}"),
    ]
    scanned = 0
    for path in root.rglob("*"):
        if not path.is_file() or path.name == "SHA256SUMS.txt" or path.stat().st_size > 5 * 1024 * 1024:
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        scanned += 1
        for pattern in secret_patterns:
            if pattern.search(text):
                fail(f"Potential secret material found in {path.relative_to(root)}")

    print("PHASE_5_VERIFICATION=PASS")
    print(f"VERIFIED_HASHED_FILES={len(entries)}")
    print(f"VERIFIED_BENCHMARK_CASES={len(benchmark_rows)}")
    print(f"VERIFIED_BENCHMARK_METHODS={len(benchmark_methods)}")
    print(f"VERIFIED_SYSTEM_PROFILES={len(system_rows)}")
    print(f"VERIFIED_READINESS_PROBES={counts.get('readinessProbes')}")
    print(f"VERIFIED_PERFORMANCE_METRICS={counts.get('performanceTelemetryInstruments')}")
    print(f"CURRENT_HTTP_STATUS={execution_statuses['http']}")
    print(f"CURRENT_MICROBENCHMARK_STATUS={execution_statuses['microbenchmarks']}")
    print(f"CURRENT_SYSTEM_STATUS={execution_statuses['system']}")
    print(f"SECRET_SCAN_FILES={scanned}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"PHASE_5_VERIFICATION=FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
