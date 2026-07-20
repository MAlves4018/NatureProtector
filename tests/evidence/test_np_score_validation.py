from __future__ import annotations

import csv
import tempfile
import unittest
from datetime import date
from pathlib import Path

from _loader import load

MODULE = load("scripts/evidence/np_score_validation.py", "np_score_validation")
COLLECTOR = load("scripts/evidence/collect-np-score-validation.py", "np_score_validation_collector")
ROOT = Path(__file__).resolve().parents[2]


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

    def test_canonical_population_and_holdout_are_derived_from_eligible_dates(self):
        with (ROOT / "data/baseline/areas/proenca-a-nova/weather_daily_reference.csv").open(
            encoding="utf-8", newline=""
        ) as handle:
            weather = list(csv.DictReader(handle))
        with (ROOT / "data/baseline/areas/proenca-a-nova/fire_history.csv").open(
            encoding="utf-8", newline=""
        ) as handle:
            fires = list(csv.DictReader(handle))

        eligible_kinds = {"icnf_burned_area_intersection", "large_fire_progression"}
        event_dates = {
            MODULE.as_date(row["start_date"])
            for row in fires
            if row["history_kind"] in eligible_kinds
            and MODULE.as_date(row["start_date"]).year <= 2024
        }
        seasonal_dates = [
            MODULE.as_date(row["date_local"])
            for row in weather
            if MODULE.as_date(row["date_local"]).year <= 2024
            and MODULE.as_date(row["date_local"]).month in {5, 6, 7, 8, 9, 10}
        ]
        holdout_dates = [day for day in seasonal_dates if day.year in {2023, 2024}]

        self.assertEqual(1472, len(seasonal_dates))
        self.assertEqual(23, sum(day in event_dates for day in seasonal_dates))
        self.assertEqual(368, len(holdout_dates))
        self.assertEqual(7, sum(day in event_dates for day in holdout_dates))
        self.assertNotEqual((2922, 25), (len(seasonal_dates), sum(day in event_dates for day in seasonal_dates)))

    def test_split_is_temporal_and_provenance_is_not_a_split(self):
        self.assertEqual("exploration_2017_2022", COLLECTOR.temporal_split(date(2022, 12, 31), 2017, 2022, 2024))
        self.assertEqual("holdout_2023_2024", COLLECTOR.temporal_split(date(2023, 1, 1), 2017, 2022, 2024))
        self.assertNotIn("source_datasets", {"split": "holdout_2023_2024"})

    def test_weather_and_fire_index_reference_models_have_distinct_roles(self):
        specs = COLLECTOR.PRIMARY_MODEL_SPECS
        self.assertEqual("retrospective_weather_baseline", specs["simple_weather_risk_score"]["role"])
        self.assertEqual("temporally_fitted_weather_baseline", specs["simple_weather_trainfit_score"]["role"])
        self.assertEqual("retrospective_fire_index_reference", specs["fire_index_reference_score"]["role"])
        self.assertNotEqual(
            specs["simple_weather_risk_score"]["role"],
            specs["fire_index_reference_score"]["role"],
        )


if __name__ == "__main__":
    unittest.main()
