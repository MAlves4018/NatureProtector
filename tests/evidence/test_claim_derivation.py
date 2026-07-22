from __future__ import annotations
import unittest
from tests.evidence._loader import load

MODULE = load("scripts/evidence/collect-report-integration-evidence.py", "phase7_claims")


class ClaimDerivationTests(unittest.TestCase):
    def test_frontend_failure_is_not_promoted(self):
        self.assertFalse(MODULE.component_state({"frontend": {"status": "FAIL"}}, "frontend")["passed"])

    def test_backend_pass_is_promoted(self):
        self.assertTrue(MODULE.component_state({"backend": {"status": "PASS"}}, "backend")["passed"])

    def test_runtime_requires_claim_ceiling(self):
        self.assertFalse(
            MODULE.component_state(
                {"currentRuntimeExecutionStatus": "PASS", "claimCeiling": {"currentIntegratedExecution": False}},
                "runtime",
            )["passed"]
        )

    def test_no_historical_fallback(self):
        self.assertEqual([], MODULE.historical_rows(None))

    def test_performance_counts_do_not_override_failed_status(self):
        summary = {
            "status": "FAIL",
            "currentResultCounts": {"latency": 3},
            "claimCeiling": {"currentPerformanceMeasurements": True},
        }
        self.assertFalse(MODULE.component_state(summary, "performance")["passed"])

    def test_performance_requires_measurements(self):
        summary = {
            "status": "PASS",
            "currentResultCounts": {},
            "claimCeiling": {"currentPerformanceMeasurements": True},
        }
        self.assertFalse(MODULE.component_state(summary, "performance")["passed"])


if __name__ == "__main__":
    unittest.main()
