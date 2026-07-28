from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location(
    "capacity", REPO / "scripts/autoscaling/analyze-capacity.py"
)
module = importlib.util.module_from_spec(spec)
assert spec.loader
spec.loader.exec_module(module)


def row(experiment: str, replicas: int, backlog_end: int = 0):
    return {
        "experiment": experiment,
        "publisher_rate": 5.0,
        "replicas": replicas,
        "processed_rate": 8.0,
        "p95_ms": 100.0,
        "backlog_end": backlog_end,
        "correctness_pass": True,
    }


class CapacityAnalysisTests(unittest.TestCase):
    def test_ready_requires_complete_correct_drained_scaled_matrix(self):
        rows = [
            row("S1", 1),
            row("S2", 2),
            row("S3", 3),
            row("S4", 1),
            row("S5", 2),
            row("S6", 2),
            row("S7", 3),
            row("S8", 3),
        ]
        result = module.recommendation(rows, acceptable_delay=3, min_replicas=1, max_replicas=4)
        self.assertTrue(result["readyForScalingExperiment"])
        self.assertEqual(24, result["targetBacklogPerReplica"])

    def test_backlog_end_prevents_ready_recommendation(self):
        rows = [
            row("S1", 1),
            row("S2", 2, backlog_end=1),
            row("S3", 3),
            row("S4", 1),
            row("S5", 2),
            row("S6", 2),
            row("S7", 3),
            row("S8", 3),
        ]
        result = module.recommendation(rows, acceptable_delay=3, min_replicas=1, max_replicas=4)
        self.assertFalse(result["readyForScalingExperiment"])
        self.assertFalse(result["allExperimentRowsDrained"])


if __name__ == "__main__":
    unittest.main()
