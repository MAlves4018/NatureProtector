from __future__ import annotations

import json
import unittest
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
RUNNER = REPO / "scripts/operator/Invoke-NP-OperatorTestSuite.ps1"
CLEANER = REPO / "scripts/operator/Stop-NP-ExistingState.ps1"
P3 = REPO / "scripts/acceptance/Invoke-NP-ControlledValidationP3.ps1"
AUDIT = REPO / "tools/data-audit/run-postgres-audit.ps1"
CONFIG = REPO / "config/operator/local-test-suite.json"


class OperatorTestKitContractTests(unittest.TestCase):
    def test_default_path_and_profiles_are_explicit(self) -> None:
        config = json.loads(CONFIG.read_text(encoding="utf-8"))
        self.assertEqual(
            config["defaultRepositoryRoot"],
            r"C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector",
        )
        self.assertEqual(config["defaultProfile"], "Functional")
        self.assertEqual(set(config["profiles"]), {"Smoke", "Functional", "Full"})

    def test_cleanup_is_first_and_final(self) -> None:
        source = RUNNER.read_text(encoding="utf-8")
        self.assertLess(source.index("00-clean-existing-state"), source.index("01-prerequisites"))
        self.assertIn("99-final-cleanup", source)
        self.assertIn("finally", source)
        self.assertIn("Stop-NP-ExistingState.ps1", source)

    def test_functional_profile_runs_real_surfaces(self) -> None:
        source = RUNNER.read_text(encoding="utf-8")
        for required in (
            "workspace.ps1",
            "Invoke-LocalFunctionalValidation.ps1",
            "Invoke-NP-P0RuntimeCoverage.ps1",
            "Invoke-NP-ControlledValidationP3.ps1",
            "Invoke-SystemResetRecoveryMatrix.ps1",
            "Get-AdminBearerToken",
            "NP_RELIABILITY_AUTH_TOKEN",
        ):
            self.assertIn(required, source)

        functional_stage = source[source.index("04-functional-routes-and-runs"):source.index("05-degradation-rbac-diagnostics")]
        self.assertNotIn("Start-NpRuntime -Prefix 'functional'", functional_stage)
        self.assertIn("functional harness auto-starts up/start/health", functional_stage)

        functional_harness = (REPO / "scripts/validation/Invoke-LocalFunctionalValidation.ps1").read_text(encoding="utf-8")
        self.assertIn("function Ensure-Phase3RuntimeReady", functional_harness)
        self.assertIn("if (-not $CleanRoom)", functional_harness)
        self.assertIn("Ensure-Phase3RuntimeReady", functional_harness)
        self.assertIn('name = "np-up-runtime"', functional_harness)
        self.assertIn('name = "np-start-runtime"', functional_harness)
        self.assertIn('name = "np-health-runtime"', functional_harness)
        self.assertLess(
            functional_harness.index("Ensure-Phase3RuntimeReady", functional_harness.index("if (-not $CleanRoom)")),
            functional_harness.index('Test-HttpStatus -Name "Backoffice API /health"'),
        )


    def test_standard_and_docker_tests_are_separated_correctly(self) -> None:
        source = RUNNER.read_text(encoding="utf-8")
        self.assertLess(source.index("03-code-tests"), source.index("03b-docker-integration-tests"))
        self.assertLess(source.index("03b-docker-integration-tests"), source.index("04-functional-routes-and-runs"))
        self.assertIn("Category!=DockerIntegration", source)
        self.assertIn("Category=DockerIntegration", source)
        self.assertIn("operator-standard.trx", source)
        self.assertIn("operator-docker-integration.trx", source)
        self.assertIn("Get-DockerIntegrationEnvironment", source)
        self.assertIn("NP_TEST_POSTGRES_PASSWORD", source)
        self.assertIn("NP_TEST_RABBITMQ_PASSWORD", source)
        self.assertIn("NP_TEST_INFLUXDB_TOKEN", source)
        self.assertIn("NP_TEST_INFLUXDB_CONTAINER = 'np-influxdb'", source)
        self.assertIn("Assert-DockerIntegrationInfrastructureReady", source)
        self.assertIn("docker-integration-preflight.json", source)
        self.assertIn("inheritedInfluxContainer", source)
        self.assertIn("effectiveInfluxContainer", source)
        self.assertIn("Save-DockerDiagnostics", source)
        self.assertIn("{{.Id}}|{{.State.Running}}", source)
        self.assertIn("{{.State.Health.Status}}", source)
        self.assertNotIn("{{if .State.Health}}", source)
        self.assertIn("health = 'not-configured'", source)
        docker_stage = source[source.index("03b-docker-integration-tests"):source.index("04-functional-routes-and-runs")]
        self.assertLess(docker_stage.index("Assert-DockerIntegrationInfrastructureReady"), docker_stage.index("dotnet-docker-integration-tests"))
        self.assertNotIn("'--filter', 'Category!=Docker'", source)
        workspace = (REPO / "scripts/workspace.ps1").read_text(encoding="utf-8")
        self.assertIn("Category!=DockerIntegration", workspace)
        self.assertNotIn('"Category!=Docker"', workspace)

    def test_external_results_include_complete_dossier_zip(self) -> None:
        source = RUNNER.read_text(encoding="utf-8")
        self.assertIn("System.IO.Compression.ZipFile", source)
        self.assertIn("DOSSIER-$runId.zip", source)
        self.assertIn("ULTIMO-DOSSIER.txt", source)

    def test_full_profile_adds_heavy_gates(self) -> None:
        source = RUNNER.read_text(encoding="utf-8")
        for required in (
            "Invoke-MultiReplicaTemporalMatrix.ps1",
            "Invoke-AutoscalingExperimentMatrix.ps1",
            "Invoke-NP-UiPerformanceCoverage.ps1",
            "run-benchmarks.ps1",
            "workspace-security",
        ):
            self.assertIn(required, source)

    def test_cleaner_is_project_scoped_and_never_global_prunes(self) -> None:
        source = CLEANER.read_text(encoding="utf-8")
        self.assertIn("docker-compose.yml", source)
        self.assertIn("--project-directory", source)
        self.assertIn("--remove-orphans", source)
        self.assertIn("-v", source)
        self.assertNotIn("docker system prune", source.lower())
        self.assertIn("AllowUnknownPortOwners", source)
        self.assertIn("5254, 5260, 5173", source)


    def test_nested_runtime_harnesses_do_not_wait_for_np_start_wrapper_exit(self) -> None:
        p0 = (REPO / "scripts/acceptance/Invoke-NP-P0RuntimeCoverage.ps1").read_text(encoding="utf-8")
        ui = (REPO / "scripts/acceptance/Invoke-NP-UiPerformanceCoverage.ps1").read_text(encoding="utf-8")
        self.assertIn("function Invoke-P0StartRuntime", p0)
        self.assertIn("-NoBrowser", p0)
        self.assertIn("-ForceRestart", p0)
        self.assertNotIn("Invoke-P0LoggedProcess -Id 'np-start'", p0)
        self.assertIn("function Invoke-UiPerfStartRuntime", ui)
        self.assertIn("-NoBrowser", ui)
        self.assertIn("-ForceRestart", ui)
        self.assertNotIn("@{ id = 'np-start'; args = @('start'); timeout = 900 }", ui)

    def test_p3_can_use_container_psql_fallback(self) -> None:
        p3 = P3.read_text(encoding="utf-8")
        audit = AUDIT.read_text(encoding="utf-8")
        self.assertIn("BLOCKED_POSTGRES_CLIENT_MISSING", p3)
        self.assertIn("Get-Command docker", p3)
        self.assertIn("docker exec", audit)
        self.assertIn("docker cp", audit)
        self.assertIn("np-postgres", audit)

    def test_windows_command_shims_are_resolved_before_process_start(self) -> None:
        runner = RUNNER.read_text(encoding="utf-8")
        common = (REPO / "scripts/acceptance/modules/Acceptance.Common.psm1").read_text(encoding="utf-8")
        p0 = (REPO / "scripts/acceptance/Invoke-NP-P0RuntimeCoverage.ps1").read_text(encoding="utf-8")
        ui = (REPO / "scripts/acceptance/Invoke-NP-UiPerformanceCoverage.ps1").read_text(encoding="utf-8")

        self.assertIn("Resolve-NpAcceptanceCommandPath", common)
        self.assertIn("New-NpAcceptanceProcessInvocation", common)
        self.assertIn("'.cmd'", common)
        self.assertIn("'.bat'", common)
        self.assertIn("'.ps1'", common)
        self.assertIn("-EncodedCommand", common)
        self.assertIn("npm.cmd", common)
        self.assertIn("New-NpAcceptanceProcessInvocation -Executable $Executable", runner)
        self.assertNotIn("$startInfo.FileName = $Executable", runner)
        self.assertIn("toolchain-resolution.json", runner)
        self.assertIn("New-NpAcceptanceProcessInvocation -Executable $Executable", p0)
        self.assertIn("New-NpAcceptanceProcessInvocation -Executable $Executable", ui)

    def test_influx_environment_mutating_tests_are_serialized(self) -> None:
        loader = (REPO / "tests/NatureProtector.Infrastructure.Influx.Tests/Configuration/InfluxDbSettingsLoaderTests.cs").read_text(encoding="utf-8")
        dependency_injection = (REPO / "tests/NatureProtector.Infrastructure.Influx.Tests/DependencyInjection/ServiceCollectionExtensionsTests.cs").read_text(encoding="utf-8")
        collection = (REPO / "tests/NatureProtector.Infrastructure.Influx.Tests/InfluxEnvironmentVariablesCollection.cs").read_text(encoding="utf-8")

        self.assertIn('[Collection("InfluxEnvironmentVariables")]', loader)
        self.assertIn('[Collection("InfluxEnvironmentVariables")]', dependency_injection)
        self.assertIn('CollectionDefinition("InfluxEnvironmentVariables", DisableParallelization = true)', collection)
        self.assertGreaterEqual(loader.count('Environment.SetEnvironmentVariable'), 2)
        self.assertGreaterEqual(dependency_injection.count('Environment.SetEnvironmentVariable'), 2)

    def test_quality_navigation_test_and_implementation_are_installed_together(self) -> None:
        quality_test = (REPO / "webUI/src/app/capabilities.test.ts").read_text(encoding="utf-8")
        registry = (REPO / "webUI/src/app/navigation/pageRegistry.ts").read_text(encoding="utf-8")
        self.assertIn("page.id === 'quality'", quality_test)
        self.assertIn("'quality',", quality_test)
        self.assertIn("id: 'quality'", registry)
        self.assertIn("requiredCapabilities: ['quality.read']", registry)
        self.assertLess(registry.index("id: 'quality'"), registry.index("id: 'qa'"))



if __name__ == "__main__":
    unittest.main()
