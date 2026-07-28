from __future__ import annotations

import importlib.util
import os
import tempfile
import unittest
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location(
    "temporal", REPO / "scripts/autoscaling/compile-scalability-temporal-comparison.py"
)
module = importlib.util.module_from_spec(spec)
assert spec.loader
spec.loader.exec_module(module)


def temporal_row(workload: str, topology: str, repeat: int) -> dict[str, str]:
    return {
        "experiment": f"{workload}-{topology}-r{repeat}",
        "workload_id": workload,
        "topology": topology,
        "repeat": str(repeat),
        "requested_rate": "1.5",
        "actual_publish_rate": "1.5",
        "confirmed_rate": "1.5",
        "completed_throughput": "1.45",
        "peak_throughput": "1.8",
        "p95_ms": "250",
        "p99_ms": "400",
        "peak_backlog": "0",
        "drain_seconds": "12",
        "replica_seconds": "30",
        "cpu_seconds": "2.5",
        "memory_mb_seconds": "1500",
        "scale_decisions": "0",
        "event_loss": "0",
        "missing_event_ids": "0",
        "duplicate_rows": "0",
        "unexpected_duplicate_effects": "0",
        "quarantined": "0",
        "accounting_reconciled": "true",
        "final_backlog": "0",
        "correctness_pass": "true",
        "simulation_run_id": f"run-{repeat}",
        "evidence_path": "raw/run",
    }


def capacity_row(replicas: int, rate: float, stable: bool, repeat: int) -> dict[str, str]:
    return {
        "replica_count": str(replicas),
        "requested_rate": str(rate),
        "stable": str(stable).lower(),
        "completed_throughput": str(rate if stable else rate * 0.5),
        "p95_ms": "100",
        "peak_backlog": "0" if stable else "4",
        "drain_seconds": "10",
        "correctness_pass": "true",
        "final_backlog": "0",
        "duplicate_rows": "0",
        "quarantined": "0",
        "simulation_run_id": f"cap-{replicas}-{rate}-{repeat}",
    }


class ScalabilityTemporalComparisonTests(unittest.TestCase):
    def test_validate_temporal_accepts_required_63_cells(self):
        rows = [
            temporal_row(workload, topology, repeat)
            for workload in sorted(module.REQUIRED_WORKLOADS)
            for topology in sorted(module.REQUIRED_TOPOLOGIES)
            for repeat in range(1, 4)
        ]

        self.assertEqual([], module.validate_temporal(rows))
        summary = module.aggregate_workloads(rows)
        self.assertEqual(21, len(summary))
        self.assertTrue(all(row["valid_repetitions"] == 3 for row in summary))

    def test_validate_temporal_rejects_missing_repetitions(self):
        rows = [temporal_row("W1-low-constant", "fixed-one", 1)]

        errors = module.validate_temporal(rows)

        self.assertTrue(any("Missing temporal cells" in error for error in errors))
        self.assertTrue(any("below the required 63" in error for error in errors))

    def test_aggregate_capacity_calculates_speedup_efficiency_and_first_unstable(self):
        rows = []
        for repeat in range(1, 4):
            rows.append(capacity_row(1, 0.8, True, repeat))
            rows.append(capacity_row(1, 1.0, False, repeat))
            rows.append(capacity_row(2, 1.5, True, repeat))
            rows.append(capacity_row(2, 2.0, False, repeat))

        _, capacity = module.aggregate_capacity(rows)
        one = next(row for row in capacity if row["replica_count"] == 1)
        two = next(row for row in capacity if row["replica_count"] == 2)

        self.assertEqual(0.8, one["stable_capacity_events_per_second"])
        self.assertEqual(1.0, one["first_unstable_events_per_second"])
        self.assertEqual(1.5, two["stable_capacity_events_per_second"])
        self.assertEqual(1.875, two["speedup"])
        self.assertEqual(0.9375, two["efficiency"])

    def test_aggregate_capacity_does_not_emit_zero_for_uncharacterized_replica_grid(self):
        rows = []
        for repeat in range(1, 4):
            rows.append(capacity_row(1, 0.8, True, repeat))
            rows.append(capacity_row(2, 1.7, False, repeat))

        _, capacity = module.aggregate_capacity(rows)
        two = next(row for row in capacity if row["replica_count"] == 2)

        self.assertEqual("NOT_CHARACTERIZED_IN_FINAL_GRID", two["capacity_status"])
        self.assertEqual("N/A", two["stable_capacity_events_per_second"])
        self.assertEqual("N/A", two["knee_point_events_per_second"])
        self.assertEqual(1.7, two["first_unstable_events_per_second"])
        self.assertEqual("N/A", two["speedup"])
        self.assertEqual("N/A", two["efficiency"])
        self.assertEqual("N/A", two["marginal_gain_events_per_second"])

    def test_resolve_input_csv_uses_latest_timestamped_child_when_top_level_missing(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            requested = root / "capacity-refinement" / "TEMPORAL_CAPACITY_RAW_RESULTS.csv"
            older = requested.parent / "20260727T010000Z" / requested.name
            newer = requested.parent / "20260727T020000Z" / requested.name
            older.parent.mkdir(parents=True)
            newer.parent.mkdir(parents=True)
            older.write_text("experiment\nold\n", encoding="utf-8")
            newer.write_text("experiment\nnew\n", encoding="utf-8")
            os.utime(older, (1_000_000, 1_000_000))
            os.utime(newer, (2_000_000, 2_000_000))

            resolved = module.resolve_input_csv(requested)

            self.assertEqual(newer, resolved)

    def test_rate_precision_and_correction_helpers_do_not_pass_empty_rows(self):
        self.assertEqual([], module.rows_with_rate_error([]))
        self.assertFalse(module.all_correction_rows([]))
        self.assertFalse(module.all_correction_rows([temporal_row("W1-low-constant", "fixed-one", 1) | {"event_loss": "1"}]))
        self.assertFalse(module.all_correction_rows([temporal_row("W1-low-constant", "fixed-one", 1) | {"missing_event_ids": "1"}]))
        self.assertFalse(module.all_correction_rows([temporal_row("W1-low-constant", "fixed-one", 1) | {"accounting_reconciled": "false"}]))
        self.assertTrue(module.all_correction_rows([temporal_row("W1-low-constant", "fixed-one", 1)]))


if __name__ == "__main__":
    unittest.main()
