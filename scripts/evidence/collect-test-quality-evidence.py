#!/usr/bin/env python3
"""Execute and preserve current NatureProtector test and coverage evidence.

This Phase 2 collector is intentionally repository-local and auditable. It:
- records toolchain availability and exact commands;
- runs backend tests with TRX and Cobertura coverage when a compatible .NET SDK exists;
- runs frontend toolchain, typecheck, lint, format, Vitest coverage and production build;
- preserves command logs, machine-readable summaries and SHA-256 hashes;
- classifies unavailable toolchains as BLOCKED rather than fabricating results.

It does not execute Git, mutate .env/.env.example, start Docker, access cloud
resources, or claim that static declarations are executed tests.
"""

from __future__ import annotations

import argparse
import atexit
import csv
import hashlib
import json
import os
import platform
import re
import shutil
import subprocess
import sys
import time
import tempfile
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable, Sequence

SCRIPT_VERSION = "1.1.2"
EVIDENCE_CLASS = "CURRENT_TEST_AND_COVERAGE_EXECUTION"




def prepare_isolated_frontend_workspace(source: Path) -> tuple[Path, Path]:
    """Copy repository-owned frontend sources to a temporary clean workspace.

    The running Vite process can lock native files under repository node_modules on
    Windows. Evidence collection must not stop the live runtime or reuse stale test
    outputs, so Phase 2 performs npm ci and all frontend checks in an isolated copy.
    """
    temp_root = Path(tempfile.mkdtemp(prefix="np-phase2-frontend-"))
    target = temp_root / "webUI"
    ignored = shutil.ignore_patterns(
        "node_modules",
        "dist",
        "coverage",
        "test-results",
        "playwright-report",
        "blob-report",
        ".vite",
        ".cache",
    )
    shutil.copytree(source, target, ignore=ignored)

    # The frontend toolchain contract also validates repository-level workflow
    # manifests. Preserve that read-only context beside the isolated webUI copy
    # without copying the rest of the repository or any runtime outputs.
    workflows_source = source.resolve().parent / ".github" / "workflows"
    workflows_target = temp_root / ".github" / "workflows"
    if workflows_source.is_dir():
        shutil.copytree(workflows_source, workflows_target)

    # Biome resolves vcs.root=".." from webUI/biome*.jsonc and therefore
    # expects the repository ignore file beside webUI in the isolated root.
    ignore_source = source.resolve().parent / ".gitignore"
    if ignore_source.is_file():
        shutil.copy2(ignore_source, temp_root / ".gitignore")

    atexit.register(lambda: shutil.rmtree(temp_root, ignore_errors=True))
    return target, temp_root

def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def default_run_id() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def safe_console_write(value: str) -> None:
    """Write subprocess output without allowing a Windows console codec to abort evidence collection."""
    if not value:
        return
    try:
        sys.stdout.write(value)
    except UnicodeEncodeError:
        encoding = getattr(sys.stdout, "encoding", None) or "utf-8"
        escaped = value.encode(encoding, errors="backslashreplace").decode(encoding, errors="strict")
        sys.stdout.write(escaped)
    sys.stdout.flush()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_csv(path: Path, rows: Sequence[dict[str, Any]], fieldnames: Sequence[str] | None = None) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if fieldnames is None:
        fieldnames = list(rows[0].keys()) if rows else []
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow({key: normalize_csv(row.get(key)) for key in fieldnames})


def normalize_csv(value: Any) -> Any:
    if value is None:
        return ""
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (list, tuple, set)):
        return "; ".join(str(item) for item in value)
    if isinstance(value, dict):
        return json.dumps(value, ensure_ascii=False, sort_keys=True)
    return value


def safe_rel(path: Path, root: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve())


def command_text(command: Sequence[str]) -> str:
    def quote(item: str) -> str:
        if not item or re.search(r"\s|[;&|<>]", item):
            return '"' + item.replace('"', '\\"') + '"'
        return item

    return " ".join(quote(str(item)) for item in command)


def parse_duration(value: str | None) -> float:
    if not value:
        return 0.0
    value = value.strip()
    try:
        if re.fullmatch(r"\d+(?:\.\d+)?", value):
            return float(value)
        days = 0
        if "." in value and value.split(".", 1)[0].isdigit() and ":" in value.split(".", 1)[1]:
            day_text, value = value.split(".", 1)
            days = int(day_text)
        parts = value.split(":")
        if len(parts) == 3:
            hours, minutes, seconds = parts
            return days * 86400 + int(hours) * 3600 + int(minutes) * 60 + float(seconds)
    except (ValueError, TypeError):
        pass
    return 0.0


@dataclass
class CommandResult:
    id: str
    component: str
    purpose: str
    status: str
    command: str
    cwd: str
    started_at_utc: str | None
    finished_at_utc: str | None
    duration_seconds: float
    exit_code: int | None
    log_file: str | None
    reason: str | None = None


