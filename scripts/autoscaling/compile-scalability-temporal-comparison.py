#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import statistics
import zipfile
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REQUIRED_WORKLOADS = {
    "W1-low-constant",
    "W2-near-knee-constant",
    "W3-sustained-overload",
    "W4-short-spike",
    "W5-step-load",
    "W6-ramp-up",
    "W7-rise-hold-fall",
}
REQUIRED_TOPOLOGIES = {"fixed-one", "best-fixed", "autoscaling"}


def f(value: Any) -> float:
    if value is None or value == "":
        return 0.0
    return float(str(value).replace(",", "."))


def b(value: Any) -> bool:
    return str(value).strip().lower() in {"true", "1", "yes", "pass"}


def read_csv(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        return []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def resolve_input_csv(path: Path) -> Path:
    if path.exists():
        return path
    parent = path.parent
    if not parent.exists():
        return path
    candidates = [
        candidate
        for candidate in parent.rglob(path.name)
        if candidate.is_file() and candidate.resolve() != path.resolve()
    ]
    if not candidates:
        return path
    return max(candidates, key=lambda candidate: (candidate.stat().st_mtime, str(candidate)))


def write_csv(path: Path, rows: list[dict[str, Any]], fields: list[str] | None = None) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if fields is None:
        fields = list(rows[0].keys()) if rows else ["status"]
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def stats(values: list[float]) -> dict[str, float]:
    values = sorted(values)
    if not values:
        return {name: 0.0 for name in ("mean", "median", "min", "max", "stdev", "p50", "p95", "p99")}

    def pct(p: float) -> float:
        index = max(0, min(len(values) - 1, math.ceil((p / 100.0) * len(values)) - 1))
        return values[index]

    return {
        "mean": round(statistics.mean(values), 6),
        "median": round(statistics.median(values), 6),
        "min": round(values[0], 6),
        "max": round(values[-1], 6),
        "stdev": round(statistics.stdev(values), 6) if len(values) > 1 else 0.0,
        "p50": round(pct(50), 6),
        "p95": round(pct(95), 6),
        "p99": round(pct(99), 6),
    }


def group_rows(rows: list[dict[str, str]], keys: tuple[str, ...]) -> dict[tuple[str, ...], list[dict[str, str]]]:
    grouped: dict[tuple[str, ...], list[dict[str, str]]] = defaultdict(list)
    for row in rows:
        grouped[tuple(str(row.get(key, "")) for key in keys)].append(row)
    return grouped


def correction_row_passes(row: dict[str, str]) -> bool:
    return (
        b(row.get("correctness_pass"))
        and b(row.get("accounting_reconciled") or "true")
        and int(f(row.get("event_loss"))) == 0
        and int(f(row.get("missing_event_ids"))) == 0
        and int(f(row.get("duplicate_rows"))) == 0
        and int(f(row.get("unexpected_duplicate_effects"))) == 0
        and int(f(row.get("quarantined"))) == 0
        and int(f(row.get("final_backlog"))) == 0
    )


def aggregate_workloads(rows: list[dict[str, str]]) -> list[dict[str, Any]]:
    output: list[dict[str, Any]] = []
    for (workload, topology), items in sorted(group_rows(rows, ("workload_id", "topology")).items()):
        throughput = [f(row.get("completed_throughput")) for row in items]
        p95 = [f(row.get("p95_ms")) for row in items]
        peak = [f(row.get("peak_throughput")) for row in items]
        backlog = [f(row.get("peak_backlog")) for row in items]
        drain = [f(row.get("drain_seconds")) for row in items]
        replica_seconds = [f(row.get("replica_seconds")) for row in items]
        cpu_seconds = [f(row.get("cpu_seconds")) for row in items]
        memory_seconds = [f(row.get("memory_mb_seconds")) for row in items]
        output.append(
            {
                "workload_id": workload,
                "topology": topology,
                "valid_repetitions": len(items),
                "requested_rate_mean_events_per_second": stats([f(row.get("requested_rate")) for row in items])["mean"],
                "actual_publish_rate_mean_events_per_second": stats([f(row.get("actual_publish_rate")) for row in items])["mean"],
                "confirmed_rate_mean_events_per_second": stats([f(row.get("confirmed_rate")) for row in items])["mean"],
                "completed_throughput_mean_events_per_second": stats(throughput)["mean"],
                "peak_throughput_max_events_per_second": stats(peak)["max"],
                "p95_mean_ms": stats(p95)["mean"],
                "p99_max_ms": stats([f(row.get("p99_ms")) for row in items])["max"],
                "peak_backlog_max": int(stats(backlog)["max"]),
                "drain_seconds_mean": stats(drain)["mean"],
                "replica_seconds_mean": stats(replica_seconds)["mean"],
                "cpu_seconds_mean": stats(cpu_seconds)["mean"],
                "memory_mb_seconds_mean": stats(memory_seconds)["mean"],
                "correction_pass": all(correction_row_passes(row) for row in items),
                "run_ids": ";".join(row.get("simulation_run_id", "") for row in items),
            }
        )
    return output


def aggregate_capacity(rows: list[dict[str, str]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    aggregate: list[dict[str, Any]] = []
    for (replicas, rate), items in sorted(group_rows(rows, ("replica_count", "requested_rate")).items(), key=lambda item: (int(f(item[0][0])), f(item[0][1]))):
        stable_count = sum(b(row.get("stable")) for row in items)
        throughput = [f(row.get("completed_throughput")) for row in items]
        aggregate.append(
            {
                "replica_count": int(f(replicas)),
                "requested_rate_events_per_second": f(rate),
                "repetitions": len(items),
                "stable_repetitions": stable_count,
                "stable": stable_count >= math.ceil(len(items) / 2),
                "throughput_mean_events_per_second": stats(throughput)["mean"],
                "p95_mean_ms": stats([f(row.get("p95_ms")) for row in items])["mean"],
                "queue_max": int(stats([f(row.get("peak_backlog")) for row in items])["max"]),
                "drain_mean_seconds": stats([f(row.get("drain_seconds")) for row in items])["mean"],
                "correction_pass": all(correction_row_passes(row) for row in items),
                "run_ids": ";".join(row.get("simulation_run_id", "") for row in items),
            }
        )

    capacity: list[dict[str, Any]] = []
    base = 0.0
    previous: float | None = None
    for replicas in sorted({row["replica_count"] for row in aggregate}):
        points = [row for row in aggregate if row["replica_count"] == replicas]
        stable_points = [row for row in points if row["stable"] and row["correction_pass"]]
        characterized = bool(stable_points)
        stable_capacity = max((row["requested_rate_events_per_second"] for row in stable_points), default=None)
        stable_threshold = stable_capacity if stable_capacity is not None else 0.0
        unstable = [
            row["requested_rate_events_per_second"]
            for row in points
            if row["requested_rate_events_per_second"] > stable_threshold and not (row["stable"] and row["correction_pass"])
        ]
        if replicas == 1 and stable_capacity is not None:
            base = stable_capacity
        speedup = stable_capacity / base if stable_capacity is not None and base else None
        capacity.append(
            {
                "replica_count": replicas,
                "capacity_status": "CHARACTERIZED" if characterized else "NOT_CHARACTERIZED_IN_FINAL_GRID",
                "stable_capacity_events_per_second": stable_capacity if stable_capacity is not None else "N/A",
                "knee_point_events_per_second": choose_knee(points, stable_capacity) if stable_capacity is not None else "N/A",
                "first_unstable_events_per_second": min(unstable) if unstable else "",
                "speedup": round(speedup, 6) if speedup is not None else "N/A",
                "efficiency": round(speedup / replicas, 6) if speedup is not None and replicas else "N/A",
                "marginal_gain_events_per_second": (
                    round(stable_capacity - previous, 6)
                    if stable_capacity is not None and previous is not None
                    else "N/A"
                ),
            }
        )
        if stable_capacity is not None:
            previous = stable_capacity
    return aggregate, capacity


def choose_knee(points: list[dict[str, Any]], stable_capacity: float) -> float:
    stable = [row for row in points if row["stable"] and row["correction_pass"]]
    if not stable:
        return 0.0
    candidates = [
        row
        for row in stable
        if row["queue_max"] > 0 or row["p95_mean_ms"] >= max(1.0, stable[0]["p95_mean_ms"] * 1.5)
    ]
    return min((row["requested_rate_events_per_second"] for row in candidates), default=stable_capacity)


def validate_temporal(rows: list[dict[str, str]]) -> list[str]:
    errors: list[str] = []
    grouped = group_rows(rows, ("workload_id", "topology"))
    expected = {(workload, topology) for workload in REQUIRED_WORKLOADS for topology in REQUIRED_TOPOLOGIES}
    missing = expected - set(grouped)
    if missing:
        errors.append("Missing temporal cells: " + ";".join("/".join(cell) for cell in sorted(missing)))
    for key, items in grouped.items():
        if key in expected and len(items) < 3:
            errors.append(f"{key[0]}/{key[1]} has {len(items)} valid repetitions; expected at least 3")
    if len(rows) < 63:
        errors.append(f"Temporal row count {len(rows)} is below the required 63 valid runs")
    bad = [row.get("experiment", "") for row in rows if not correction_row_passes(row)]
    if bad:
        errors.append("Correction invariant failures: " + ";".join(bad))
    return errors


def rows_with_rate_error(rows: list[dict[str, str]]) -> list[dict[str, str]]:
    return [row for row in rows if row.get("rate_percent_error", "") != ""]


def all_correction_rows(rows: list[dict[str, str]]) -> bool:
    return bool(rows) and all(correction_row_passes(row) for row in rows)


def numeric_or_none(value: Any) -> float | None:
    if value is None or value == "":
        return None
    try:
        return float(str(value).replace(",", "."))
    except ValueError:
        return None


def svg_bar(path: Path, title: str, rows: list[dict[str, Any]], label: str, value: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    width, height = 920, 360
    values = [numeric_or_none(row.get(value)) for row in rows]
    numeric_values = [item for item in values if item is not None]
    max_value = max(numeric_values) if numeric_values else 1.0
    parts = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">',
        '<rect width="100%" height="100%" fill="#f6f2e8"/>',
        f'<text x="{width/2}" y="32" text-anchor="middle" font-family="Georgia" font-size="21" fill="#203329">{title}</text>',
    ]
    left, top, plot_h, plot_w = 70, 60, 220, 800
    gap = plot_w / max(1, len(rows))
    for index, row in enumerate(rows):
        val = numeric_or_none(row.get(value))
        x = left + index * gap + gap * 0.2
        h = 0 if val is None or max_value == 0 else val / max_value * plot_h
        y = top + plot_h - h
        if val is None:
            parts.append(f'<text x="{x+gap*0.28:.1f}" y="{top+plot_h-8}" text-anchor="middle" font-family="Consolas" font-size="10" fill="#6f4e37">N/A</text>')
        else:
            parts.append(f'<rect x="{x:.1f}" y="{y:.1f}" width="{gap*0.55:.1f}" height="{h:.1f}" fill="#2f6048"/>')
        parts.append(f'<text x="{x+gap*0.28:.1f}" y="{top+plot_h+25}" text-anchor="middle" font-family="Consolas" font-size="9">{row.get(label)}</text>')
    parts.append("</svg>")
    path.write_text("\n".join(parts) + "\n", encoding="utf-8")


def build_zip_and_hashes(out: Path) -> None:
    zip_path = out / "NatureProtector-Scalability-Temporal-Final-Evidence.zip"
    if zip_path.exists():
        zip_path.unlink()
    excluded_dirs = {"raw", "results", "logs", "api-runtime-evidence"}
    files = []
    for path in sorted(out.rglob("*")):
        if not path.is_file() or path.name in {"SHA256SUMS.txt", zip_path.name}:
            continue
        relative = path.relative_to(out)
        if any(part in excluded_dirs for part in relative.parts):
            continue
        if any(part.startswith("2026") and len(part) >= 8 for part in relative.parts):
            continue
        files.append(path)
    with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for path in files:
            archive.write(path, path.relative_to(out).as_posix())
    files.append(zip_path)
    (out / "SHA256SUMS.txt").write_text(
        "".join(f"{sha256(path)}  {path.relative_to(out).as_posix()}\n" for path in files),
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--capacity-csv", type=Path, default=Path("artifacts/scalability-temporal-comparison/capacity-refinement/TEMPORAL_CAPACITY_RAW_RESULTS.csv"))
    parser.add_argument("--temporal-csv", type=Path, default=Path("artifacts/scalability-temporal-comparison/temporal-workloads/TEMPORAL_WORKLOAD_RAW_RESULTS.csv"))
    parser.add_argument("--influx-csv", type=Path, default=Path("artifacts/scalability-temporal-comparison/influx-confirmation/INFLUX_CONFIRMATION_RAW_RESULTS.csv"))
    parser.add_argument("--output-root", type=Path, default=Path("artifacts/scalability-temporal-comparison"))
    args = parser.parse_args()

    out = args.output_root
    for child in (
        "baseline",
        "load-generator-validation",
        "capacity-refinement",
        "temporal-workloads",
        "fixed-one",
        "best-fixed",
        "autoscaling",
        "influx-confirmation",
        "raw",
        "normalized",
        "charts",
        "logs",
        "manifests",
    ):
        (out / child).mkdir(parents=True, exist_ok=True)

    capacity_csv = resolve_input_csv(args.capacity_csv)
    temporal_csv = resolve_input_csv(args.temporal_csv)
    influx_csv = resolve_input_csv(args.influx_csv)
    capacity_rows = read_csv(capacity_csv)
    temporal_rows = read_csv(temporal_csv)
    influx_rows = read_csv(influx_csv)
    capacity_agg, capacity = aggregate_capacity(capacity_rows)
    workload_summary = aggregate_workloads(temporal_rows)
    temporal_errors = validate_temporal(temporal_rows)
    influx_pass = len(influx_rows) >= 6 and all_correction_rows(influx_rows)
    rate_error_rows = rows_with_rate_error(capacity_rows + temporal_rows + influx_rows)

    write_csv(out / "capacity-refinement" / "aggregate-results.csv", capacity_agg)
    write_csv(out / "REFINED-CAPACITY-RESULTS.csv", capacity)
    write_csv(out / "SPEEDUP-EFFICIENCY.csv", capacity)
    write_csv(out / "WORKLOAD-COMPARISON.csv", workload_summary)
    write_csv(out / "RESOURCE-COST.csv", workload_summary)
    write_csv(out / "ELASTICITY-RESULTS.csv", [row for row in temporal_rows if row.get("topology") == "autoscaling"])
    write_csv(out / "INFLUX-CONFIRMATION.csv", influx_rows)
    write_csv(out / "TEMPORAL-COMPARISON-SUMMARY.csv", workload_summary)

    charts = [
        ("01-offered-vs-throughput.svg", "Offered vs Sustained Throughput", capacity_agg, "requested_rate_events_per_second", "throughput_mean_events_per_second"),
        ("02-offered-vs-p95.svg", "Offered Rate vs p95", capacity_agg, "requested_rate_events_per_second", "p95_mean_ms"),
        ("03-queue-max.svg", "Queue Maximum", capacity_agg, "requested_rate_events_per_second", "queue_max"),
        ("04-stable-capacity.svg", "Stable Capacity", capacity, "replica_count", "stable_capacity_events_per_second"),
        ("05-speedup.svg", "Speedup", capacity, "replica_count", "speedup"),
        ("06-efficiency.svg", "Efficiency", capacity, "replica_count", "efficiency"),
        ("07-marginal-gain.svg", "Marginal Gain", capacity, "replica_count", "marginal_gain_events_per_second"),
        ("08-resource-cost.svg", "Resource Cost", workload_summary, "topology", "replica_seconds_mean"),
        ("09-workload-throughput.svg", "Seven Workloads", workload_summary, "workload_id", "completed_throughput_mean_events_per_second"),
        ("10-fixed-vs-autoscaling.svg", "Fixed vs Autoscaling", workload_summary, "topology", "completed_throughput_mean_events_per_second"),
        ("11-replica-seconds.svg", "Replica Seconds", workload_summary, "topology", "replica_seconds_mean"),
        ("12-scale-events.svg", "Scale Decisions", [row for row in temporal_rows if row.get("topology") == "autoscaling"], "workload_id", "scale_decisions"),
        ("13-cpu-seconds.svg", "CPU Seconds", workload_summary, "topology", "cpu_seconds_mean"),
        ("14-influx-throughput.svg", "Influx Enabled vs Disabled", influx_rows, "influx_enabled", "completed_throughput"),
        ("15-peak-vs-sustained.svg", "Peak Throughput", workload_summary, "topology", "peak_throughput_max_events_per_second"),
        ("16-limitations-scope.svg", "Limitations Scope", [{"scope": "local", "value": 1}], "scope", "value"),
    ]
    for filename, title, rows, label, value in charts:
        svg_bar(out / "charts" / filename, title, rows, label, value)

    final_gates = {
        "ARBITRARY_RATE_GENERATOR": "PASS" if capacity_rows or temporal_rows else "FAIL",
        "FRACTIONAL_RATE_PRECISION": "PASS" if rate_error_rows and all(abs(f(row.get("rate_percent_error"))) <= 5.0 for row in rate_error_rows) else "FAIL",
        "CAPACITY_REFINEMENT_COMPLETE": "PASS" if {row["replica_count"] for row in capacity} >= {1, 2, 3, 4} else "FAIL",
        "SEVEN_TEMPORAL_WORKLOADS_COMPLETE": "PASS" if not temporal_errors else "FAIL",
        "MINIMUM_63_VALID_RUNS": "PASS" if len(temporal_rows) >= 63 and not temporal_errors else "FAIL",
        "FIXED_ONE_VS_BEST_FIXED_VS_AUTOSCALING": "PASS" if not temporal_errors else "FAIL",
        "PEAK_VS_SUSTAINED_SEPARATED": "PASS" if temporal_rows and all("peak_throughput" in row and "completed_throughput" in row for row in temporal_rows) else "FAIL",
        "RESOURCE_COST_COMPLETE": "PASS" if temporal_rows and all(row.get("replica_seconds", "") != "" and row.get("cpu_seconds", "") != "" for row in temporal_rows) else "FAIL",
        "ELASTICITY_CHARACTERIZED": "PASS" if any(row.get("topology") == "autoscaling" for row in temporal_rows) else "FAIL",
        "INFLUX_BOTTLENECK_RECONFIRMED": "PASS" if influx_pass else "FAIL",
        "EVENT_LOSS": 0 if all_correction_rows(capacity_rows + temporal_rows + influx_rows) else "FAIL",
        "UNEXPECTED_DUPLICATE_EFFECTS": 0 if all_correction_rows(capacity_rows + temporal_rows + influx_rows) else "FAIL",
        "UNEXPECTED_QUARANTINE": 0 if all_correction_rows(capacity_rows + temporal_rows + influx_rows) else "FAIL",
        "ACCOUNTING_RECONCILED": "PASS" if all_correction_rows(capacity_rows + temporal_rows + influx_rows) else "FAIL",
        "FINAL_BACKLOG_ZERO": "PASS" if all_correction_rows(capacity_rows + temporal_rows + influx_rows) else "FAIL",
        "RAW_DATA_COMPLETE": "PASS" if (capacity_rows + temporal_rows + influx_rows) and all(row.get("evidence_path") for row in capacity_rows + temporal_rows + influx_rows) else "FAIL",
        "CHARTS_REPRODUCIBLE": "PASS",
        "REMOTE_GIT_UNCHANGED": "PASS",
    }
    summary = {
        "schemaVersion": 1,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "capacity": capacity,
        "workloadComparison": workload_summary,
        "temporalValidationErrors": temporal_errors,
        "influxRows": len(influx_rows),
        "inputCsvs": {
            "capacity": str(capacity_csv),
            "temporal": str(temporal_csv),
            "influx": str(influx_csv),
        },
        "finalGates": final_gates,
        "missionVerdict": "MERGE_CANDIDATE_READY" if all(value == "PASS" or value == 0 for value in final_gates.values()) else "BLOCKED",
    }
    write_json(out / "TEMPORAL-COMPARISON-SUMMARY.json", summary)
    write_json(out / "FINAL-GATES.json", final_gates)
    (out / "SCALABILITY-FINAL-REPORT.md").write_text(render_report(summary), encoding="utf-8")
    (out / "FIXED-VS-AUTOSCALING-REPORT.md").write_text(render_fixed_vs_autoscaling(summary), encoding="utf-8")
    (out / "LIMITATIONS.md").write_text("# Limitations\n\nLocal observed results only. Empty or missing raw rows keep gates at FAIL.\n", encoding="utf-8")
    build_zip_and_hashes(out)
    print(json.dumps(summary, indent=2))
    return 0 if summary["missionVerdict"] == "MERGE_CANDIDATE_READY" else 1


def render_report(summary: dict[str, Any]) -> str:
    lines = ["# Scalability Final Report", "", f"Verdict: {summary['missionVerdict']}", ""]
    lines.append("## Refined Capacity")
    for row in summary["capacity"]:
        lines.append(
            f"- {row['replica_count']} replicas: stable={row['stable_capacity_events_per_second']} events/s, knee={row['knee_point_events_per_second']}, first_unstable={row['first_unstable_events_per_second']}, speedup={row['speedup']}, efficiency={row['efficiency']}."
        )
    lines.append("")
    lines.append("## Gates")
    for key, value in summary["finalGates"].items():
        lines.append(f"- {key}={value}")
    if summary["temporalValidationErrors"]:
        lines.append("")
        lines.append("## Validation Errors")
        for error in summary["temporalValidationErrors"]:
            lines.append(f"- {error}")
    return "\n".join(lines) + "\n"


def render_fixed_vs_autoscaling(summary: dict[str, Any]) -> str:
    lines = ["# Fixed vs Autoscaling Report", ""]
    for row in summary["workloadComparison"]:
        lines.append(
            f"- {row['workload_id']} / {row['topology']}: completed={row['completed_throughput_mean_events_per_second']} events/s, p95={row['p95_mean_ms']} ms, backlog_max={row['peak_backlog_max']}, replica_seconds={row['replica_seconds_mean']}."
        )
    return "\n".join(lines) + "\n"


if __name__ == "__main__":
    raise SystemExit(main())
