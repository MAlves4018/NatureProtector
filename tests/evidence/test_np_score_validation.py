from __future__ import annotations

import tempfile
import unittest
from datetime import date
from pathlib import Path

from _loader import load

MODULE = load("scripts/evidence/np_score_validation.py", "np_score_validation_core")


class NpScoreValidationTests(unittest.TestCase):
    def test_metric_boundaries_match_candidate_v1(self):
        self.assertEqual(0.40, MODULE.metric_risk("temperature", 29.999))
        self.assertEqual(0.65, MODULE.metric_risk("temperature", 30.0))
        self.assertEqual(0.40, MODULE.metric_risk("humidity", 35.0))
        self.assertEqual(0.70, MODULE.metric_risk("humidity", 34.999))
        self.assertEqual(0.30, MODULE.metric_risk("wind", 5.0))

    def test_known_auc(self):
        labels = [0, 0, 1, 1]
        scores = [0.1, 0.4, 0.35, 0.8]
        self.assertAlmostEqual(0.75, MODULE.roc_auc(labels, scores), places=8)

    def test_average_precision_groups_equal_thresholds(self):
        labels = [1, 0, 0, 1]
        scores = [0.5, 0.5, 0.5, 0.5]
        self.assertAlmostEqual(0.5, MODULE.average_precision(labels, scores), places=8)
        points = MODULE.precision_recall_curve(labels, scores)
        self.assertEqual(2, len(points))

    def test_mann_whitney_reports_tie_corrected_approximation(self):
        result = MODULE.mann_whitney([1, 1, 0, 0], [0.5, 0.5, 0.5, 0.5])
        self.assertTrue(result["tieCorrected"])
        self.assertAlmostEqual(1.0, result["pApprox"], places=8)
        self.assertAlmostEqual(0.0, result["cliffsDelta"], places=8)

    def test_area_aggregation_uses_nearest_rank_p80_and_max(self):
        aggregation = {"percentile": 0.8, "percentileWeight": 0.7, "maximumWeight": 0.3}
        self.assertAlmostEqual(0.7 * 0.8 + 0.3 * 1.0, MODULE.aggregate_area([0.1, 0.2, 0.4, 0.8, 1.0], aggregation))

    def test_matched_controls_are_deterministic_and_exclude_events(self):
        events = [date(2020, 7, 10)]
        candidates = [date(2019, 7, day) for day in range(1, 10)] + events
        first = MODULE.matched_control_dates(events, candidates, set(events), 3, 42)
        second = MODULE.matched_control_dates(events, candidates, set(events), 3, 42)
        self.assertEqual(first, second)
        self.assertNotIn(events[0], first[events[0]])

    def test_formula_contract_detects_drift(self):
        config = {"formulaContract": {"version": "V1", "constants": {"Weight": 0.5}}}
        checks = MODULE.validate_formula_contract(config, {"Version": "V1", "Weight": 0.6})
        self.assertFalse(all(item["match"] for item in checks))


if __name__ == "__main__":
    unittest.main()
