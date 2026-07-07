from __future__ import annotations
import importlib.util
import json
import shutil
import tempfile
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[1] / "validate.py"
SPEC = importlib.util.spec_from_file_location("frontend_audit_validate", MODULE_PATH)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class FrontendDecompositionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repo = Path(__file__).resolve().parents[3]

    def test_current_repository_passes(self):
        self.assertEqual("PASS", MODULE.validate(self.repo)["status"])

    def test_legitimate_body_change_is_allowed(self):
        with tempfile.TemporaryDirectory() as temp:
            fixture = self._fixture(Path(temp))
            path = fixture / "webUI/src/app/pages/MissionControlPage.tsx"
            path.write_text(
                path.read_text(encoding="utf-8").replace("Mission Control", "Operations Control"),
                encoding="utf-8",
            )
            self.assertEqual("PASS", MODULE.validate(fixture)["status"])

    def test_workspace_monolith_regression_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            fixture = self._fixture(Path(temp))
            contract = json.loads((fixture / "config/quality/frontend-decomposition.json").read_text(encoding="utf-8"))
            path = fixture / contract["workspace_entrypoint"]
            path.write_text(path.read_text(encoding="utf-8") + "\n" + ("// regression\n" * 600), encoding="utf-8")
            payload = MODULE.validate(fixture)
            self.assertEqual("FAIL", payload["status"])
            self.assertTrue(any("workspace-normalized-size" in failure for failure in payload["failures"]))

    def test_missing_type_export_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            fixture = self._fixture(Path(temp))
            path = fixture / "webUI/src/app/types/scenario.ts"
            path.write_text(
                path.read_text(encoding="utf-8").replace(
                    "export interface ScenarioResponse", "interface ScenarioResponse"
                ),
                encoding="utf-8",
            )
            self.assertEqual("FAIL", MODULE.validate(fixture)["status"])

    def _fixture(self, root):
        contract = json.loads((self.repo / "config/quality/frontend-decomposition.json").read_text(encoding="utf-8"))
        paths = (
            set(contract["workspace_modules"])
            | set(contract["type_modules"])
            | {
                contract["types_barrel"],
                "config/quality/frontend-decomposition.json",
                contract["migration_proof"],
                "webUI/src/app/App.tsx",
            }
        )
        for relative in paths:
            source = self.repo / relative
            target = root / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
        return root


if __name__ == "__main__":
    unittest.main()
