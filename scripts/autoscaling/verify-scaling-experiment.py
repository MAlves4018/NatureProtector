#!/usr/bin/env python3
from __future__ import annotations
import argparse
import csv
import json
from pathlib import Path

REQUIRED = {f"S{i}" for i in range(1, 9)}


def n(r, k, typ=float):
    try:
        value = str(r.get(k, "")).replace(",", ".")
        return typ(value)
    except (TypeError, ValueError):
        return 0


def normalized(row):
    out = dict(row)
    if "replicas" not in out or out.get("replicas", "") == "":
        out["replicas"] = out.get("observed_max_replicas", "")
    if "final_replicas" not in out or out.get("final_replicas", "") == "":
        out["final_replicas"] = out.get("replicas", "")
    if "p95_ms" not in out or out.get("p95_ms", "") == "":
        out["p95_ms"] = out.get("processing_p95_ms", "")
    if "backlog_end" not in out or out.get("backlog_end", "") == "":
        out["backlog_end"] = out.get("final_backlog", "")
    if "processed_rate" not in out or out.get("processed_rate", "") == "":
        processed = n(out, "processed")
        drain = n(out, "time_to_drain")
        out["processed_rate"] = str(round(processed / drain, 6)) if processed > 0 and drain > 0 else ""
    if "correctness_pass" not in out or out.get("correctness_pass", "") == "":
        out["correctness_pass"] = str(out.get("result", "")).upper() == "PASS"
    return out


def analyze(rows):
    by = {r.get("experiment"): normalized(r) for r in rows}
    if not REQUIRED.issubset(set(by)):
        return []
    baseline_rate = n(by["S1"], "processed_rate")
    analysis = []
    previous_rate = None
    for experiment in sorted(REQUIRED):
        row = by[experiment]
        rate = n(row, "processed_rate")
        replicas = max(1, n(row, "replicas", int))
        speedup = rate / baseline_rate if baseline_rate > 0 else 0
        efficiency = speedup / replicas if replicas > 0 else 0
        marginal_gain = 0 if previous_rate is None else rate - previous_rate
        analysis.append(
            {
                "experiment": experiment,
                "replicas": replicas,
                "final_replicas": n(row, "final_replicas", int),
                "publisher_rate": n(row, "publisher_rate"),
                "processed_rate": rate,
                "speedup": round(speedup, 6),
                "efficiency": round(efficiency, 6),
                "marginal_gain": round(marginal_gain, 6),
                "p95_ms": n(row, "p95_ms"),
                "peak_backlog": n(row, "peak_backlog", int),
                "backlog_end": n(row, "backlog_end", int),
            }
        )
        previous_rate = rate
    return analysis


def validate(rows):
    errors = []
    by = {r.get("experiment"): normalized(r) for r in rows}
    if set(by) != REQUIRED:
        errors.append(f"Expected exactly S1-S8, got {sorted(by)}")
    for i, r in by.items():
        if str(r.get("correctness_pass", "")).lower() != "true":
            errors.append(f"{i}: correctness failed")
        if n(r, "processed_rate") <= 0 or n(r, "p95_ms") <= 0:
            errors.append(f"{i}: invalid throughput/latency")
        if n(r, "backlog_end", int) != 0:
            errors.append(f"{i}: final backlog must be zero after drain")
    if not errors:
        s1, s2, s3, s4, s5, s6, s7, s8 = [by[f"S{i}"] for i in range(1, 9)]
        if n(s1, "replicas", int) != 1:
            errors.append("S1 must be one-replica baseline")
        if n(s2, "replicas", int) <= 1:
            errors.append("S2 must demonstrate scale-up above one replica")
        if n(s3, "replicas", int) <= 1 or n(s3, "backlog_end", int) > max(1, n(s2, "backlog_end", int)):
            errors.append("S3 sustained load is unstable")
        if n(s4, "final_replicas", int) != 1:
            errors.append("S4 must demonstrate scale-down to one replica")
        if n(s5, "replicas", int) <= 1:
            errors.append("S5 retry backlog must prevent premature scale-down")
        if n(s6, "replicas", int) <= 1:
            errors.append("S6 must demonstrate recovery with replacement replicas")
        if n(s7, "replicas", int) <= 1:
            errors.append("S7 must exercise multi-replica duplicate/out-of-order correctness")
        if n(s8, "replicas", int) < 1 or n(s8, "backlog_end", int) > 0:
            errors.append("S8 long run must drain backlog completely")
        if n(s8, "p95_ms") > max(60000, n(s1, "p95_ms") * 10):
            errors.append("S8 latency is outside reviewed bound")
    return errors


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("matrix", type=Path)
    ap.add_argument("--output", type=Path)
    a = ap.parse_args()
    with a.matrix.open(newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    errors = validate(rows)
    result = {
        "status": "AUTOSCALING_EXPERIMENT_PASS" if not errors else "AUTOSCALING_EXPERIMENT_FAIL",
        "experiments": sorted({r.get("experiment") for r in rows}),
        "errors": errors,
        "analysis": analyze(rows),
    }
    if a.output:
        a.output.parent.mkdir(parents=True, exist_ok=True)
        a.output.write_text(json.dumps(result, indent=2) + "\n")
    print(result["status"])
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
