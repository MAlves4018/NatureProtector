#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import xml.etree.ElementTree as ET
from pathlib import Path

TRX_NS = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"


def _safe_int(value: str | None, default: int = 0) -> int:
    try:
        return int(value) if value is not None else default
    except (ValueError, TypeError):
        return default


def _non_negative(value: int) -> int:
    return max(value, 0)


def parse_junit_xml(path: Path) -> dict | None:
    try:
        tree = ET.parse(path)
        root = tree.getroot()
    except (ET.ParseError, FileNotFoundError):
        return None

    if root.tag == "testsuites":
        total_tests = 0
        total_failures = 0
        total_errors = 0
        total_skipped = 0
        for suite in root.iter("testsuite"):
            total_tests += _safe_int(suite.get("tests"))
            total_failures += _safe_int(suite.get("failures"))
            total_errors += _safe_int(suite.get("errors"))
            total_skipped += _safe_int(suite.get("skipped"))
    elif root.tag == "testsuite":
        total_tests = _safe_int(root.get("tests"))
        total_failures = _safe_int(root.get("failures"))
        total_errors = _safe_int(root.get("errors"))
        total_skipped = _safe_int(root.get("skipped"))
    else:
        return None

    passed = _non_negative(total_tests - total_failures - total_errors - total_skipped)

    return {
        "tests": total_tests,
        "passed": passed,
        "failed": total_failures + total_errors,
        "skipped": total_skipped,
    }


def parse_trx_xml(path: Path) -> dict | None:
    try:
        tree = ET.parse(path)
        root = tree.getroot()
    except (ET.ParseError, FileNotFoundError):
        return None

    results = root.findall(f".//{{{TRX_NS}}}UnitTestResult")
    if not results:
        results = root.findall(".//UnitTestResult")
    if not results:
        return None

    passed = 0
    failed = 0
    skipped = 0
    for r in results:
        outcome = r.get("outcome", "")
        if outcome == "Passed":
            passed += 1
        elif outcome in ("Failed", "Error", "Timeout", "Aborted"):
            failed += 1
        elif outcome in ("Skipped", "NotExecuted", "Pending"):
            skipped += 1

    total = passed + failed + skipped
    if total == 0:
        return None

    return {
        "tests": total,
        "passed": passed,
        "failed": failed,
        "skipped": skipped,
    }


def consolidate(root_dir: Path) -> dict:
    jobs: dict[str, dict] = {}
    files_read: list[str] = []
    files_ignored: list[str] = []
    warnings: list[str] = []

    if not root_dir.is_dir():
        return {
            "schemaVersion": 1,
            "jobs": {},
            "totals": {"tests": 0, "passed": 0, "failed": 0, "skipped": 0},
            "warnings": [f"Directory not found: {root_dir}"],
            "filesRead": [],
            "filesIgnored": [],
        }

    for artifact_dir in sorted(root_dir.iterdir()):
        if not artifact_dir.is_dir():
            continue
        job_name = artifact_dir.name

        xml_files = list(artifact_dir.rglob("*.xml"))
        if not xml_files:
            warnings.append(f"Job '{job_name}' has no XML files in {artifact_dir}")
            continue

        for xml_file in xml_files:
            if xml_file.stat().st_size == 0:
                files_ignored.append(str(xml_file.relative_to(root_dir)))
                continue

            parsed = parse_junit_xml(xml_file)
            if parsed is None:
                parsed = parse_trx_xml(xml_file)

            if parsed is None:
                files_ignored.append(str(xml_file.relative_to(root_dir)))
                continue

            files_read.append(str(xml_file.relative_to(root_dir)))
            existing = jobs.get(job_name)
            if existing:
                existing["tests"] += parsed["tests"]
                existing["passed"] += parsed["passed"]
                existing["failed"] += parsed["failed"]
                existing["skipped"] += parsed["skipped"]
            else:
                jobs[job_name] = dict(parsed)

    total_tests = sum(j["tests"] for j in jobs.values())
    total_passed = sum(j["passed"] for j in jobs.values())
    total_failed = sum(j["failed"] for j in jobs.values())
    total_skipped = sum(j["skipped"] for j in jobs.values())

    return {
        "schemaVersion": 1,
        "jobs": jobs,
        "totals": {
            "tests": total_tests,
            "passed": total_passed,
            "failed": total_failed,
            "skipped": total_skipped,
        },
        "warnings": warnings,
        "filesRead": sorted(files_read),
        "filesIgnored": sorted(files_ignored),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--artifact-root", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    root = Path(args.artifact_root)
    result = consolidate(root)

    with open(args.output, "w", encoding="utf-8", newline="\n") as f:
        json.dump(result, f, indent=2, ensure_ascii=False)
        f.write("\n")

    return 0 if result.get("jobs") else 1


if __name__ == "__main__":
    raise SystemExit(main())
