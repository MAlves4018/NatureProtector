from __future__ import annotations

import csv
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
FINAL = json.loads((REPO / "config/acceptance/final-acceptance.json").read_text(encoding="utf-8"))
CONFIG = json.loads((REPO / "config/acceptance/ui-performance-coverage.json").read_text(encoding="utf-8"))
SCRIPT = (REPO / "scripts/acceptance/Invoke-NP-UiPerformanceCoverage.ps1").read_text(encoding="utf-8")
SPEC = (REPO / "webUI/e2e/live-role-journeys.spec.ts").read_text(encoding="utf-8")


class UiPerformanceCoverageContractTests(unittest.TestCase):
    def test_stage_is_full_only_and_fail_closed(self) -> None:
        self.assertIn("ui-performance-coverage", FINAL["profiles"]["Full"]["stages"])
        for profile in ("Static", "Smoke", "Functional"):
            self.assertNotIn("ui-performance-coverage", FINAL["profiles"][profile]["stages"])
        component = FINAL["components"]["ui-performance-coverage"]
        self.assertTrue(component["supportsSkipBuild"])
        self.assertIn("docker", component["requiredCommands"])
        self.assertIn("{stageEvidence}", component["arguments"])
        self.assertEqual(component["timeoutSeconds"], 7200)

    def test_live_role_matrix_matches_seeded_backend_capabilities(self) -> None:
        with (REPO / "docs/reference/generated/role-capability-matrix.csv").open(newline="", encoding="utf-8") as handle:
            rows = list(csv.DictReader(handle))
        capabilities: dict[str, set[str]] = {}
        for row in rows:
            capabilities.setdefault(row["role"], set()).add(row["capability_value"])
        configured_roles = {item["role"] for item in CONFIG["playwright"]["roles"]}
        self.assertEqual(configured_roles, {"Admin", "Sim", "Pipeline", "QA", "Operations", "ReleaseApprover"})
        for journey in CONFIG["playwright"]["roles"]:
            self.assertTrue(set(journey["requiredCapabilities"]).issubset(capabilities[journey["role"]]))
            self.assertTrue(set(journey["forbiddenCapabilities"]).isdisjoint(capabilities[journey["role"]]))

    def test_live_browser_suite_uses_real_identity_lifecycle_and_axe(self) -> None:
        for token in (
            "request.newContext",
            "/api/users-roles/login",
            "/api/users-roles/roles",
            "/api/users-roles/users",
            "AxeBuilder",
            "Acesso negado",
            "createdUserIds",
            "api.delete",
            "Unexpected HTTP 5xx responses",
            "Unexpected browser errors",
        ):
            self.assertIn(token, SPEC)
        self.assertIn("test.describe.serial", SPEC)
        self.assertIn("LIVE_RUNTIME", SPEC)

    def test_rate_limit_contract_requires_retry_after_policy_and_health_bypass(self) -> None:
        source = (REPO / "scripts/acceptance/verify_rate_limit_contract.py").read_text(encoding="utf-8")
        for token in ("Retry-After", "X-RateLimit-Policy", "problem details policy", "health unrestricted"):
            self.assertIn(token, source)
        self.assertEqual(CONFIG["rateLimiting"]["expectedPolicy"], "authentication")
        self.assertEqual(CONFIG["rateLimiting"]["expectedLimitStatus"], 429)
        self.assertEqual(CONFIG["rateLimiting"]["healthPathsAfterLimit"], ["/health/live", "/health/ready"])

    def test_bounded_performance_contract_has_calibration_and_b0(self) -> None:
        self.assertEqual([item["profile"] for item in CONFIG["performance"]["http"]["profiles"]], ["Calibration", "B0"])
        self.assertEqual([item["profile"] for item in CONFIG["performance"]["system"]["profiles"]], ["Calibration", "B0"])
        self.assertTrue(CONFIG["performance"]["system"]["requireAcceptedEqualsExpected"])
        self.assertTrue(CONFIG["performance"]["system"]["requireRiskEqualsExpected"])
        self.assertTrue(CONFIG["performance"]["system"]["requireFinalQueueEmpty"])
        self.assertIn("not establish production capacity", CONFIG["performance"]["claimBoundary"])
        for token in ("run-http-workload.py", "run-system-capacity-workload.ps1", "CalibrationRunDirectory", "verify_ui_performance_coverage.py"):
            self.assertIn(token, SCRIPT)

    def test_obsolete_ui_v2_route_is_not_used_by_current_performance_probes(self) -> None:
        http_source = (REPO / "scripts/performance/run-http-workload.py").read_text(encoding="utf-8")
        readiness_source = (REPO / "scripts/performance/run-local-readiness-workload.ps1").read_text(encoding="utf-8")
        self.assertNotIn('"/ui-v2"', http_source)
        self.assertNotIn('"/ui-v2"', readiness_source)
        self.assertIn('"/demo"', http_source)
        self.assertIn('"/demo"', readiness_source)

    def test_sensitive_live_browser_artifacts_are_disabled(self) -> None:
        playwright_config = (REPO / "webUI/playwright.config.ts").read_text(encoding="utf-8")
        self.assertIn("NP_UI_SENSITIVE_ACCEPTANCE", playwright_config)
        self.assertIn("sensitiveAcceptance ? 'off' : 'retain-on-failure'", playwright_config)
        self.assertIn("NP_UI_SENSITIVE_ACCEPTANCE = '1'", SCRIPT)
        self.assertIn("NP_UI_SENSITIVE_ACCEPTANCE = '0'", SCRIPT)

    def test_harness_is_scoped_redacted_and_cleans_up(self) -> None:
        for token in (
            "OutputRoot must be a run-scoped child",
            "NP_PERFORMANCE_AUTH_TOKEN",
            "Administrator token acquired and retained only in process memory",
            "np-stop",
            "np-down",
            "no running project containers",
            "acceptance-result.json",
            "hashes.sha256",
        ):
            self.assertIn(token, SCRIPT)
        self.assertNotIn("NatureProtector.brain", SCRIPT)

    def test_verifier_accepts_complete_synthetic_evidence_and_rejects_queue_regression(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self._write_synthetic_evidence(root, queue_final=0)
            output = root / "verification.json"
            command = [
                sys.executable,
                str(REPO / "scripts/acceptance/verify_ui_performance_coverage.py"),
                "--config",
                str(REPO / "config/acceptance/ui-performance-coverage.json"),
                "--evidence-root",
                str(root),
                "--output",
                str(output),
            ]
            passed = subprocess.run(command, text=True, capture_output=True, check=False)
            self.assertEqual(passed.returncode, 0, passed.stdout + passed.stderr)
            self.assertEqual(json.loads(output.read_text(encoding="utf-8"))["status"], "PASS")

            self._write_synthetic_evidence(root, queue_final=1)
            failed = subprocess.run(command, text=True, capture_output=True, check=False)
            self.assertEqual(failed.returncode, 1, failed.stdout + failed.stderr)
            self.assertEqual(json.loads(output.read_text(encoding="utf-8"))["status"], "FAIL")

            self._write_synthetic_evidence(root, queue_final=0)
            system_summary = root / "performance/system/B0/system-B0-synthetic/summary.json"
            payload = json.loads(system_summary.read_text(encoding="utf-8"))
            payload["backlogDrainMs"]["p95"] = None
            system_summary.write_text(json.dumps(payload), encoding="utf-8")
            missing_metric = subprocess.run(command, text=True, capture_output=True, check=False)
            self.assertEqual(missing_metric.returncode, 1, missing_metric.stdout + missing_metric.stderr)
            self.assertEqual(json.loads(output.read_text(encoding="utf-8"))["status"], "FAIL")

    @staticmethod
    def _write_synthetic_evidence(root: Path, queue_final: int) -> None:
        for suite in ("fixture", "live"):
            path = root / "ui" / suite / "playwright-results.json"
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(json.dumps({"stats": {"expected": 2, "unexpected": 0, "skipped": 0}}), encoding="utf-8")
        rate = root / "rate-limit/rate-limit-result.json"
        rate.parent.mkdir(parents=True, exist_ok=True)
        rate.write_text(json.dumps({"status": "PASS"}), encoding="utf-8")
        for profile, attempts in (("Calibration", 24), ("B0", 160)):
            directory = root / "performance/http" / profile
            directory.mkdir(parents=True, exist_ok=True)
            (directory / "status.json").write_text(json.dumps({"status": "PASS", "measuredAttempts": attempts}), encoding="utf-8")
            (directory / "summary.json").write_text(json.dumps([{"p95ElapsedMs": 10.0}]), encoding="utf-8")
        for profile, successful in (("Calibration", 1), ("B0", 2)):
            directory = root / "performance/system" / profile / f"system-{profile}-synthetic"
            directory.mkdir(parents=True, exist_ok=True)
            (directory / "summary.json").write_text(
                json.dumps(
                    {
                        "profile": profile,
                        "status": "Completed",
                        "successfulRuns": successful,
                        "failedRuns": 0,
                        "expectedEventsTotal": 8,
                        "acceptedReadingsTotal": 8,
                        "riskAssessmentsTotal": 8,
                        "rejectedTotal": 0,
                        "quarantinedTotal": 0,
                        "lostEventsTotal": 0,
                        "elapsedMs": {"p95": 1000},
                        "backlogDrainMs": {"p95": 100},
                        "queueTotalAfter": {"final": queue_final},
                    }
                ),
                encoding="utf-8",
            )


if __name__ == "__main__":
    unittest.main()
