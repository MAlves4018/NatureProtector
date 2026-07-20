from __future__ import annotations

import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


class EvidencePythonBootstrapTests(unittest.TestCase):
    def test_requirements_cover_evidence_and_static_validation_tools(self):
        requirements = (
            ROOT / "scripts/evidence/requirements-report.txt"
        ).read_text(encoding="utf-8")
        names = {
            re.split(r"[<=>\[]", line, maxsplit=1)[0].lower()
            for line in requirements.splitlines()
            if line and not line.startswith("#")
        }

        self.assertTrue(
            {
                "cairosvg",
                "jsonschema",
                "matplotlib",
                "psycopg",
                "pytest",
                "python-hcl2",
                "pyyaml",
            }.issubset(names)
        )

    def test_bootstrap_validates_pytest_before_reporting_ready(self):
        bootstrap = (
            ROOT / "scripts/evidence/Initialize-NP-EvidencePython.ps1"
        ).read_text(encoding="utf-8")

        self.assertIn("import cairosvg", bootstrap)
        self.assertIn("pytest", bootstrap)
        self.assertIn("pip freeze", bootstrap)


if __name__ == "__main__":
    unittest.main()
