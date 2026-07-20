from __future__ import annotations
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / "scripts" / "evidence" / "verify-final-evidence-campaign.py"


def load_module():
    spec = importlib.util.spec_from_file_location("portfolio_plan_verify", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class PlanVerifierTests(unittest.TestCase):
    def test_plan_case_requires_plan_contract_not_live_artifacts(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temp:
            case = Path(temp)
            (case / "configuration").mkdir()
            (case / "configuration" / "plan.json").write_text("{}", encoding="utf-8")
            (case / "verdict.json").write_text(json.dumps({"status": "PLANNED", "operationId": None, "simulationRunId": None}), encoding="utf-8")
            (case / "hashes.sha256").write_text("placeholder", encoding="utf-8")
            self.assertEqual([], module.validate_plan_case_tree(case))


if __name__ == "__main__":
    unittest.main()
