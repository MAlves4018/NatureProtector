from __future__ import annotations
import csv, tempfile, unittest
from pathlib import Path
from _loader import load

MODULE = load("scripts/evidence/collect-performance-evidence.py", "performance_isolation")


class BaselineIsolationTests(unittest.TestCase):
    def test_collector_reads_only_requested_baseline(self):
        with tempfile.TemporaryDirectory() as temp:
            repo = Path(temp)
            for baseline, name in (("old", "old_duration"), ("new", "new_duration")):
                path = repo / "artifacts/report-evidence" / baseline / "01-inventory/telemetry-metrics.csv"
                path.parent.mkdir(parents=True, exist_ok=True)
                with path.open("w", encoding="utf-8", newline="") as handle:
                    writer = csv.DictWriter(handle, fieldnames=["name", "instrument_kind", "unit", "source", "line"])
                    writer.writeheader()
                    writer.writerow(
                        {"name": name, "instrument_kind": "Histogram", "unit": "ms", "source": "x", "line": "1"}
                    )
            rows = MODULE.collect_performance_metrics(repo, "new")
            self.assertEqual(["new_duration"], [row["name"] for row in rows])


if __name__ == "__main__":
    unittest.main()