class EvidenceRunner:
    def __init__(self, repo: Path, output: Path, timeout_seconds: int, echo: bool = True) -> None:
        self.repo = repo
        self.output = output
        self.logs = output / "logs"
        self.logs.mkdir(parents=True, exist_ok=True)
        self.timeout_seconds = timeout_seconds
        self.echo = echo
        self.results: list[CommandResult] = []

    def record_nonexecution(
        self,
        command_id: str,
        component: str,
        purpose: str,
        status: str,
        reason: str,
        command: Sequence[str] | None = None,
        cwd: Path | None = None,
    ) -> CommandResult:
        result = CommandResult(
            id=command_id,
            component=component,
            purpose=purpose,
            status=status,
            command=command_text(command or []),
            cwd=safe_rel(cwd or self.repo, self.repo),
            started_at_utc=None,
            finished_at_utc=None,
            duration_seconds=0.0,
            exit_code=None,
            log_file=None,
            reason=reason,
        )
        self.results.append(result)
        print(f"{command_id}: {status} — {reason}")
        return result

    def run(
        self,
        command_id: str,
        component: str,
        purpose: str,
        command: Sequence[str],
        cwd: Path,
        env: dict[str, str] | None = None,
        dependency_ok: bool = True,
        dependency_reason: str | None = None,
    ) -> CommandResult:
        if not dependency_ok:
            return self.record_nonexecution(
                command_id,
                component,
                purpose,
                "BLOCKED",
                dependency_reason or "A prerequisite command failed.",
                command,
                cwd,
            )

        log_path = self.logs / f"{command_id}.log"
        started_text = utc_now()
        started = time.monotonic()
        print(f"\n[{command_id}] {command_text(command)}")
        exit_code: int | None = None
        status = "FAIL"
        reason: str | None = None
        merged_env = os.environ.copy()
        if env:
            merged_env.update(env)

        output_text = ""
        try:
            completed = subprocess.run(
                [str(item) for item in command],
                cwd=str(cwd),
                env=merged_env,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=self.timeout_seconds,
                check=False,
            )
            exit_code = completed.returncode
            output_text = completed.stdout or ""
        except subprocess.TimeoutExpired as exc:
            exit_code = 124
            reason = f"Command exceeded timeout of {self.timeout_seconds} seconds."
            captured = exc.stdout or ""
            if isinstance(captured, bytes):
                captured = captured.decode("utf-8", errors="replace")
            output_text = str(captured) + f"\nTIMEOUT={self.timeout_seconds}\n"
        except FileNotFoundError as exc:
            exit_code = 127
            reason = f"Executable not found: {exc.filename}"
            output_text = reason + "\n"
        except OSError as exc:
            exit_code = 126
            reason = f"Command execution failed: {exc}"
            output_text = reason + "\n"

        with log_path.open("w", encoding="utf-8", errors="replace") as log:
            log.write(f"COMMAND_ID={command_id}\n")
            log.write(f"STARTED_AT_UTC={started_text}\n")
            log.write(f"CWD={cwd.resolve()}\n")
            log.write(f"COMMAND={command_text(command)}\n\n")
            log.write(output_text)
        if self.echo and output_text:
            safe_console_write(output_text if output_text.endswith("\n") else output_text + "\n")

        duration = round(time.monotonic() - started, 3)
        finished_text = utc_now()
        if exit_code == 0:
            status = "PASS"
        elif exit_code == 124:
            status = "FAIL"
        else:
            status = "FAIL"
            reason = reason or f"Command exited with code {exit_code}."

        with log_path.open("a", encoding="utf-8", errors="replace") as log:
            log.write(f"\nFINISHED_AT_UTC={finished_text}\n")
            log.write(f"DURATION_SECONDS={duration}\n")
            log.write(f"EXIT_CODE={exit_code}\n")
            log.write(f"STATUS={status}\n")
            if reason:
                log.write(f"REASON={reason}\n")

        result = CommandResult(
            id=command_id,
            component=component,
            purpose=purpose,
            status=status,
            command=command_text(command),
            cwd=safe_rel(cwd, self.repo),
            started_at_utc=started_text,
            finished_at_utc=finished_text,
            duration_seconds=duration,
            exit_code=exit_code,
            log_file=safe_rel(log_path, self.output),
            reason=reason,
        )
        self.results.append(result)
        print(f"[{command_id}] STATUS={status} EXIT_CODE={exit_code} DURATION_SECONDS={duration}")
        return result


def tool_path(name: str) -> str | None:
    return shutil.which(name)


def capture_version(command: Sequence[str], cwd: Path, timeout: int = 30) -> dict[str, Any]:
    try:
        completed = subprocess.run(
            [str(item) for item in command],
            cwd=str(cwd),
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout,
            check=False,
        )
        return {
            "available": True,
            "command": command_text(command),
            "exit_code": completed.returncode,
            "output": completed.stdout.strip(),
        }
    except (FileNotFoundError, subprocess.TimeoutExpired, OSError) as exc:
        return {
            "available": False,
            "command": command_text(command),
            "exit_code": None,
            "output": str(exc),
        }


def parse_trx_files(paths: Iterable[Path], output: Path) -> dict[str, Any]:
    detail_rows: list[dict[str, Any]] = []
    run_rows: list[dict[str, Any]] = []
    seen_results: set[tuple[str, str, str]] = set()
    outcome_counts: Counter[str] = Counter()

    for path in sorted(paths):
        try:
            root = ET.parse(path).getroot()
        except (ET.ParseError, OSError) as exc:
            run_rows.append(
                {
                    "file": safe_rel(path, output),
                    "parse_status": "FAIL",
                    "error": str(exc),
                    "total": 0,
                    "passed": 0,
                    "failed": 0,
                    "skipped_or_not_executed": 0,
                    "duration_seconds": 0.0,
                }
            )
            continue

        unit_by_id: dict[str, dict[str, str]] = {}
        for unit in root.findall(".//{*}UnitTest"):
            test_id = unit.attrib.get("id", "")
            method = unit.find(".//{*}TestMethod")
            unit_by_id[test_id] = {
                "storage": unit.attrib.get("storage", ""),
                "class_name": method.attrib.get("className", "") if method is not None else "",
                "method_name": method.attrib.get("name", "") if method is not None else "",
            }

        file_counts: Counter[str] = Counter()
        file_duration = 0.0
        file_total = 0
        for result in root.findall(".//{*}UnitTestResult"):
            key = (
                path.as_posix(),
                result.attrib.get("testId", ""),
                result.attrib.get("executionId", result.attrib.get("testName", "")),
            )
            if key in seen_results:
                continue
            seen_results.add(key)
            outcome = result.attrib.get("outcome", "Unknown")
            duration = parse_duration(result.attrib.get("duration"))
            meta = unit_by_id.get(result.attrib.get("testId", ""), {})
            detail_rows.append(
                {
                    "trx_file": safe_rel(path, output),
                    "test_name": result.attrib.get("testName", ""),
                    "class_name": meta.get("class_name", ""),
                    "method_name": meta.get("method_name", ""),
                    "test_assembly": Path(meta.get("storage", "")).name,
                    "outcome": outcome,
                    "duration_seconds": round(duration, 6),
                    "start_time": result.attrib.get("startTime", ""),
                    "end_time": result.attrib.get("endTime", ""),
                    "computer_name": result.attrib.get("computerName", ""),
                }
            )
            file_counts[outcome] += 1
            outcome_counts[outcome] += 1
            file_total += 1
            file_duration += duration

        run_rows.append(
            {
                "file": safe_rel(path, output),
                "parse_status": "PASS",
                "error": "",
                "total": file_total,
                "passed": file_counts.get("Passed", 0),
                "failed": file_counts.get("Failed", 0),
                "skipped_or_not_executed": sum(
                    file_counts.get(key, 0) for key in ("NotExecuted", "Skipped", "Inconclusive", "NotRunnable")
                ),
                "duration_seconds": round(file_duration, 6),
            }
        )

    write_csv(
        output / "backend" / "test-results.csv",
        detail_rows,
        [
            "trx_file",
            "test_name",
            "class_name",
            "method_name",
            "test_assembly",
            "outcome",
            "duration_seconds",
            "start_time",
            "end_time",
            "computer_name",
        ],
    )
    write_csv(
        output / "backend" / "trx-runs.csv",
        run_rows,
        [
            "file",
            "parse_status",
            "error",
            "total",
            "passed",
            "failed",
            "skipped_or_not_executed",
            "duration_seconds",
        ],
    )

    total = sum(outcome_counts.values())
    passed = outcome_counts.get("Passed", 0)
    failed = outcome_counts.get("Failed", 0)
    skipped = sum(outcome_counts.get(key, 0) for key in ("NotExecuted", "Skipped", "Inconclusive", "NotRunnable"))
    return {
        "trx_file_count": len(run_rows),
        "test_result_count": total,
        "passed": passed,
        "failed": failed,
        "skipped_or_not_executed": skipped,
        "other_outcomes": {
            key: value
            for key, value in sorted(outcome_counts.items())
            if key not in {"Passed", "Failed", "NotExecuted", "Skipped", "Inconclusive", "NotRunnable"}
        },
        "summed_test_duration_seconds": round(sum(row["duration_seconds"] for row in detail_rows), 6),
        "parse_failures": sum(1 for row in run_rows if row["parse_status"] != "PASS"),
    }


