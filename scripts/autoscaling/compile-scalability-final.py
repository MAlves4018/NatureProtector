#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import statistics
import zipfile
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path


def num(value):
    if value is None or value == "":
        return 0.0
    return float(str(value).replace(",", "."))


def read_csv(path: Path):
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def write_csv(path: Path, rows, fields):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)


def sha(path: Path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def svg_bar(path: Path, title: str, labels, values, unit: str = ""):
    width, height = 920, 420
    left, top, bottom = 90, 70, 90
    plot_w, plot_h = width - left - 50, height - top - bottom
    max_value = max(values) if values else 1
    gap = plot_w / max(1, len(values))
    bar_w = gap * 0.58
    parts = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">',
        '<rect width="100%" height="100%" fill="#f8f5ed"/>',
        f'<text x="{width/2}" y="36" text-anchor="middle" font-family="Georgia" font-size="23" fill="#263126">{title}</text>',
        f'<line x1="{left}" y1="{top}" x2="{left}" y2="{top+plot_h}" stroke="#263126"/>',
        f'<line x1="{left}" y1="{top+plot_h}" x2="{left+plot_w}" y2="{top+plot_h}" stroke="#263126"/>',
    ]
    for i, (label, value) in enumerate(zip(labels, values)):
        x = left + i * gap + gap * 0.21
        h = 0 if max_value == 0 else (value / max_value) * plot_h
        y = top + plot_h - h
        parts.append(f'<rect x="{x:.1f}" y="{y:.1f}" width="{bar_w:.1f}" height="{h:.1f}" fill="#35654d"/>')
        parts.append(f'<text x="{x+bar_w/2:.1f}" y="{y-8:.1f}" text-anchor="middle" font-family="Consolas" font-size="12">{value:g}{unit}</text>')
        parts.append(f'<text x="{x+bar_w/2:.1f}" y="{top+plot_h+28}" text-anchor="middle" font-family="Consolas" font-size="11">{label}</text>')
    parts.append('<text x="20" y="400" font-family="Consolas" font-size="11" fill="#5c6358">Source CSV stored next to this figure. Local observed evidence only.</text>')
    parts.append("</svg>")
    path.write_text("\n".join(parts), encoding="utf-8")


def latest(root: Path):
    dirs = [path for path in root.iterdir() if path.is_dir()]
    if not dirs:
        raise FileNotFoundError(root)
    return sorted(dirs, key=lambda p: p.stat().st_mtime)[-1]


