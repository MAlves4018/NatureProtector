#!/usr/bin/env python3
"""Verify a NatureProtector Phase 2 test and coverage evidence directory."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
from pathlib import Path
from typing import Any


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def require(condition: bool, message: str, failures: list[str]) -> None:
    if not condition:
        failures.append(message)


def csv_count(path: Path) -> int:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        return sum(1 for _ in csv.DictReader(stream))


def as_int(value: Any) -> int:
    try:
        return int(value or 0)
    except (ValueError, TypeError):
        return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence-root", type=Path, required=True)
    args = parser.parse_args()
    root = args.evidence_root.resolve()
    failures: list[str] = []

    summary_path = root / "phase2-summary.json"
    commands_json_path = root / "command-results.json"
    commands_csv_path = root / "command-results.csv"
    environment_path = root / "environment.json"
    sums_path = root / "SHA256SUMS.txt"
    report_metrics_csv = root / "report-ready-metrics.csv"
    report_metrics_md = root / "report-ready-metrics.md"
    frontend_coverage_csv = root / "frontend" / "coverage-summary.csv"
    for path in (
        summary_path,
        commands_json_path,
        commands_csv_path,
        environment_path,
        sums_path,
        report_metrics_csv,
        report_metrics_md,
        frontend_coverage_csv,
    ):
        require(path.is_file(), f"Missing required file: {path.name}", failures)
    if failures:
        for failure in failures:
            print(f"ERROR={failure}")
        print("PHASE_2_VERIFICATION=FAIL")
        return 1

    summary = json.loads(summary_path.read_text(encoding="utf-8-sig"))
    commands = json.loads(commands_json_path.read_text(encoding="utf-8-sig"))
    environment = json.loads(environment_path.read_text(encoding="utf-8-sig"))

    require(
        summary.get("evidence_class") == "CURRENT_TEST_AND_COVERAGE_EXECUTION", "Unexpected evidence class", failures
    )
    require(bool(summary.get("baseline_id")), "Missing baseline ID", failures)
    require(bool(summary.get("run_id")), "Missing run ID", failures)
    require(
        summary.get("overall_status") in {"PASS", "FAIL", "PARTIAL", "BLOCKED", "PARTIAL_PASS_BLOCKED_ENVIRONMENT"},
        "Unexpected overall status",
        failures,
    )
    require(
        environment.get("collector_version") == summary.get("collector_version"), "Collector version mismatch", failures
    )
    require(len(commands) == csv_count(commands_csv_path), "Command JSON/CSV row count mismatch", failures)
    require(csv_count(report_metrics_csv) >= 10, "Report-ready metrics table is unexpectedly small", failures)
    require(csv_count(frontend_coverage_csv) == 4, "Frontend coverage summary must contain four metric rows", failures)

    command_ids = [item.get("id") for item in commands]
    require(len(command_ids) == len(set(command_ids)), "Duplicate command IDs", failures)
    required_command_ids = {
        "backend_tool_restore",
        "backend_restore",
        "backend_build",
        "backend_test_coverage",
        "backend_coverage_report",
        "frontend_npm_ci",
        "frontend_toolchain",
        "frontend_typecheck",
        "frontend_lint",
        "frontend_format",
        "frontend_test_coverage",
        "frontend_build",
        "frontend_e2e",
    }
    require(
        required_command_ids.issubset(set(command_ids)), "One or more required command records are absent", failures
    )

    for command in commands:
        require(
            command.get("status") in {"PASS", "FAIL", "BLOCKED", "SKIPPED"},
            f"Invalid command status for {command.get('id')}",
            failures,
        )
        if command.get("status") in {"PASS", "FAIL"}:
            require(
                command.get("exit_code") is not None, f"Executed command lacks exit code: {command.get('id')}", failures
            )
            require(bool(command.get("log_file")), f"Executed command lacks log file: {command.get('id')}", failures)
            if command.get("log_file"):
                require(
                    (root / command["log_file"]).is_file(), f"Missing command log: {command.get('log_file')}", failures
                )
        else:
            require(
                command.get("exit_code") is None, f"Non-executed command has exit code: {command.get('id')}", failures
            )
            require(bool(command.get("reason")), f"Non-executed command lacks reason: {command.get('id')}", failures)

    backend = summary.get("backend", {})
    frontend = summary.get("frontend", {})
    backend_tests = backend.get("tests", {})
    frontend_tests = frontend.get("tests", {})
    require(
        as_int(backend_tests.get("test_result_count"))
        == as_int(backend_tests.get("passed"))
        + as_int(backend_tests.get("failed"))
        + as_int(backend_tests.get("skipped_or_not_executed"))
        + sum(as_int(value) for value in backend_tests.get("other_outcomes", {}).values()),
        "Backend test result reconciliation failed",
        failures,
    )
    require(
        as_int(frontend_tests.get("test_count"))
        == as_int(frontend_tests.get("passed"))
        + as_int(frontend_tests.get("failed"))
        + as_int(frontend_tests.get("errors"))
        + as_int(frontend_tests.get("skipped")),
        "Frontend test result reconciliation failed",
        failures,
    )

    backend_csv = root / "backend" / "test-results.csv"
    frontend_csv = root / "frontend" / "test-results.csv"
    require(backend_csv.is_file(), "Missing backend test result CSV", failures)
    require(frontend_csv.is_file(), "Missing frontend test result CSV", failures)
    if backend_csv.is_file():
        require(
            csv_count(backend_csv) == as_int(backend_tests.get("test_result_count")),
            "Backend CSV row count mismatch",
            failures,
        )
    if frontend_csv.is_file():
        require(
            csv_count(frontend_csv) == as_int(frontend_tests.get("test_count")),
            "Frontend CSV row count mismatch",
            failures,
        )

    if backend.get("status") == "PASS":
        require(
            as_int(backend_tests.get("test_result_count")) > 0, "Backend PASS without parsed test results", failures
        )
        require(as_int(backend_tests.get("failed")) == 0, "Backend PASS with failed tests", failures)
    if frontend.get("status") == "PASS":
        require(as_int(frontend_tests.get("test_count")) > 0, "Frontend PASS without parsed test results", failures)
        require(
            as_int(frontend_tests.get("failed")) + as_int(frontend_tests.get("errors")) == 0,
            "Frontend PASS with failed tests",
            failures,
        )

    hashed_lines = [line for line in sums_path.read_text(encoding="utf-8").splitlines() if line.strip()]
    for line_number, line in enumerate(hashed_lines, start=1):
        try:
            expected, filename = line.split("  ", 1)
        except ValueError:
            failures.append(f"Malformed SHA256SUMS line {line_number}")
            continue
        path = root / filename
        require(path.is_file(), f"Hashed file missing: {filename}", failures)
        if path.is_file():
            require(sha256_file(path) == expected, f"SHA-256 mismatch: {filename}", failures)

    actual_hashed = [path for path in root.rglob("*") if path.is_file() and path.name != "SHA256SUMS.txt"]
    require(len(hashed_lines) == len(actual_hashed), "SHA256SUMS does not cover every evidence file", failures)

    if failures:
        for failure in failures:
            print(f"ERROR={failure}")
        print("PHASE_2_VERIFICATION=FAIL")
        return 1

    print("PHASE_2_VERIFICATION=PASS")
    print(f"BASELINE_ID={summary.get('baseline_id')}")
    print(f"RUN_ID={summary.get('run_id')}")
    print(f"PHASE_2_STATUS={summary.get('overall_status')}")
    print(f"BACKEND_STATUS={backend.get('status')}")
    print(f"FRONTEND_STATUS={frontend.get('status')}")
    print(f"HASHED_FILE_COUNT={len(hashed_lines)}")
    print(f"EVIDENCE_ROOT={root}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