def merge_cobertura(paths: Iterable[Path], output: Path) -> dict[str, Any]:
    line_hits: dict[tuple[str, str, int], int] = {}
    branch_counts: dict[tuple[str, str, int], tuple[int, int]] = {}
    report_hashes: set[str] = set()
    parsed_files = 0
    parse_failures: list[dict[str, str]] = []

    for path in sorted(paths):
        digest = sha256_file(path)
        if digest in report_hashes:
            continue
        report_hashes.add(digest)
        try:
            root = ET.parse(path).getroot()
        except (ET.ParseError, OSError) as exc:
            parse_failures.append({"file": safe_rel(path, output), "error": str(exc)})
            continue
        parsed_files += 1
        for package in root.findall(".//{*}package"):
            assembly = package.attrib.get("name", "unknown")
            for cls in package.findall(".//{*}class"):
                filename = cls.attrib.get("filename", cls.attrib.get("name", "unknown"))
                for line in cls.findall(".//{*}line"):
                    try:
                        number = int(line.attrib.get("number", "0"))
                        hits = int(float(line.attrib.get("hits", "0")))
                    except ValueError:
                        continue
                    key = (assembly, filename, number)
                    line_hits[key] = max(line_hits.get(key, 0), hits)
                    condition = line.attrib.get("condition-coverage", "")
                    match = re.search(r"\((\d+)\s*/\s*(\d+)\)", condition)
                    if match:
                        covered, total = int(match.group(1)), int(match.group(2))
                        prev_covered, prev_total = branch_counts.get(key, (0, 0))
                        branch_counts[key] = (max(prev_covered, covered), max(prev_total, total))

    assembly_totals: dict[str, dict[str, int]] = defaultdict(
        lambda: {
            "lines_valid": 0,
            "lines_covered": 0,
            "branches_valid": 0,
            "branches_covered": 0,
        }
    )
    for (assembly, _filename, _number), hits in line_hits.items():
        assembly_totals[assembly]["lines_valid"] += 1
        if hits > 0:
            assembly_totals[assembly]["lines_covered"] += 1
    for (assembly, _filename, _number), (covered, total) in branch_counts.items():
        assembly_totals[assembly]["branches_valid"] += total
        assembly_totals[assembly]["branches_covered"] += min(covered, total)

    assembly_rows: list[dict[str, Any]] = []
    for assembly, totals in sorted(assembly_totals.items()):
        lines_valid = totals["lines_valid"]
        branches_valid = totals["branches_valid"]
        assembly_rows.append(
            {
                "assembly": assembly,
                **totals,
                "line_coverage_percent": round(100.0 * totals["lines_covered"] / lines_valid, 2)
                if lines_valid
                else None,
                "branch_coverage_percent": round(100.0 * totals["branches_covered"] / branches_valid, 2)
                if branches_valid
                else None,
            }
        )

    write_csv(
        output / "backend" / "coverage-by-assembly.csv",
        assembly_rows,
        [
            "assembly",
            "lines_valid",
            "lines_covered",
            "line_coverage_percent",
            "branches_valid",
            "branches_covered",
            "branch_coverage_percent",
        ],
    )
    write_json(output / "backend" / "coverage-parse-failures.json", parse_failures)

    total_lines_valid = sum(row["lines_valid"] for row in assembly_rows)
    total_lines_covered = sum(row["lines_covered"] for row in assembly_rows)
    total_branches_valid = sum(row["branches_valid"] for row in assembly_rows)
    total_branches_covered = sum(row["branches_covered"] for row in assembly_rows)
    return {
        "input_file_count": len(list(paths)) if not isinstance(paths, list) else len(paths),
        "unique_report_count": len(report_hashes),
        "parsed_report_count": parsed_files,
        "parse_failure_count": len(parse_failures),
        "lines_valid": total_lines_valid,
        "lines_covered": total_lines_covered,
        "line_coverage_percent": round(100.0 * total_lines_covered / total_lines_valid, 2)
        if total_lines_valid
        else None,
        "branches_valid": total_branches_valid,
        "branches_covered": total_branches_covered,
        "branch_coverage_percent": round(100.0 * total_branches_covered / total_branches_valid, 2)
        if total_branches_valid
        else None,
        "assembly_count": len(assembly_rows),
        "merge_rule": "Unique source line; maximum observed hits across unique Cobertura files. Branch numerator/denominator use the maximum observed values per source line.",
    }


