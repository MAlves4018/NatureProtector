from __future__ import annotations

import csv
import json
import subprocess
import sys
import unittest
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
CONFIG_PATH = REPO / "config/acceptance/final-acceptance.json"


class FinalAcceptanceContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.config = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))

    def test_status_and_exit_code_contract_is_closed(self) -> None:
        self.assertEqual(
            self.config["statuses"],
            ["PASS", "FAIL", "BLOCKED_PREREQUISITE", "HARNESS_ERROR", "NOT_SELECTED"],
        )
        self.assertEqual(
            self.config["exitCodes"],
            {"PASS": 0, "FAIL": 1, "BLOCKED_PREREQUISITE": 2, "HARNESS_ERROR": 3, "NOT_SELECTED": 0},
        )

    def test_every_selected_stage_has_one_definition(self) -> None:
        components = self.config["components"]
        for profile_name, profile in self.config["profiles"].items():
            stages = profile["stages"]
            self.assertEqual(len(stages), len(set(stages)), profile_name)
            for stage in stages:
                self.assertIn(stage, components, f"{profile_name}:{stage}")
                self.assertGreater(components[stage]["timeoutSeconds"], 0)
                self.assertTrue(components[stage]["requiredCommands"])

    def test_static_audits_write_files_not_directories(self) -> None:
        for stage in (
            "configuration-audit",
            "control-plane-audit",
            "frontend-audit",
            "workflow-audit",
            "operations-audit",
            "script-audit",
            "final-repository-audit",
            "final-delivery-audit",
        ):
            arguments = self.config["components"][stage]["arguments"]
            self.assertIn("{stageEvidence}/result.json", arguments, stage)
            self.assertNotIn("{stageEvidence}", arguments, stage)

    def test_profiles_are_monotonic(self) -> None:
        profiles = self.config["profiles"]
        static = set(profiles["Static"]["stages"])
        smoke = set(profiles["Smoke"]["stages"])
        functional = set(profiles["Functional"]["stages"])
        full = set(profiles["Full"]["stages"])
        self.assertTrue(static < smoke)
        self.assertTrue(static < functional)
        self.assertTrue(functional < full)

    def test_p3_remains_fail_closed(self) -> None:
        p3 = self.config["components"]["controlled-validation-p3"]
        self.assertTrue(p3["requiresControlledValidationExecution"])
        self.assertNotIn("-Execute", p3["arguments"])
        self.assertNotIn("-AcknowledgeNonProduction", p3["arguments"])
        self.assertEqual(
            p3["controlledValidationExecutionArguments"],
            ["-Execute", "-AcknowledgeNonProduction"],
        )
        self.assertIn("docker", p3["requiredCommands"])
        self.assertNotIn("psql", p3["requiredCommands"])
        runner = (REPO / "scripts/acceptance/Invoke-NP-FinalAcceptance.ps1").read_text(encoding="utf-8")
        self.assertIn("ExecuteControlledValidationP3", runner)
        self.assertIn("AcknowledgeNonProduction", runner)
        self.assertIn("NP_RELIABILITY_AUTH_TOKEN", runner)

    def test_advanced_matrices_are_repository_local_and_guarded(self) -> None:
        for name in (
            "Invoke-SystemResetRecoveryMatrix.ps1",
            "Invoke-MultiReplicaTemporalMatrix.ps1",
            "Invoke-AutoscalingExperimentMatrix.ps1",
        ):
            source = (REPO / "scripts/testing" / name).read_text(encoding="utf-8")
            self.assertNotIn("NatureProtector.brain", source)
            self.assertIn("artifacts", source)
            self.assertIn("run-scoped child", source)
            self.assertIn("$MatrixOutputBase", source)
            self.assertIn("$FinalAcceptanceBase", source)
            self.assertIn("acceptance-result.json", source)

    def test_p3_stage_requires_exact_run_postgres_audit(self) -> None:
        source = (REPO / "scripts/acceptance/Invoke-NP-ControlledValidationP3.ps1").read_text(encoding="utf-8")
        self.assertIn("run-controlled-validation-p3.py", source)
        self.assertIn("run-postgres-audit.ps1", source)
        self.assertIn("ControlledValidationRunLabel", source)
        self.assertIn("postgresAuditStatus", source)
        self.assertIn("P3_EXECUTION_AND_EXACT_RUN_AUDIT_PASS", source)
        self.assertIn("--no-latest-pointer", source)
        self.assertIn("POSTGRES_HOST", source)
        self.assertIn("NP_POSTGRES_CONNECTION_STRING", source)

    def test_performance_skip_build_uses_supported_parameter(self) -> None:
        component = self.config["components"]["performance-smoke"]
        self.assertFalse(component["supportsSkipBuild"])
        runner = (REPO / "scripts/acceptance/Invoke-NP-FinalAcceptance.ps1").read_text(encoding="utf-8")
        self.assertIn("$arguments.Add('-NoBuild')", runner)

    def test_acceptance_root_cannot_equal_artifacts_root(self) -> None:
        module = (REPO / "scripts/acceptance/modules/Acceptance.Common.psm1").read_text(encoding="utf-8")
        self.assertIn("$resolved.Equals($acceptanceRoot", module)
        self.assertIn("artifactsRoot 'final-acceptance'", module)
        self.assertIn("run-scoped child", module)

    def test_runner_writes_the_normalized_output_contract(self) -> None:
        source = (REPO / "scripts/acceptance/Invoke-NP-FinalAcceptance.ps1").read_text(encoding="utf-8")
        for output in (
            "environment.json",
            "run-spec.json",
            "summary.json",
            "SUMMARY.md",
            "tests.csv",
            "commands.csv",
            "blockers.csv",
            "evidence-manifest.csv",
            "hashes.sha256",
        ):
            self.assertIn(output, source)
        for status in self.config["statuses"]:
            self.assertIn(status, (REPO / "scripts/acceptance/modules/Acceptance.Common.psm1").read_text(encoding="utf-8") + source)

    def test_quality_route_is_registered(self) -> None:
        path = REPO / "docs/reference/generated/ui-route-capability-matrix.csv"
        with path.open(newline="", encoding="utf-8") as handle:
            rows = list(csv.DictReader(handle))
        quality = [row for row in rows if row["route"] == "/quality"]
        self.assertEqual(len(quality), 1)
        self.assertEqual(quality[0]["status"], "page")
        self.assertEqual(quality[0]["required_capabilities"], "quality.read")
        self.assertFalse(any(row["status"] == "mounted-unregistered" for row in rows))

    def test_generated_catalogues_are_current_without_mutation(self) -> None:
        completed = subprocess.run(
            [sys.executable, "scripts/docs/generate_reference_catalogs.py", "--repo", str(REPO), "--check"],
            cwd=REPO,
            text=True,
            capture_output=True,
            check=False,
        )
        self.assertEqual(completed.returncode, 0, completed.stdout + completed.stderr)
        self.assertIn("catalogue_status: PASS", completed.stdout)


if __name__ == "__main__":
    unittest.main()
