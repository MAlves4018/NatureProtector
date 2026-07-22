from __future__ import annotations
import unittest
from types import SimpleNamespace
from tests.evidence._loader import load

MODULE = load("scripts/evidence/run-report-evidence-campaign.py", "campaign_profiles")


class CampaignProfileTests(unittest.TestCase):
    def test_static_and_quality_include_phase9_validation(self):
        self.assertEqual(["phase1", "phase3", "phase9", "phase11", "phase7"], MODULE.PROFILE_STEPS["static"])
        self.assertEqual(["phase1", "phase2", "phase3", "phase9", "phase11", "phase7"], MODULE.PROFILE_STEPS["quality"])

    def test_full_profile_orders_phase9_after_runtime_and_before_report_integration(self):
        self.assertEqual("phase9", MODULE.PROFILE_STEPS["full"][-3])
        self.assertEqual("phase11", MODULE.PROFILE_STEPS["full"][-2])
        self.assertEqual("phase7", MODULE.PROFILE_STEPS["full"][-1])
        self.assertIn("phase4", MODULE.PROFILE_STEPS["full"])
        self.assertIn("phase5", MODULE.PROFILE_STEPS["full"])
        self.assertIn("phase6", MODULE.PROFILE_STEPS["full"])

    def test_partial_package_is_success(self):
        rows = [SimpleNamespace(status="PASS"), SimpleNamespace(status="PASS_PARTIAL_REPORT_PACKAGE")]
        self.assertEqual("PASS_PARTIAL_REPORT_PACKAGE", MODULE.campaign_status(rows, [], True, "static"))


if __name__ == "__main__":
    unittest.main()
