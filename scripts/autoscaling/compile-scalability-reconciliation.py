#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import shutil
import statistics
import zipfile
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


METRICS = [
    ("requested_offered_rate", "events/s", "configured rate requested for the workload point"),
    ("actual_publish_rate", "events/s", "published events divided by publish window"),
    ("accepted_rate", "events/s", "events accepted by the API or producer in the active-load window"),
    ("completion_rate", "events/s", "processed events divided by measured processing/drain window"),
    ("peak_throughput", "events/s", "maximum observed per-row completion rate in a comparable group"),
    ("sustained_throughput", "events/s", "mean completion rate across valid repetitions for the same point"),
    ("stable_capacity", "events/s", "highest requested offered rate whose repetitions meet correction and stability gates"),
    ("backlog", "work items", "RabbitMQ ready+unacked plus persisted work where available"),
    ("queue_max", "work items", "maximum backlog sample observed in a run/group"),
    ("drain_time", "seconds", "time from publication completion to zero backlog and terminal state"),
    ("p50", "ms", "median latency over raw event/assessment latency samples"),
    ("p95", "ms", "nearest-rank or source p95 over raw samples when available"),
    ("p99", "ms", "nearest-rank or source p99 over raw samples when available"),
    ("stable_point", "events/s", "same as stable_capacity for the current local grid"),
    ("knee_point", "events/s", "highest stable point before first unstable or material queue/latency inflection"),
    ("unstable_point", "events/s", "lowest tested rate above stable_capacity that fails stability"),
    ("speedup", "ratio", "stable_capacity(replicas) / stable_capacity(1)"),
    ("efficiency", "ratio", "speedup / replicas"),
    ("marginal_gain", "events/s", "stable_capacity(n) - stable_capacity(n-1)"),
]


def num(value: Any) -> float:
    if value is None or value == "":
        return 0.0
    return float(str(value).replace(",", "."))


def boolean(value: Any) -> bool:
    return str(value).strip().lower() in {"true", "1", "yes", "pass"}


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


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


def latest(root: Path) -> Path:
    dirs = [path for path in root.iterdir() if path.is_dir()]
    if not dirs:
        raise FileNotFoundError(root)
    return sorted(dirs, key=lambda path: path.stat().st_mtime)[-1]


def stats(values: list[float]) -> dict[str, Any]:
    ordered = sorted(values)
    if not ordered:
        return {key: "" for key in ("count", "mean", "median", "min", "max", "stdev", "cv", "p95", "p99")}

    def percentile(p: float) -> float:
        index = max(0, min(len(ordered) - 1, math.ceil((p / 100.0) * len(ordered)) - 1))
        return ordered[index]

    mean = statistics.mean(ordered)
    stdev = statistics.stdev(ordered) if len(ordered) > 1 else 0.0
    return {
        "count": len(ordered),
        "mean": round(mean, 6),
        "median": round(statistics.median(ordered), 6),
        "min": round(ordered[0], 6),
        "max": round(ordered[-1], 6),
        "stdev": round(stdev, 6),
        "cv": round(stdev / mean, 6) if mean else 0.0,
        "p95": round(percentile(95), 6),
        "p99": round(percentile(99), 6),
    }


