from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location(
    "verify", REPO / "scripts/autoscaling/verify-scaling-experiment.py"
)
module = importlib.util.module_from_spec(spec)
assert spec.loader
spec.loader.exec_module(module)


def row(experiment: str, publisher_rate: float, replicas: int, processed_rate: float, p95_ms: float, backlog_end: int = 0):
    return {
        "experiment": experiment,
        "publisher_rate": str(publisher_rate),
        "replicas": str(replicas),
        "final_replicas": "1" if experiment == "S4" else str(replicas),
        "processed_rate": str(processed_rate),
        "p95_ms": str(p95_ms),
        "peak_backlog": "4",
        "backlog_end": str(backlog_end),
        "correctness_pass": "true",
    }


def valid_rows():
    return [
        row("S1", 5, 1, 8, 110),
        row("S2", 12, 2, 13, 150),
        row("S3", 16, 3, 18, 170),
        row("S4", 2, 1, 8, 105),
        row("S5", 12, 2, 13, 180),
        row("S6", 16, 2, 12, 240),
        row("S7", 12, 3, 16, 200),
        row("S8", 10, 3, 15, 190),
    ]


class ScalingVerifierAdversarialTests(unittest.TestCase):
    def test_valid_scaling_matrix_passes_and_calculates_speedup(self):
        errors = module.validate(valid_rows())
        self.assertEqual([], errors)
        analysis = module.analyze(valid_rows())
        self.assertEqual(8, len(analysis))
        self.assertGreater(analysis[1]["speedup"], 1)
        self.assertGreater(analysis[1]["efficiency"], 0)

    def test_absurd_constant_one_replica_matrix_is_rejected(self):
        rows = [
            row(f"S{i}", publisher_rate=100, replicas=1, processed_rate=1, p95_ms=999999, backlog_end=0)
            for i in range(1, 9)
        ]
        self.assertTrue(module.validate(rows))

    def test_missing_experiments_are_rejected(self):
        rows = [row("S1", 1, 1, 1, 10)]
        self.assertTrue(module.validate(rows))

    def test_final_backlog_is_rejected_even_when_correctness_passes(self):
        rows = valid_rows()
        rows[1]["backlog_end"] = "1"
        self.assertIn("S2: final backlog must be zero after drain", module.validate(rows))

    def test_live_matrix_aliases_are_accepted(self):
        rows = []
        for source in valid_rows():
            rows.append(
                {
                    "experiment": source["experiment"],
                    "publisher_rate": source["publisher_rate"],
                    "observed_max_replicas": source["replicas"],
                    "final_replicas": source["final_replicas"],
                    "processed": str(int(float(source["processed_rate"]) * 10)),
                    "time_to_drain": "10",
                    "processing_p95_ms": source["p95_ms"],
                    "backlog_end": source["backlog_end"],
                    "result": "PASS",
                }
            )
        self.assertEqual([], module.validate(rows))


if __name__ == "__main__":
    unittest.main()
