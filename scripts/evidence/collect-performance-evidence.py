#!/usr/bin/env python3
"""Collect Phase 5 performance evidence for NatureProtector.

The default mode is non-invasive: it inventories microbenchmarks, bounded HTTP
workloads, system-capacity profiles, telemetry instruments and claim boundaries.
Optional execution can run read-only HTTP probes and BenchmarkDotNet. Existing
system-capacity output can be ingested, but the collector never starts Docker,
changes data, runs deployment actions or fabricates measurements.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import platform
import re
import shutil
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Sequence
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

SCRIPT_VERSION = "1.0.0"
STATIC_CLASS = "STATIC_PERFORMANCE_CONTRACT"
CURRENT_HTTP_CLASS = "CURRENT_LOCAL_HTTP_MEASUREMENT"
CURRENT_MICROBENCH_CLASS = "CURRENT_LOCAL_MICROBENCHMARK"
CURRENT_SYSTEM_CLASS = "CURRENT_LOCAL_SYSTEM_WORKLOAD"


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def compact_utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace") if path.exists() else ""


def safe_rel(path: Path, root: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve())


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def normalize(value: Any) -> Any:
    if value is None:
        return ""
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (list, tuple, set)):
        return "; ".join(str(item) for item in value)
    if isinstance(value, dict):
        return json.dumps(value, ensure_ascii=False, sort_keys=True)
    return value


def write_csv(path: Path, rows: Sequence[dict[str, Any]], fieldnames: Sequence[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow({field: normalize(row.get(field)) for field in fieldnames})


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def run_command(command: list[str], cwd: Path, stdout_path: Path, stderr_path: Path, timeout: int) -> dict[str, Any]:
    started = time.perf_counter()
    try:
        process = subprocess.run(
            command,
            cwd=cwd,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=timeout if timeout > 0 else None,
            check=False,
        )
        status = "PASS" if process.returncode == 0 else "FAIL"
        exit_code = process.returncode
        stdout = process.stdout
        stderr = process.stderr
        timed_out = False
    except subprocess.TimeoutExpired as exc:
        status = "TIMEOUT"
        exit_code = 124
        stdout = exc.stdout or ""
        stderr = exc.stderr or ""
        timed_out = True
    stdout_path.parent.mkdir(parents=True, exist_ok=True)
    stdout_path.write_text(stdout, encoding="utf-8")
    stderr_path.write_text(stderr, encoding="utf-8")
    return {
        "status": status,
        "exitCode": exit_code,
        "timedOut": timed_out,
        "durationSeconds": round(time.perf_counter() - started, 3),
        "command": command,
        "stdout": stdout_path.name,
        "stderr": stderr_path.name,
    }


def collect_benchmark_catalog(repo: Path) -> tuple[list[dict[str, Any]], list[dict[str, Any]], list[int]]:
    source = repo / "benchmarks/NatureProtector.Benchmarks/Program.cs"
    text = read_text(source)
    batches_match = re.search(r"\[Params\(([^\]]+)\)\]", text)
    batches = [int(value.strip()) for value in batches_match.group(1).split(",")] if batches_match else []
    profiles: list[dict[str, Any]] = []
    profile_pattern = re.compile(
        r'"(B[012])"\s*=>\s*new BenchmarkProfile\(\s*"\1",\s*"([^"]+)",\s*Job\.([A-Za-z]+)\.WithId',
        re.S,
    )
    for match in profile_pattern.finditer(text):
        profiles.append(
            {
                "profile": match.group(1),
                "benchmarkDotNetJob": match.group(3),
                "description": match.group(2),
                "evidenceClass": STATIC_CLASS,
                "source": safe_rel(source, repo),
            }
        )
    benchmark_rows: list[dict[str, Any]] = []
    class_pattern = re.compile(r"public class (\w+Benchmarks).*?\n}\n", re.S)
    for class_match in class_pattern.finditer(text):
        class_name = class_match.group(1)
        block = class_match.group(0)
        for method_match in re.finditer(r"\[Benchmark\]\s*public\s+[\w<>\[\]?]+\s+(\w+)\s*\(", block):
            method = method_match.group(1)
            for batch in batches or [None]:
                benchmark_rows.append(
                    {
                        "benchmarkClass": class_name,
                        "method": method,
                        "batchSize": batch,
                        "evidenceClass": STATIC_CLASS,
                        "source": safe_rel(source, repo),
                    }
                )
    return benchmark_rows, profiles, batches


def extract_ps_profile_block(text: str, name: str) -> str:
    match = re.search(rf'"{re.escape(name)}"\s*\{{\s*return \[ordered\]@\{{(.*?)\n\s*\}}\s*\}}', text, re.S)
    return match.group(1) if match else ""


def ps_value(block: str, key: str) -> str | None:
    match = re.search(rf"^\s*{re.escape(key)}\s*=\s*(.+?)\s*$", block, re.M)
    if not match:
        return None
    value = match.group(1).strip()
    if value.startswith('"') and value.endswith('"'):
        return value[1:-1]
    return value


def collect_system_profiles(repo: Path) -> list[dict[str, Any]]:
    path = repo / "scripts/performance/run-system-capacity-workload.ps1"
    text = read_text(path)
    rows: list[dict[str, Any]] = []
    for name in ("Calibration", "B0", "B1", "B2"):
        block = extract_ps_profile_block(text, name)
        sensor_count = int(ps_value(block, "sensorCount") or 0)
        cycles = int(ps_value(block, "numberOfCycles") or 0)
        repetitions = int(ps_value(block, "repetitions") or 0)
        expected_per_run = sensor_count * cycles
        rows.append(
            {
                "profile": name,
                "sensorCount": sensor_count,
                "numberOfCycles": cycles,
                "intervalSeconds": int(ps_value(block, "intervalSeconds") or 0),
                "repetitions": repetitions,
                "timeoutSeconds": int(ps_value(block, "timeoutSeconds") or 0),
                "observationWaitSeconds": int(ps_value(block, "observationWaitSeconds") or 0),
                "backlogDrainWaitSeconds": int(ps_value(block, "backlogDrainWaitSeconds") or 0),
                "expectedEventsPerRun": expected_per_run,
                "expectedEventsCampaign": expected_per_run * repetitions,
                "purpose": ps_value(block, "purpose") or "",
                "evidenceClass": STATIC_CLASS,
                "source": safe_rel(path, repo),
            }
        )
    return rows


def collect_readiness_probes(repo: Path) -> list[dict[str, Any]]:
    path = repo / "scripts/performance/run-local-readiness-workload.ps1"
    text = read_text(path)
    rows: list[dict[str, Any]] = []
    pattern = re.compile(
        r'New-Probe\s+-Surface\s+"([^"]+)"\s+-Name\s+"([^"]+)"\s+-Method\s+"([^"]+)"\s+-Url\s+.*?'
        r'-ExpectedStatusCodes\s+@\(([^)]+)\)\s+-Purpose\s+"([^"]+)"',
        re.S,
    )
    for match in pattern.finditer(text):
        rows.append(
            {
                "surface": match.group(1),
                "probe": match.group(2),
                "method": match.group(3),
                "expectedStatusCodes": "|".join(re.findall(r"\d+", match.group(4))),
                "purpose": match.group(5),
                "evidenceClass": STATIC_CLASS,
                "source": safe_rel(path, repo),
            }
        )
    return rows


def collect_http_profiles(repo: Path) -> list[dict[str, Any]]:
    path = repo / "scripts/performance/run-http-workload.py"
    text = read_text(path)
    rows: list[dict[str, Any]] = []
    for name in ("Calibration", "B0", "B1", "B2"):
        block_match = re.search(rf'"{name}"\s*:\s*\{{(.*?)\n\s*\}},', text, re.S)
        block = block_match.group(1) if block_match else ""

        def number(key: str) -> int:
            match = re.search(rf'"{key}"\s*:\s*(\d+)', block)
            return int(match.group(1)) if match else 0

        purpose_match = re.search(r'"purpose"\s*:\s*"([^"]+)"', block)
        rows.append(
            {
                "profile": name,
                "warmupPerProbe": number("warmupPerProbe"),
                "measuredPerProbe": number("measuredPerProbe"),
                "concurrency": number("concurrency"),
                "purpose": purpose_match.group(1) if purpose_match else "",
                "evidenceClass": STATIC_CLASS,
                "source": safe_rel(path, repo),
            }
        )
    return rows


def collect_performance_metrics(repo: Path, baseline_id: str) -> list[dict[str, Any]]:
    inventory = repo / "artifacts" / "report-evidence" / baseline_id / "01-inventory" / "telemetry-metrics.csv"
    rows: list[dict[str, Any]] = []
    if inventory.exists():
        with inventory.open(encoding="utf-8-sig", newline="") as stream:
            for row in csv.DictReader(stream):
                name = row.get("name", "")
                if any(token in name for token in ("duration", "requests", "events", "messages", "batch", "rows")):
                    rows.append(
                        {
                            "name": name,
                            "instrumentKind": row.get("instrument_kind", ""),
                            "unit": row.get("unit", ""),
                            "source": row.get("source", ""),
                            "line": row.get("line", ""),
                            "evidenceClass": STATIC_CLASS,
                        }
                    )
    return rows


def health_probe(api_base_url: str, timeout: float) -> dict[str, Any]:
    url = api_base_url.rstrip("/") + "/health"
    started = time.perf_counter()
    try:
        request = Request(url, method="GET", headers={"User-Agent": "NatureProtector-Phase5/1.0"})
        with urlopen(request, timeout=timeout) as response:
            body = response.read()
            status = "PASS" if int(response.status) == 200 else "FAIL"
            return {
                "status": status,
                "url": url,
                "httpStatus": int(response.status),
                "elapsedMs": round((time.perf_counter() - started) * 1000.0, 3),
                "responseBytes": len(body),
                "error": "",
            }
    except HTTPError as exc:
        return {
            "status": "FAIL",
            "url": url,
            "httpStatus": int(exc.code),
            "elapsedMs": round((time.perf_counter() - started) * 1000.0, 3),
            "responseBytes": 0,
            "error": str(exc),
        }
    except (URLError, TimeoutError, OSError) as exc:
        return {
            "status": "BLOCKED_API_UNAVAILABLE",
            "url": url,
            "httpStatus": None,
            "elapsedMs": round((time.perf_counter() - started) * 1000.0, 3),
            "responseBytes": 0,
            "error": f"{type(exc).__name__}: {exc}",
        }


def parse_benchmark_results(directory: Path) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    summary_path = directory / "summary.json"
    if summary_path.exists():
        payload = json.loads(read_text(summary_path))
        for benchmark in payload.get("benchmarks", []):
            rows.append({**benchmark, "evidenceClass": CURRENT_MICROBENCH_CLASS})
        return rows, {
            "status": "PASS"
            if payload.get("status") == "ready" and rows
            else str(payload.get("status", "UNKNOWN")).upper(),
            "sourceDirectory": str(directory),
            "benchmarkCount": len(rows),
        }
    reports = sorted(directory.rglob("*-report-brief.json"))
    for report in reports:
        try:
            payload = json.loads(read_text(report))
            for benchmark in payload.get("Benchmarks", []):
                stats = benchmark.get("Statistics") or {}
                memory = benchmark.get("Memory") or {}
                rows.append(
                    {
                        "report": safe_rel(report, directory),
                        "type": benchmark.get("Type", ""),
                        "method": benchmark.get("Method", ""),
                        "parameters": benchmark.get("Parameters", ""),
                        "meanNanoseconds": stats.get("Mean"),
                        "medianNanoseconds": stats.get("Median"),
                        "standardDeviationNanoseconds": stats.get("StandardDeviation"),
                        "allocatedBytesPerOperation": memory.get("BytesAllocatedPerOperation"),
                        "evidenceClass": CURRENT_MICROBENCH_CLASS,
                    }
                )
        except Exception:
            continue
    return rows, {
        "status": "PASS" if rows else "NO_RESULTS",
        "sourceDirectory": str(directory),
        "benchmarkCount": len(rows),
        "reportCount": len(reports),
    }


def ingest_http(directory: Path) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    status_path = directory / "status.json"
    summary_path = directory / "summary.csv"
    status = json.loads(read_text(status_path)) if status_path.exists() else {"status": "NO_RESULTS"}
    rows: list[dict[str, Any]] = []
    if summary_path.exists():
        with summary_path.open(encoding="utf-8-sig", newline="") as stream:
            rows = [{**row, "evidenceClass": CURRENT_HTTP_CLASS} for row in csv.DictReader(stream)]
    return rows, {**status, "sourceDirectory": str(directory), "summaryRows": len(rows)}


def ingest_system(directory: Path) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    summary_path = directory / "summary.json"
    measurements_path = directory / "measurements.csv"
    if not summary_path.exists():
        return [], {"status": "NO_RESULTS", "sourceDirectory": str(directory)}
    summary = json.loads(read_text(summary_path))
    rows: list[dict[str, Any]] = []
    if measurements_path.exists():
        with measurements_path.open(encoding="utf-8-sig", newline="") as stream:
            rows = [{**row, "evidenceClass": CURRENT_SYSTEM_CLASS} for row in csv.DictReader(stream)]
    status = (
        "PASS"
        if summary.get("status") == "Completed" and int(summary.get("failedRuns", 0)) == 0
        else str(summary.get("status", "UNKNOWN")).upper()
    )
    return rows, {**summary, "status": status, "sourceDirectory": str(directory), "measurementRows": len(rows)}


def findings(benchmark_profiles: list[dict[str, Any]], system_profiles: list[dict[str, Any]]) -> list[dict[str, Any]]:
    system_by_name = {row["profile"]: row for row in system_profiles}
    return [
        {
            "id": "PERF-F01",
            "severity": "HIGH",
            "finding": "Current system profiles use one or two repetitions; p95/p99 across those runs collapse to the maximum and are not report-grade distribution estimates.",
            "action": "Use the Repetitions override with at least 10 completed runs for the report baseline, while retaining Calibration/B0 for smoke gates.",
            "evidenceClass": STATIC_CLASS,
        },
        {
            "id": "PERF-F02",
            "severity": "HIGH",
            "finding": "PublishedAt is not persisted, so full publish-to-projection latency remains unsupported.",
            "action": "Report request/run durations and persisted partial timings only; add a governed publish timestamp before claiming end-to-end latency.",
            "evidenceClass": STATIC_CLASS,
        },
        {
            "id": "PERF-F03",
            "severity": "MEDIUM",
            "finding": "The existing capacity script observes queue totals after each run and at drain, not a guaranteed continuous maximum queue depth.",
            "action": "Retain time-series RabbitMQ sampling during future campaigns before presenting peak backlog.",
            "evidenceClass": STATIC_CLASS,
        },
        {
            "id": "PERF-F04",
            "severity": "MEDIUM",
            "finding": "BenchmarkDotNet isolates scoring, temporal classification, territorial mapping and serialization; it cannot establish distributed-system throughput.",
            "action": "Present microbenchmarks separately from HTTP and integrated pipeline workloads.",
            "evidenceClass": STATIC_CLASS,
        },
        {
            "id": "PERF-F05",
            "severity": "MEDIUM",
            "finding": "The B0 BenchmarkDotNet profile uses Job.Dry and is a harness smoke check rather than a stable performance baseline.",
            "action": "Use B1 or B2 for reportable local comparisons and preserve raw BenchmarkDotNet artifacts.",
            "evidenceClass": STATIC_CLASS,
        },
        {
            "id": "PERF-F06",
            "severity": "INFO",
            "finding": f"The configured B1 system campaign represents {system_by_name.get('B1', {}).get('expectedEventsCampaign', 0)} expected events and B2 represents {system_by_name.get('B2', {}).get('expectedEventsCampaign', 0)}; these are bounded engineering workloads, not stress limits.",
            "action": "Label profile parameters alongside every reported measurement.",
            "evidenceClass": STATIC_CLASS,
        },
    ]


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--baseline-id", required=True)
    parser.add_argument("--run-id")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--api-base-url", default="http://localhost:5254")
    parser.add_argument("--probe-timeout-seconds", type=float, default=2.0)
    parser.add_argument("--run-http", action="store_true")
    parser.add_argument("--http-profile", choices=("Calibration", "B0", "B1", "B2"), default="Calibration")
    parser.add_argument("--include-web", action="store_true")
    parser.add_argument("--run-microbenchmarks", action="store_true")
    parser.add_argument("--benchmark-profile", choices=("B0", "B1", "B2"), default="B0")
    parser.add_argument("--benchmark-timeout-seconds", type=int, default=900)
    parser.add_argument("--benchmark-run-directory", type=Path)
    parser.add_argument("--http-run-directory", type=Path)
    parser.add_argument("--system-run-directory", type=Path)
    parser.add_argument("--require-http", action="store_true")
    parser.add_argument("--require-microbenchmarks", action="store_true")
    parser.add_argument("--require-system", action="store_true")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    repo = args.repo.resolve()
    run_id = args.run_id or compact_utc_now()
    output = (
        args.output or repo / "artifacts/report-evidence" / args.baseline_id / "05-performance" / run_id
    ).resolve()
    static_dir = output / "static"
    execution_dir = output / "execution"
    report_dir = output / "report-ready"
    for directory in (static_dir, execution_dir, report_dir):
        directory.mkdir(parents=True, exist_ok=True)

    benchmark_rows, benchmark_profiles, batch_sizes = collect_benchmark_catalog(repo)
    system_profiles = collect_system_profiles(repo)
    readiness_probes = collect_readiness_probes(repo)
    http_profiles = collect_http_profiles(repo)
    perf_metrics = collect_performance_metrics(repo, args.baseline_id)
    static_findings = findings(benchmark_profiles, system_profiles)

    write_csv(
        static_dir / "microbenchmark-catalog.csv",
        benchmark_rows,
        ["benchmarkClass", "method", "batchSize", "evidenceClass", "source"],
    )
    write_json(static_dir / "microbenchmark-catalog.json", benchmark_rows)
    write_csv(
        static_dir / "microbenchmark-profiles.csv",
        benchmark_profiles,
        ["profile", "benchmarkDotNetJob", "description", "evidenceClass", "source"],
    )
    write_json(static_dir / "microbenchmark-profiles.json", benchmark_profiles)
    write_csv(
        static_dir / "system-workload-profiles.csv",
        system_profiles,
        [
            "profile",
            "sensorCount",
            "numberOfCycles",
            "intervalSeconds",
            "repetitions",
            "timeoutSeconds",
            "observationWaitSeconds",
            "backlogDrainWaitSeconds",
            "expectedEventsPerRun",
            "expectedEventsCampaign",
            "purpose",
            "evidenceClass",
            "source",
        ],
    )
    write_json(static_dir / "system-workload-profiles.json", system_profiles)
    write_csv(
        static_dir / "readiness-probes.csv",
        readiness_probes,
        ["surface", "probe", "method", "expectedStatusCodes", "purpose", "evidenceClass", "source"],
    )
    write_json(static_dir / "readiness-probes.json", readiness_probes)
    write_csv(
        static_dir / "http-workload-profiles.csv",
        http_profiles,
        ["profile", "warmupPerProbe", "measuredPerProbe", "concurrency", "purpose", "evidenceClass", "source"],
    )
    write_json(static_dir / "http-workload-profiles.json", http_profiles)
    write_csv(
        static_dir / "performance-telemetry.csv",
        perf_metrics,
        ["name", "instrumentKind", "unit", "source", "line", "evidenceClass"],
    )
    write_json(static_dir / "performance-telemetry.json", perf_metrics)
    write_csv(
        static_dir / "performance-findings.csv",
        static_findings,
        ["id", "severity", "finding", "action", "evidenceClass"],
    )
    write_json(static_dir / "performance-findings.json", static_findings)

    def command_version(command: str, arguments: list[str]) -> dict[str, Any]:
        executable = shutil.which(command)
        if not executable:
            return {"available": False, "path": None, "version": None}
        try:
            result = subprocess.run(
                [executable, *arguments],
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                timeout=10,
                check=False,
            )
            value = next((line.strip() for line in result.stdout.splitlines() if line.strip()), "")
        except Exception as exc:
            value = f"unavailable: {type(exc).__name__}"
        return {"available": True, "path": executable, "version": value}

    environment = {
        "generatedAtUtc": utc_now(),
        "python": {"version": sys.version.split()[0], "executable": sys.executable},
        "platform": platform.platform(),
        "machine": platform.machine(),
        "dotnet": command_version("dotnet", ["--version"]),
        "node": command_version("node", ["--version"]),
        "npm": command_version("npm", ["--version"]),
        "docker": command_version("docker", ["--version"]),
        "pwsh": command_version("pwsh", ["--version"]),
    }
    write_json(output / "environment.json", environment)

    health = health_probe(args.api_base_url, args.probe_timeout_seconds)
    write_json(execution_dir / "environment-health-probe.json", health)

    micro_rows: list[dict[str, Any]] = []
    micro_status: dict[str, Any] = {"status": "NOT_REQUESTED", "evidenceClass": CURRENT_MICROBENCH_CLASS}
    benchmark_dir = args.benchmark_run_directory.resolve() if args.benchmark_run_directory else None
    if args.run_microbenchmarks:
        dotnet = shutil.which("dotnet")
        if not dotnet:
            micro_status = {"status": "BLOCKED_DOTNET_UNAVAILABLE", "evidenceClass": CURRENT_MICROBENCH_CLASS}
        else:
            benchmark_dir = execution_dir / f"microbenchmarks-{args.benchmark_profile}"
            benchmark_dir.mkdir(parents=True, exist_ok=True)
            command = [
                dotnet,
                "run",
                "--project",
                str(repo / "benchmarks/NatureProtector.Benchmarks/NatureProtector.Benchmarks.csproj"),
                "-c",
                "Release",
                "--",
                "--profile",
                args.benchmark_profile,
                "--filter",
                "*",
                "--artifacts",
                str(benchmark_dir),
            ]
            command_result = run_command(
                command,
                repo,
                benchmark_dir / "benchmark.stdout.log",
                benchmark_dir / "benchmark.stderr.log",
                args.benchmark_timeout_seconds,
            )
            write_json(benchmark_dir / "command-result.json", command_result)
            micro_rows, parsed = parse_benchmark_results(benchmark_dir)
            micro_status = {
                **command_result,
                **parsed,
                "profile": args.benchmark_profile,
                "evidenceClass": CURRENT_MICROBENCH_CLASS,
            }
    elif benchmark_dir:
        micro_rows, micro_status = parse_benchmark_results(benchmark_dir)
        micro_status["evidenceClass"] = CURRENT_MICROBENCH_CLASS
    write_json(execution_dir / "microbenchmarks-status.json", micro_status)
    write_csv(
        execution_dir / "microbenchmark-results.csv",
        micro_rows,
        sorted({key for row in micro_rows for key in row})
        if micro_rows
        else [
            "type",
            "method",
            "parameters",
            "meanNanoseconds",
            "medianNanoseconds",
            "allocatedBytesPerOperation",
            "evidenceClass",
        ],
    )
    write_json(execution_dir / "microbenchmark-results.json", micro_rows)

    http_rows: list[dict[str, Any]] = []
    http_status: dict[str, Any] = {"status": "NOT_REQUESTED", "evidenceClass": CURRENT_HTTP_CLASS}
    http_dir = args.http_run_directory.resolve() if args.http_run_directory else None
    if args.run_http:
        http_dir = execution_dir / f"http-{args.http_profile}"
        command = [
            sys.executable,
            str(repo / "scripts/performance/run-http-workload.py"),
            "--profile",
            args.http_profile,
            "--api-base-url",
            args.api_base_url,
            "--output",
            str(http_dir),
        ]
        if args.include_web:
            command.append("--include-web")
        command_result = run_command(
            command,
            repo,
            execution_dir / "http-workload.stdout.log",
            execution_dir / "http-workload.stderr.log",
            900,
        )
        if (http_dir / "status.json").exists():
            http_rows, parsed = ingest_http(http_dir)
            http_status = {
                **command_result,
                **parsed,
                "profile": args.http_profile,
                "evidenceClass": CURRENT_HTTP_CLASS,
            }
        else:
            http_status = {
                **command_result,
                "status": "FAIL",
                "profile": args.http_profile,
                "evidenceClass": CURRENT_HTTP_CLASS,
            }
    elif http_dir:
        http_rows, http_status = ingest_http(http_dir)
        http_status["evidenceClass"] = CURRENT_HTTP_CLASS
    write_json(execution_dir / "http-status.json", http_status)
    write_csv(
        execution_dir / "http-results.csv",
        http_rows,
        sorted({key for row in http_rows for key in row})
        if http_rows
        else [
            "surface",
            "probe",
            "attempts",
            "successRatePercent",
            "p50ElapsedMs",
            "p95ElapsedMs",
            "p99ElapsedMs",
            "observedRequestsPerSecond",
            "evidenceClass",
        ],
    )
    write_json(execution_dir / "http-results.json", http_rows)

    system_rows: list[dict[str, Any]] = []
    system_status: dict[str, Any] = {"status": "NOT_REQUESTED", "evidenceClass": CURRENT_SYSTEM_CLASS}
    if args.system_run_directory:
        system_rows, system_status = ingest_system(args.system_run_directory.resolve())
        system_status["evidenceClass"] = CURRENT_SYSTEM_CLASS
    write_json(execution_dir / "system-status.json", system_status)
    write_csv(
        execution_dir / "system-results.csv",
        system_rows,
        sorted({key for row in system_rows for key in row})
        if system_rows
        else [
            "profile",
            "iteration",
            "simulationRunId",
            "elapsedMs",
            "expectedEvents",
            "acceptedReadings",
            "riskAssessments",
            "backlogDrainTimeMs",
            "lostEvents",
            "evidenceClass",
        ],
    )
    write_json(execution_dir / "system-results.json", system_rows)

    capabilities = [
        {
            "area": "Microbenchmark harness",
            "status": "IMPLEMENTED",
            "currentEvidence": micro_status.get("status"),
            "claimCeiling": "Local isolated component timing only.",
        },
        {
            "area": "Read-only HTTP workload",
            "status": "IMPLEMENTED",
            "currentEvidence": http_status.get("status"),
            "claimCeiling": "Local request latency and observed request rate only.",
        },
        {
            "area": "Integrated system workload",
            "status": "IMPLEMENTED",
            "currentEvidence": system_status.get("status"),
            "claimCeiling": "Local bounded campaign; not production capacity.",
        },
        {
            "area": "Performance telemetry",
            "status": "IMPLEMENTED",
            "currentEvidence": "STATIC_DECLARATIONS",
            "claimCeiling": "Instrumentation exists; runtime values require a current execution.",
        },
        {
            "area": "Publish-to-projection latency",
            "status": "NOT_PROVED",
            "currentEvidence": "UNSUPPORTED_TIMESTAMP",
            "claimCeiling": "Do not claim until PublishedAt is persisted.",
        },
    ]
    write_csv(
        report_dir / "performance-capability-summary.csv",
        capabilities,
        ["area", "status", "currentEvidence", "claimCeiling"],
    )
    write_json(report_dir / "performance-capability-summary.json", capabilities)
    chart_specs = [
        {
            "id": "P5-CH01",
            "title": "HTTP p50/p95/p99 by endpoint and profile",
            "requires": "CURRENT_LOCAL_HTTP_MEASUREMENT=PASS",
            "preferredFormat": "grouped point/bar chart",
        },
        {
            "id": "P5-CH02",
            "title": "Observed requests per second by endpoint and concurrency",
            "requires": "At least B0 and B1 HTTP runs",
            "preferredFormat": "line chart",
        },
        {
            "id": "P5-CH03",
            "title": "Microbenchmark mean and allocated bytes by batch size",
            "requires": "CURRENT_LOCAL_MICROBENCHMARK=PASS with B1/B2",
            "preferredFormat": "log-scale line chart",
        },
        {
            "id": "P5-CH04",
            "title": "Integrated run duration and throughput by profile",
            "requires": "CURRENT_LOCAL_SYSTEM_WORKLOAD=PASS",
            "preferredFormat": "two separate charts",
        },
        {
            "id": "P5-CH05",
            "title": "Backlog drain duration and final queue state",
            "requires": "CURRENT_LOCAL_SYSTEM_WORKLOAD=PASS",
            "preferredFormat": "point chart plus reconciliation table",
        },
    ]
    write_csv(report_dir / "chart-specifications.csv", chart_specs, ["id", "title", "requires", "preferredFormat"])
    write_json(report_dir / "chart-specifications.json", chart_specs)

    current_passes = {
        "microbenchmarks": micro_status.get("status") == "PASS",
        "http": http_status.get("status") == "PASS",
        "system": system_status.get("status") == "PASS",
    }
    requested_failures = []
    if args.require_microbenchmarks and not current_passes["microbenchmarks"]:
        requested_failures.append("microbenchmarks")
    if args.require_http and not current_passes["http"]:
        requested_failures.append("http")
    if args.require_system and not current_passes["system"]:
        requested_failures.append("system")
    status = "PASS" if all(current_passes.values()) else "PARTIAL_PASS_BLOCKED_ENVIRONMENT"
    if requested_failures:
        status = "FAIL_REQUIRED_EXECUTION"
    summary = {
        "phase": 5,
        "scriptVersion": SCRIPT_VERSION,
        "generatedAtUtc": utc_now(),
        "baselineId": args.baseline_id,
        "runId": run_id,
        "status": status,
        "staticPerformanceContractStatus": "PASS",
        "environmentHealthProbeStatus": health.get("status"),
        "microbenchmarkStatus": micro_status.get("status"),
        "httpWorkloadStatus": http_status.get("status"),
        "systemWorkloadStatus": system_status.get("status"),
        "staticCounts": {
            "benchmarkCases": len(benchmark_rows),
            "benchmarkMethods": len({(row["benchmarkClass"], row["method"]) for row in benchmark_rows}),
            "benchmarkBatchSizes": len(batch_sizes),
            "benchmarkProfiles": len(benchmark_profiles),
            "systemProfiles": len(system_profiles),
            "readinessProbes": len(readiness_probes),
            "httpProfiles": len(http_profiles),
            "performanceTelemetryInstruments": len(perf_metrics),
            "findings": len(static_findings),
        },
        "currentResultCounts": {
            "microbenchmarkRows": len(micro_rows),
            "httpRows": len(http_rows),
            "systemRows": len(system_rows),
        },
        "requestedFailures": requested_failures,
        "claimBoundaries": [
            "No current measurement is inferred from static code or historical prose.",
            "Microbenchmarks do not prove distributed throughput.",
            "HTTP request duration is not event end-to-end latency.",
            "Integrated local workloads do not establish production capacity or SLOs.",
            "PublishedAt is not persisted, so publish-to-projection latency is unsupported.",
        ],
    }
    write_json(output / "phase5-summary.json", summary)
    markdown = [
        "# NatureProtector — Phase 5 performance evidence",
        "",
        f"- Status: **{status}**",
        f"- Baseline: `{args.baseline_id}`",
        f"- Run: `{run_id}`",
        f"- Environment health probe: `{health.get('status')}`",
        f"- Current microbenchmark execution: `{micro_status.get('status')}`",
        f"- Current HTTP workload: `{http_status.get('status')}`",
        f"- Current system workload: `{system_status.get('status')}`",
        "",
        "## Static inventory",
        "",
        f"- Benchmark methods: `{summary['staticCounts']['benchmarkMethods']}`",
        f"- Benchmark cases per profile: `{summary['staticCounts']['benchmarkCases']}`",
        f"- Batch sizes: `{', '.join(str(value) for value in batch_sizes)}`",
        f"- System profiles: `{summary['staticCounts']['systemProfiles']}`",
        f"- Readiness probes: `{summary['staticCounts']['readinessProbes']}`",
        f"- Performance-relevant telemetry instruments: `{summary['staticCounts']['performanceTelemetryInstruments']}`",
        "",
        "## Claim boundary",
        "",
    ] + [f"- {item}" for item in summary["claimBoundaries"]]
    (output / "phase5-summary.md").write_text("\n".join(markdown) + "\n", encoding="utf-8")
    (report_dir / "performance-summary.md").write_text("\n".join(markdown) + "\n", encoding="utf-8")

    latest = repo / "artifacts/report-evidence" / args.baseline_id / "05-performance/LATEST.txt"
    latest.parent.mkdir(parents=True, exist_ok=True)
    latest.write_text(str(output) + "\n", encoding="utf-8")
    hashed: list[str] = []
    for path in sorted(output.rglob("*")):
        if path.is_file() and path.name != "SHA256SUMS.txt":
            hashed.append(f"{sha256(path)}  {path.relative_to(output).as_posix()}")
    (output / "SHA256SUMS.txt").write_text("\n".join(hashed) + "\n", encoding="utf-8")

    print(f"PHASE_5_STATUS={status}")
    print("STATIC_PERFORMANCE_CONTRACT_STATUS=PASS")
    print(f"ENVIRONMENT_HEALTH_PROBE_STATUS={health.get('status')}")
    print(f"MICROBENCHMARK_STATUS={micro_status.get('status')}")
    print(f"HTTP_WORKLOAD_STATUS={http_status.get('status')}")
    print(f"SYSTEM_WORKLOAD_STATUS={system_status.get('status')}")
    print(f"EVIDENCE_ROOT={output}")
    return 1 if requested_failures else 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"PHASE_5_STATUS=ERROR: {exc}", file=sys.stderr)
        raise SystemExit(2)