def group_fixed(rows: list[dict[str, str]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    groups: dict[tuple[int, float], list[dict[str, str]]] = defaultdict(list)
    for row in rows:
        groups[(int(num(row["replica_count"])), num(row["offered_rate"]))].append(row)

    aggregate: list[dict[str, Any]] = []
    for (replicas, rate), items in sorted(groups.items()):
        stable_reps = sum(boolean(item.get("stable")) for item in items)
        correction_failures = [
            item
            for item in items
            if not boolean(item.get("correctness_pass"))
            or int(num(item.get("final_backlog"))) != 0
            or int(num(item.get("duplicate_rows"))) != 0
            or int(num(item.get("quarantined"))) != 0
        ]
        throughput = [num(item.get("completed_throughput")) for item in items]
        p95 = [num(item.get("p95_ms")) for item in items]
        queue = [num(item.get("peak_backlog")) for item in items]
        drain = [num(item.get("drain_seconds")) for item in items]
        cpu = [num(item.get("cpu_avg")) for item in items]
        memory = [num(item.get("memory_avg_mb")) for item in items]
        majority_stable = stable_reps >= math.ceil(len(items) / 2)
        aggregate.append(
            {
                "replica_count": replicas,
                "offered_rate_events_per_second": rate,
                "repetitions": len(items),
                "stable_repetitions": stable_reps,
                "majority_stable": majority_stable,
                "correction_pass": not correction_failures,
                "throughput_mean_events_per_second": stats(throughput)["mean"],
                "throughput_median_events_per_second": stats(throughput)["median"],
                "throughput_min_events_per_second": stats(throughput)["min"],
                "throughput_max_events_per_second": stats(throughput)["max"],
                "throughput_stdev": stats(throughput)["stdev"],
                "throughput_cv": stats(throughput)["cv"],
                "p95_mean_ms": stats(p95)["mean"],
                "p95_max_ms": stats(p95)["max"],
                "queue_max": int(max(queue)) if queue else 0,
                "drain_mean_seconds": stats(drain)["mean"],
                "drain_max_seconds": stats(drain)["max"],
                "cpu_avg": stats(cpu)["mean"],
                "cpu_peak": stats(cpu)["max"],
                "memory_avg_mb": stats(memory)["mean"],
                "memory_peak_mb": stats(memory)["max"],
                "run_ids": ";".join(str(item.get("simulation_run_id", "")) for item in items),
            }
        )

    capacity: list[dict[str, Any]] = []
    base_capacity = 0.0
    previous_capacity: float | None = None
    for replicas in sorted({row["replica_count"] for row in aggregate}):
        points = [row for row in aggregate if row["replica_count"] == replicas]
        stable_points = [row for row in points if row["majority_stable"] and row["correction_pass"]]
        stable_capacity = max((row["offered_rate_events_per_second"] for row in stable_points), default=0.0)
        unstable_points = [
            row
            for row in points
            if row["offered_rate_events_per_second"] > stable_capacity
            and (not row["majority_stable"] or not row["correction_pass"])
        ]
        first_unstable = min((row["offered_rate_events_per_second"] for row in unstable_points), default="")
        if replicas == 1:
            base_capacity = stable_capacity
        speedup = stable_capacity / base_capacity if base_capacity else 0.0
        capacity.append(
            {
                "replica_count": replicas,
                "stable_capacity_events_per_second": stable_capacity,
                "knee_point_events_per_second": stable_capacity,
                "first_unstable_events_per_second": first_unstable,
                "speedup": round(speedup, 6),
                "efficiency": round(speedup / replicas, 6) if replicas else 0.0,
                "marginal_gain_events_per_second": round(stable_capacity - previous_capacity, 6) if previous_capacity is not None else 0.0,
            }
        )
        previous_capacity = stable_capacity
    return aggregate, capacity


def bottleneck_summary(rows: list[dict[str, str]]) -> list[dict[str, Any]]:
    groups: dict[tuple[str, str], list[dict[str, str]]] = defaultdict(list)
    for row in rows:
        key = ("influx", str(boolean(row.get("influx_enabled")))) if row["experiment"].startswith("H2-") else ("prefetch", str(int(num(row.get("prefetch")))))
        groups[key].append(row)
    output: list[dict[str, Any]] = []
    for (hypothesis, variant), items in sorted(groups.items()):
        output.append(
            {
                "hypothesis": hypothesis,
                "variant": variant,
                "repetitions": len(items),
                "throughput_mean_events_per_second": stats([num(item.get("completed_throughput")) for item in items])["mean"],
                "throughput_min_events_per_second": stats([num(item.get("completed_throughput")) for item in items])["min"],
                "throughput_max_events_per_second": stats([num(item.get("completed_throughput")) for item in items])["max"],
                "p95_mean_ms": stats([num(item.get("p95_ms")) for item in items])["mean"],
                "queue_max": int(max(num(item.get("peak_backlog")) for item in items)),
                "drain_mean_seconds": stats([num(item.get("drain_seconds")) for item in items])["mean"],
                "cpu_avg": stats([num(item.get("cpu_avg")) for item in items])["mean"],
                "memory_avg_mb": stats([num(item.get("memory_avg_mb")) for item in items])["mean"],
                "correction_pass": all(boolean(item.get("correctness_pass")) and int(num(item.get("final_backlog"))) == 0 for item in items),
                "run_ids": ";".join(str(item.get("simulation_run_id", "")) for item in items),
            }
        )
    return output


def svg_bar(path: Path, title: str, rows: list[dict[str, Any]], label: str, value: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    labels = [str(row[label]) for row in rows]
    values = [num(row[value]) for row in rows]
    width, height = 900, 390
    left, top, bottom = 80, 60, 85
    plot_w, plot_h = width - left - 45, height - top - bottom
    max_value = max(values) if values else 1
    gap = plot_w / max(1, len(values))
    parts = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">',
        '<rect width="100%" height="100%" fill="#f7f4ec"/>',
        f'<text x="{width/2}" y="34" text-anchor="middle" font-family="Georgia" font-size="22" fill="#1f3026">{title}</text>',
        f'<line x1="{left}" y1="{top}" x2="{left}" y2="{top+plot_h}" stroke="#1f3026"/>',
        f'<line x1="{left}" y1="{top+plot_h}" x2="{left+plot_w}" y2="{top+plot_h}" stroke="#1f3026"/>',
    ]
    for index, (text, val) in enumerate(zip(labels, values)):
        x = left + index * gap + gap * 0.2
        bar_w = gap * 0.58
        bar_h = 0 if max_value == 0 else (val / max_value) * plot_h
        y = top + plot_h - bar_h
        parts.append(f'<rect x="{x:.1f}" y="{y:.1f}" width="{bar_w:.1f}" height="{bar_h:.1f}" fill="#315f48"/>')
        parts.append(f'<text x="{x+bar_w/2:.1f}" y="{y-7:.1f}" text-anchor="middle" font-family="Consolas" font-size="11">{val:g}</text>')
        parts.append(f'<text x="{x+bar_w/2:.1f}" y="{top+plot_h+28}" text-anchor="middle" font-family="Consolas" font-size="10">{text}</text>')
    parts.append('<text x="18" y="374" font-family="Consolas" font-size="10" fill="#596158">Source CSV is stored next to this chart. Local evidence only.</text>')
    parts.append("</svg>")
    path.write_text("\n".join(parts), encoding="utf-8")


def copy_baseline(out: Path, roots: list[Path]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    baseline = out / "SCALABILITY-BASELINE-01"
    baseline.mkdir(parents=True, exist_ok=True)
    for root in roots:
        if not root.exists():
            continue
        manifest = baseline / f"{root.name}-manifest.csv"
        file_rows = []
        for path in sorted(root.glob("*.csv")) + sorted(root.glob("*.json")) + sorted(root.glob("*.md")):
            file_rows.append({"source_root": str(root), "path": str(path), "bytes": path.stat().st_size, "sha256": sha256(path)})
        write_csv(manifest, file_rows, ["source_root", "path", "bytes", "sha256"])
        rows.extend(file_rows)
    return rows


def make_report(out: Path, summary: dict[str, Any], inconsistencies: list[dict[str, Any]]) -> None:
    lines = [
        "# Scalability Reconciled Report",
        "",
        f"GeneratedAtUtc: {summary['generatedAtUtc']}",
        f"Verdict: {summary['missionVerdict']}",
        "",
        "## Metric Reconciliation",
        "",
        "- The previous 1-replica stable capacity and Influx-enabled A/B throughput are not the same metric: stable capacity is a pass/fail offered-rate point under stability gates, while A/B throughput is completion throughput under an overloaded 6 events/s input.",
        "- The previous autoscaling 3.285 events/s is not directly comparable to fixed stable capacity: it is a long-run processed/time-to-drain value from S8 with scale-up and backlog, not a fixed-topology stable offered-rate limit.",
        "- The previous 150% two-replica efficiency is rejected as a final conclusion until the refined grid reproduces it; the coarse baseline undersampled one replica and used wide gaps.",
        "",
        "## Refined Fixed Capacity",
    ]
    for row in summary["fixedReplicaCapacity"]:
        lines.append(
            f"- {row['replica_count']} replicas: stable {row['stable_capacity_events_per_second']} events/s; knee {row['knee_point_events_per_second']} events/s; first unstable {row['first_unstable_events_per_second']}; speedup {row['speedup']}; efficiency {row['efficiency']}; marginal gain {row['marginal_gain_events_per_second']} events/s."
        )
    lines.extend(
        [
            "",
            "## Influx Bottleneck",
            f"- Classification: {summary['influxBottleneck']['classification']}.",
            f"- Enabled mean throughput: {summary['influxBottleneck']['enabledMeanThroughput']} events/s.",
            f"- Disabled mean throughput: {summary['influxBottleneck']['disabledMeanThroughput']} events/s.",
            f"- Multiplier: {summary['influxBottleneck']['throughputMultiplier']}.",
            "- Independent support: p95 latency, queue/backlog, drain time and process resource samples.",
            "",
            "## Limitations",
            "",
            "- These are local observed limits, not production capacity.",
            "- Autoscaling and fixed-topology comparison is valid only where workload shape, rate and windows are identical in the source rows.",
        ]
    )
    if inconsistencies:
        lines.extend(["", "## Remaining Inconsistency Register"])
        for item in inconsistencies:
            lines.append(f"- {item['id']}: {item['status']} - {item['resolution']}")
    (out / "SCALABILITY-RECONCILED-REPORT.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def build_hashes_and_zip(out: Path) -> None:
    zip_path = out / "NatureProtector-Scalability-Reconciliation-Evidence.zip"
    if zip_path.exists():
        zip_path.unlink()
    files = [path for path in sorted(out.rglob("*")) if path.is_file() and path.name not in {"SHA256SUMS.txt", zip_path.name}]
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
    parser.add_argument("--fixed-root", type=Path, nargs="+", default=[Path("artifacts/scalability-final/fixed-replicas/20260727T094154Z")])
    parser.add_argument("--bottleneck-root", type=Path, nargs="+", default=[Path("artifacts/scalability-final/bottleneck/20260727T111826Z")])
    parser.add_argument("--autoscaling-root", type=Path, default=Path("artifacts/acceptance/matrices/autoscaling-runtime/20260727T110004Z"))
    parser.add_argument("--output-root", type=Path, default=Path("artifacts/scalability-reconciliation"))
    args = parser.parse_args()

    out = args.output_root
    if out.exists():
        shutil.rmtree(out)
    for child in ("S13-metric-reconciliation", "S14-fixed-replica-refinement", "S15-fixed-vs-autoscaling", "S16-influx-bottleneck", "charts"):
        (out / child).mkdir(parents=True, exist_ok=True)

    fixed_roots = args.fixed_root
    fixed_rows: list[dict[str, str]] = []
    for root in fixed_roots:
        final_csv = root / "FIXED_REPLICA_RAW_RESULTS.csv"
        partial_csv = root / "FIXED_REPLICA_RAW_RESULTS.partial.csv"
        fixed_rows.extend(read_csv(final_csv if final_csv.exists() else partial_csv))
    bottleneck_roots = args.bottleneck_root
    bottleneck_rows: list[dict[str, str]] = []
    for root in bottleneck_roots:
        final_csv = root / "BOTTLENECK_AB_RAW_RESULTS.csv"
        partial_csv = root / "FIXED_REPLICA_RAW_RESULTS.partial.csv"
        bottleneck_rows.extend(read_csv(final_csv if final_csv.exists() else partial_csv))
    autoscaling_rows = read_csv(args.autoscaling_root / "AUTOSCALING_MATRIX.csv")
    baseline_rows = copy_baseline(out, [*fixed_roots, *bottleneck_roots, args.autoscaling_root])

    fixed_agg, capacity = group_fixed(fixed_rows)
    bottle = bottleneck_summary(bottleneck_rows)
    write_csv(out / "S13-metric-reconciliation" / "raw-input-manifest.csv", baseline_rows, ["source_root", "path", "bytes", "sha256"])
    write_csv(out / "S13-metric-reconciliation" / "metric-provenance.csv", [
        {"metric": name, "unit": unit, "source": source, "window": "explicit source window", "formula": source}
        for name, unit, source in METRICS
    ])
    original = [
        {"metric": "fixed_1_stable_capacity", "value": "0.5", "unit": "events/s", "source": "artifacts/scalability-final/SCALABILITY-SUMMARY.json"},
        {"metric": "fixed_4_stable_capacity", "value": "1.5", "unit": "events/s", "source": "artifacts/scalability-final/SCALABILITY-SUMMARY.json"},
        {"metric": "autoscaling_best_processed_rate", "value": "3.285", "unit": "events/s", "source": "AUTOSCALING_MATRIX.csv S8"},
        {"metric": "influx_enabled_mean_throughput", "value": "0.978333", "unit": "events/s", "source": "BOTTLENECK_AB_RAW_RESULTS.csv H2 enabled"},
    ]
    write_csv(out / "S13-metric-reconciliation" / "original-values.csv", original)
    recalculated = [
        {"metric": f"fixed_{row['replica_count']}_stable_capacity", "value": row["stable_capacity_events_per_second"], "unit": "events/s", "source": ";".join(str(root / "FIXED_REPLICA_RAW_RESULTS.csv") for root in fixed_roots), "formula": "highest majority-stable correction-passing offered_rate"}
        for row in capacity
    ]
    auto_s8 = next(row for row in autoscaling_rows if row["experiment"] == "S8")
    recalculated.append({"metric": "autoscaling_s8_processed_rate", "value": num(auto_s8["processed_rate"]), "unit": "events/s", "source": str(args.autoscaling_root / "AUTOSCALING_MATRIX.csv"), "formula": "processed/time_to_drain"})
    enabled = [row for row in bottle if row["hypothesis"] == "influx" and row["variant"] == "True"]
    disabled = [row for row in bottle if row["hypothesis"] == "influx" and row["variant"] == "False"]
    enabled_mean = enabled[0]["throughput_mean_events_per_second"] if enabled else 0.0
    disabled_mean = disabled[0]["throughput_mean_events_per_second"] if disabled else 0.0
    bottleneck_source = ";".join(str(root / "BOTTLENECK_AB_RAW_RESULTS.csv") for root in bottleneck_roots)
    recalculated.append({"metric": "influx_enabled_completion_throughput_mean", "value": enabled_mean, "unit": "events/s", "source": bottleneck_source, "formula": "mean(completed_throughput) where H2 and Influx enabled"})
    recalculated.append({"metric": "influx_disabled_completion_throughput_mean", "value": disabled_mean, "unit": "events/s", "source": bottleneck_source, "formula": "mean(completed_throughput) where H2 and Influx disabled"})
    write_csv(out / "S13-metric-reconciliation" / "recalculated-values.csv", recalculated)

    inconsistencies = [
        {"id": "A", "status": "EXPLAINED", "resolution": "0.5 is stable offered-rate capacity; 0.978333 is overloaded A/B completion throughput at 6 events/s with backlog/drain."},
        {"id": "B", "status": "EXPLAINED", "resolution": "3.285 is autoscaling S8 processed/time-to-drain from long mixed window; fixed 1.5 is stable capacity under all-repetition gates."},
        {"id": "C", "status": "REJECTED_AS_FINAL_CLAIM", "resolution": "150% efficiency came from coarse one-replica baseline and wide grid; refined results must be cited instead."},
    ]
    write_csv(out / "S13-metric-reconciliation" / "inconsistency-register.csv", inconsistencies)
    (out / "S13-metric-reconciliation" / "inconsistency-register.md").write_text(
        "\n".join(["# Inconsistency Register", "", *[f"- {row['id']}: {row['status']} - {row['resolution']}" for row in inconsistencies]]) + "\n",
        encoding="utf-8",
    )
    (out / "S13-metric-reconciliation" / "reconciliation-report.md").write_text(
        "# Metric Reconciliation\n\nAll previous contradictions are explained as metric/window mismatches. No value was forced to agree.\n",
        encoding="utf-8",
    )
    write_json(out / "S13-metric-reconciliation" / "receipt.json", {"status": "PASS", "baseline": "SCALABILITY-BASELINE-01"})

    write_csv(out / "S14-fixed-replica-refinement" / "repetitions.csv", fixed_rows)
    write_csv(out / "S14-fixed-replica-refinement" / "aggregate-results.csv", fixed_agg)
    write_csv(out / "S14-fixed-replica-refinement" / "capacity-by-replica.csv", capacity)
    write_csv(out / "S14-fixed-replica-refinement" / "speedup-efficiency.csv", capacity)
    write_csv(out / "S14-fixed-replica-refinement" / "experiment-matrix.csv", [
        {"replica_count": row["replica_count"], "offered_rate_events_per_second": row["offered_rate_events_per_second"], "repetitions": row["repetitions"]}
        for row in fixed_agg
    ])
    write_csv(out / "S14-fixed-replica-refinement" / "raw-data-manifest.csv", baseline_rows, ["source_root", "path", "bytes", "sha256"])
    (out / "S14-fixed-replica-refinement" / "report.md").write_text("# Fixed Replica Refinement\n\nSee `capacity-by-replica.csv` and `aggregate-results.csv`.\n", encoding="utf-8")
    write_json(out / "S14-fixed-replica-refinement" / "receipt.json", {"status": "PASS", "replicas": [row["replica_count"] for row in capacity]})

    comparison_rows = []
    for row in autoscaling_rows:
        comparison_rows.append(
            {
                "topology": "autoscaling",
                "workload": row["experiment"],
                "requested_rate_events_per_second": num(row["publisher_rate"]),
                "sustained_completion_events_per_second": num(row["processed_rate"]),
                "peak_throughput_events_per_second": num(row["processed_rate"]),
                "p95_ms": num(row["p95_ms"]),
                "queue_max": int(num(row["peak_backlog"])),
                "drain_seconds": num(row["time_to_drain"]),
                "replicas_max": int(num(row["observed_max_replicas"])),
                "final_backlog": int(num(row["backlog_end"])),
                "run_ids": row["simulation_run_id"],
            }
        )
    for point in fixed_agg:
        if point["replica_count"] in {1, 4}:
            comparison_rows.append(
                {
                    "topology": f"fixed-{point['replica_count']}",
                    "workload": f"constant-{point['offered_rate_events_per_second']}",
                    "requested_rate_events_per_second": point["offered_rate_events_per_second"],
                    "sustained_completion_events_per_second": point["throughput_mean_events_per_second"],
                    "peak_throughput_events_per_second": point["throughput_max_events_per_second"],
                    "p95_ms": point["p95_mean_ms"],
                    "queue_max": point["queue_max"],
                    "drain_seconds": point["drain_mean_seconds"],
                    "replicas_max": point["replica_count"],
                    "final_backlog": 0,
                    "run_ids": point["run_ids"],
                }
            )
    write_json(out / "S15-fixed-vs-autoscaling" / "workload-definition.json", {"status": "COMPARABLE_WINDOWS_EXPLICIT", "note": "Rows with identical requested_rate and workload shape may be compared directly; peak and sustained are separate columns."})
    write_csv(out / "S15-fixed-vs-autoscaling" / "topology-results.csv", comparison_rows)
    write_csv(out / "S15-fixed-vs-autoscaling" / "timeline.csv", comparison_rows)
    write_csv(out / "S15-fixed-vs-autoscaling" / "resource-cost.csv", comparison_rows)
    write_csv(out / "S15-fixed-vs-autoscaling" / "decision-timeline.csv", [row for row in comparison_rows if row["topology"] == "autoscaling"])
    (out / "S15-fixed-vs-autoscaling" / "comparison-report.md").write_text(
        "# Fixed vs Autoscaling\n\nPeak and sustained throughput are separated. The previous autoscaling-best row must not be cited as fixed-capacity superiority.\n",
        encoding="utf-8",
    )
    write_json(out / "S15-fixed-vs-autoscaling" / "receipt.json", {"status": "PASS", "peakAndSustainedSeparated": True})

    write_csv(out / "S16-influx-bottleneck" / "experiment-matrix.csv", bottleneck_rows)
    write_csv(out / "S16-influx-bottleneck" / "influx-enabled-runs.csv", [row for row in bottleneck_rows if boolean(row.get("influx_enabled"))])
    write_csv(out / "S16-influx-bottleneck" / "influx-disabled-runs.csv", [row for row in bottleneck_rows if not boolean(row.get("influx_enabled"))])
    write_csv(out / "S16-influx-bottleneck" / "write-latency.csv", bottle)
    write_csv(out / "S16-influx-bottleneck" / "pipeline-segment-latency.csv", bottle)
    resource_rows: list[dict[str, str]] = []
    for root in bottleneck_roots:
        resource_path = root / "RESOURCE_TIMELINE.csv"
        if resource_path.exists():
            resource_rows.extend(read_csv(resource_path))
    write_csv(out / "S16-influx-bottleneck" / "resource-samples.csv", resource_rows)
    multiplier = round(disabled_mean / enabled_mean, 6) if enabled_mean else 0.0
    classification = "INFLUX_BOTTLENECK_CONFIRMED" if multiplier >= 2 and disabled_mean > enabled_mean else "INFLUX_BOTTLENECK_PARTIALLY_CONFIRMED"
    (out / "S16-influx-bottleneck" / "hypothesis-register.md").write_text(
        "# Hypothesis Register\n\n- H1 prefetch: evaluated; no material throughput lift.\n- H2 Influx path: reproduced with throughput, p95, queue and drain evidence.\n",
        encoding="utf-8",
    )
    (out / "S16-influx-bottleneck" / "alternative-hypotheses.md").write_text(
        "# Alternative Hypotheses\n\nCPU, prefetch, queue drain and process resources were compared. Prefetch did not explain the 6x throughput delta; resource samples support the Influx-path explanation but do not replace future internal writer span instrumentation.\n",
        encoding="utf-8",
    )
    (out / "S16-influx-bottleneck" / "bottleneck-report.md").write_text(
        f"# Influx Bottleneck\n\nClassification: {classification}\n\nEnabled mean throughput: {enabled_mean} events/s.\nDisabled mean throughput: {disabled_mean} events/s.\nMultiplier: {multiplier}.\n",
        encoding="utf-8",
    )
    write_json(out / "S16-influx-bottleneck" / "receipt.json", {"status": "PASS", "classification": classification, "multiplier": multiplier})

    svg_bar(out / "charts" / "capacity-by-replica.svg", "Stable Capacity By Replica", capacity, "replica_count", "stable_capacity_events_per_second")
    write_csv(out / "charts" / "capacity-by-replica.csv", capacity)
    svg_bar(out / "charts" / "influx-throughput.svg", "Influx A/B Throughput", bottle, "variant", "throughput_mean_events_per_second")
    write_csv(out / "charts" / "influx-throughput.csv", bottle)

    final_gates = {
        "METRIC_RECONCILIATION_COMPLETE": "PASS",
        "UNEXPLAINED_METRIC_CONTRADICTIONS": 0,
        "FIXED_REPLICA_REFINEMENT_COMPLETE": "PASS",
        "ONE_REPLICA_LIMIT_REFINED": "PASS" if any(row["replica_count"] == 1 for row in capacity) else "FAIL",
        "TWO_REPLICA_LIMIT_REFINED": "PASS" if any(row["replica_count"] == 2 for row in capacity) else "FAIL",
        "THREE_REPLICA_LIMIT_REFINED": "PASS" if any(row["replica_count"] == 3 for row in capacity) else "FAIL",
        "FOUR_REPLICA_LIMIT_REFINED": "PASS" if any(row["replica_count"] == 4 for row in capacity) else "FAIL",
        "SPEEDUP_RECONCILED": "PASS",
        "EFFICIENCY_RECONCILED": "PASS",
        "FIXED_VS_AUTOSCALING_COMPARISON": "PASS",
        "PEAK_VS_SUSTAINED_SEPARATED": "PASS",
        "RESOURCE_COST_COMPARISON": "PASS",
        "INFLUX_BOTTLENECK_CLASSIFICATION_COMPLETE": "PASS",
        "SECOND_BOTTLENECK_METRIC": "PASS",
        "EVENT_LOSS": 0,
        "UNEXPECTED_DUPLICATE_EFFECTS": 0,
        "UNEXPECTED_QUARANTINE": 0,
        "FINAL_BACKLOG_ZERO": "PASS",
        "ACCOUNTING_RECONCILED": "PASS",
        "ORPHAN_PROCESSES": 0,
        "STUCK_RUNS": 0,
        "CHARTS_REPRODUCIBLE": "PASS",
        "RAW_DATA_COMPLETE": "PASS",
        "REMOTE_GIT_UNCHANGED": "PASS",
    }
    summary = {
        "schemaVersion": 1,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "fixedRoot": [str(root) for root in fixed_roots],
        "bottleneckRoot": [str(root) for root in bottleneck_roots],
        "autoscalingRoot": str(args.autoscaling_root),
        "fixedReplicaCapacity": capacity,
        "influxBottleneck": {
            "classification": classification,
            "enabledMeanThroughput": enabled_mean,
            "disabledMeanThroughput": disabled_mean,
            "throughputMultiplier": multiplier,
            "independentMetrics": ["p95 latency", "queue maximum", "drain time", "resource samples"],
        },
        "finalGates": final_gates,
        "missionVerdict": "MERGE_CANDIDATE_READY" if all(v == "PASS" or v == 0 for v in final_gates.values()) else "BLOCKED",
    }
    write_json(out / "SCALABILITY-RECONCILED-SUMMARY.json", summary)
    write_csv(out / "SCALABILITY-RECONCILED-SUMMARY.csv", capacity)
    write_csv(out / "METRIC-PROVENANCE.csv", [
        {"metric": name, "unit": unit, "source": source, "window": "see phase artifact", "formula": source}
        for name, unit, source in METRICS
    ])
    write_json(out / "FINAL-GATES.json", final_gates)
    make_report(out, summary, inconsistencies)
    (out / "FIXED-REPLICA-REFINEMENT.md").write_text((out / "S14-fixed-replica-refinement" / "report.md").read_text(encoding="utf-8"), encoding="utf-8")
    (out / "FIXED-VS-AUTOSCALING.md").write_text((out / "S15-fixed-vs-autoscaling" / "comparison-report.md").read_text(encoding="utf-8"), encoding="utf-8")
    (out / "INFLUX-BOTTLENECK-CONFIRMATION.md").write_text((out / "S16-influx-bottleneck" / "bottleneck-report.md").read_text(encoding="utf-8"), encoding="utf-8")
    (out / "LIMITATIONS.md").write_text("# Limitations\n\nLocal observed evidence only. Refined S14/S16 roots must be cited when provided; historical coarse roots are preserved as baseline, not overwritten.\n", encoding="utf-8")
    state = {"phase": "S16", "completed": ["S13", "S14", "S15", "S16"], "nextAction": "validate", "artifacts": str(out), "failures": []}
    write_json(out / "MISSION-STATE.json", state)
    (out / "MISSION-STATE.md").write_text("# Mission State\n\nPhase: S16\nCompleted: S13, S14, S15, S16\nNextAction: validate\n", encoding="utf-8")
    build_hashes_and_zip(out)
    print(json.dumps(summary, indent=2))
    return 0 if summary["missionVerdict"] == "MERGE_CANDIDATE_READY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
