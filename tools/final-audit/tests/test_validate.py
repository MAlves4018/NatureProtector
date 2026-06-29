from __future__ import annotations
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
VALIDATOR = ROOT / "tools/final-audit/validate.py"


def validate(repo):
    return subprocess.run(
        [sys.executable, str(VALIDATOR), "--repo", str(repo)], text=True, capture_output=True, check=False
    )


class FinalCleanupTests(unittest.TestCase):
    def test_repository_contract_passes(self):
        completed = validate(ROOT)
        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        self.assertEqual("PASS", json.loads(completed.stdout)["status"])

    def test_missing_canonical_deployment_wrapper_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            repo = Path(temp) / "repo"
            shutil.copytree(
                ROOT,
                repo,
                ignore=shutil.ignore_patterns(
                    "node_modules", "dist", "coverage", "test-results", "__pycache__", "artifacts"
                ),
            )
            (repo / "scripts/cloud/probes/capacity.ps1").unlink()
            completed = validate(repo)
            self.assertNotEqual(0, completed.returncode)
            self.assertIn("deployment-wrapper-preserved", completed.stdout)

    def test_reintroduced_dependency_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            repo = Path(temp) / "repo"
            shutil.copytree(
                ROOT,
                repo,
                ignore=shutil.ignore_patterns(
                    "node_modules", "dist", "coverage", "test-results", "__pycache__", "artifacts"
                ),
            )
            path = repo / "webUI/package.json"
            package = json.loads(path.read_text(encoding="utf-8"))
            package["dependencies"]["react-leaflet"] = "1.0.0"
            path.write_text(json.dumps(package, indent=2) + "\n", encoding="utf-8")
            completed = validate(repo)
            self.assertNotEqual(0, completed.returncode)
            self.assertIn("npm-package-not-declared:react-leaflet", completed.stdout)


if __name__ == "__main__":
    unittest.main()
