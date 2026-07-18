#!/usr/bin/env python3
from __future__ import annotations
import argparse
import csv
import json
from pathlib import Path

REQUIRED = {f"S{i}" for i in range(1, 9)}


def n(r, k, typ=float):
    try:
        return typ(r.get(k, ""))
    except (TypeError, ValueError):
        return 0


def validate(rows):
    errors = []
    by = {r.get("experiment"): r for r in rows}
    if set(by) != REQUIRED:
        errors.append(f"Expected exactly S1-S8, got {sorted(by)}")
    for i, r in by.items():
        if str(r.get("correctness_pass", "")).lower() != "true":
            errors.append(f"{i}: correctness failed")
        if n(r, "processed_rate") <= 0 or n(r, "p95_ms") <= 0:
            errors.append(f"{i}: invalid throughput/latency")
    if not errors:
        s1, s2, s3, s4, s5, s6, s7, s8 = [by[f"S{i}"] for i in range(1, 9)]
        if n(s1, "replicas", int) != 1:
            errors.append("S1 must be one-replica baseline")
        if n(s2, "replicas", int) <= 1:
            errors.append("S2 must demonstrate scale-up above one replica")
        if n(s2, "backlog_end", int) >= n(s1, "backlog_end", int) and n(s2, "publisher_rate") > n(s1, "publisher_rate"):
            errors.append("S2 does not demonstrate backlog control")
        if n(s3, "replicas", int) <= 1 or n(s3, "backlog_end", int) > max(1, n(s2, "backlog_end", int)):
            errors.append("S3 sustained load is unstable")
        if n(s4, "replicas", int) != 1:
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
    }
    if a.output:
        a.output.parent.mkdir(parents=True, exist_ok=True)
        a.output.write_text(json.dumps(result, indent=2) + "\n")
    print(result["status"])
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