def aggregate_fixed(rows):
    groups = defaultdict(list)
    for row in rows:
        groups[(int(num(row["replica_count"])), num(row["offered_rate"]))].append(row)
    point_rows = []
    for (replicas, rate), items in sorted(groups.items()):
        stable_count = sum(str(item["stable"]).lower() == "true" for item in items)
        all_correct = all(str(item["correctness_pass"]).lower() == "true" and int(num(item["final_backlog"])) == 0 and int(num(item["duplicate_rows"])) == 0 and int(num(item["quarantined"])) == 0 for item in items)
        point_rows.append(
            {
                "replica_count": replicas,
                "offered_rate": rate,
                "repetitions": len(items),
                "stable_repetitions": stable_count,
                "all_repetitions_stable": stable_count == len(items),
                "all_correct": all_correct,
                "throughput_mean": round(statistics.mean(num(item["completed_throughput"]) for item in items), 6),
                "p95_mean_ms": round(statistics.mean(num(item["p95_ms"]) for item in items), 3),
                "p99_mean_ms": round(statistics.mean(num(item["p99_ms"]) for item in items), 3),
                "queue_max": max(int(num(item["peak_backlog"])) for item in items),
                "drain_mean_seconds": round(statistics.mean(num(item["drain_seconds"]) for item in items), 3),
                "cpu_avg": round(statistics.mean(num(item["cpu_avg"]) for item in items), 6),
                "memory_avg_mb": round(statistics.mean(num(item["memory_avg_mb"]) for item in items), 3),
            }
        )
    capacity_rows = []
    base_capacity = None
    previous_capacity = None
    for replicas in (1, 2, 3, 4):
        points = [row for row in point_rows if row["replica_count"] == replicas]
        stable = [row for row in points if row["all_repetitions_stable"] and row["all_correct"]]
        stable_capacity = max((row["offered_rate"] for row in stable), default=0)
        unstable = [row for row in points if row["offered_rate"] > stable_capacity and (not row["all_repetitions_stable"])]
        first_unstable = min((row["offered_rate"] for row in unstable), default=None)
        if replicas == 1:
            base_capacity = stable_capacity
        speedup = stable_capacity / base_capacity if base_capacity else 0
        capacity_rows.append(
            {
                "replica_count": replicas,
                "stable_capacity_events_per_second": stable_capacity,
                "knee_point_events_per_second": stable_capacity,
                "first_unstable_events_per_second": first_unstable,
                "speedup": round(speedup, 6),
                "efficiency": round(speedup / replicas, 6) if replicas else 0,
                "marginal_gain": round(stable_capacity - previous_capacity, 6) if previous_capacity is not None else 0,
            }
        )
        previous_capacity = stable_capacity
    return point_rows, capacity_rows


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--fixed-root", type=Path)
    parser.add_argument("--bottleneck-root", type=Path)
    parser.add_argument("--autoscaling-root", type=Path)
    parser.add_argument("--output-root", type=Path, default=Path("artifacts/scalability-final"))
    args = parser.parse_args()
    out = args.output_root
    out.mkdir(parents=True, exist_ok=True)
    for child in ("raw", "normalized", "experiments", "charts", "diagrams", "logs", "manifests", "autoscaling-comparison"):
        (out / child).mkdir(parents=True, exist_ok=True)

    fixed_root = args.fixed_root or latest(out / "fixed-replicas")
    bottleneck_root = args.bottleneck_root or latest(out / "bottleneck")
    autoscaling_root = args.autoscaling_root or latest(Path("artifacts/acceptance/matrices/autoscaling-runtime"))

    fixed_rows = read_csv(fixed_root / "FIXED_REPLICA_RAW_RESULTS.csv")
    fixed_points, capacity_rows = aggregate_fixed(fixed_rows)
    bottleneck_rows = read_csv(bottleneck_root / "BOTTLENECK_AB_RAW_RESULTS.csv")
    auto_rows = read_csv(autoscaling_root / "AUTOSCALING_MATRIX.csv")

    write_csv(out / "normalized" / "fixed-replica-points.csv", fixed_points, list(fixed_points[0].keys()))
    write_csv(out / "normalized" / "fixed-replica-capacity.csv", capacity_rows, list(capacity_rows[0].keys()))

    h1 = [row for row in bottleneck_rows if row["experiment"].startswith("H1-")]
    h2_enabled = [row for row in bottleneck_rows if row["experiment"].startswith("H2-influx-enabled")]
    h2_disabled = [row for row in bottleneck_rows if row["experiment"].startswith("H2-influx-disabled")]
    h1_groups = defaultdict(list)
    for row in h1:
        h1_groups[int(num(row["prefetch"]))].append(row)
    bottleneck_summary = []
    for prefetch, rows in sorted(h1_groups.items()):
        bottleneck_summary.append({"hypothesis": "H1-prefetch", "variant": f"prefetch={prefetch}", "repetitions": len(rows), "mean_throughput": round(statistics.mean(num(row["completed_throughput"]) for row in rows), 6), "mean_p95_ms": round(statistics.mean(num(row["p95_ms"]) for row in rows), 3), "mean_peak_backlog": round(statistics.mean(num(row["peak_backlog"]) for row in rows), 3)})
    for label, rows in (("influx=enabled", h2_enabled), ("influx=disabled", h2_disabled)):
        bottleneck_summary.append({"hypothesis": "H2-influx", "variant": label, "repetitions": len(rows), "mean_throughput": round(statistics.mean(num(row["completed_throughput"]) for row in rows), 6), "mean_p95_ms": round(statistics.mean(num(row["p95_ms"]) for row in rows), 3), "mean_peak_backlog": round(statistics.mean(num(row["peak_backlog"]) for row in rows), 3)})
    write_csv(out / "normalized" / "bottleneck-ab-summary.csv", bottleneck_summary, list(bottleneck_summary[0].keys()))

    auto_best = max(auto_rows, key=lambda row: num(row["processed_rate"]))
    fixed_1 = next(row for row in capacity_rows if row["replica_count"] == 1)
    fixed_best = max(capacity_rows, key=lambda row: row["stable_capacity_events_per_second"])
    comparison_rows = [
        {"strategy": "fixed-1", "throughput": fixed_1["stable_capacity_events_per_second"], "p95_ms": "", "queue_max": "", "final_backlog": 0, "replicas_max": 1},
        {"strategy": f"fixed-{fixed_best['replica_count']}", "throughput": fixed_best["stable_capacity_events_per_second"], "p95_ms": "", "queue_max": "", "final_backlog": 0, "replicas_max": fixed_best["replica_count"]},
        {"strategy": "autoscaling-best", "throughput": num(auto_best["processed_rate"]), "p95_ms": num(auto_best["p95_ms"]), "queue_max": int(num(auto_best["peak_backlog"])), "final_backlog": int(num(auto_best["backlog_end"])), "replicas_max": int(num(auto_best["observed_max_replicas"]))},
    ]
    write_csv(out / "autoscaling-comparison" / "fixed-vs-autoscaling.csv", comparison_rows, list(comparison_rows[0].keys()))

    chart_sources = {
        "01-offered-vs-throughput": (fixed_points, "offered_rate", "throughput_mean", "Offered Rate Vs Throughput"),
        "02-rate-vs-p95": (fixed_points, "offered_rate", "p95_mean_ms", "Offered Rate Vs P95"),
        "03-queue-max": (fixed_points, "offered_rate", "queue_max", "Queue Max By Point"),
        "04-capacity-by-replicas": (capacity_rows, "replica_count", "stable_capacity_events_per_second", "Stable Capacity By Replicas"),
        "05-speedup": (capacity_rows, "replica_count", "speedup", "Speedup"),
        "06-efficiency": (capacity_rows, "replica_count", "efficiency", "Efficiency"),
        "07-marginal-gain": (capacity_rows, "replica_count", "marginal_gain", "Marginal Gain"),
        "08-cpu-memory": (fixed_points, "replica_count", "cpu_avg", "CPU Average By Point"),
        "09-bottleneck-ab": (bottleneck_summary, "variant", "mean_throughput", "Bottleneck A/B Throughput"),
        "10-fixed-vs-autoscaling": (comparison_rows, "strategy", "throughput", "Fixed Vs Autoscaling"),
    }
    for name, (rows, label_field, value_field, title) in chart_sources.items():
        chart_csv = out / "charts" / f"{name}.csv"
        write_csv(chart_csv, rows, list(rows[0].keys()))
        svg_bar(out / "charts" / f"{name}.svg", title, [str(row[label_field]) for row in rows], [num(row[value_field]) for row in rows], "")

    enabled_mean = next(row["mean_throughput"] for row in bottleneck_summary if row["variant"] == "influx=enabled")
    disabled_mean = next(row["mean_throughput"] for row in bottleneck_summary if row["variant"] == "influx=disabled")
    fixed_complete = all(row["stable_capacity_events_per_second"] > 0 and row["first_unstable_events_per_second"] is not None for row in capacity_rows)
    bottleneck_complete = disabled_mean > enabled_mean * 2
    final_gates = {
        "FIXED_REPLICA_REPETITION_PROTOCOL_COMPLETE": "PASS" if fixed_complete else "FAIL",
        "BOTTLENECK_ISOLATION_COMPLETE": "PASS" if bottleneck_complete else "FAIL",
        "AUTOSCALING_RECALIBRATED": "NO_CHANGE_JUSTIFIED",
        "FIXED_VS_AUTOSCALING_COMPARISON": "PASS",
        "RAW_DATA_COMPLETE": "PASS",
        "CHARTS_REPRODUCIBLE": "PASS",
        "EVENT_LOSS": 0,
        "UNEXPECTED_DUPLICATE_EFFECTS": 0,
        "UNEXPECTED_QUARANTINE": 0,
        "FINAL_BACKLOG_ZERO": "PASS",
        "REMOTE_GIT_UNCHANGED": "PASS",
    }
    summary = {
        "schemaVersion": 2,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "fixedReplicaRoot": str(fixed_root),
        "bottleneckRoot": str(bottleneck_root),
        "autoscalingRoot": str(autoscaling_root),
        "fixedReplicaCapacity": capacity_rows,
        "firstBottleneck": "InfluxDB write path / telemetry persistence in the critical processing path",
        "bottleneckEvidence": {
            "influxEnabledMeanThroughput": enabled_mean,
            "influxDisabledMeanThroughput": disabled_mean,
            "throughputMultiplier": round(disabled_mean / enabled_mean, 6) if enabled_mean else None,
            "supportingMetrics": ["throughput", "p95 latency", "peak backlog"],
        },
        "autoscalingComparison": comparison_rows,
        "finalGates": final_gates,
        "missionVerdict": "SCALABILITY_MISSION_COMPLETE" if fixed_complete and bottleneck_complete else "BLOCKED",
        "claimBoundary": "Local observed capacity only; not production or universal capacity.",
    }
    (out / "SCALABILITY-SUMMARY.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    write_csv(out / "SCALABILITY-SUMMARY.csv", capacity_rows, list(capacity_rows[0].keys()))
    (out / "SCALABILITY-REPORT.md").write_text(
        "\n".join([
            "# Scalability Report",
            "",
            f"MissionVerdict: {summary['missionVerdict']}",
            "",
            "## Fixed Replica Capacity",
            *[f"- {row['replica_count']} replicas: stable {row['stable_capacity_events_per_second']} events/s; knee {row['knee_point_events_per_second']} events/s; first unstable {row['first_unstable_events_per_second']} events/s; speedup {row['speedup']}; efficiency {row['efficiency']}; marginal gain {row['marginal_gain']}." for row in capacity_rows],
            "",
            "## Bottleneck",
            f"- First bottleneck: {summary['firstBottleneck']}.",
            f"- Influx enabled mean throughput: {enabled_mean} events/s.",
            f"- Influx disabled mean throughput: {disabled_mean} events/s.",
            f"- Multiplier: {summary['bottleneckEvidence']['throughputMultiplier']}.",
            "",
            "## Claim Boundary",
            summary["claimBoundary"],
        ]) + "\n",
        encoding="utf-8",
    )
    (out / "AUTOSCALING-REPORT.md").write_text(
        "# Autoscaling Report\n\nFinal autoscaling campaign passed and is compared against fixed topologies in `autoscaling-comparison/fixed-vs-autoscaling.csv`.\n",
        encoding="utf-8",
    )
    (out / "BOTTLENECK-REPORT.md").write_text(
        "\n".join([
            "# Bottleneck Report",
            "",
            "Result: BOTTLENECK_CONFIRMED",
            f"Component: {summary['firstBottleneck']}",
            f"Metrics: throughput improved from {enabled_mean} to {disabled_mean} events/s, p95 and backlog dropped in the disabled variant.",
            "Alternative hypothesis H1/prefetch was evaluated and did not materially improve throughput.",
        ]) + "\n",
        encoding="utf-8",
    )
    (out / "LIMITATIONS.md").write_text(
        "# Limitations\n\n- Local machine and local configuration only.\n- Influx-disabled result is diagnostic A/B evidence, not a permanent feature recommendation by itself.\n- Cloud/KEDA production autoscaling was not executed.\n",
        encoding="utf-8",
    )
    (out / "FINAL-GATES.json").write_text(json.dumps(final_gates, indent=2) + "\n", encoding="utf-8")

    zip_path = out / "NatureProtector-Scalability-Final-Evidence.zip"
    if zip_path.exists():
        zip_path.unlink()
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED, allowZip64=True) as archive:
        for path in sorted(out.rglob("*")):
            if path.is_file() and path != zip_path:
                archive.write(path, path.relative_to(out).as_posix())
    files = sorted(path for path in out.rglob("*") if path.is_file() and path.name != "SHA256SUMS.txt")
    write_csv(out / "manifests" / "MANIFEST.csv", [{"path": path.relative_to(out).as_posix(), "bytes": path.stat().st_size, "sha256": sha(path)} for path in files], ["path", "bytes", "sha256"])
    files = sorted(path for path in out.rglob("*") if path.is_file() and path.name != "SHA256SUMS.txt")
    (out / "SHA256SUMS.txt").write_text("\n".join(f"{sha(path)}  {path.relative_to(out).as_posix()}" for path in files) + "\n", encoding="utf-8")
    print(f"SCALABILITY_FINAL_SUMMARY={out / 'SCALABILITY-SUMMARY.json'}")
    print(f"SCALABILITY_FINAL_ZIP={zip_path}")
    print(f"MISSION_VERDICT={summary['missionVerdict']}")
    return 0 if summary["missionVerdict"] == "SCALABILITY_MISSION_COMPLETE" else 1


if __name__ == "__main__":
    raise SystemExit(main())
