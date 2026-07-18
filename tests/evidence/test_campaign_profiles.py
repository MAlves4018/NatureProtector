from __future__ import annotations
import unittest
from types import SimpleNamespace
from tests.evidence._loader import load

MODULE = load("scripts/evidence/run-report-evidence-campaign.py", "campaign_profiles")


class CampaignProfileTests(unittest.TestCase):
    def test_static_and_quality_include_partial_phase7(self):
        self.assertEqual(["phase1", "phase3", "phase7"], MODULE.PROFILE_STEPS["static"])
        self.assertEqual(["phase1", "phase2", "phase3", "phase7"], MODULE.PROFILE_STEPS["quality"])

    def test_partial_package_is_success(self):
        rows = [SimpleNamespace(status="PASS"), SimpleNamespace(status="PASS_PARTIAL_REPORT_PACKAGE")]
        self.assertEqual("PASS_PARTIAL_REPORT_PACKAGE", MODULE.campaign_status(rows, [], True, "static"))


if __name__ == "__main__":
    unittest.main()