def parse_junit(path: Path, output: Path) -> dict[str, Any]:
    rows: list[dict[str, Any]] = []
    if not path.is_file():
        write_csv(
            output / "frontend" / "test-results.csv",
            rows,
            [
                "suite",
                "class_name",
                "test_name",
                "outcome",
                "duration_seconds",
                "failure_message",
            ],
        )
        return {
            "junit_file_present": False,
            "test_count": 0,
            "passed": 0,
            "failed": 0,
            "errors": 0,
            "skipped": 0,
            "duration_seconds": 0.0,
            "parse_status": "NOT_AVAILABLE",
        }
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError as exc:
        return {
            "junit_file_present": True,
            "test_count": 0,
            "passed": 0,
            "failed": 0,
            "errors": 0,
            "skipped": 0,
            "duration_seconds": 0.0,
            "parse_status": "FAIL",
            "error": str(exc),
        }

    failed = errors = skipped = passed = 0
    duration = 0.0
    for suite in root.findall(".//testsuite") + ([root] if root.tag.endswith("testsuite") else []):
        suite_name = suite.attrib.get("name", "")
        for case in suite.findall("./testcase"):
            case_duration = parse_duration(case.attrib.get("time"))
            duration += case_duration
            failure = case.find("failure")
            error = case.find("error")
            skip = case.find("skipped")
            if failure is not None:
                outcome = "Failed"
                failed += 1
                message = failure.attrib.get("message", "") or (failure.text or "").strip()[:500]
            elif error is not None:
                outcome = "Error"
                errors += 1
                message = error.attrib.get("message", "") or (error.text or "").strip()[:500]
            elif skip is not None:
                outcome = "Skipped"
                skipped += 1
                message = skip.attrib.get("message", "") or (skip.text or "").strip()[:500]
            else:
                outcome = "Passed"
                passed += 1
                message = ""
            rows.append(
                {
                    "suite": suite_name,
                    "class_name": case.attrib.get("classname", ""),
                    "test_name": case.attrib.get("name", ""),
                    "outcome": outcome,
                    "duration_seconds": round(case_duration, 6),
                    "failure_message": message,
                }
            )

    # De-duplicate when the root suite is also returned by .//testsuite on some parsers.
    deduped: list[dict[str, Any]] = []
    seen: set[tuple[str, str, str, str]] = set()
    for row in rows:
        key = (row["suite"], row["class_name"], row["test_name"], row["outcome"])
        if key not in seen:
            seen.add(key)
            deduped.append(row)
    rows = deduped
    counts = Counter(row["outcome"] for row in rows)
    duration = sum(float(row["duration_seconds"]) for row in rows)
    write_csv(
        output / "frontend" / "test-results.csv",
        rows,
        [
            "suite",
            "class_name",
            "test_name",
            "outcome",
            "duration_seconds",
            "failure_message",
        ],
    )
    return {
        "junit_file_present": True,
        "test_count": len(rows),
        "passed": counts.get("Passed", 0),
        "failed": counts.get("Failed", 0),
        "errors": counts.get("Error", 0),
        "skipped": counts.get("Skipped", 0),
        "duration_seconds": round(duration, 6),
        "parse_status": "PASS",
    }


def parse_frontend_coverage(path: Path) -> dict[str, Any]:
    if not path.is_file():
        return {"coverage_summary_present": False}
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except (json.JSONDecodeError, OSError) as exc:
        return {"coverage_summary_present": True, "parse_status": "FAIL", "error": str(exc)}
    total = payload.get("total", {})
    result: dict[str, Any] = {"coverage_summary_present": True, "parse_status": "PASS"}
    for metric in ("lines", "statements", "functions", "branches"):
        values = total.get(metric, {})
        result[metric] = {
            "total": values.get("total"),
            "covered": values.get("covered"),
            "skipped": values.get("skipped"),
            "percent": values.get("pct"),
        }
    return result


def directory_metrics(path: Path) -> dict[str, Any]:
    if not path.is_dir():
        return {"present": False, "file_count": 0, "bytes": 0}
    files = [item for item in path.rglob("*") if item.is_file()]
    return {
        "present": True,
        "file_count": len(files),
        "bytes": sum(item.stat().st_size for item in files),
    }


def copy_tree_if_present(source: Path, target: Path) -> None:
    if not source.exists():
        return
    if target.exists():
        shutil.rmtree(target)
    shutil.copytree(source, target)


def make_hash_manifest(output: Path) -> int:
    files = sorted(path for path in output.rglob("*") if path.is_file() and path.name != "SHA256SUMS.txt")
    lines = [f"{sha256_file(path)}  {safe_rel(path, output)}" for path in files]
    (output / "SHA256SUMS.txt").write_text("\n".join(lines) + ("\n" if lines else ""), encoding="utf-8")
    return len(files)


def command_group_status(results: Sequence[CommandResult], component: str, required_ids: set[str]) -> str:
    selected = [result for result in results if result.component == component and result.id in required_ids]
    if not selected:
        return "NOT_EXECUTED"
    if any(result.status == "FAIL" for result in selected):
        return "FAIL"
    if any(result.status == "BLOCKED" for result in selected):
        if any(result.status == "PASS" for result in selected):
            return "PARTIAL_BLOCKED"
        return "BLOCKED"
    if all(result.status == "PASS" for result in selected):
        return "PASS"
    return "PARTIAL"


