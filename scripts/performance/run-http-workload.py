#!/usr/bin/env python3
"""Run a bounded, read-only HTTP latency/throughput workload for NatureProtector.

The workload uses only GET requests. It records warm-up separately, preserves every
measurement, calculates request-level percentiles, and makes no production-capacity
or end-to-end event-latency claim.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import csv
import hashlib
import json
import math
import os
import platform
import socket
import sys
import time
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Sequence
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

SCRIPT_VERSION = "1.0.0"

PROFILE_SPECS: dict[str, dict[str, int | str]] = {
    "Calibration": {
        "warmupPerProbe": 1,
        "measuredPerProbe": 3,
        "concurrency": 1,
        "purpose": "Connectivity and harness calibration only.",
    },
    "B0": {
        "warmupPerProbe": 2,
        "measuredPerProbe": 20,
        "concurrency": 1,
        "purpose": "Sequential local read-path baseline.",
    },
    "B1": {
        "warmupPerProbe": 3,
        "measuredPerProbe": 50,
        "concurrency": 4,
        "purpose": "Bounded concurrent local read-path comparison.",
    },
    "B2": {
        "warmupPerProbe": 5,
        "measuredPerProbe": 100,
        "concurrency": 8,
        "purpose": "Deeper bounded local read-path measurement; not a stress test.",
    },
}


@dataclass(frozen=True)
class Probe:
    surface: str
    name: str
    path: str
    expected_statuses: tuple[int, ...]
    purpose: str


DEFAULT_API_PROBES = (
    Probe("api", "api-health", "/health", (200,), "API health availability."),
    Probe("api", "areas-list", "/api/control/areas", (200,), "Anonymous area-list read path."),
    Probe("api", "area-detail", "/api/control/areas/{areaCode}", (200,), "Anonymous area-detail read path."),
    Probe(
        "api",
        "area-grid-cells",
        "/api/control/areas/{areaCode}/grid-cells?take=25",
        (200,),
        "Bounded anonymous grid-cell read path.",
    ),
    Probe(
        "api",
        "area-sensor-nodes",
        "/api/control/areas/{areaCode}/sensor-nodes",
        (200,),
        "Anonymous sensor-node read path.",
    ),
    Probe(
        "api",
        "area-alerts-active",
        "/api/control/areas/{areaCode}/alerts/active",
        (200,),
        "Anonymous active-alert read path.",
    ),
)

DEFAULT_WEB_PROBES = (
    Probe("web", "web-root", "/", (200,), "Web root availability."),
    Probe("web", "web-ui-v2", "/ui-v2", (200,), "UI v2 route availability."),
)


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def compact_utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def write_csv(path: Path, rows: Sequence[dict[str, Any]], fieldnames: Sequence[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow(row)


def percentile(values: Sequence[float], p: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    rank = max(0, min(len(ordered) - 1, math.ceil((p / 100.0) * len(ordered)) - 1))
    return round(float(ordered[rank]), 3)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def url_join(base: str, path: str) -> str:
    return base.rstrip("/") + (path if path.startswith("/") else "/" + path)


def execute_request(
    sequence: int,
    phase: str,
    probe: Probe,
    url: str,
    timeout_seconds: float,
    headers: dict[str, str],
) -> dict[str, Any]:
    started = time.perf_counter()
    status_code: int | None = None
    byte_count = 0
    error_kind = ""
    error_message = ""
    try:
        request = Request(url, method="GET", headers=headers)
        with urlopen(request, timeout=timeout_seconds) as response:
            status_code = int(response.status)
            byte_count = len(response.read())
    except HTTPError as exc:
        status_code = int(exc.code)
        try:
            byte_count = len(exc.read())
        except Exception:
            byte_count = 0
    except (URLError, TimeoutError, socket.timeout, OSError) as exc:
        error_kind = type(exc).__name__
        error_message = str(exc)
    elapsed_ms = (time.perf_counter() - started) * 1000.0
    expected = status_code in probe.expected_statuses if status_code is not None else False
    return {
        "generatedAtUtc": utc_now(),
        "sequence": sequence,
        "phase": phase,
        "surface": probe.surface,
        "probe": probe.name,
        "method": "GET",
        "url": url,
        "expectedStatusCodes": "|".join(str(code) for code in probe.expected_statuses),
        "statusCode": status_code if status_code is not None else "",
        "expectedStatusObserved": expected,
        "elapsedMs": round(elapsed_ms, 3),
        "byteCount": byte_count,
        "errorKind": error_kind,
        "errorMessage": error_message,
        "purpose": probe.purpose,
    }


def build_probes(api_base_url: str, web_base_url: str, area_code: str, include_web: bool) -> list[tuple[Probe, str]]:
    rows: list[tuple[Probe, str]] = []
    for probe in DEFAULT_API_PROBES:
        rows.append((probe, url_join(api_base_url, probe.path.format(areaCode=area_code))))
    if include_web:
        for probe in DEFAULT_WEB_PROBES:
            rows.append((probe, url_join(web_base_url, probe.path)))
    return rows


def run_phase(
    phase: str,
    probes: Sequence[tuple[Probe, str]],
    repetitions: int,
    concurrency: int,
    timeout_seconds: float,
    headers: dict[str, str],
) -> tuple[list[dict[str, Any]], float]:
    tasks: list[tuple[int, Probe, str]] = []
    sequence = 0
    for _ in range(repetitions):
        for probe, url in probes:
            sequence += 1
            tasks.append((sequence, probe, url))
    started = time.perf_counter()
    rows: list[dict[str, Any]] = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, concurrency)) as executor:
        futures = [
            executor.submit(execute_request, seq, phase, probe, url, timeout_seconds, headers)
            for seq, probe, url in tasks
        ]
        for future in concurrent.futures.as_completed(futures):
            rows.append(future.result())
    wall_seconds = time.perf_counter() - started
    rows.sort(key=lambda row: int(row["sequence"]))
    return rows, wall_seconds


def login_for_token(api_base_url: str, username: str, password: str, timeout_seconds: float) -> str:
    payload = json.dumps({"usernameOrEmail": username, "password": password}).encode("utf-8")
    request = Request(
        url_join(api_base_url, "/api/users-roles/login"),
        data=payload,
        method="POST",
        headers={
            "Accept": "application/json",
            "Content-Type": "application/json",
            "User-Agent": "NatureProtector-HTTP-Workload/1.0",
        },
    )
    with urlopen(request, timeout=timeout_seconds) as response:
        body = json.loads(response.read().decode("utf-8"))
    token = str((body or {}).get("token") or "").strip()
    if not token:
        raise RuntimeError("Login returned no bearer token for HTTP workload.")
    return token


def build_request_headers(args: argparse.Namespace) -> tuple[dict[str, str], str]:
    headers = {
        "Accept": "application/json",
        "User-Agent": "NatureProtector-HTTP-Workload/1.0",
    }
    evidence_run_id = os.getenv(args.evidence_run_id_env, "").strip()
    if evidence_run_id:
        headers["X-NP-Evidence-Run-Id"] = evidence_run_id

    token = os.getenv(args.bearer_token_env, "").strip()
    username = os.getenv(args.username_env, "").strip()
    password = os.getenv(args.password_env, "").strip()
    auth_mode = "none"
    if not token and username and password:
        token = login_for_token(args.api_base_url, username, password, args.timeout_seconds)
        auth_mode = "login"
    elif token:
        auth_mode = "bearer-env"

    if token:
        headers["Authorization"] = f"Bearer {token}"
    elif args.auth_required:
        raise RuntimeError(
            f"HTTP workload requires authentication. Set {args.bearer_token_env} or both "
            f"{args.username_env}/{args.password_env}."
        )
    return headers, auth_mode


def summarize(rows: Sequence[dict[str, Any]], measured_wall_seconds: float) -> list[dict[str, Any]]:
    groups: dict[tuple[str, str], list[dict[str, Any]]] = {}
    for row in rows:
        if row["phase"] != "measured":
            continue
        groups.setdefault((str(row["surface"]), str(row["probe"])), []).append(row)
    summaries: list[dict[str, Any]] = []
    total_measured = sum(len(group) for group in groups.values())
    for (surface, probe_name), group in sorted(groups.items()):
        success = [row for row in group if row["expectedStatusObserved"]]
        latencies = [float(row["elapsedMs"]) for row in success]
        duration_seconds = measured_wall_seconds * (len(group) / total_measured) if total_measured else 0.0
        summaries.append(
            {
                "surface": surface,
                "probe": probe_name,
                "attempts": len(group),
                "expectedStatusCount": len(success),
                "unexpectedStatusOrErrorCount": len(group) - len(success),
                "successRatePercent": round((len(success) / len(group)) * 100.0, 3) if group else 0.0,
                "minElapsedMs": round(min(latencies), 3) if latencies else "",
                "avgElapsedMs": round(sum(latencies) / len(latencies), 3) if latencies else "",
                "p50ElapsedMs": percentile(latencies, 50) or "",
                "p95ElapsedMs": percentile(latencies, 95) or "",
                "p99ElapsedMs": percentile(latencies, 99) or "",
                "maxElapsedMs": round(max(latencies), 3) if latencies else "",
                "observedRequestsPerSecond": round(len(group) / duration_seconds, 3) if duration_seconds > 0 else "",
                "responseBytesTotal": sum(int(row["byteCount"]) for row in group),
                "purpose": group[0]["purpose"],
            }
        )
    return summaries


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=PROFILE_SPECS, default="Calibration")
    parser.add_argument("--api-base-url", default="http://localhost:5254")
    parser.add_argument("--web-base-url", default="http://localhost:5173")
    parser.add_argument("--area-code", default="proenca-a-nova")
    parser.add_argument("--include-web", action="store_true")
    parser.add_argument("--warmup-per-probe", type=int)
    parser.add_argument("--measured-per-probe", type=int)
    parser.add_argument("--concurrency", type=int)
    parser.add_argument("--timeout-seconds", type=float, default=10.0)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--auth-required", action="store_true")
    parser.add_argument("--bearer-token-env", default="NP_PERFORMANCE_AUTH_TOKEN")
    parser.add_argument("--username-env", default="NP_PERFORMANCE_USERNAME")
    parser.add_argument("--password-env", default="NP_PERFORMANCE_PASSWORD")
    parser.add_argument("--evidence-run-id-env", default="NP_EVIDENCE_RUN_ID")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    spec = dict(PROFILE_SPECS[args.profile])
    warmup = args.warmup_per_probe if args.warmup_per_probe is not None else int(spec["warmupPerProbe"])
    measured = args.measured_per_probe if args.measured_per_probe is not None else int(spec["measuredPerProbe"])
    concurrency = args.concurrency if args.concurrency is not None else int(spec["concurrency"])
    if warmup < 0 or measured < 1 or concurrency < 1 or args.timeout_seconds <= 0:
        raise ValueError("Invalid workload parameters")
    output = (args.output or Path("artifacts/performance") / f"http-{args.profile}-{compact_utc_now()}").resolve()
    output.mkdir(parents=True, exist_ok=True)
    probes = build_probes(args.api_base_url, args.web_base_url, args.area_code, args.include_web)
    headers, auth_mode = build_request_headers(args)
    manifest = {
        "scriptVersion": SCRIPT_VERSION,
        "generatedAtUtc": utc_now(),
        "profile": args.profile,
        "purpose": spec["purpose"],
        "apiBaseUrl": args.api_base_url,
        "webBaseUrl": args.web_base_url if args.include_web else "not-included",
        "areaCode": args.area_code,
        "warmupPerProbe": warmup,
        "measuredPerProbe": measured,
        "concurrency": concurrency,
        "timeoutSeconds": args.timeout_seconds,
        "probeCount": len(probes),
        "dryRun": args.dry_run,
        "authentication": {
            "mode": auth_mode,
            "tokenPersisted": False,
            "authRequired": args.auth_required,
        },
        "environment": {
            "python": sys.version.split()[0],
            "platform": platform.platform(),
            "machine": platform.machine(),
        },
        "claimBoundary": "Read-only local HTTP response measurements only; not event end-to-end latency, production capacity, scalability, or scientific validation.",
        "probes": [
            {**asdict(probe), "expected_statuses": list(probe.expected_statuses), "url": url} for probe, url in probes
        ],
    }
    write_json(output / "run-manifest.json", manifest)
    if args.dry_run:
        write_json(output / "status.json", {"status": "DRY_RUN", "httpCallsExecuted": 0})
        (output / "summary.md").write_text(
            "# HTTP workload dry run\n\n"
            f"- Profile: `{args.profile}`\n"
            f"- Probes: `{len(probes)}`\n"
            f"- Warm-up requests planned: `{warmup * len(probes)}`\n"
            f"- Measured requests planned: `{measured * len(probes)}`\n"
            f"- Concurrency: `{concurrency}`\n",
            encoding="utf-8",
        )
        return 0

    warmup_rows, warmup_wall = run_phase("warmup", probes, warmup, concurrency, args.timeout_seconds, headers)
    measured_rows, measured_wall = run_phase("measured", probes, measured, concurrency, args.timeout_seconds, headers)
    rows = warmup_rows + measured_rows
    fields = [
        "generatedAtUtc",
        "sequence",
        "phase",
        "surface",
        "probe",
        "method",
        "url",
        "expectedStatusCodes",
        "statusCode",
        "expectedStatusObserved",
        "elapsedMs",
        "byteCount",
        "errorKind",
        "errorMessage",
        "purpose",
    ]
    write_csv(output / "measurements.csv", rows, fields)
    write_json(output / "measurements.json", rows)
    summaries = summarize(rows, measured_wall)
    summary_fields = [
        "surface",
        "probe",
        "attempts",
        "expectedStatusCount",
        "unexpectedStatusOrErrorCount",
        "successRatePercent",
        "minElapsedMs",
        "avgElapsedMs",
        "p50ElapsedMs",
        "p95ElapsedMs",
        "p99ElapsedMs",
        "maxElapsedMs",
        "observedRequestsPerSecond",
        "responseBytesTotal",
        "purpose",
    ]
    write_csv(output / "summary.csv", summaries, summary_fields)
    write_json(output / "summary.json", summaries)
    expected = sum(int(row["expectedStatusCount"]) for row in summaries)
    attempts = sum(int(row["attempts"]) for row in summaries)
    status = "PASS" if attempts > 0 and expected == attempts else "FAIL"
    run_status = {
        "status": status,
        "warmupWallSeconds": round(warmup_wall, 3),
        "measuredWallSeconds": round(measured_wall, 3),
        "measuredAttempts": attempts,
        "expectedStatusAttempts": expected,
        "unexpectedStatusOrErrorAttempts": attempts - expected,
        "aggregateObservedRequestsPerSecond": round(attempts / measured_wall, 3) if measured_wall > 0 else None,
        "authenticationMode": auth_mode,
    }
    write_json(output / "status.json", run_status)
    lines = [
        "# HTTP latency and throughput workload",
        "",
        f"- Status: **{status}**",
        f"- Profile: `{args.profile}`",
        f"- Probes: `{len(probes)}`",
        f"- Warm-up requests: `{len(warmup_rows)}`",
        f"- Measured requests: `{attempts}`",
        f"- Concurrency: `{concurrency}`",
        f"- Aggregate observed requests/s: `{run_status['aggregateObservedRequestsPerSecond']}`",
        "",
        "| Surface | Probe | Success | p50 ms | p95 ms | p99 ms | Observed req/s |",
        "| --- | --- | ---: | ---: | ---: | ---: | ---: |",
    ]
    for row in summaries:
        lines.append(
            f"| {row['surface']} | {row['probe']} | {row['expectedStatusCount']}/{row['attempts']} | "
            f"{row['p50ElapsedMs']} | {row['p95ElapsedMs']} | {row['p99ElapsedMs']} | {row['observedRequestsPerSecond']} |"
        )
    lines += [
        "",
        "## Claim boundary",
        "",
        "These are read-only local HTTP response measurements. They do not establish event publish-to-projection latency, production capacity, production SLOs, scalability, or scientific validity.",
    ]
    (output / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    hashed = []
    for path in sorted(output.rglob("*")):
        if path.is_file() and path.name != "SHA256SUMS.txt":
            hashed.append(f"{sha256(path)}  {path.relative_to(output).as_posix()}")
    (output / "SHA256SUMS.txt").write_text("\n".join(hashed) + "\n", encoding="utf-8")
    print(f"HTTP_WORKLOAD_STATUS={status}")
    print(f"HTTP_WORKLOAD_OUTPUT={output}")
    print(f"HTTP_MEASURED_ATTEMPTS={attempts}")
    print(f"HTTP_UNEXPECTED_ATTEMPTS={attempts - expected}")
    return 0 if status == "PASS" else 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"HTTP_WORKLOAD_STATUS=ERROR: {exc}", file=sys.stderr)
        raise SystemExit(2)
