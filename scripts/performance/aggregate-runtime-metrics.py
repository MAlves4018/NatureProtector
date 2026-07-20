#!/usr/bin/env python3
"""Aggregate bounded NatureProtector performance evidence into report-ready tables.

The script consumes existing workload artifacts. It does not query production
systems and it refuses to calculate unsupported event latency or processing
throughput when the required timestamps/windows are absent.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import statistics
from collections import defaultdict
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Sequence

SCRIPT_VERSION = "1.0.0"
UNSUPPORTED = "UNSUPPORTED"
NOT_AVAILABLE = "NOT_AVAILABLE"


@dataclass(frozen=True)
class EvidenceInput:
    kind: str
    path: Path


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


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


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def parse_float(value: Any) -> float | None:
    if value is None:
        return None
    text = str(value).strip()
    if not text:
        return None
    try:
        number = float(text)
    except ValueError:
        return None
    if not math.isfinite(number):
        return None
    return number


def parse_int(value: Any) -> int | None:
    number = parse_float(value)
    if number is None:
        return None
    return int(number)


def percentile_nearest_rank(values: Sequence[float], percentile: float) -> float | None:
    cleaned = sorted(value for value in values if math.isfinite(value))
    if not cleaned:
        return None
    rank = max(0, min(len(cleaned) - 1, math.ceil((percentile / 100.0) * len(cleaned)) - 1))
    return round(cleaned[rank], 3)


def summarize_values(values: Sequence[float]) -> dict[str, Any]:
    cleaned = [value for value in values if math.isfinite(value)]
    if not cleaned:
        return {
            "count": 0,
            "min": "",
            "p50": "",
            "p90": "",
            "p95": "",
            "p99": "",
            "max": "",
            "mean": "",
            "stddev": "",
            "missingSamples": 0,
            "invalidSamples": 0,
        }
    return {
        "count": len(cleaned),
        "min": round(min(cleaned), 3),
        "p50": percentile_nearest_rank(cleaned, 50),
        "p90": percentile_nearest_rank(cleaned, 90),
        "p95": percentile_nearest_rank(cleaned, 95),
        "p99": percentile_nearest_rank(cleaned, 99),
        "max": round(max(cleaned), 3),
        "mean": round(statistics.fmean(cleaned), 3),
        "stddev": round(statistics.pstdev(cleaned), 3) if len(cleaned) > 1 else 0.0,
        "missingSamples": 0,
        "invalidSamples": 0,
    }


def safe_rel(path: Path, base: Path) -> str:
    try:
        return path.resolve().relative_to(base.resolve()).as_posix()
    except ValueError:
        return str(path)


def collect_system_run(run_dir: Path) -> dict[str, Any]:
    measurements_path = run_dir / "measurements.csv"
    summary_path = run_dir / "summary.json"
    workload_path = run_dir / "workload.json"
    rows = read_csv(measurements_path) if measurements_path.is_file() else []
    summary = read_json(summary_path) if summary_path.is_file() else {}
    workload = read_json(workload_path) if workload_path.is_file() else {}
    return {
        "kind": "system",
        "path": run_dir,
        "summary": summary,
        "workload": workload,
        "measurements": rows,
    }


def collect_http_run(run_dir: Path) -> dict[str, Any]:
    measurements_path = run_dir / "measurements.csv"
    summary_path = run_dir / "summary.json"
    manifest_path = run_dir / "run-manifest.json"
    return {
        "kind": "http",
        "path": run_dir,
        "summary": read_json(summary_path) if summary_path.is_file() else [],
        "manifest": read_json(manifest_path) if manifest_path.is_file() else {},
        "measurements": read_csv(measurements_path) if measurements_path.is_file() else [],
    }


def collect_benchmark_run(run_dir: Path) -> dict[str, Any]:
    summary_path = run_dir / "summary.json"
    manifest_path = run_dir / "run-manifest.json"
    return {
        "kind": "benchmark",
        "path": run_dir,
        "summary": read_json(summary_path) if summary_path.is_file() else {},
        "manifest": read_json(manifest_path) if manifest_path.is_file() else {},
    }


def build_latency_rows(inputs: Sequence[dict[str, Any]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    raw: list[dict[str, Any]] = []
    for item in inputs:
        if item["kind"] == "system":
            for row in item["measurements"]:
                source = str(item["path"])
                run_id = row.get("simulationRunId") or row.get("runLabel") or ""
                elapsed = parse_float(row.get("elapsedMs"))
                if elapsed is not None:
                    raw.append({
                        "sourceType": "system-workload",
                        "sourcePath": source,
                        "profile": row.get("profile", ""),
                        "runId": run_id,
                        "stage": "run_request_elapsed",
                        "durationMs": round(elapsed, 3),
                        "timestampBasis": "Stopwatch around API run request, completion wait and evidence checks",
                        "claimCeiling": "Local run request duration; not per-event or publish-to-end latency.",
                    })
                drain = parse_float(row.get("backlogDrainTimeMs"))
                if drain is not None:
                    raw.append({
                        "sourceType": "system-workload",
                        "sourcePath": source,
                        "profile": row.get("profile", ""),
                        "runId": run_id,
                        "stage": "queue_drain_after_run",
                        "durationMs": round(drain, 3),
                        "timestampBasis": "Stopwatch after run request until np.ingestion.readings queue total reached zero",
                        "claimCeiling": "Local queue drain time for configured queue only.",
                    })
                for field, stage in (
                    ("timeToFirstInboxMs", "time_to_first_inbox"),
                    ("timeToFirstProcessingAttemptMs", "time_to_first_processing_attempt"),
                    ("timeToFirstRiskAssessmentMs", "time_to_first_risk_assessment"),
                ):
                    value = parse_float(row.get(field))
                    if value is not None:
                        raw.append({
                            "sourceType": "system-workload",
                            "sourcePath": source,
                            "profile": row.get("profile", ""),
                            "runId": run_id,
                            "stage": stage,
                            "durationMs": round(value, 3),
                            "timestampBasis": "Persisted runtime timing endpoint",
                            "claimCeiling": "Time-to-first persisted signal; not full distribution.",
                        })
        elif item["kind"] == "http":
            for row in item["measurements"]:
                if row.get("phase") != "measured" or str(row.get("expectedStatusObserved")).lower() not in {"true", "1"}:
                    continue
                elapsed = parse_float(row.get("elapsedMs"))
                if elapsed is None:
                    continue
                raw.append({
                    "sourceType": "http-workload",
                    "sourcePath": str(item["path"]),
                    "profile": item["manifest"].get("profile", ""),
                    "runId": "",
                    "stage": f"http_{row.get('surface', '')}_{row.get('probe', '')}",
                    "durationMs": round(elapsed, 3),
                    "timestampBasis": "perf_counter around one GET request",
                    "claimCeiling": "Local read-only HTTP response timing only.",
                })

    summary_rows: list[dict[str, Any]] = []
    groups: dict[tuple[str, str], list[float]] = defaultdict(list)
    for row in raw:
        groups[(str(row["sourceType"]), str(row["stage"]))].append(float(row["durationMs"]))
    for (source_type, stage), values in sorted(groups.items()):
        stats = summarize_values(values)
        summary_rows.append({
            "sourceType": source_type,
            "stage": stage,
            "unit": "ms",
            "status": "MEASURED",
            **stats,
            "claimCeiling": next(row["claimCeiling"] for row in raw if row["sourceType"] == source_type and row["stage"] == stage),
        })
    summary_rows.append({
        "sourceType": "domain-event",
        "stage": "publish_to_receive",
        "unit": "ms",
        "status": UNSUPPORTED,
        "count": 0,
        "min": "",
        "p50": "",
        "p90": "",
        "p95": "",
        "p99": "",
        "max": "",
        "mean": "",
        "stddev": "",
        "missingSamples": "",
        "invalidSamples": "",
        "claimCeiling": "Not claimable: EventEnvelope has no persisted PublishedAt timestamp.",
    })
    return raw, summary_rows


def build_throughput_rows(inputs: Sequence[dict[str, Any]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    windows: list[dict[str, Any]] = []
    results: list[dict[str, Any]] = []
    for item in inputs:
        if item["kind"] == "http":
            status = item["path"] / "status.json"
            manifest = item["manifest"]
            if status.is_file():
                payload = read_json(status)
                attempts = parse_float(payload.get("measuredAttempts"))
                seconds = parse_float(payload.get("measuredWallSeconds"))
                rate = parse_float(payload.get("aggregateObservedRequestsPerSecond"))
                run_passed = str(payload.get("status", "PASS")).upper() == "PASS"
                windows.append({
                    "sourceType": "http-workload",
                    "sourcePath": str(item["path"]),
                    "profile": manifest.get("profile", ""),
                    "window": "measured",
                    "durationSeconds": seconds if seconds is not None else "",
                    "volume": attempts if attempts is not None else "",
                    "exclusions": "warmup requests excluded",
                    "validForProcessingThroughput": "false",
                })
                results.append({
                    "sourceType": "http-workload",
                    "metric": "observed_http_requests_per_second",
                    "profile": manifest.get("profile", ""),
                    "value": rate if rate is not None and run_passed else "",
                    "unit": "requests/s",
                    "status": "MEASURED" if rate is not None and run_passed else "FAILED_UNUSABLE",
                    "formula": "measured attempts / measured phase wall seconds",
                    "claimCeiling": "Observed local read-only request rate, not event processing throughput. Failed workloads are retained but not reportable as successful API rate.",
                })
    for item in inputs:
        if item["kind"] == "system":
            for row in item["measurements"]:
                elapsed = parse_float(row.get("elapsedMs"))
                accepted = parse_float(row.get("acceptedReadings"))
                windows.append({
                    "sourceType": "system-workload",
                    "sourcePath": str(item["path"]),
                    "profile": row.get("profile", ""),
                    "window": "run_request_total",
                    "durationSeconds": round(elapsed / 1000.0, 3) if elapsed else "",
                    "volume": accepted if accepted is not None else "",
                    "exclusions": "none; includes deliberate generation interval, completion wait and evidence checks",
                    "validForProcessingThroughput": "false",
                })
    results.append({
        "sourceType": "system-workload",
        "metric": "pipeline_processing_throughput",
        "profile": "",
        "value": "",
        "unit": "events/s",
        "status": UNSUPPORTED,
        "formula": "Not calculated because current run_request_total window includes generation intervals and waits.",
        "claimCeiling": "Requires explicit steady-state processing windows before use.",
    })
    return windows, results


def build_queue_rows(inputs: Sequence[dict[str, Any]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]], list[dict[str, Any]]]:
    samples: list[dict[str, Any]] = []
    drain: list[dict[str, Any]] = []
    for item in inputs:
        if item["kind"] != "system":
            continue
        for row in item["measurements"]:
            for field, metric in (
                ("queueReadyAfter", "messages_ready_after"),
                ("queueUnacknowledgedAfter", "messages_unacknowledged_after"),
                ("queueTotalAfter", "messages_total_after"),
                ("queueConsumersAfter", "consumers_after"),
            ):
                value = parse_float(row.get(field))
                if value is not None:
                    samples.append({
                        "sourcePath": str(item["path"]),
                        "profile": row.get("profile", ""),
                        "runId": row.get("simulationRunId", ""),
                        "queue": "np.ingestion.readings",
                        "samplePoint": "after_run",
                        "metric": metric,
                        "value": value,
                        "unit": "count",
                        "source": "system-capacity measurements.csv",
                    })
            drain_value = parse_float(row.get("backlogDrainTimeMs"))
            if drain_value is not None:
                drain.append({
                    "sourcePath": str(item["path"]),
                    "profile": row.get("profile", ""),
                    "runId": row.get("simulationRunId", ""),
                    "drained": row.get("backlogDrained", ""),
                    "drainTimeMs": round(drain_value, 3),
                    "queueTotalAfter": row.get("queueTotalAfter", ""),
                    "claimCeiling": "Queue drain after run request for np.ingestion.readings.",
                })
        for metric_file in sorted((item["path"] / "metrics").glob("*.json")):
            try:
                payload = read_json(metric_file)
            except (OSError, json.JSONDecodeError):
                continue
            rabbit = payload.get("rabbitmq") if isinstance(payload, dict) else None
            if not isinstance(rabbit, dict):
                continue
            for queue in rabbit.get("queues", []) or []:
                samples.append({
                    "sourcePath": str(metric_file),
                    "profile": item["summary"].get("profile", ""),
                    "runId": "",
                    "queue": queue.get("queueName", ""),
                    "samplePoint": metric_file.stem,
                    "metric": "messages_total",
                    "value": queue.get("messagesTotal", ""),
                    "unit": "count",
                    "source": "RabbitMQ observability snapshot",
                })
    grouped: dict[tuple[str, str, str], list[float]] = defaultdict(list)
    for row in samples:
        value = parse_float(row.get("value"))
        if value is not None:
            grouped[(str(row["profile"]), str(row["queue"]), str(row["metric"]))].append(value)
    summary = [
        {
            "profile": profile,
            "queue": queue,
            "metric": metric,
            "unit": "count",
            **summarize_values(values),
            "claimCeiling": "Queue samples from workload/RabbitMQ snapshots only.",
        }
        for (profile, queue, metric), values in sorted(grouped.items())
    ]
    return samples, summary, drain


def build_disposition_rows(inputs: Sequence[dict[str, Any]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    counts: list[dict[str, Any]] = []
    for item in inputs:
        if item["kind"] != "system":
            continue
        for row in item["measurements"]:
            expected = parse_float(row.get("expectedEvents")) or 0
            for metric in ("acceptedReadings", "riskAssessments", "rejected", "quarantined", "lostEvents", "attemptCount", "failedAttempts", "quarantinedAttempts"):
                value = parse_float(row.get(metric))
                if value is None:
                    continue
                counts.append({
                    "sourcePath": str(item["path"]),
                    "profile": row.get("profile", ""),
                    "runId": row.get("simulationRunId", ""),
                    "metric": metric,
                    "count": value,
                    "denominator": expected if expected else "",
                    "rate": round(value / expected, 6) if expected else "",
                    "source": "system-capacity measurements.csv",
                    "claimCeiling": "Application persisted/audit disposition count; broker redelivery/NACK only if separately captured.",
                })
    rates = [row for row in counts if row["rate"] != ""]
    return counts, rates


def build_resource_rows(inputs: Sequence[dict[str, Any]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]], list[dict[str, Any]]]:
    samples: list[dict[str, Any]] = []
    storage: list[dict[str, Any]] = []
    for item in inputs:
        if item["kind"] != "system":
            continue
        for metric_file in sorted((item["path"] / "metrics").glob("*.json")):
            try:
                payload = read_json(metric_file)
            except (OSError, json.JSONDecodeError):
                continue
            observed_at = payload.get("generatedAtUtc", "")
            for process in payload.get("processes", []) or []:
                for field, unit in (("workingSetBytes", "bytes"), ("cpuSeconds", "seconds"), ("threadCount", "count")):
                    value = parse_float(process.get(field))
                    if value is not None:
                        samples.append({
                            "sourcePath": str(metric_file),
                            "timestamp": observed_at,
                            "profile": item["summary"].get("profile", ""),
                            "service": process.get("name", ""),
                            "processId": process.get("processId", ""),
                            "metric": field,
                            "value": value,
                            "unit": unit,
                            "source": "Win32_Process/Get-Process snapshot",
                            "claimCeiling": "Opportunistic local process sample only.",
                        })
            docker_stats = payload.get("dockerStatsPath")
            if docker_stats:
                storage.append({
                    "sourcePath": str(metric_file),
                    "timestamp": observed_at,
                    "metric": "dockerStatsPath",
                    "value": docker_stats,
                    "unit": "path",
                    "claimCeiling": "Raw docker stats path retained when available; not normalized here.",
                })
    grouped: dict[tuple[str, str, str], list[float]] = defaultdict(list)
    for row in samples:
        value = parse_float(row["value"])
        if value is not None:
            grouped[(str(row["profile"]), str(row["service"]), str(row["metric"]))].append(value)
    summary = [
        {
            "profile": profile,
            "service": service,
            "metric": metric,
            "unit": next(row["unit"] for row in samples if row["profile"] == profile and row["service"] == service and row["metric"] == metric),
            **summarize_values(values),
            "claimCeiling": "Local resource samples, not cross-environment capacity comparison.",
        }
        for (profile, service, metric), values in sorted(grouped.items())
    ]
    return samples, summary, storage


def build_api_rows(inputs: Sequence[dict[str, Any]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    samples: list[dict[str, Any]] = []
    for item in inputs:
        if item["kind"] != "http":
            continue
        for row in item["measurements"]:
            samples.append({
                "sourcePath": str(item["path"]),
                "profile": item["manifest"].get("profile", ""),
                "phase": row.get("phase", ""),
                "route": row.get("url", ""),
                "surface": row.get("surface", ""),
                "probe": row.get("probe", ""),
                "statusCode": row.get("statusCode", ""),
                "expectedStatusObserved": row.get("expectedStatusObserved", ""),
                "elapsedMs": row.get("elapsedMs", ""),
                "byteCount": row.get("byteCount", ""),
                "errorKind": row.get("errorKind", ""),
            })
    groups: dict[tuple[str, str], list[dict[str, Any]]] = defaultdict(list)
    for row in samples:
        if row["phase"] == "measured":
            groups[(str(row["surface"]), str(row["probe"]))].append(row)
    summary: list[dict[str, Any]] = []
    for (surface, probe), rows in sorted(groups.items()):
        successful = [row for row in rows if str(row["expectedStatusObserved"]).lower() in {"true", "1"}]
        latencies = [value for value in (parse_float(row["elapsedMs"]) for row in successful) if value is not None]
        stats = summarize_values(latencies)
        summary.append({
            "surface": surface,
            "probe": probe,
            "count": len(rows),
            "success": len(successful),
            "errors": len(rows) - len(successful),
            "p50ElapsedMs": stats["p50"],
            "p95ElapsedMs": stats["p95"],
            "p99ElapsedMs": stats["p99"],
            "maxElapsedMs": stats["max"],
            "payloadBytesTotal": sum(parse_int(row.get("byteCount")) or 0 for row in rows),
            "claimCeiling": "Read-only local HTTP route measurement.",
        })
    return samples, summary


def build_benchmark_rows(inputs: Sequence[dict[str, Any]]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for item in inputs:
        if item["kind"] != "benchmark":
            continue
        summary = item["summary"]
        for benchmark in summary.get("benchmarks", []) or []:
            rows.append({
                "sourcePath": str(item["path"]),
                "profile": summary.get("profile", ""),
                "status": summary.get("status", ""),
                "type": benchmark.get("type", ""),
                "method": benchmark.get("method", ""),
                "parameters": benchmark.get("parameters", ""),
                "meanNanoseconds": benchmark.get("meanNanoseconds", ""),
                "standardDeviationNanoseconds": benchmark.get("standardDeviationNanoseconds", ""),
                "allocatedBytesPerOperation": benchmark.get("allocatedBytesPerOperation", ""),
                "claimCeiling": "BenchmarkDotNet microbenchmark only.",
            })
    return rows


def write_method_files(output_root: Path) -> None:
    (output_root / "05-latency" / "LATENCY_METHOD.md").write_text(
        "# Latency Method\n\n"
        "Only explicit elapsed-duration samples are summarized. Full publish-to-receive and publish-to-end latency remain `UNSUPPORTED` until a persisted `PublishedAt` or compatible stage timestamps exist.\n",
        encoding="utf-8",
    )
    (output_root / "05-latency" / "LATENCY_VALIDATION.md").write_text(
        "# Latency Validation\n\nPercentiles use nearest-rank over finite samples. Unsupported stages are emitted as rows instead of being imputed.\n",
        encoding="utf-8",
    )
    (output_root / "06-throughput" / "THROUGHPUT_METHOD.md").write_text(
        "# Throughput Method\n\nHTTP observed request rate is calculated only over the measured phase. System processing throughput is not calculated from run request duration because that window includes deliberate generation intervals and waits.\n",
        encoding="utf-8",
    )
    (output_root / "07-queues" / "QUEUE_METHOD.md").write_text(
        "# Queue Method\n\nQueue metrics come from system workload measurements and RabbitMQ observability snapshots. Samples retain source paths and are not converted into capacity claims.\n",
        encoding="utf-8",
    )
    (output_root / "08-dispositions" / "DISPOSITION_METHOD.md").write_text(
        "# Disposition Method\n\nDisposition counts are taken from persisted audit/timing rows when present. Broker-level NACK/redelivery/DLQ rates are not inferred from application counts.\n",
        encoding="utf-8",
    )
    (output_root / "09-resources" / "RESOURCE_METHOD.md").write_text(
        "# Resource Method\n\nResource metrics are local process/container snapshots. They are useful for reproducibility context but not for cross-environment capacity comparison.\n",
        encoding="utf-8",
    )
    (output_root / "10-api" / "API_METHOD.md").write_text(
        "# API Method\n\nAPI metrics are read-only HTTP request measurements from the workload harness. They do not measure asynchronous runtime completion unless a route explicitly performs that work.\n",
        encoding="utf-8",
    )


def write_svg_bar_chart(path: Path, title: str, rows: Sequence[dict[str, Any]], label_field: str, value_field: str, unit: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    values: list[tuple[str, float]] = []
    for row in rows[:12]:
        value = parse_float(row.get(value_field))
        if value is not None:
            values.append((str(row.get(label_field, ""))[:32], value))
    width = 920
    height = max(220, 90 + len(values) * 34)
    max_value = max([value for _, value in values], default=1.0)
    lines = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" role="img" aria-label="{title}">',
        '<rect width="100%" height="100%" fill="#f8f5ed"/>',
        f'<text x="28" y="34" font-family="Georgia, serif" font-size="22" fill="#243124">{title}</text>',
        f'<text x="28" y="60" font-family="Consolas, monospace" font-size="12" fill="#5d665d">Unit: {unit}. Generated by aggregate-runtime-metrics.py.</text>',
    ]
    y = 92
    for label, value in values:
        bar_width = int(650 * (value / max_value)) if max_value > 0 else 0
        lines.append(f'<text x="28" y="{y + 16}" font-family="Consolas, monospace" font-size="12" fill="#243124">{label}</text>')
        lines.append(f'<rect x="300" y="{y}" width="{bar_width}" height="22" fill="#527a56"/>')
        lines.append(f'<text x="{310 + bar_width}" y="{y + 16}" font-family="Consolas, monospace" font-size="12" fill="#243124">{round(value, 3)}</text>')
        y += 34
    if not values:
        lines.append('<text x="28" y="108" font-family="Consolas, monospace" font-size="13" fill="#8a3d2a">No supported numeric samples.</text>')
    lines.append("</svg>")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_report_tables(output_root: Path, canonical_rows: Sequence[dict[str, Any]], latency_summary: Sequence[dict[str, Any]], throughput_results: Sequence[dict[str, Any]], queue_summary: Sequence[dict[str, Any]], api_summary: Sequence[dict[str, Any]], benchmark_rows: Sequence[dict[str, Any]]) -> None:
    report = output_root / "24-report-ready" / "report"
    report.mkdir(parents=True, exist_ok=True)
    write_csv(report / "CANONICAL_METRICS.csv", canonical_rows, [
        "metric",
        "status",
        "value",
        "unit",
        "source",
        "claimCeiling",
    ])
    write_csv(report / "LATENCY_RESULTS.csv", latency_summary, [
        "sourceType",
        "stage",
        "unit",
        "status",
        "count",
        "min",
        "p50",
        "p90",
        "p95",
        "p99",
        "max",
        "mean",
        "stddev",
        "missingSamples",
        "invalidSamples",
        "claimCeiling",
    ])
    write_csv(report / "THROUGHPUT_RESULTS.csv", throughput_results, [
        "sourceType",
        "metric",
        "profile",
        "value",
        "unit",
        "status",
        "formula",
        "claimCeiling",
    ])
    write_csv(report / "QUEUE_RESULTS.csv", queue_summary, [
        "profile",
        "queue",
        "metric",
        "unit",
        "count",
        "min",
        "p50",
        "p90",
        "p95",
        "p99",
        "max",
        "mean",
        "stddev",
        "missingSamples",
        "invalidSamples",
        "claimCeiling",
    ])
    write_csv(report / "API_RESULTS.csv", api_summary, [
        "surface",
        "probe",
        "count",
        "success",
        "errors",
        "p50ElapsedMs",
        "p95ElapsedMs",
        "p99ElapsedMs",
        "maxElapsedMs",
        "payloadBytesTotal",
        "claimCeiling",
    ])
    write_csv(report / "B0_RESULTS.csv", [row for row in benchmark_rows if row.get("profile") == "B0"], [
        "sourcePath",
        "profile",
        "status",
        "type",
        "method",
        "parameters",
        "meanNanoseconds",
        "standardDeviationNanoseconds",
        "allocatedBytesPerOperation",
        "claimCeiling",
    ])
    write_csv(report / "B1_RESULTS.csv", [row for row in benchmark_rows if row.get("profile") == "B1"], [
        "sourcePath",
        "profile",
        "status",
        "type",
        "method",
        "parameters",
        "meanNanoseconds",
        "standardDeviationNanoseconds",
        "allocatedBytesPerOperation",
        "claimCeiling",
    ])
    (report / "ALLOWED_CLAIMS.md").write_text(
        "# Allowed Claims\n\n"
        "- Local read-only HTTP response timings when backed by `API_RESULTS.csv`.\n"
        "- BenchmarkDotNet microbenchmark timings when backed by `B0_RESULTS.csv` or `B1_RESULTS.csv`.\n"
        "- Local queue drain/accounting measurements when backed by workload artifacts.\n",
        encoding="utf-8",
    )
    (report / "FORBIDDEN_CLAIMS.md").write_text(
        "# Forbidden Claims\n\n"
        "- Production readiness or SLO/SLA compliance.\n"
        "- Publish-to-projection latency without persisted `PublishedAt` and compatible stage timestamps.\n"
        "- Sustained or maximum system throughput from run request duration.\n"
        "- Scientific equivalence between NP Score, FWI, KBDI, IPMA, EFFIS, PIR or RCM.\n",
        encoding="utf-8",
    )
    (report / "OPEN_LIMITATIONS.md").write_text(
        "# Open Limitations\n\n"
        "- The mandatory handover was unavailable in this workspace.\n"
        "- Full event latency and steady-state processing throughput require additional runtime timestamps/windows.\n"
        "- Resource samples are local and not normalized across environments.\n",
        encoding="utf-8",
    )
    (report / "REPORT_INTEGRATION_HANDOVER.md").write_text(
        "# Report Integration Handover\n\n"
        "| Report area | Action | Source | Claim permitted | Limitation |\n"
        "| --- | --- | --- | --- | --- |\n"
        "| Performance evidence | Insert bounded metrics table | `CANONICAL_METRICS.csv` | local engineering measurement | not production capacity |\n"
        "| Latency | Replace unsupported end-to-end text | `LATENCY_RESULTS.csv` | supported stages only | no PublishedAt |\n"
        "| Throughput | Use observed HTTP request rate only | `THROUGHPUT_RESULTS.csv` | local request rate | not pipeline throughput |\n"
        "| Queues | Add queue/drain table when samples exist | `QUEUE_RESULTS.csv` | queue snapshot/drain | not broker capacity |\n",
        encoding="utf-8",
    )


def build_canonical_metrics(latency_summary: Sequence[dict[str, Any]], throughput_results: Sequence[dict[str, Any]], queue_summary: Sequence[dict[str, Any]], disposition_counts: Sequence[dict[str, Any]], resource_summary: Sequence[dict[str, Any]], api_summary: Sequence[dict[str, Any]], benchmark_rows: Sequence[dict[str, Any]]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for item in latency_summary:
        if item.get("status") == "MEASURED" and item.get("p95") != "":
            rows.append({
                "metric": f"latency.{item['sourceType']}.{item['stage']}.p95",
                "status": "MEASURED",
                "value": item.get("p95", ""),
                "unit": item.get("unit", ""),
                "source": "05-latency/LATENCY_SUMMARY.csv",
                "claimCeiling": item.get("claimCeiling", ""),
            })
    for item in throughput_results:
        rows.append({
            "metric": f"throughput.{item['metric']}",
            "status": item.get("status", ""),
            "value": item.get("value", ""),
            "unit": item.get("unit", ""),
            "source": "06-throughput/THROUGHPUT_RESULTS.csv",
            "claimCeiling": item.get("claimCeiling", ""),
        })
    for item in queue_summary:
        if item.get("max") != "":
            rows.append({
                "metric": f"queue.{item['queue']}.{item['metric']}.max",
                "status": "MEASURED",
                "value": item.get("max", ""),
                "unit": item.get("unit", ""),
                "source": "07-queues/QUEUE_SUMMARY.csv",
                "claimCeiling": item.get("claimCeiling", ""),
            })
    for item in disposition_counts[:20]:
        rows.append({
            "metric": f"disposition.{item['metric']}",
            "status": "MEASURED",
            "value": item.get("count", ""),
            "unit": "count",
            "source": "08-dispositions/DISPOSITION_COUNTS.csv",
            "claimCeiling": item.get("claimCeiling", ""),
        })
    for item in resource_summary[:20]:
        rows.append({
            "metric": f"resource.{item['service']}.{item['metric']}.max",
            "status": "MEASURED",
            "value": item.get("max", ""),
            "unit": item.get("unit", ""),
            "source": "09-resources/RESOURCE_SUMMARY.csv",
            "claimCeiling": item.get("claimCeiling", ""),
        })
    for item in api_summary:
        rows.append({
            "metric": f"api.{item['surface']}.{item['probe']}.p95",
            "status": "MEASURED",
            "value": item.get("p95ElapsedMs", ""),
            "unit": "ms",
            "source": "10-api/API_ROUTE_SUMMARY.csv",
            "claimCeiling": item.get("claimCeiling", ""),
        })
    for item in benchmark_rows:
        rows.append({
            "metric": f"benchmark.{item['profile']}.{item['type']}.{item['method']}.mean",
            "status": "MEASURED" if item.get("meanNanoseconds") != "" else item.get("status", ""),
            "value": item.get("meanNanoseconds", ""),
            "unit": "ns",
            "source": "11-b0/BENCHMARK_RESULTS.csv or 12-b1/BENCHMARK_RESULTS.csv",
            "claimCeiling": item.get("claimCeiling", ""),
        })
    return rows


def write_manifest(output_root: Path) -> None:
    files = []
    for path in sorted(output_root.rglob("*")):
        if path.is_file() and path.name != "SHA256SUMS.txt":
            files.append({
                "path": path.relative_to(output_root).as_posix(),
                "bytes": path.stat().st_size,
                "sha256": sha256(path),
            })
    write_csv(output_root / "MANIFEST.csv", files, ["path", "bytes", "sha256"])
    sums = [f"{item['sha256']}  {item['path']}" for item in files]
    (output_root / "SHA256SUMS.txt").write_text("\n".join(sums) + "\n", encoding="utf-8")


def aggregate(args: argparse.Namespace) -> dict[str, Any]:
    output_root = args.output_root.resolve()
    output_root.mkdir(parents=True, exist_ok=True)
    inputs: list[dict[str, Any]] = []
    for path in args.system_run_dir:
        inputs.append(collect_system_run(path.resolve()))
    for path in args.http_run_dir:
        inputs.append(collect_http_run(path.resolve()))
    for path in args.benchmark_dir:
        inputs.append(collect_benchmark_run(path.resolve()))

    raw_latency, latency_summary = build_latency_rows(inputs)
    throughput_windows, throughput_results = build_throughput_rows(inputs)
    queue_samples, queue_summary, drain_results = build_queue_rows(inputs)
    disposition_counts, disposition_rates = build_disposition_rows(inputs)
    resource_samples, resource_summary, storage_growth = build_resource_rows(inputs)
    api_samples, api_summary = build_api_rows(inputs)
    benchmark_rows = build_benchmark_rows(inputs)
    canonical_rows = build_canonical_metrics(
        latency_summary,
        throughput_results,
        queue_summary,
        disposition_counts,
        resource_summary,
        api_summary,
        benchmark_rows,
    )

    write_csv(output_root / "05-latency" / "RAW_LATENCY_SAMPLES.csv", raw_latency, [
        "sourceType", "sourcePath", "profile", "runId", "stage", "durationMs", "timestampBasis", "claimCeiling"
    ])
    write_csv(output_root / "05-latency" / "LATENCY_SUMMARY.csv", latency_summary, [
        "sourceType", "stage", "unit", "status", "count", "min", "p50", "p90", "p95", "p99", "max", "mean", "stddev", "missingSamples", "invalidSamples", "claimCeiling"
    ])
    write_csv(output_root / "06-throughput" / "THROUGHPUT_WINDOWS.csv", throughput_windows, [
        "sourceType", "sourcePath", "profile", "window", "durationSeconds", "volume", "exclusions", "validForProcessingThroughput"
    ])
    write_csv(output_root / "06-throughput" / "THROUGHPUT_RESULTS.csv", throughput_results, [
        "sourceType", "metric", "profile", "value", "unit", "status", "formula", "claimCeiling"
    ])
    write_csv(output_root / "07-queues" / "QUEUE_SAMPLES.csv", queue_samples, [
        "sourcePath", "profile", "runId", "queue", "samplePoint", "metric", "value", "unit", "source"
    ])
    write_csv(output_root / "07-queues" / "QUEUE_SUMMARY.csv", queue_summary, [
        "profile", "queue", "metric", "unit", "count", "min", "p50", "p90", "p95", "p99", "max", "mean", "stddev", "missingSamples", "invalidSamples", "claimCeiling"
    ])
    write_csv(output_root / "07-queues" / "DRAIN_RESULTS.csv", drain_results, [
        "sourcePath", "profile", "runId", "drained", "drainTimeMs", "queueTotalAfter", "claimCeiling"
    ])
    write_csv(output_root / "08-dispositions" / "DISPOSITION_COUNTS.csv", disposition_counts, [
        "sourcePath", "profile", "runId", "metric", "count", "denominator", "rate", "source", "claimCeiling"
    ])
    write_csv(output_root / "08-dispositions" / "DISPOSITION_RATES.csv", disposition_rates, [
        "sourcePath", "profile", "runId", "metric", "count", "denominator", "rate", "source", "claimCeiling"
    ])
    for name in ("DUPLICATE_EVIDENCE.csv", "OUT_OF_ORDER_EVIDENCE.csv", "LAG_DELAY_EVIDENCE.csv"):
        write_csv(output_root / "08-dispositions" / name, [], ["status", "source", "limitation"])
    write_csv(output_root / "09-resources" / "RESOURCE_SAMPLES.csv", resource_samples, [
        "sourcePath", "timestamp", "profile", "service", "processId", "metric", "value", "unit", "source", "claimCeiling"
    ])
    write_csv(output_root / "09-resources" / "RESOURCE_SUMMARY.csv", resource_summary, [
        "profile", "service", "metric", "unit", "count", "min", "p50", "p90", "p95", "p99", "max", "mean", "stddev", "missingSamples", "invalidSamples", "claimCeiling"
    ])
    write_csv(output_root / "09-resources" / "STORAGE_GROWTH.csv", storage_growth, [
        "sourcePath", "timestamp", "metric", "value", "unit", "claimCeiling"
    ])
    write_csv(output_root / "10-api" / "API_REQUEST_SAMPLES.csv", api_samples, [
        "sourcePath", "profile", "phase", "route", "surface", "probe", "statusCode", "expectedStatusObserved", "elapsedMs", "byteCount", "errorKind"
    ])
    write_csv(output_root / "10-api" / "API_ROUTE_SUMMARY.csv", api_summary, [
        "surface", "probe", "count", "success", "errors", "p50ElapsedMs", "p95ElapsedMs", "p99ElapsedMs", "maxElapsedMs", "payloadBytesTotal", "claimCeiling"
    ])
    write_csv(output_root / "11-b0" / "BENCHMARK_RESULTS.csv", [row for row in benchmark_rows if row.get("profile") == "B0"], [
        "sourcePath", "profile", "status", "type", "method", "parameters", "meanNanoseconds", "standardDeviationNanoseconds", "allocatedBytesPerOperation", "claimCeiling"
    ])
    write_csv(output_root / "12-b1" / "BENCHMARK_RESULTS.csv", [row for row in benchmark_rows if row.get("profile") == "B1"], [
        "sourcePath", "profile", "status", "type", "method", "parameters", "meanNanoseconds", "standardDeviationNanoseconds", "allocatedBytesPerOperation", "claimCeiling"
    ])
    write_method_files(output_root)
    write_report_tables(output_root, canonical_rows, latency_summary, throughput_results, queue_summary, api_summary, benchmark_rows)
    write_csv(output_root / "24-report-ready" / "report" / "METRICS_AVAILABILITY_MATRIX.csv", [
        {
            "metric": row["metric"],
            "exists_directly": row["status"] == "MEASURED",
            "can_be_calculated": row["status"] == "MEASURED",
            "requires_new_run": "depends_on_input",
            "requires_instrumentation": row["status"] != "MEASURED",
            "source": row["source"],
            "formula": "",
            "timestamp_quality": "see method file",
            "confidence": "bounded",
            "claim_ceiling": row["claimCeiling"],
        }
        for row in canonical_rows
    ], ["metric", "exists_directly", "can_be_calculated", "requires_new_run", "requires_instrumentation", "source", "formula", "timestamp_quality", "confidence", "claim_ceiling"])

    figures = output_root / "22-report-assets" / "figures"
    tables = output_root / "22-report-assets" / "tables"
    tables.mkdir(parents=True, exist_ok=True)
    write_svg_bar_chart(figures / "latency-p95.svg", "Latency p95 By Supported Stage", latency_summary, "stage", "p95", "ms")
    write_svg_bar_chart(figures / "api-p95.svg", "API Route p95", api_summary, "probe", "p95ElapsedMs", "ms")
    write_svg_bar_chart(figures / "queue-max.svg", "Queue Max Observed Samples", queue_summary, "metric", "max", "count")
    write_csv(output_root / "22-report-assets" / "FIGURE_REGISTER.csv", [
        {"figure": "latency-p95.svg", "sourceCsv": "05-latency/LATENCY_SUMMARY.csv", "claim": "Supported local latency stages only."},
        {"figure": "api-p95.svg", "sourceCsv": "10-api/API_ROUTE_SUMMARY.csv", "claim": "Read-only API route p95."},
        {"figure": "queue-max.svg", "sourceCsv": "07-queues/QUEUE_SUMMARY.csv", "claim": "Queue snapshot maxima only."},
    ], ["figure", "sourceCsv", "claim"])
    write_csv(output_root / "22-report-assets" / "TABLE_REGISTER.csv", [
        {"table": "CANONICAL_METRICS.csv", "source": "24-report-ready/report/CANONICAL_METRICS.csv", "claim": "Bounded canonical metric ledger."},
        {"table": "LATENCY_RESULTS.csv", "source": "24-report-ready/report/LATENCY_RESULTS.csv", "claim": "Supported stage latency."},
        {"table": "THROUGHPUT_RESULTS.csv", "source": "24-report-ready/report/THROUGHPUT_RESULTS.csv", "claim": "Observed HTTP rate and unsupported processing-throughput boundary."},
    ], ["table", "source", "claim"])
    (output_root / "22-report-assets" / "CAPTIONS.md").write_text(
        "# Captions\n\n"
        "- `latency-p95.svg`: p95 for stages with explicit elapsed samples; unsupported end-to-end stages are excluded from the figure and retained in the CSV.\n"
        "- `api-p95.svg`: p95 read-only local route response times.\n"
        "- `queue-max.svg`: maximum observed queue snapshot values, not capacity.\n",
        encoding="utf-8",
    )

    summary = {
        "generatedAtUtc": utc_now(),
        "scriptVersion": SCRIPT_VERSION,
        "status": "PASS",
        "inputs": [{"kind": item["kind"], "path": str(item["path"])} for item in inputs],
        "counts": {
            "latencySamples": len(raw_latency),
            "latencySummaryRows": len(latency_summary),
            "throughputRows": len(throughput_results),
            "queueSamples": len(queue_samples),
            "dispositionCounts": len(disposition_counts),
            "resourceSamples": len(resource_samples),
            "apiSamples": len(api_samples),
            "benchmarkRows": len(benchmark_rows),
            "canonicalMetrics": len(canonical_rows),
        },
        "limitations": [
            "No full publish-to-receive latency is calculated without persisted PublishedAt.",
            "No processing throughput is calculated from run request duration.",
            "Resource samples are local and opportunistic.",
        ],
    }
    write_json(output_root / "aggregation-summary.json", summary)
    write_manifest(output_root)
    return summary


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--system-run-dir", type=Path, action="append", default=[])
    parser.add_argument("--http-run-dir", type=Path, action="append", default=[])
    parser.add_argument("--benchmark-dir", type=Path, action="append", default=[])
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    summary = aggregate(args)
    print(f"METRICS_AGGREGATION_STATUS={summary['status']}")
    print(f"METRICS_AGGREGATION_OUTPUT={args.output_root.resolve()}")
    print(f"CANONICAL_METRICS={summary['counts']['canonicalMetrics']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
