from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
VALIDATOR = ROOT / "tools/quality-gates/validate.py"
RUNNER = ROOT / "tools/quality-gates/run.py"


class QualityGateTests(unittest.TestCase):
    def test_repository_policy_passes(self) -> None:
        completed = subprocess.run(
            [sys.executable, str(VALIDATOR), "--repo", str(ROOT)],
            text=True,
            capture_output=True,
            check=False,
        )
        self.assertEqual(completed.returncode, 0, completed.stdout + completed.stderr)
        payload = json.loads(completed.stdout)
        self.assertEqual(payload["status"], "PASS")
        policy = json.loads((ROOT / "config/quality/quality-gates.json").read_text(encoding="utf-8"))
        self.assertEqual(payload["summary"]["gates"], len(policy["gates"]))

    def test_report_mode_never_promotes_findings_to_failure(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "config.json").write_text(
                json.dumps(
                    {
                        "schema_version": 1,
                        "default_mode": "report",
                        "gates": [
                            {
                                "id": "failing",
                                "description": "fixture",
                                "rollout": "enforce",
                                "cwd": ".",
                                "command": ["python", "-c", "raise SystemExit(7)"],
                            }
                        ],
                    }
                ),
                encoding="utf-8",
            )
            completed = subprocess.run(
                [sys.executable, str(RUNNER), "--repo", str(root), "--config", "config.json", "--mode", "report"],
                text=True,
                capture_output=True,
                check=False,
            )
            self.assertEqual(completed.returncode, 0, completed.stdout + completed.stderr)
            payload = json.loads(completed.stdout)
            self.assertEqual(payload["results"][0]["status"], "FINDINGS")

    def test_enforce_mode_fails_only_promoted_gate(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "config.json").write_text(
                json.dumps(
                    {
                        "schema_version": 1,
                        "default_mode": "report",
                        "gates": [
                            {
                                "id": "report-debt",
                                "description": "fixture",
                                "rollout": "report",
                                "cwd": ".",
                                "command": ["python", "-c", "raise SystemExit(3)"],
                            },
                            {
                                "id": "enforced-debt",
                                "description": "fixture",
                                "rollout": "enforce",
                                "cwd": ".",
                                "command": ["python", "-c", "raise SystemExit(4)"],
                            },
                        ],
                    }
                ),
                encoding="utf-8",
            )
            completed = subprocess.run(
                [sys.executable, str(RUNNER), "--repo", str(root), "--config", "config.json", "--mode", "enforce"],
                text=True,
                capture_output=True,
                check=False,
            )
            self.assertEqual(completed.returncode, 1, completed.stdout + completed.stderr)
            payload = json.loads(completed.stdout)
            self.assertEqual(payload["summary"]["enforced_failures"], 1)


if __name__ == "__main__":
    unittest.main()
