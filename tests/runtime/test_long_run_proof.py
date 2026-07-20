from __future__ import annotations

import importlib.util
import tempfile
import unittest
from unittest.mock import patch
from datetime import datetime, timedelta, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts/runtime/run-long-run-proof.py"
spec = importlib.util.spec_from_file_location("long_run_proof", SCRIPT)
assert spec and spec.loader
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class LongRunProofTests(unittest.TestCase):
    def operation(self, duration: int, terminal: str = "SystemCompleted", settled: bool = True):
        started = datetime(2026, 7, 14, tzinfo=timezone.utc)
        finished = started + timedelta(seconds=duration)
        return {
            "operationId": "11111111-1111-1111-1111-111111111111",
            "simulationRunId": "22222222-2222-2222-2222-222222222222",
            "startedAt": started.isoformat().replace("+00:00", "Z"),
            "finishedAt": finished.isoformat().replace("+00:00", "Z"),
            "systemCompletedAt": finished.isoformat().replace("+00:00", "Z") if terminal == "SystemCompleted" else None,
            "state": terminal,
            "terminalOutcome": terminal,
            "accounting": {
                "settled": settled,
                "pendingInbox": 0,
                "processingInbox": 0,
                "retryPendingInbox": 0,
                "expectedObservations": 6,
                "acceptedObservations": 6,
                "processedInbox": 6,
                "quarantinedInbox": 0,
            },
        }

    def test_completed_case_passes(self):
        case = {"id": "LR-090", "expectedMinimumWallSeconds": 84}
        result = module.evaluate_case(case, self.operation(91))
        self.assertEqual("PASS", result["status"])
        self.assertEqual("CompletedNormally", result["terminationReason"])

    def test_historical_one_minute_cutoff_fails(self):
        case = {"id": "LR-180", "expectedMinimumWallSeconds": 170}
        result = module.evaluate_case(case, self.operation(60))
        self.assertEqual("FAIL", result["status"])
        self.assertTrue(any("one-minute" in item for item in result["failures"]))

    def test_unknown_termination_fails(self):
        case = {"id": "LR-X", "expectedMinimumWallSeconds": 1}
        operation = self.operation(2, terminal="Mystery")
        result = module.evaluate_case(case, operation)
        self.assertEqual("FAIL", result["status"])
        self.assertEqual("Unknown", result["terminationReason"])

    def test_nonzero_exit_is_classified(self):
        operation = self.operation(3, terminal="Failed")
        operation["failureCode"] = "process_exit_nonzero"
        self.assertEqual("ProcessExitedNonZero", module.derive_termination_reason(operation))

    def test_settlement_wait_repolls_when_temporal_snapshot_is_below_expected_coverage(self):
        first = self.operation(300)
        first["accounting"].update(
            expectedObservations=36,
            acceptedObservations=33,
            processedInbox=33,
            settled=True,
        )
        complete = self.operation(303)
        complete["accounting"].update(
            expectedObservations=36,
            acceptedObservations=36,
            processedInbox=36,
            settled=True,
        )

        class FakeClient:
            def __init__(self):
                self.requests = 0

            def request(self, method, path):
                self.requests += 1
                return complete

        client = FakeClient()
        with patch.object(module.time, "sleep", return_value=None):
            result = module.wait_for_pipeline_settlement(
                client,
                first,
                poll_seconds=0,
                max_wait_seconds=1,
            )

        self.assertEqual(1, client.requests)
        self.assertEqual(36, result["accounting"]["acceptedObservations"])

    def test_matrix_loads(self):
        cases = module.load_matrix(ROOT / "config/runtime/long-run-proof-matrix.json")
        self.assertEqual(4, len(cases))
        self.assertEqual("LR-300-EVIDENCE", cases[-1]["id"])


    def test_expected_timeout_passes_without_settlement_requirement(self):
        case = {
            "id": "LR-S-ABOVE",
            "expectedMinimumWallSeconds": 40,
            "expectedOutcome": "TimedOut",
            "requireSettlement": False,
        }
        result = module.evaluate_case(case, self.operation(45, terminal="TimedOut", settled=False))
        self.assertEqual("PASS", result["status"])
        self.assertEqual("ConfiguredTimeout", result["terminationReason"])

    def test_expected_validation_rejection_passes_without_runtime_accounting(self):
        case = {
            "id": "LR-INVALID",
            "expectedOutcome": "Rejected",
            "expectedMinimumWallSeconds": 0,
            "requireDuration": False,
            "requireSettlement": False,
        }
        operation = {
            "state": "Rejected",
            "terminalOutcome": "Rejected",
            "httpStatus": 400,
            "acceptedAt": "2026-07-18T10:00:00Z",
            "finishedAt": "2026-07-18T10:00:00Z",
            "accounting": {},
        }
        result = module.evaluate_case(case, operation)
        self.assertEqual("PASS", result["status"])
        self.assertEqual("RequestRejected", result["terminationReason"])

    def test_final_matrix_loads_sync_and_validation_cases(self):
        path = ROOT / "config/runtime/long-run-final-matrix.json"
        cases = module.load_matrix(path)
        self.assertEqual(7, len(cases))
        self.assertEqual(["Rejected"], module.expected_terminal_outcomes(cases[-1]))

    def test_hash_manifest_is_deterministic_and_excludes_itself(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "a.txt").write_text("a", encoding="utf-8")
            module.write_hashes(root)
            lines = (root / "hashes.sha256").read_text(encoding="utf-8").splitlines()
            self.assertEqual(1, len(lines))
            self.assertTrue(lines[0].endswith("  a.txt"))

    def test_influx_configuration_failure_is_not_treated_as_transient_busy(self):
        error = module.ApiError(
            400,
            "POST",
            "/api/control/runtime/reset",
            '{"status":"Rejected","stores":[{"store":"InfluxDB","status":"Unavailable","message":"InfluxDB data-only delete API configuration is incomplete."}]}',
        )
        self.assertTrue(module.reset_error_is_infrastructure_precondition(error))
        self.assertFalse(module.reset_error_is_transient_busy(error))

    def test_unacknowledged_rabbitmq_reset_failure_is_transient_busy(self):
        error = module.ApiError(
            400,
            "POST",
            "/api/control/runtime/reset",
            '{"status":"Rejected","stores":[{"store":"RabbitMQ","status":"Busy","unacknowledged":1}]}',
        )
        self.assertFalse(module.reset_error_is_infrastructure_precondition(error))
        self.assertTrue(module.reset_error_is_transient_busy(error))


if __name__ == "__main__":
    unittest.main()
