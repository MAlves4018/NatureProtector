#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import json
from collections import Counter
from pathlib import Path
from typing import Any


STAGE_FILES = [
    "confirmed-event-ids.csv",
    "inbox-event-ids.csv",
    "processed-event-ids.csv",
    "persisted-event-ids.csv",
    "projected-event-ids.csv",
    "final-effect-event-ids.csv",
]


def read_rows(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        return []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def event_id(row: dict[str, str]) -> str:
    return str(row.get("event_id") or row.get("EventId") or "").strip().lower()


def distinct_event_ids(rows: list[dict[str, str]]) -> set[str]:
    return {value for value in (event_id(row) for row in rows) if value}


def duplicate_event_ids(rows: list[dict[str, str]]) -> dict[str, int]:
    counts = Counter(value for value in (event_id(row) for row in rows) if value)
    return {value: count for value, count in sorted(counts.items()) if count > 1}


def write_csv(path: Path, rows: list[dict[str, Any]], fields: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)


def reconcile(trace_dir: Path) -> dict[str, Any]:
    stage_rows = {name: read_rows(trace_dir / name) for name in STAGE_FILES}
    stage_sets = {name: distinct_event_ids(rows) for name, rows in stage_rows.items()}
    confirmed = stage_sets["confirmed-event-ids.csv"]
    final_effects = stage_sets["final-effect-event-ids.csv"]
    missing = sorted(confirmed - final_effects)
    unexpected_final = sorted(final_effects - confirmed)
    duplicate_effects = duplicate_event_ids(stage_rows["final-effect-event-ids.csv"])
    stage_missing = {
        name: sorted(confirmed - values)
        for name, values in stage_sets.items()
        if name != "confirmed-event-ids.csv"
    }
    stage_unexpected = {
        name: sorted(values - confirmed)
        for name, values in stage_sets.items()
        if name != "confirmed-event-ids.csv"
    }
    stages_reconciled = all(
        not stage_missing[name] and not stage_unexpected[name]
        for name in stage_missing
    )

    missing_rows = [{"event_id": value} for value in missing]
    duplicate_rows = [
        {"event_id": value, "occurrences": occurrences}
        for value, occurrences in duplicate_effects.items()
    ]
    write_csv(trace_dir / "missing-event-ids.csv", missing_rows, ["event_id"])
    write_csv(trace_dir / "duplicate-event-ids.csv", duplicate_rows, ["event_id", "occurrences"])

    summary = {
        "confirmed_distinct": len(confirmed),
        "inbox_distinct": len(stage_sets["inbox-event-ids.csv"]),
        "processed_distinct": len(stage_sets["processed-event-ids.csv"]),
        "persisted_distinct": len(stage_sets["persisted-event-ids.csv"]),
        "projected_distinct": len(stage_sets["projected-event-ids.csv"]),
        "final_effect_distinct": len(final_effects),
        "event_loss": len(missing),
        "missing_event_ids": len(missing),
        "unexpected_final_effect_event_ids": len(unexpected_final),
        "unexpected_duplicate_effects": len(duplicate_effects),
        "stage_missing_event_ids": {name: len(values) for name, values in stage_missing.items()},
        "stage_unexpected_event_ids": {name: len(values) for name, values in stage_unexpected.items()},
        "accounting_reconciled": stages_reconciled and len(duplicate_effects) == 0,
        "stage_files": {name: str((trace_dir / name).as_posix()) for name in STAGE_FILES},
    }
    (trace_dir / "event-accounting-summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    (trace_dir / "event-trace-report.md").write_text(render_report(summary, missing, unexpected_final), encoding="utf-8")
    return summary


def render_report(summary: dict[str, Any], missing: list[str], unexpected_final: list[str]) -> str:
    lines = [
        "# Event Trace Report",
        "",
        "Loss is calculated as distinct confirmed EventIds minus distinct final-effect EventIds.",
        "Ordinal, sequence, CycleIndex and maximum observed effect indexes are not used as counts.",
        "",
        "## Stage Cardinality",
        f"- publisher-confirmed: {summary['confirmed_distinct']}",
        f"- RabbitMQ/inbox: {summary['inbox_distinct']}",
        f"- processed: {summary['processed_distinct']}",
        f"- persisted: {summary['persisted_distinct']}",
        f"- projected: {summary['projected_distinct']}",
        f"- final effects: {summary['final_effect_distinct']}",
        "",
        "## Reconciliation",
        f"- EVENT_LOSS={summary['event_loss']}",
        f"- MISSING_EVENT_IDS={summary['missing_event_ids']}",
        f"- UNEXPECTED_DUPLICATE_EFFECTS={summary['unexpected_duplicate_effects']}",
        f"- UNEXPECTED_FINAL_EFFECT_EVENT_IDS={summary['unexpected_final_effect_event_ids']}",
        f"- ACCOUNTING_RECONCILED={'PASS' if summary['accounting_reconciled'] else 'FAIL'}",
    ]
    lines.extend(["", "## Stage Set Differences"])
    for name, count in summary["stage_missing_event_ids"].items():
        lines.append(f"- {name} missing_confirmed_event_ids={count}")
    for name, count in summary["stage_unexpected_event_ids"].items():
        lines.append(f"- {name} unexpected_event_ids={count}")
    if missing:
        lines.extend(["", "## Missing EventIds", *[f"- {value}" for value in missing]])
    if unexpected_final:
        lines.extend(["", "## Unexpected Final Effect EventIds", *[f"- {value}" for value in unexpected_final]])
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--trace-dir", type=Path, required=True)
    args = parser.parse_args()
    summary = reconcile(args.trace_dir)
    print(json.dumps(summary, indent=2))
    return 0 if summary["accounting_reconciled"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
