from __future__ import annotations
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
VALIDATOR = ROOT / "tools/workflow-audit/validate.py"


def fixture(destination):
    shutil.copytree(ROOT / ".github", destination / ".github")
    target = destination / "config/quality"
    target.mkdir(parents=True)
    shutil.copy2(ROOT / "config/quality/workflow-convergence.json", target / "workflow-convergence.json")
    return destination


def validate(repo):
    return subprocess.run(
        [sys.executable, str(VALIDATOR), "--repo", str(repo)], text=True, capture_output=True, check=False
    )


class WorkflowAuthorityTests(unittest.TestCase):
    def test_repository_contract_passes(self):
        completed = validate(ROOT)
        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        self.assertEqual("PASS", json.loads(completed.stdout)["status"])

    def test_canonical_deployment_change_is_observed_without_freezing_workflow_evolution(self):
        with tempfile.TemporaryDirectory() as temp:
            repo = fixture(Path(temp))
            path = repo / ".github/workflows/cd-staging.yml"
            path.write_text(path.read_text(encoding="utf-8") + "\n# reviewed evolution\n", encoding="utf-8")
            completed = validate(repo)
            self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
            payload = json.loads(completed.stdout)
            self.assertTrue(
                any(item["workflow"] == "cd-staging.yml" for item in payload["workflow_snapshot_observations"])
            )

    def test_unpinned_tooling_action_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            repo = fixture(Path(temp))
            path = repo / ".github/workflows/quality-guardrails.yml"
            path.write_text(
                path.read_text(encoding="utf-8").replace(
                    "actions/checkout@34e114876b0b11c390a56381ad16ebd13914f8d5", "actions/checkout@v4"
                ),
                encoding="utf-8",
            )
            completed = validate(repo)
            self.assertNotEqual(0, completed.returncode)
            self.assertIn("external-action-pinned", completed.stdout)

    def test_missing_quality_workflow_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            repo = fixture(Path(temp))
            (repo / ".github/workflows/quality-guardrails.yml").unlink()
            self.assertNotEqual(0, validate(repo).returncode)


if __name__ == "__main__":
    unittest.main()
