from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from _loader import load

MODULE = load("scripts/evidence/collect-evidence-gap-closure.py", "evidence_gap_closure")


class EvidenceGapClosureTests(unittest.TestCase):
    def test_summary_status_accepts_phase2_and_phase3_conventions(self):
        self.assertEqual("PARTIAL", MODULE.summary_status({"overall_status": "PARTIAL"}))
        self.assertEqual("PASS", MODULE.summary_status({"phase_status": "PASS"}))

    def test_component_result_separates_current_static_and_analytical(self):
        state, status = MODULE.component_result(
            {"evidenceClassWhenClosed": "CURRENT_STATIC_VERIFICATION"}, {"phase_status": "PASS"}
        )
        self.assertEqual("CLOSED_STATIC", state)
        self.assertEqual("PASS", status)
        state, _ = MODULE.component_result(
            {"evidenceClassWhenClosed": "CURRENT_ANALYTICAL_EVIDENCE"}, {"status": "PASS_EXPLORATORY_VALIDATION"}
        )
        self.assertEqual("CLOSED_ANALYTICAL", state)

    def test_frontend_component_can_close_when_backend_is_partial(self):
        summary = {
            "overall_status": "PARTIAL",
            "frontend": {"status": "PASS"},
            "backend": {"status": "PARTIAL"},
        }
        frontend_state, _ = MODULE.component_result({"component": "frontend"}, summary)
        backend_state, _ = MODULE.component_result({"component": "backend"}, summary)
        self.assertEqual("CLOSED_CURRENT", frontend_state)
        self.assertEqual("PARTIAL", backend_state)

    def test_historical_bc_admission_reconciles_expected_inbox_and_missing(self):
        with tempfile.TemporaryDirectory() as temp:
            repo = Path(temp)
            evidence = repo / "docs/evidence/progress-2026-05-22"
            evidence.mkdir(parents=True)
            source = evidence / "06-compare-b-vs-c.json"
            source.write_text(json.dumps({
                "runs": {
                    "scenario_b": {
                        "status": "Completed", "rejected": 0, "inboxEvents": 30,
                        "quarantined": 0, "missingEvents": 0, "expectedEvents": 30,
                        "riskAssessments": 30, "simulationRunId": "d8203d4b-1839-4908-87ef-05633c1f1ae5",
                        "degradationProfile": "none"
                    },
                    "scenario_c": {
                        "status": "Completed", "rejected": 0, "inboxEvents": 24,
                        "quarantined": 0, "missingEvents": 6, "expectedEvents": 30,
                        "riskAssessments": 24, "simulationRunId": "36caca67-352c-41f1-80e3-8fe951a1582c",
                        "degradationProfile": "missing-readings"
                    }
                },
                "comparison": {"scenarioCShowsControlledDegradation": True},
                "generatedAtUtc": "2026-05-18T23:32:55Z"
            }), encoding="utf-8")
            manifests = {}
            for scenario, name, degradation in (
                ("scenario_b", "scenario-b.json", "none"),
                ("scenario_c", "scenario-c.json", "missing-readings"),
            ):
                path = evidence / name
                path.write_text(json.dumps({
                    "scenarioCode": scenario, "sensorCount": 6, "numberOfCycles": 5,
                    "degradationProfile": degradation
                }), encoding="utf-8")
                manifests[scenario] = path.relative_to(repo).as_posix()
            sql_extracts = {}
            sql_values = {
                "scenario_b": ("d8203d4b-1839-4908-87ef-05633c1f1ae5", 30, 30),
                "scenario_c": ("36caca67-352c-41f1-80e3-8fe951a1582c", 24, 24),
            }
            for scenario, (run_id, inbox, assessments) in sql_values.items():
                path = evidence / f"{scenario}.sql.txt"
                path.write_text(
                    f"run | {run_id} | {scenario}\n"
                    f"inbox | {inbox}\n"
                    f"risk_assessments | {assessments} | 6\n"
                    "rejected | 0\nquarantined | 0\n",
                    encoding="utf-8",
                )
                sql_extracts[scenario] = path.relative_to(repo).as_posix()
            requirement = {
                "historicalSource": source.relative_to(repo).as_posix(),
                "scenarioManifests": manifests,
                "sqlSummaryExtracts": sql_extracts,
            }
            output = repo / "out"
            output.mkdir()
            rows, audit = MODULE.admit_historical_bc(repo, requirement, output)
            self.assertEqual("ADMITTED_HISTORICAL", audit["status"])
            self.assertEqual(2, len(rows))
            self.assertEqual(6, next(row for row in rows if row["scenario"] == "scenario_c")["missing"])
            self.assertTrue((output / "admitted/historical-runs.csv").is_file())
            self.assertTrue(audit["sqlExtractsReconciled"])

    def test_invalid_historical_source_is_not_admitted(self):
        with tempfile.TemporaryDirectory() as temp:
            repo = Path(temp)
            evidence = repo / "docs"
            evidence.mkdir()
            source = evidence / "comparison.json"
            source.write_text(json.dumps({"runs": {}, "comparison": {}}), encoding="utf-8")
            output = repo / "out"
            output.mkdir()
            rows, audit = MODULE.admit_historical_bc(
                repo, {"historicalSource": "docs/comparison.json", "scenarioManifests": {}}, output
            )
            self.assertEqual([], rows)
            self.assertEqual("INVALID_SOURCE", audit["status"])
            self.assertFalse((output / "admitted/historical-runs.csv").exists())

    def test_svg_distinguishes_actual_coverage_from_readiness(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "coverage.svg"
            MODULE.build_svg(path, "Coverage", [("Actual", 55.6), ("Readiness", 100)], "Target is not achieved evidence")
            text = path.read_text(encoding="utf-8")
            self.assertIn("55.6%", text)
            self.assertIn("100.0%", text)
            self.assertIn("Target is not achieved evidence", text)


if __name__ == "__main__":
    unittest.main()
