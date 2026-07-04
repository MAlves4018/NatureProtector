from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
VALIDATOR = REPO / "tools/operations-audit/validate.py"


class OperationsArchitectureTests(unittest.TestCase):
    def test_operations_audit_passes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "operations-audit.json"
            result = subprocess.run(
                [sys.executable, str(VALIDATOR), "--repo", str(REPO), "--output", str(output)],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            payload = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual(payload["status"], "PASS")
            self.assertEqual(payload["summary"]["failures"], 0)

    def test_dangerous_capabilities_are_separated_from_admin(self) -> None:
        text = (REPO / "src/NatureProtector.Backoffice.Api/Operations/Authorization/OperationCapabilities.cs").read_text(encoding="utf-8")
        admin = text.split('["Admin"] =', 1)[1].split("\n            ]", 1)[0]
        approver = text.split('["ReleaseApprover"] =', 1)[1].split("\n            ]", 1)[0]
        self.assertNotIn("DeploymentDeployProduction", admin)
        self.assertNotIn("CloudDestroy", admin)
        self.assertIn("DeploymentDeployProduction", approver)
        self.assertIn("CloudDestroy", approver)

    def test_browser_contract_has_no_provider_credential_fields(self) -> None:
        contracts = (REPO / "src/NatureProtector.Backoffice.Api/Operations/Contracts/OperationContracts.cs").read_text(encoding="utf-8").lower()
        self.assertNotIn("githubtoken", contracts)
        self.assertNotIn("serviceaccountkey", contracts)
        self.assertNotIn("callbacksecret", contracts)


if __name__ == "__main__":
    unittest.main()
