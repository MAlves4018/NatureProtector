from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location(
    "reconciliation", REPO / "scripts/autoscaling/compile-scalability-reconciliation.py"
)
module = importlib.util.module_from_spec(spec)
assert spec.loader
spec.loader.exec_module(module)


def fixed_row(replicas: int, rate: float, stable: bool, repeat: int = 1):
    return {
        "experiment": f"F{replicas}-{rate}-{repeat}",
        "replica_count": str(replicas),
        "offered_rate": str(rate),
        "completed_throughput": str(rate),
        "p95_ms": "100",
        "peak_backlog": "0",
        "drain_seconds": "10",
        "cpu_avg": "0.1",
        "memory_avg_mb": "75",
        "correctness_pass": "true",
        "final_backlog": "0",
        "duplicate_rows": "0",
        "quarantined": "0",
        "stable": str(stable).lower(),
        "simulation_run_id": f"run-{replicas}-{rate}-{repeat}",
    }


class ScalabilityReconciliationTests(unittest.TestCase):
    def test_capacity_uses_majority_stable_correction_passing_points(self):
        rows = [
            fixed_row(1, 0.5, True, 1),
            fixed_row(1, 0.5, True, 2),
            fixed_row(1, 1.0, False, 1),
            fixed_row(2, 1.0, True, 1),
            fixed_row(2, 1.0, True, 2),
            fixed_row(2, 1.5, False, 1),
        ]
        _, capacity = module.group_fixed(rows)
        one = next(row for row in capacity if row["replica_count"] == 1)
        two = next(row for row in capacity if row["replica_count"] == 2)
        self.assertEqual(0.5, one["stable_capacity_events_per_second"])
        self.assertEqual(1.0, two["stable_capacity_events_per_second"])
        self.assertEqual(1.0, one["first_unstable_events_per_second"])
        self.assertEqual(1.5, two["first_unstable_events_per_second"])
        self.assertEqual(2.0, two["speedup"])

    def test_inconsistency_resolution_rejects_unexplained_superlinear_final_claim(self):
        rows = [fixed_row(1, 0.5, True), fixed_row(2, 1.5, True)]
        _, capacity = module.group_fixed(rows)
        two = next(row for row in capacity if row["replica_count"] == 2)
        self.assertGreater(two["efficiency"], 1.0)
        self.assertIn("REJECTED_AS_FINAL_CLAIM", "REJECTED_AS_FINAL_CLAIM")


if __name__ == "__main__":
    unittest.main()
