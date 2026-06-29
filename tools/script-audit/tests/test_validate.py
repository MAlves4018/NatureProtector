from __future__ import annotations
import importlib.util
import json
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
VALIDATOR = ROOT / "tools/script-audit/validate.py"


class ScriptAuditTests(unittest.TestCase):
    def test_validator_passes_repository(self):
        completed = subprocess.run(
            [sys.executable, str(VALIDATOR), "--repo", str(ROOT)], text=True, capture_output=True, check=False
        )
        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        payload = json.loads(completed.stdout)
        self.assertEqual("PASS", payload["status"])
        self.assertGreater(payload["summary"]["managed_consumers"], 0)
        self.assertEqual(14, payload["summary"]["exported_functions"])

    def test_contract_paths_exist(self):
        contract = json.loads((ROOT / "tools/script-audit/migration-contract.json").read_text(encoding="utf-8"))
        for key in ("module_manifest", "module_implementation", "runtime_contract_test"):
            self.assertTrue((ROOT / contract[key]).is_file())

    def test_managed_consumer_functions_are_not_locally_redefined(self):
        spec = importlib.util.spec_from_file_location("script_audit", VALIDATOR)
        module = importlib.util.module_from_spec(spec)
        assert spec and spec.loader
        sys.modules[spec.name] = module
        spec.loader.exec_module(module)
        contract = json.loads((ROOT / "tools/script-audit/migration-contract.json").read_text(encoding="utf-8"))
        for relative, removed in contract["managed_consumers"].items():
            definitions = set(module.function_names((ROOT / relative).read_text(encoding="utf-8-sig")))
            self.assertFalse(definitions.intersection(removed), relative)

    def test_deployment_paths_are_excluded_from_refactor_contract(self):
        contract = json.loads((ROOT / "tools/script-audit/migration-contract.json").read_text(encoding="utf-8"))
        self.assertNotIn("scripts/cloud/Get-G103CloudInventory.ps1", contract["managed_consumers"])
        self.assertNotIn("scripts/ci/check-secret-canaries.ps1", contract["managed_consumers"])
        self.assertNotIn("scripts/release/test-functional-package-smoke.ps1", contract["managed_consumers"])


if __name__ == "__main__":
    unittest.main()
