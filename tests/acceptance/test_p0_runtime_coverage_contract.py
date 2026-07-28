from __future__ import annotations

import csv
import json
import re
import unittest
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
FINAL_CONFIG = json.loads((REPO / "config/acceptance/final-acceptance.json").read_text(encoding="utf-8"))
P0_CONFIG = json.loads((REPO / "config/acceptance/p0-runtime-coverage.json").read_text(encoding="utf-8"))
P0_SCRIPT = (REPO / "scripts/acceptance/Invoke-NP-P0RuntimeCoverage.ps1").read_text(encoding="utf-8")
VERIFIER = (REPO / "scripts/acceptance/verify_scenario_profile_matrix.py").read_text(encoding="utf-8")


class P0RuntimeCoverageContractTests(unittest.TestCase):
    def test_stage_is_selected_by_functional_and_full_only(self) -> None:
        self.assertIn("p0-runtime-coverage", FINAL_CONFIG["profiles"]["Functional"]["stages"])
        self.assertIn("p0-runtime-coverage", FINAL_CONFIG["profiles"]["Full"]["stages"])
        self.assertNotIn("p0-runtime-coverage", FINAL_CONFIG["profiles"]["Static"]["stages"])
        self.assertNotIn("p0-runtime-coverage", FINAL_CONFIG["profiles"]["Smoke"]["stages"])
        component = FINAL_CONFIG["components"]["p0-runtime-coverage"]
        self.assertTrue(component["supportsSkipBuild"])
        self.assertEqual(component["timeoutSeconds"], 10800)
        self.assertIn("docker", component["requiredCommands"])
        self.assertIn("python", component["requiredCommands"])
        self.assertIn("{stageEvidence}", component["arguments"])

    def test_profile_matrix_exactly_matches_runtime_catalog(self) -> None:
        source = (REPO / "src/NatureProtector.Simulator.Host/Services/SimulationDegradationProfiles.cs").read_text(encoding="utf-8")
        values = re.findall(r'public const string \w+\s*=\s*"([^"]+)";', source)
        configured = P0_CONFIG["scenarioMatrix"]["profiles"]
        self.assertEqual(set(configured), set(values))
        self.assertEqual(len(configured), 12)
        self.assertEqual(len(configured), len(set(configured)))

    def test_rbac_matrix_uses_seeded_roles_and_real_capabilities(self) -> None:
        with (REPO / "docs/reference/generated/role-capability-matrix.csv").open(newline="", encoding="utf-8") as handle:
            rows = list(csv.DictReader(handle))
        role_caps: dict[str, set[str]] = {}
        for row in rows:
            role_caps.setdefault(row["role"], set()).add(row["capability_value"])
        for spec in P0_CONFIG["rbac"]["roles"]:
            role = spec["role"]
            self.assertIn(role, role_caps)
            self.assertTrue(set(spec["requiredCapabilities"]).issubset(role_caps[role]), role)
            self.assertTrue(set(spec["forbiddenCapabilities"]).isdisjoint(role_caps[role]), role)
            self.assertEqual(spec["deniedProbe"]["expectedStatus"], 403)

    def test_diagnostic_contract_tracks_exact_generated_catalog(self) -> None:
        with (REPO / P0_CONFIG["diagnostics"]["generatedCatalog"]).open(newline="", encoding="utf-8") as handle:
            ids = [row["diagnostic_id"] for row in csv.DictReader(handle)]
        self.assertEqual(len(ids), 28)
        self.assertEqual(len(ids), len(set(ids)))
        self.assertTrue(P0_CONFIG["diagnostics"]["requireExactCatalogMatch"])
        self.assertIn("runtime diagnostic catalog exact match", P0_SCRIPT)
        self.assertIn("foreach ($diagnosticId in $runtimeIds)", P0_SCRIPT)

    def test_harness_collects_real_persisted_and_delivery_evidence(self) -> None:
        for token in (
            "projection.accepted_reading_log",
            "pipeline.event_inbox",
            "pipeline.processing_attempts",
            "projection.cycle_observation",
            "Get-P0PublishEvents",
            "operationByRequest",
            "operationByRun",
            "case-evidence.json",
        ):
            self.assertIn(token, P0_SCRIPT)
        self.assertNotIn("projection.cycle_observations", P0_SCRIPT)
        self.assertIn("WHEN 3 THEN 'RetryScheduled'", P0_SCRIPT)
        self.assertNotIn('"Outcome"::text', P0_SCRIPT)

    def test_harness_is_deterministic_scoped_and_fail_closed(self) -> None:
        for token in (
            "OutputRoot must be a run-scoped child",
            "exclusive local runtime precondition",
            "np-clean-local",
            "Refusing to overlap acceptance execution",
            "-KeepRuntime selected; a P0 acceptance run cannot pass",
            "no running project containers after down",
        ):
            self.assertIn(token, P0_SCRIPT)
        self.assertNotIn("NatureProtector.brain", P0_SCRIPT)
        self.assertNotIn("$last.pending", P0_SCRIPT)
        self.assertNotIn("$last.processing", P0_SCRIPT)
        self.assertNotIn("$last.retryPending", P0_SCRIPT)

    def test_output_contract_is_machine_readable_and_hashed(self) -> None:
        for output in (
            "run-spec.json",
            "acceptance-result.json",
            "summary.json",
            "SUMMARY.md",
            "tests.csv",
            "commands.csv",
            "blockers.csv",
            "hashes.sha256",
            "evidence-manifest.csv",
            "scenario-matrix-result.json",
        ):
            self.assertIn(output, P0_SCRIPT + VERIFIER)
        self.assertIn("P0_RUNTIME_FUNCTIONAL_COVERAGE_PASS", P0_SCRIPT)
        self.assertIn("SCENARIO_PROFILE_MATRIX_PASS", VERIFIER)


    def test_diagnostics_prepare_both_sides_of_b_c_comparison(self) -> None:
        prep = P0_CONFIG["diagnostics"]["prepareScenarioC"]
        self.assertEqual(prep["scenarioCode"], "scenario_c")
        self.assertEqual(prep["primaryProfile"], "missing-readings")
        self.assertIn("scenario-c-prerequisite-run.json", P0_SCRIPT)
        self.assertLess(P0_SCRIPT.index("scenario-c-prerequisite-run.json"), P0_SCRIPT.rindex("Invoke-P0DiagnosticCoverage"))

    def test_runtime_urls_and_influx_auth_match_local_compose_contract(self) -> None:
        env_values: dict[str, str] = {}
        for line in (REPO / ".env.example").read_text(encoding="utf-8").splitlines():
            if not line or line.lstrip().startswith("#") or "=" not in line:
                continue
            key, value = line.split("=", 1)
            env_values[key.strip()] = value.strip()
        self.assertEqual(P0_CONFIG["runtime"]["influxHealthUrl"], f"http://127.0.0.1:{env_values['INFLUXDB_PORT']}/health")
        self.assertEqual(P0_CONFIG["runtime"]["rabbitManagementUrl"], f"http://127.0.0.1:{env_values['RABBITMQ_MANAGEMENT_PORT']}")
        self.assertEqual(P0_CONFIG["runtime"]["grafanaHealthUrl"], f"http://127.0.0.1:{env_values['GRAFANA_PORT']}/api/health")
        self.assertEqual(P0_CONFIG["runtime"]["influxTokenEnvironmentVariable"], "INFLUXDB_TOKEN")
        self.assertEqual(P0_CONFIG["runtime"]["influxDatabaseEnvironmentVariable"], "INFLUXDB_DATABASE")
        self.assertEqual(P0_CONFIG["runtime"]["defaultInfluxDatabase"], env_values["INFLUXDB_DATABASE"])
        self.assertIn('Authorization = "Bearer $InfluxToken"', P0_SCRIPT)
        self.assertIn("InfluxDB authenticated health", P0_SCRIPT)
        self.assertIn("/api/v3/query_sql", P0_SCRIPT)
        self.assertIn("simulation_run_id", P0_SCRIPT)
        self.assertIn("influx-run-query.json", P0_SCRIPT)
        self.assertIn("Invoke-P0ObservabilityCoverage -SimulationRunId", P0_SCRIPT)

    def test_observability_statuses_are_explicitly_allowlisted(self) -> None:
        allowed = P0_CONFIG["observability"]["allowedOperationalStatuses"]
        self.assertEqual(allowed, ["Healthy", "Degraded", "AuthRequired"])
        self.assertIn("allowedOperationalStatuses", P0_SCRIPT)
        self.assertNotIn("-notin @('Unhealthy', 'Unknown')", P0_SCRIPT)


    def test_rbac_lifecycle_revalidates_authority_after_role_removal(self) -> None:
        for token in (
            "user lifecycle create/read/update",
            "membership visibility",
            "roleless-capabilities.json",
            "removal changes fresh authority",
            "invalid credentials rejected",
            "anonymous protected endpoint rejected",
            "admin logout endpoint",
        ):
            self.assertIn(token, P0_SCRIPT)

    def test_all_p0_areas_are_exercised(self) -> None:
        for area in (
            "scenario-matrix",
            "rbac",
            "diagnostics",
            "alerts",
            "observability",
            "evidence",
            "shutdown",
        ):
            self.assertIn(f"-Area '{area}'", P0_SCRIPT)


if __name__ == "__main__":
    unittest.main()
