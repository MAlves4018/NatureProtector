from __future__ import annotations
import importlib.util
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "validate.py"
SPEC = importlib.util.spec_from_file_location("configuration_authority_validator", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)
ROOT = Path(__file__).resolve().parents[3]


class ConfigurationAuthorityTests(unittest.TestCase):
    def test_repository_passes(self):
        result = MODULE.validate_repository(ROOT)
        self.assertEqual("PASS", result["status"], result["errors"])

    def test_project_package_version_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "repo"
            shutil.copytree(
                ROOT,
                target,
                ignore=shutil.ignore_patterns(
                    "node_modules", "artifacts", "dist", "coverage", "bin", "obj", "__pycache__"
                ),
            )
            project = target / "src/NatureProtector.Backoffice.Api/NatureProtector.Backoffice.Api.csproj"
            text = project.read_text(encoding="utf-8").replace(
                'PackageReference Include="Microsoft.AspNetCore.OpenApi"',
                'PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.10"',
            )
            project.write_text(text, encoding="utf-8")
            result = MODULE.validate_repository(target)
            self.assertTrue(any(x.startswith("project-package-version-declared:") for x in result["errors"]))

    def test_canonical_deployment_change_is_reported_without_freezing_evolution(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "repo"
            shutil.copytree(
                ROOT,
                target,
                ignore=shutil.ignore_patterns(
                    "node_modules", "artifacts", "dist", "coverage", "bin", "obj", "__pycache__"
                ),
            )
            manifest = json.loads((target / MODULE.AUTHORITY_MANIFEST).read_text(encoding="utf-8"))
            rel = next(iter(manifest["protected_hashes"]))
            path = target / rel
            path.write_bytes(path.read_bytes() + b"\n")
            result = MODULE.validate_repository(target)
            self.assertEqual("PASS", result["status"], result["errors"])
            self.assertTrue(any(item["path"] == rel for item in result["deployment_snapshot_observations"]))

    def test_np_semantic_merge_requires_deployment_markers(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "repo"
            shutil.copytree(
                ROOT,
                target,
                ignore=shutil.ignore_patterns(
                    "node_modules", "artifacts", "dist", "coverage", "bin", "obj", "__pycache__"
                ),
            )
            path = target / "scripts/np.ps1"
            text = path.read_text(encoding="utf-8").replace("environment-remediation-static", "removed-marker")
            path.write_text(text, encoding="utf-8")
            result = MODULE.validate_repository(target)
            self.assertTrue(
                any(
                    "semantic-merge-deployment-marker-missing:scripts/np.ps1:environment-remediation-static" == x
                    for x in result["errors"]
                )
            )


if __name__ == "__main__":
    unittest.main()
