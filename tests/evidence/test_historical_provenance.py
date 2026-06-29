from __future__ import annotations
import csv, tempfile, unittest
from pathlib import Path
from _loader import load

MODULE = load("scripts/evidence/collect-report-integration-evidence.py", "phase7_history")


class HistoricalProvenanceTests(unittest.TestCase):
    def test_only_identified_b_c_rows_are_promoted(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "historical-runs.csv"
            fields = [
                "scenario",
                "run_id",
                "expected_events",
                "inbox_events",
                "risk_assessments",
                "missing_events",
                "rejected_events",
                "quarantined_events",
            ]
            with path.open("w", encoding="utf-8", newline="") as handle:
                writer = csv.DictWriter(handle, fieldnames=fields)
                writer.writeheader()
                writer.writerow(
                    {
                        "scenario": "scenario_b",
                        "run_id": "run-b",
                        "expected_events": "30",
                        "inbox_events": "30",
                        "risk_assessments": "75",
                        "missing_events": "0",
                        "rejected_events": "0",
                        "quarantined_events": "0",
                    }
                )
                writer.writerow(
                    {
                        "scenario": "scenario_a",
                        "run_id": "run-a",
                        "expected_events": "30",
                        "inbox_events": "30",
                        "risk_assessments": "75",
                        "missing_events": "0",
                        "rejected_events": "0",
                        "quarantined_events": "0",
                    }
                )
                writer.writerow(
                    {
                        "scenario": "scenario_c",
                        "run_id": "",
                        "expected_events": "30",
                        "inbox_events": "24",
                        "risk_assessments": "60",
                        "missing_events": "6",
                        "rejected_events": "0",
                        "quarantined_events": "0",
                    }
                )
            rows = MODULE.historical_rows(path)
            self.assertEqual(1, len(rows))
            self.assertEqual("scenario_b", rows[0]["scenario"])
            self.assertEqual("run-b", rows[0]["run_id"])
            self.assertEqual(64, len(rows[0]["source_sha256"]))

    def test_invalid_numbers_are_rejected_not_replaced_with_zero(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "historical-runs.csv"
            path.write_text(
                "scenario,run_id,expected_events,inbox_events,risk_assessments,missing_events,rejected_events,quarantined_events\nscenario_b,run-b,not-a-number,30,75,0,0,0\n",
                encoding="utf-8",
            )
            self.assertEqual([], MODULE.historical_rows(path))


if __name__ == "__main__":
    unittest.main()
