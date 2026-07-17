"""Static contract for the canonical local clone-to-run path.

This test intentionally does not start Docker or install dependencies. It
prevents the local wrapper, documentation and PostgreSQL bootstrap from
silently drifting apart. A full executable clean-room Windows CI job remains
a required post-integration validation and is documented in the handover.
"""

from __future__ import annotations

import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(relative_path: str) -> str:
    return (ROOT / relative_path).read_text(encoding="utf-8")


class LocalCloneToRunContractTests(unittest.TestCase):
    def test_canonical_cli_exposes_explicit_prepare_step(self) -> None:
        np_script = read("scripts/np.ps1")
        self.assertIn('"prepare-local"', np_script)
        self.assertIn("scripts\\setup\\Initialize-LocalWorkspace.ps1", np_script)
        self.assertIn("local-clone-to-run-contract", np_script)

        readme = read("README.md")
        setup_doc = read("docs/setup/local-baseline-setup.md")
        self.assertIn(r".\scripts\np.ps1 prepare-local", readme)
        self.assertIn(r".\scripts\np.ps1 prepare-local", setup_doc)
        for path in (
            "docs/runtime/local-runtime.md",
            "docs/freeze/FREEZE-CANDIDATE.md",
            "docs/architecture/current-capabilities-and-how-to-run.md",
            "docs/testing/validation-gates.md",
        ):
            self.assertIn(r".\scripts\np.ps1 prepare-local", read(path), path)

    def test_prepare_step_uses_locked_dependency_commands(self) -> None:
        prepare = read("scripts/setup/Initialize-LocalWorkspace.ps1")
        self.assertIn("dotnet restore", prepare)
        self.assertIn("NuGet.Config", prepare)
        self.assertRegex(prepare, r"(?m)^\s*& npm ci\s*$")
        self.assertNotIn("npm install", prepare)
        self.assertIn("webUI/package-lock.json", prepare)

    def test_start_fails_early_with_canonical_recovery_command(self) -> None:
        launcher = read("scripts/dev/start-local-runtime.ps1")
        self.assertIn(
            "node_modules 'vite/package.json'",
            launcher.replace("Join-Path $webUiNodeModules", "node_modules"),
        )
        self.assertIn(r".\scripts\np.ps1 prepare-local", launcher)
        self.assertIn("/health/ready", launcher)

    def test_start_enables_the_local_runtime_orchestrator_explicitly(self) -> None:
        launcher = read("scripts/dev/start-local-runtime.ps1")
        self.assertIn("RuntimeOrchestration__Mode", launcher)
        self.assertIn("'LocalProcess'", launcher)
        self.assertIn("RuntimeOrchestration__EvidenceMode", launcher)
        self.assertIn("'FileSystem'", launcher)
        self.assertIn("RuntimeOrchestration__WorkingDirectory", launcher)

    def test_start_propagates_influx_reset_configuration(self) -> None:
        launcher = read("scripts/dev/start-local-runtime.ps1")
        self.assertIn("InfluxDb__Enabled", launcher)
        self.assertIn("InfluxDb__Bucket", launcher)
        self.assertIn("INFLUXDB_BUCKET", launcher)

    def test_postgres_bootstrap_uses_effective_configuration(self) -> None:
        bootstrap = read("scripts/postgres/bootstrap-control-plane.ps1")
        self.assertIn("Read-NpDotEnv", bootstrap)
        self.assertIn("POSTGRES_HOST", bootstrap)
        self.assertIn("POSTGRES_PORT", bootstrap)
        self.assertIn("-EnvironmentFirst", bootstrap)
        self.assertIn("Test-NpTcpEndpoint", bootstrap)
        self.assertNotRegex(
            bootstrap,
            re.compile(
                r'Test-NetConnection\s+-ComputerName\s+["\']localhost["\']\s+-Port\s+5433'
            ),
        )

    def test_documented_port_keys_exist_and_are_consumed(self) -> None:
        dot_env = read(".env.example")
        launcher = read("scripts/dev/start-local-runtime.ps1")
        health = read("scripts/runtime/Test-LocalRuntimeHealth.ps1")

        for key in (
            "POSTGRES_HOST",
            "POSTGRES_PORT",
            "BACKOFFICE_API_PORT",
            "PREVENTION_HOST_PORT",
            "WEBUI_PORT",
        ):
            self.assertRegex(dot_env, rf"(?m)^{key}=.+$")

        for key in ("BACKOFFICE_API_PORT", "PREVENTION_HOST_PORT", "WEBUI_PORT"):
            self.assertIn(key, launcher)
            self.assertIn(key, health)

    def test_doctor_reports_checkout_dependency_state_without_installing(self) -> None:
        doctor = read("scripts/setup/Test-LocalPrerequisites.ps1")
        self.assertIn("Frontend lockfile", doctor)
        self.assertIn("Frontend dependencies", doctor)
        self.assertIn("prepare-local", doctor)
        self.assertNotIn("& npm ci", doctor)
        self.assertNotIn("dotnet restore", doctor)


if __name__ == "__main__":
    unittest.main(verbosity=2)
