#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import xml.etree.ElementTree as ET
from pathlib import Path


def parse_junit_xml(path: Path) -> dict | None:
    try:
        tree = ET.parse(path)
        root = tree.getroot()
    except (ET.ParseError, FileNotFoundError):
        return None

    total_tests = 0
    total_failures = 0
    total_errors = 0
    total_skipped = 0

    if root.tag == "testsuites":
        for suite in root.iter("testsuite"):
            total_tests += int(suite.get("tests", 0))
            total_failures += int(suite.get("failures", 0))
            total_errors += int(suite.get("errors", 0))
            total_skipped += int(suite.get("skipped", 0))
    elif root.tag == "testsuite":
        total_tests = int(root.get("tests", 0))
        total_failures = int(root.get("failures", 0))
        total_errors = int(root.get("errors", 0))
        total_skipped = int(root.get("skipped", 0))
    else:
        return None

    return {
        "tests": total_tests,
        "passed": total_tests - total_failures - total_errors,
        "failed": total_failures + total_errors,
        "skipped": total_skipped,
    }


def parse_trx_xml(path: Path) -> dict | None:
    ns = {"": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    try:
        tree = ET.parse(path)
        root = tree.getroot()
    except (ET.ParseError, FileNotFoundError):
        return None

    results = root.findall(".//UnitTestResult", ns)
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


def consolidate(root_dir: Path) -> dict[str, dict]:
    results: dict[str, dict] = {}

    for artifact_dir in sorted(root_dir.iterdir()):
        if not artifact_dir.is_dir():
            continue
        job_name = artifact_dir.name

        for xml_file in artifact_dir.rglob("*.xml"):
            if xml_file.stat().st_size == 0:
                continue

            parsed = parse_junit_xml(xml_file) or parse_trx_xml(xml_file)
            if parsed is None:
                continue

            existing = results.get(job_name)
            if existing:
                existing["tests"] += parsed["tests"]
                existing["passed"] += parsed["passed"]
                existing["failed"] += parsed["failed"]
                existing["skipped"] += parsed["skipped"]
            else:
                results[job_name] = dict(parsed)

    return results


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--artifact-root", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    root = Path(args.artifact_root)
    if not root.is_dir():
        print(json.dumps({"error": f"Directory not found: {root}"}))
        return 1

    results = consolidate(root)
    with open(args.output, "w") as f:
        json.dump(results, f, indent=2)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
