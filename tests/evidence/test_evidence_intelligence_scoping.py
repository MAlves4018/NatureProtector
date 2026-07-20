from __future__ import annotations
import importlib.util
import sys
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / "scripts" / "evidence" / "collect-evidence-intelligence.py"


def load_module():
    spec = importlib.util.spec_from_file_location("evidence_intelligence_scoping", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class FigureClassificationTests(unittest.TestCase):
    def test_coverage_icons_are_not_report_figures(self):
        module = load_module()
        self.assertEqual("other", module.artifact_role(Path("02-tests/run/backend/coverage-report/icon_plus.svg")))
        self.assertEqual("other", module.artifact_role(Path("02-tests/run/frontend/coverage/favicon.png")))

    def test_actual_figure_directories_are_figures(self):
        module = load_module()
        self.assertEqual("figure", module.artifact_role(Path("09-np-score-validation/run/figures/roc-comparison.svg")))
        self.assertEqual("figure", module.artifact_role(Path("03-database/run/diagrams/erd-full.png")))


if __name__ == "__main__":
    unittest.main()
