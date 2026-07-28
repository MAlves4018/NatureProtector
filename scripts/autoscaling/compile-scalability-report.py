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


def parse_number(value):
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


def sha256(path: Path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--evidence-root", type=Path, required=True)
    parser.add_argument("--matrix", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, default=Path("artifacts/scalability-final"))
    args = parser.parse_args()

    out = args.output_root
    for child in ("raw", "normalized", "experiments", "charts", "diagrams", "logs", "manifests"):
        (out / child).mkdir(parents=True, exist_ok=True)

    matrix = read_csv(args.matrix)
    timeline = read_csv(args.evidence_root / "REPLICA_TIMELINE.csv")
    backlog = read_csv(args.evidence_root / "BACKLOG_TIMELINE.csv")
    correctness = read_csv(args.evidence_root / "CORRECTNESS_RESULTS.csv")
    verification = json.loads((out / "live-autoscaling-verification-normalized.json").read_text(encoding="utf-8"))
    capacity = json.loads((out / "live-capacity-analysis-normalized" / "autoscaling-recommendation.json").read_text(encoding="utf-8"))

    analysis = verification.get("analysis", [])
    by_exp = {row["experiment"]: row for row in matrix}
    backlog_by_exp = defaultdict(list)
    for row in backlog:
        backlog_by_exp[row["experiment"]].append(parse_number(row["total_work"]))
    replica_by_exp = defaultdict(list)
    for row in timeline:
        replica_by_exp[row["experiment"]].append(parse_number(row["active_replicas"]))

    summary_rows = []
    for row in analysis:
        exp = row["experiment"]
        raw = by_exp[exp]
        summary_rows.append(
            {
                "experiment": exp,
                "simulationRunId": raw.get("simulation_run_id", ""),
                "operationId": raw.get("operation_id", ""),
                "publisherRateEventsPerSecond": row["publisher_rate"],
                "processedRateEventsPerSecond": row["processed_rate"],
                "maxReplicas": row["replicas"],
                "finalReplicas": row.get("final_replicas", ""),
                "speedupVsS1": row["speedup"],
                "efficiency": row["efficiency"],
                "marginalGainEventsPerSecond": row["marginal_gain"],
                "p95Ms": row["p95_ms"],
                "peakBacklog": row["peak_backlog"],
                "finalBacklog": row["backlog_end"],
                "replicaSamples": len(replica_by_exp[exp]),
                "backlogSamples": len(backlog_by_exp[exp]),
            }
        )
    write_csv(
        out / "SCALABILITY-SUMMARY.csv",
        summary_rows,
        [
            "experiment",
            "simulationRunId",
            "operationId",
            "publisherRateEventsPerSecond",
            "processedRateEventsPerSecond",
            "maxReplicas",
            "finalReplicas",
            "speedupVsS1",
            "efficiency",
            "marginalGainEventsPerSecond",
            "p95Ms",
            "peakBacklog",
            "finalBacklog",
            "replicaSamples",
            "backlogSamples",
        ],
    )

    s1 = next(row for row in summary_rows if row["experiment"] == "S1")
    scale_up_times = [parse_number(row.get("time_to_scale_up")) for row in matrix if row.get("time_to_scale_up")]
    correctness_pass = all(str(row["correctness_pass"]).lower() == "true" for row in correctness)
    duplicate_rows = sum(int(row["duplicate_rows"]) for row in correctness)
    quarantined = sum(int(row["quarantined"]) for row in correctness)
    final_backlog_zero = all(int(float(str(row["backlog_end"]).replace(",", "."))) == 0 for row in matrix)

    summary = {
        "schemaVersion": 1,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "evidenceRoot": str(args.evidence_root),
        "verificationStatus": verification["status"],
        "capacityRecommendationReady": capacity["readyForScalingExperiment"],
        "oneReplicaMeasuredCapacityEventsPerSecond": capacity["processingRatePerReplicaMedian"],
        "firstObservedOneReplicaUnstableOfferedRateEventsPerSecond": s1["publisherRateEventsPerSecond"],
        "firstObservedOneReplicaProcessedRateEventsPerSecond": s1["processedRateEventsPerSecond"],
        "firstObservedOneReplicaPeakBacklog": s1["peakBacklog"],
        "bestObservedDynamicProcessedRateEventsPerSecond": max(row["processedRateEventsPerSecond"] for row in summary_rows),
        "bestObservedExperiment": max(summary_rows, key=lambda row: row["processedRateEventsPerSecond"])["experiment"],
        "bestObservedSpeedup": max(row["speedupVsS1"] for row in summary_rows),
        "bestObservedEfficiency": max(row["efficiency"] for row in summary_rows),
        "scaleUpTimeSecondsMedian": statistics.median(scale_up_times) if scale_up_times else None,
        "scaleUpTimeSecondsMax": max(scale_up_times) if scale_up_times else None,
        "scaleDownObserved": all(str(row.get("final_replicas", row.get("replicas", ""))).replace(",", ".") == "1" for row in matrix),
        "correctnessPass": correctness_pass,
        "unexpectedDuplicateEffects": duplicate_rows,
        "unexpectedQuarantine": quarantined,
        "finalBacklogZero": final_backlog_zero,
        "rawDataPreserved": True,
        "fixedReplicaRepetitionProtocolComplete": False,
        "bottleneckIsolationComplete": False,
        "claimBoundary": "Local observed runtime campaign. Dynamic autoscaling is demonstrated; full fixed-replica repetition and isolated bottleneck protocol remain open.",
    }
    (out / "SCALABILITY-SUMMARY.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")

    report = [
        "# NatureProtector Scalability And Autoscaling Report",
        "",
        f"GeneratedAtUtc: {summary['generatedAtUtc']}",
        f"EvidenceRoot: `{args.evidence_root}`",
        "",
        "## Measured Results",
        f"- Verification: {summary['verificationStatus']}.",
        f"- One-replica measured capacity baseline: {summary['oneReplicaMeasuredCapacityEventsPerSecond']} events/s.",
        f"- First observed one-replica overload point: offered {summary['firstObservedOneReplicaUnstableOfferedRateEventsPerSecond']} events/s, processed {summary['firstObservedOneReplicaProcessedRateEventsPerSecond']} events/s, peak backlog {summary['firstObservedOneReplicaPeakBacklog']}.",
        f"- Best dynamic autoscaling throughput: {summary['bestObservedDynamicProcessedRateEventsPerSecond']} events/s in {summary['bestObservedExperiment']}.",
        f"- Best observed speedup: {summary['bestObservedSpeedup']}.",
        f"- Median scale-up decision time: {summary['scaleUpTimeSecondsMedian']} s; max {summary['scaleUpTimeSecondsMax']} s.",
        f"- Scale-down observed: {summary['scaleDownObserved']}.",
        f"- Correctness pass: {summary['correctnessPass']}; duplicate effects {summary['unexpectedDuplicateEffects']}; unexpected quarantine {summary['unexpectedQuarantine']}; final backlog zero {summary['finalBacklogZero']}.",
        "",
        "## Interpretation",
        "The live S1-S8 campaign demonstrates metric-driven local process autoscaling of Prevention.Host from one to four replicas and back to one replica after drain.",
        "The first observed bottleneck is the consumer pipeline under one-replica overload, supported by offered rate exceeding processed rate and positive peak backlog.",
        "",
        "## Limitations",
        "- Fixed 1/2/3/4 replica repeated protocol is not complete in this campaign.",
        "- Bottleneck has not been isolated between Prevention CPU, PostgreSQL and InfluxDB with A/B experiments.",
        "- The result is local observed capacity only and must not be presented as production capacity.",
    ]
    (out / "SCALABILITY-REPORT.md").write_text("\n".join(report) + "\n", encoding="utf-8")
    (out / "AUTOSCALING-REPORT.md").write_text("\n".join(report) + "\n", encoding="utf-8")
    (out / "BOTTLENECK-REPORT.md").write_text(
        "# Bottleneck Report\n\nObserved first overloaded component: Prevention/RabbitMQ consumer pipeline under one-replica load. Isolation is incomplete until single-variable A/B experiments are executed.\n",
        encoding="utf-8",
    )
    (out / "LIMITATIONS.md").write_text("\n".join(report[report.index("## Limitations") :]) + "\n", encoding="utf-8")
    (out / "REPRODUCTION.md").write_text(
        "\n".join(
            [
                "# Reproduction",
                "",
                "1. Start local infrastructure: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/docker/Start-LocalInfrastructure.ps1`.",
                "2. Execute autoscaling matrix: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Invoke-AutoscalingExperimentMatrix.ps1 -SkipBuild`.",
                "3. Normalize current matrix with final replicas from `REPLICA_TIMELINE.csv`.",
                "4. Verify: `python scripts/autoscaling/verify-scaling-experiment.py artifacts/scalability-final/live-autoscaling-normalized-matrix.csv --output artifacts/scalability-final/live-autoscaling-verification-normalized.json`.",
                "5. Analyze: `python scripts/autoscaling/analyze-capacity.py artifacts/scalability-final/live-autoscaling-normalized-matrix.csv --output-dir artifacts/scalability-final/live-capacity-analysis-normalized`.",
            ]
        )
        + "\n",
        encoding="utf-8",
    )

    copied = []
    for source in [
        args.evidence_root / "AUTOSCALING_MATRIX.csv",
        args.evidence_root / "REPLICA_TIMELINE.csv",
        args.evidence_root / "BACKLOG_TIMELINE.csv",
        args.evidence_root / "LATENCY_RESULTS.csv",
        args.evidence_root / "CORRECTNESS_RESULTS.csv",
        args.evidence_root / "CAPACITY_BASELINE.json",
        args.matrix,
        out / "live-autoscaling-verification-normalized.json",
        out / "live-capacity-analysis-normalized" / "autoscaling-recommendation.json",
    ]:
        if source.exists():
            target = out / "raw" / source.name
            target.write_bytes(source.read_bytes())
            copied.append(target)

    files = sorted([path for path in out.rglob("*") if path.is_file()])
    write_csv(
        out / "manifests" / "MANIFEST.csv",
        [{"path": str(path.relative_to(out)).replace("\\", "/"), "bytes": path.stat().st_size, "sha256": sha256(path)} for path in files],
        ["path", "bytes", "sha256"],
    )
    files = sorted([path for path in out.rglob("*") if path.is_file() and path.name != "SHA256SUMS.txt"])
    (out / "SHA256SUMS.txt").write_text(
        "\n".join(f"{sha256(path)}  {path.relative_to(out).as_posix()}" for path in files) + "\n",
        encoding="utf-8",
    )
    zip_path = out / "NatureProtector-Scalability-Evidence.zip"
    if zip_path.exists():
        zip_path.unlink()
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(out.rglob("*")):
            if path.is_file() and path != zip_path:
                archive.write(path, path.relative_to(out).as_posix())
    print(f"SCALABILITY_REPORT={out / 'SCALABILITY-REPORT.md'}")
    print(f"SCALABILITY_SUMMARY={out / 'SCALABILITY-SUMMARY.json'}")
    print(f"SCALABILITY_ZIP={zip_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