def markdown_summary(summary: dict[str, Any]) -> str:
    backend = summary["backend"]
    frontend = summary["frontend"]
    lines = [
        "# NatureProtector — Phase 2 current test and coverage evidence",
        "",
        f"- Baseline ID: `{summary['baseline_id']}`",
        f"- Run ID: `{summary['run_id']}`",
        f"- Overall status: **{summary['overall_status']}**",
        f"- Backend status: **{backend['status']}**",
        f"- Frontend status: **{frontend['status']}**",
        f"- Evidence class: `{summary['evidence_class']}`",
        "",
        "## Backend",
        "",
        f"- .NET available: `{str(backend['tool_available']).lower()}`",
        f"- TRX files: `{backend['tests'].get('trx_file_count', 0)}`",
        f"- Executed test results: `{backend['tests'].get('test_result_count', 0)}`",
        f"- Passed: `{backend['tests'].get('passed', 0)}`",
        f"- Failed: `{backend['tests'].get('failed', 0)}`",
        f"- Skipped/not executed: `{backend['tests'].get('skipped_or_not_executed', 0)}`",
        f"- Line coverage: `{backend['coverage'].get('line_coverage_percent')}`",
        f"- Branch coverage: `{backend['coverage'].get('branch_coverage_percent')}`",
        "",
        "## Frontend",
        "",
        f"- npm available: `{str(frontend['tool_available']).lower()}`",
        f"- Executed test results: `{frontend['tests'].get('test_count', 0)}`",
        f"- Passed: `{frontend['tests'].get('passed', 0)}`",
        f"- Failed/errors: `{frontend['tests'].get('failed', 0) + frontend['tests'].get('errors', 0)}`",
        f"- Skipped: `{frontend['tests'].get('skipped', 0)}`",
        f"- Line coverage: `{frontend['coverage'].get('lines', {}).get('percent')}`",
        f"- Branch coverage: `{frontend['coverage'].get('branches', {}).get('percent')}`",
        f"- Production bundle bytes: `{frontend['build'].get('bytes', 0)}`",
        "",
        "## Interpretation",
        "",
    ]
    for item in summary["claim_ceiling"]:
        lines.append(f"- {item}")
    lines.extend(
        [
            "",
            "## Commands",
            "",
            "| ID | Component | Status | Exit | Duration (s) | Purpose |",
            "| --- | --- | --- | ---: | ---: | --- |",
        ]
    )
    for command in summary["commands"]:
        lines.append(
            f"| {command['id']} | {command['component']} | {command['status']} | "
            f"{'' if command['exit_code'] is None else command['exit_code']} | "
            f"{command['duration_seconds']} | {command['purpose']} |"
        )
    lines.append("")
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path.cwd())
    parser.add_argument("--baseline-id", required=True)
    parser.add_argument("--run-id", default=default_run_id())
    parser.add_argument("--output-root", type=Path)
    parser.add_argument("--skip-backend", action="store_true")
    parser.add_argument("--skip-frontend", action="store_true")
    parser.add_argument("--skip-npm-ci", action="store_true")
    parser.add_argument(
        "--frontend-workspace-mode",
        choices=("isolated", "repository"),
        default="isolated",
        help="Run frontend checks in a temporary clean copy (default) or directly in repository webUI.",
    )
    parser.add_argument("--include-e2e", action="store_true")
    parser.add_argument("--no-restore", action="store_true")
    parser.add_argument("--no-build", action="store_true")
    parser.add_argument("--timeout-seconds", type=int, default=1800)
    parser.add_argument("--quiet", action="store_true")
    args = parser.parse_args()

    repo = args.repo.resolve()
    if not (repo / "NatureProtector.sln").is_file() or not (repo / "webUI" / "package.json").is_file():
        parser.error(f"Not a NatureProtector repository root: {repo}")

    output = (
        args.output_root.resolve()
        if args.output_root
        else repo / "artifacts" / "report-evidence" / args.baseline_id / "02-tests" / args.run_id
    )
    if output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True, exist_ok=True)
    (output / "backend").mkdir()
    (output / "frontend").mkdir()

    parent = output.parent
    parent.mkdir(parents=True, exist_ok=True)
    (parent / "LATEST.txt").write_text(args.run_id + "\n", encoding="utf-8")

    dotnet_executable = tool_path("dotnet")
    node_executable = tool_path("node")
    npm_executable = tool_path("npm")

    toolchains = {
        "python": capture_version([sys.executable, "--version"], repo),
        "dotnet": capture_version([dotnet_executable, "--info"], repo)
        if dotnet_executable
        else {
            "available": False,
            "command": "dotnet --info",
            "exit_code": None,
            "output": "dotnet executable not found on PATH",
        },
        "node": capture_version([node_executable, "--version"], repo)
        if node_executable
        else {
            "available": False,
            "command": "node --version",
            "exit_code": None,
            "output": "node executable not found on PATH",
        },
        "npm": capture_version([npm_executable, "--version"], repo)
        if npm_executable
        else {
            "available": False,
            "command": "npm --version",
            "exit_code": None,
            "output": "npm executable not found on PATH",
        },
    }
    source_webui = repo / "webUI"
    webui = source_webui
    frontend_workspace_root: Path | None = None
    frontend_workspace_error: str | None = None
    if not args.skip_frontend and args.frontend_workspace_mode == "isolated":
        try:
            webui, frontend_workspace_root = prepare_isolated_frontend_workspace(source_webui)
        except Exception as exc:
            frontend_workspace_error = str(exc)

    environment = {
        "generated_at_utc": utc_now(),
        "collector_version": SCRIPT_VERSION,
        "platform": platform.platform(),
        "system": platform.system(),
        "release": platform.release(),
        "machine": platform.machine(),
        "processor": platform.processor(),
        "python_executable": sys.executable,
        "cpu_count": os.cpu_count(),
        "toolchains": toolchains,
        "frontend_workspace": {
            "mode": args.frontend_workspace_mode,
            "source": str(source_webui),
            "effective": str(webui),
            "temporaryRoot": str(frontend_workspace_root) if frontend_workspace_root else "",
            "preparationError": frontend_workspace_error or "",
            "staleRepositoryOutputsExcluded": args.frontend_workspace_mode == "isolated",
        },
    }
    write_json(output / "environment.json", environment)

    runner = EvidenceRunner(repo, output, args.timeout_seconds, echo=not args.quiet)

    # Backend current execution.
    dotnet_available = bool(toolchains["dotnet"].get("available") and toolchains["dotnet"].get("exit_code") == 0)
    backend_results_dir = output / "backend" / "test-results"
    backend_results_dir.mkdir(parents=True, exist_ok=True)
    backend_prerequisite = True
    if args.skip_backend:
        for command_id, purpose in (
            ("backend_tool_restore", "Restore repository-local .NET tools."),
            ("backend_restore", "Restore solution dependencies."),
            ("backend_build", "Build the backend solution in Release."),
            ("backend_test_coverage", "Execute non-Docker backend tests with TRX and Cobertura coverage."),
        ):
            runner.record_nonexecution(
                command_id, "backend", purpose, "SKIPPED", "Backend execution disabled by --skip-backend."
            )
        backend_prerequisite = False
    elif not dotnet_available:
        for command_id, purpose in (
            ("backend_tool_restore", "Restore repository-local .NET tools."),
            ("backend_restore", "Restore solution dependencies."),
            ("backend_build", "Build the backend solution in Release."),
            ("backend_test_coverage", "Execute non-Docker backend tests with TRX and Cobertura coverage."),
        ):
            runner.record_nonexecution(
                command_id, "backend", purpose, "BLOCKED", "Compatible dotnet executable is not available on PATH."
            )
        backend_prerequisite = False
    else:
        tool_restore = runner.run(
            "backend_tool_restore",
            "backend",
            "Restore repository-local .NET tools.",
            ["dotnet", "tool", "restore"],
            repo,
        )
        backend_prerequisite = tool_restore.status == "PASS"
        if args.no_restore:
            runner.record_nonexecution(
                "backend_restore",
                "backend",
                "Restore solution dependencies.",
                "SKIPPED",
                "Restore disabled by --no-restore.",
            )
            restore_ok = backend_prerequisite
        else:
            restore = runner.run(
                "backend_restore",
                "backend",
                "Restore solution dependencies.",
                ["dotnet", "restore", "NatureProtector.sln", "--nologo"],
                repo,
                dependency_ok=backend_prerequisite,
                dependency_reason="Repository-local .NET tool restore failed.",
            )
            restore_ok = restore.status == "PASS"

        if args.no_build:
            runner.record_nonexecution(
                "backend_build",
                "backend",
                "Build the backend solution in Release.",
                "SKIPPED",
                "Build disabled by --no-build.",
            )
            build_ok = restore_ok
        else:
            build = runner.run(
                "backend_build",
                "backend",
                "Build the backend solution in Release.",
                ["dotnet", "build", "NatureProtector.sln", "-c", "Release", "--nologo", "--no-restore"],
                repo,
                dependency_ok=restore_ok,
                dependency_reason="Solution restore failed.",
            )
            build_ok = build.status == "PASS"

        test_command = [
            "dotnet",
            "test",
            "NatureProtector.sln",
            "-c",
            "Release",
            "--nologo",
            "-v",
            "minimal",
            "-m:1",
            "--logger",
            "trx;LogFilePrefix=phase2-current",
            "--results-directory",
            str(backend_results_dir),
            "--collect:XPlat Code Coverage",
            "--settings",
            "coverage.runsettings",
            "--filter",
            "Category!=DockerIntegration",
        ]
        if restore_ok:
            test_command.append("--no-restore")
        if build_ok:
            test_command.append("--no-build")
        runner.run(
            "backend_test_coverage",
            "backend",
            "Execute non-Docker backend tests with TRX and Cobertura coverage.",
            test_command,
            repo,
            dependency_ok=build_ok,
            dependency_reason="Backend build did not pass.",
        )

    trx_paths = list(backend_results_dir.rglob("*.trx"))
    coverage_paths = list(backend_results_dir.rglob("coverage.cobertura.xml"))
    backend_tests = parse_trx_files(trx_paths, output)
    backend_coverage = merge_cobertura(coverage_paths, output)

    # Optional HTML/merged reports; not a prerequisite for the measurements above.
    if dotnet_available and coverage_paths:
        reports = ";".join(str(path.resolve()) for path in sorted(coverage_paths))
        report_target = output / "backend" / "coverage-report"
        runner.run(
            "backend_coverage_report",
            "backend",
            "Generate a merged human-readable backend coverage report.",
            [
                "dotnet",
                "tool",
                "run",
                "reportgenerator",
                "--",
                f"-reports:{reports}",
                f"-targetdir:{report_target.resolve()}",
                "-reporttypes:Html;TextSummary;JsonSummary;Cobertura",
                "-assemblyfilters:+NatureProtector.*;-*.Tests",
                "-filefilters:-**/bin/**;-**/obj/**;-**/*.g.cs;-**/*.Designer.cs",
            ],
            repo,
            dependency_ok=any(
                result.id == "backend_tool_restore" and result.status == "PASS" for result in runner.results
            ),
            dependency_reason="Repository-local .NET tools were not restored.",
        )
    else:
        runner.record_nonexecution(
            "backend_coverage_report",
            "backend",
            "Generate a merged human-readable backend coverage report.",
            "BLOCKED" if not args.skip_backend else "SKIPPED",
            "No backend Cobertura files are available." if not args.skip_backend else "Backend execution disabled.",
        )

    # Frontend current execution.
    npm_available = bool(toolchains["npm"].get("available") and toolchains["npm"].get("exit_code") == 0)
    frontend_prerequisite = True
    if frontend_workspace_error:
        for command_id, purpose in (
            ("frontend_npm_ci", "Install the lockfile-defined frontend dependency graph."),
            ("frontend_toolchain", "Validate the declared Node/npm/React toolchain contract."),
            ("frontend_typecheck", "Run TypeScript static type checking."),
            ("frontend_lint", "Run Biome lint checks."),
            ("frontend_format", "Check repository-owned frontend formatting."),
            ("frontend_test_coverage", "Execute Vitest tests with current coverage."),
            ("frontend_build", "Build the production frontend bundle."),
        ):
            runner.record_nonexecution(
                command_id, "frontend", purpose, "BLOCKED", f"Isolated frontend workspace could not be prepared: {frontend_workspace_error}"
            )
        frontend_prerequisite = False
    elif args.skip_frontend:
        for command_id, purpose in (
            ("frontend_npm_ci", "Install the lockfile-defined frontend dependency graph."),
            ("frontend_toolchain", "Validate the declared Node/npm/React toolchain contract."),
            ("frontend_typecheck", "Run TypeScript static type checking."),
            ("frontend_lint", "Run Biome lint checks."),
            ("frontend_format", "Check repository-owned frontend formatting."),
            ("frontend_test_coverage", "Execute Vitest tests with current coverage."),
            ("frontend_build", "Build the production frontend bundle."),
        ):
            runner.record_nonexecution(
                command_id, "frontend", purpose, "SKIPPED", "Frontend execution disabled by --skip-frontend."
            )
        frontend_prerequisite = False
    elif not npm_available:
        for command_id, purpose in (
            ("frontend_npm_ci", "Install the lockfile-defined frontend dependency graph."),
            ("frontend_toolchain", "Validate the declared Node/npm/React toolchain contract."),
            ("frontend_typecheck", "Run TypeScript static type checking."),
            ("frontend_lint", "Run Biome lint checks."),
            ("frontend_format", "Check repository-owned frontend formatting."),
            ("frontend_test_coverage", "Execute Vitest tests with current coverage."),
            ("frontend_build", "Build the production frontend bundle."),
        ):
            runner.record_nonexecution(
                command_id, "frontend", purpose, "BLOCKED", "npm executable is not available on PATH."
            )
        frontend_prerequisite = False
    else:
        if args.skip_npm_ci:
            runner.record_nonexecution(
                "frontend_npm_ci",
                "frontend",
                "Install the lockfile-defined frontend dependency graph.",
                "SKIPPED",
                "npm ci disabled by --skip-npm-ci.",
            )
            npm_ci_ok = (webui / "node_modules").is_dir()
            if not npm_ci_ok:
                frontend_prerequisite = False
        else:
            npm_ci = runner.run(
                "frontend_npm_ci",
                "frontend",
                "Install the lockfile-defined frontend dependency graph.",
                [npm_executable or "npm", "ci"],
                webui,
            )
            npm_ci_ok = npm_ci.status == "PASS"
            frontend_prerequisite = npm_ci_ok

        toolchain = runner.run(
            "frontend_toolchain",
            "frontend",
            "Validate the declared Node/npm/React toolchain contract.",
            [npm_executable or "npm", "run", "check:toolchain"],
            webui,
            dependency_ok=frontend_prerequisite,
            dependency_reason="Frontend dependencies are unavailable.",
        )
        base_ok = toolchain.status == "PASS"
        runner.run(
            "frontend_typecheck",
            "frontend",
            "Run TypeScript static type checking.",
            [npm_executable or "npm", "run", "typecheck"],
            webui,
            dependency_ok=base_ok,
            dependency_reason="Frontend toolchain validation failed.",
        )
        runner.run(
            "frontend_lint",
            "frontend",
            "Run Biome lint checks.",
            [npm_executable or "npm", "run", "lint"],
            webui,
            dependency_ok=base_ok,
            dependency_reason="Frontend toolchain validation failed.",
        )
        runner.run(
            "frontend_format",
            "frontend",
            "Check repository-owned frontend formatting.",
            [npm_executable or "npm", "run", "format:check"],
            webui,
            dependency_ok=base_ok,
            dependency_reason="Frontend toolchain validation failed.",
        )
        runner.run(
            "frontend_test_coverage",
            "frontend",
            "Execute Vitest tests with current coverage.",
            [npm_executable or "npm", "run", "test:coverage"],
            webui,
            dependency_ok=base_ok,
            dependency_reason="Frontend toolchain validation failed.",
        )
        runner.run(
            "frontend_build",
            "frontend",
            "Build the production frontend bundle.",
            [npm_executable or "npm", "run", "build"],
            webui,
            dependency_ok=base_ok,
            dependency_reason="Frontend toolchain validation failed.",
        )

    if args.include_e2e and npm_available and not args.skip_frontend:
        runner.run(
            "frontend_e2e",
            "frontend-e2e",
            "Execute Playwright browser tests.",
            [npm_executable or "npm", "run", "test:e2e"],
            webui,
            dependency_ok=(webui / "node_modules").is_dir(),
            dependency_reason="Frontend dependencies are unavailable.",
        )
    else:
        runner.record_nonexecution(
            "frontend_e2e",
            "frontend-e2e",
            "Execute Playwright browser tests.",
            "SKIPPED",
            "E2E execution is outside the default Phase 2 scope; use --include-e2e when runtime prerequisites are ready.",
        )

    # Copy generated frontend evidence into the immutable run directory.
    copy_tree_if_present(webui / "test-results", output / "frontend" / "raw-test-results")
    copy_tree_if_present(webui / "coverage", output / "frontend" / "coverage")
    frontend_tests = parse_junit(webui / "test-results" / "vitest-junit.xml", output)
    frontend_coverage = parse_frontend_coverage(webui / "coverage" / "coverage-summary.json")
    frontend_build = directory_metrics(webui / "dist")
    frontend_coverage_rows = []
    for metric in ("statements", "branches", "functions", "lines"):
        values = frontend_coverage.get(metric, {})
        frontend_coverage_rows.append(
            {
                "metric": metric,
                "covered": values.get("covered"),
                "total": values.get("total"),
                "skipped": values.get("skipped"),
                "percent": values.get("percent"),
            }
        )
    write_csv(
        output / "frontend" / "coverage-summary.csv",
        frontend_coverage_rows,
        [
            "metric",
            "covered",
            "total",
            "skipped",
            "percent",
        ],
    )

    command_dicts = [asdict(item) for item in runner.results]
    write_json(output / "command-results.json", command_dicts)
    write_csv(
        output / "command-results.csv",
        command_dicts,
        [
            "id",
            "component",
            "purpose",
            "status",
            "exit_code",
            "duration_seconds",
            "started_at_utc",
            "finished_at_utc",
            "cwd",
            "command",
            "log_file",
            "reason",
        ],
    )

    backend_required = {"backend_tool_restore", "backend_restore", "backend_build", "backend_test_coverage"}
    frontend_required = {
        "frontend_npm_ci",
        "frontend_toolchain",
        "frontend_typecheck",
        "frontend_lint",
        "frontend_format",
        "frontend_test_coverage",
        "frontend_build",
    }
    backend_status = command_group_status(runner.results, "backend", backend_required)
    frontend_status = command_group_status(runner.results, "frontend", frontend_required)

    if "FAIL" in {backend_status, frontend_status}:
        overall_status = "FAIL"
    elif backend_status == "PASS" and frontend_status == "PASS":
        overall_status = "PASS"
    elif "PASS" in {backend_status, frontend_status} and any(
        "BLOCKED" in status for status in {backend_status, frontend_status}
    ):
        overall_status = "PARTIAL_PASS_BLOCKED_ENVIRONMENT"
    elif all(status in {"BLOCKED", "NOT_EXECUTED"} for status in {backend_status, frontend_status}):
        overall_status = "BLOCKED"
    else:
        overall_status = "PARTIAL"

    summary = {
        "schema_version": "1.0",
        "collector_version": SCRIPT_VERSION,
        "evidence_class": EVIDENCE_CLASS,
        "baseline_id": args.baseline_id,
        "run_id": args.run_id,
        "generated_at_utc": utc_now(),
        "overall_status": overall_status,
        "backend": {
            "status": backend_status,
            "tool_available": dotnet_available,
            "tests": backend_tests,
            "coverage": backend_coverage,
        },
        "frontend": {
            "status": frontend_status,
            "tool_available": npm_available,
            "tests": frontend_tests,
            "coverage": frontend_coverage,
            "build": frontend_build,
        },
        "commands": command_dicts,
        "claim_ceiling": [
            "Test counts in this package refer only to parsed current execution results, not static declarations.",
            "The default backend run excludes Category=DockerIntegration and therefore does not prove real PostgreSQL, RabbitMQ or InfluxDB integration.",
            "Frontend Vitest results do not prove browser E2E behavior; Playwright is intentionally a separate optional command.",
            "Coverage percentages describe executed instrumented code in this environment and do not by themselves establish correctness, production capacity or scientific validity.",
            "A BLOCKED component must remain described as not executed in the assessed environment.",
        ],
    }
    write_json(output / "phase2-summary.json", summary)
    (output / "phase2-summary.md").write_text(markdown_summary(summary), encoding="utf-8")

    report_rows = [
        {
            "area": "Backend",
            "metric": "Execution status",
            "value": backend_status,
            "unit": "status",
            "interpretation": "Current non-Docker backend execution in the assessed environment.",
        },
        {
            "area": "Backend",
            "metric": "Executed test results",
            "value": backend_tests.get("test_result_count", 0),
            "unit": "tests",
            "interpretation": "Parsed current TRX results; zero when the .NET toolchain is blocked.",
        },
        {
            "area": "Backend",
            "metric": "Passed",
            "value": backend_tests.get("passed", 0),
            "unit": "tests",
            "interpretation": "Current parsed passed results.",
        },
        {
            "area": "Backend",
            "metric": "Failed",
            "value": backend_tests.get("failed", 0),
            "unit": "tests",
            "interpretation": "Current parsed failed results.",
        },
        {
            "area": "Backend",
            "metric": "Line coverage",
            "value": backend_coverage.get("line_coverage_percent"),
            "unit": "percent",
            "interpretation": "Merged current Cobertura coverage when available.",
        },
        {
            "area": "Backend",
            "metric": "Branch coverage",
            "value": backend_coverage.get("branch_coverage_percent"),
            "unit": "percent",
            "interpretation": "Merged current Cobertura branch coverage when available.",
        },
        {
            "area": "Frontend",
            "metric": "Execution status",
            "value": frontend_status,
            "unit": "status",
            "interpretation": "Current lockfile install, checks, Vitest coverage and production build.",
        },
        {
            "area": "Frontend",
            "metric": "Executed test results",
            "value": frontend_tests.get("test_count", 0),
            "unit": "tests",
            "interpretation": "Parsed current Vitest JUnit results.",
        },
        {
            "area": "Frontend",
            "metric": "Passed",
            "value": frontend_tests.get("passed", 0),
            "unit": "tests",
            "interpretation": "Current parsed passed results.",
        },
        {
            "area": "Frontend",
            "metric": "Failed or errored",
            "value": frontend_tests.get("failed", 0) + frontend_tests.get("errors", 0),
            "unit": "tests",
            "interpretation": "Current parsed failed/error results.",
        },
        {
            "area": "Frontend",
            "metric": "Test duration",
            "value": frontend_tests.get("duration_seconds", 0),
            "unit": "seconds",
            "interpretation": "Sum of JUnit testcase durations; not wall-clock command duration.",
        },
        {
            "area": "Frontend",
            "metric": "Line coverage",
            "value": frontend_coverage.get("lines", {}).get("percent"),
            "unit": "percent",
            "interpretation": "Current V8 coverage over the configured frontend scope.",
        },
        {
            "area": "Frontend",
            "metric": "Branch coverage",
            "value": frontend_coverage.get("branches", {}).get("percent"),
            "unit": "percent",
            "interpretation": "Current V8 branch coverage over the configured frontend scope.",
        },
        {
            "area": "Frontend",
            "metric": "Function coverage",
            "value": frontend_coverage.get("functions", {}).get("percent"),
            "unit": "percent",
            "interpretation": "Current V8 function coverage over the configured frontend scope.",
        },
        {
            "area": "Frontend",
            "metric": "Statement coverage",
            "value": frontend_coverage.get("statements", {}).get("percent"),
            "unit": "percent",
            "interpretation": "Current V8 statement coverage over the configured frontend scope.",
        },
        {
            "area": "Frontend",
            "metric": "Production bundle files",
            "value": frontend_build.get("file_count", 0),
            "unit": "files",
            "interpretation": "Files produced under webUI/dist by the current build.",
        },
        {
            "area": "Frontend",
            "metric": "Production bundle size",
            "value": frontend_build.get("bytes", 0),
            "unit": "bytes",
            "interpretation": "Uncompressed sum of files produced under webUI/dist.",
        },
    ]
    write_csv(output / "report-ready-metrics.csv", report_rows, ["area", "metric", "value", "unit", "interpretation"])
    report_markdown = [
        "# Report-ready current test and coverage metrics",
        "",
        "| Area | Metric | Value | Unit | Interpretation |",
        "| --- | --- | ---: | --- | --- |",
    ]
    for row in report_rows:
        value = "—" if row["value"] is None else row["value"]
        report_markdown.append(
            f"| {row['area']} | {row['metric']} | {value} | {row['unit']} | {row['interpretation']} |"
        )
    report_markdown.append("")
    (output / "report-ready-metrics.md").write_text("\n".join(report_markdown), encoding="utf-8")

    hashed_count = make_hash_manifest(output)
    print("\nPHASE_2_COLLECTION_COMPLETE")
    print(f"PHASE_2_STATUS={overall_status}")
    print(f"BASELINE_ID={args.baseline_id}")
    print(f"RUN_ID={args.run_id}")
    print(f"BACKEND_STATUS={backend_status}")
    print(f"FRONTEND_STATUS={frontend_status}")
    print(f"HASHED_FILE_COUNT={hashed_count}")
    print(f"EVIDENCE_ROOT={output}")
    return 1 if overall_status == "FAIL" else 0


if __name__ == "__main__":
    raise SystemExit(main())
