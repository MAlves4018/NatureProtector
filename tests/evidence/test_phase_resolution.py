from __future__ import annotations
import tempfile, unittest
from pathlib import Path
from tests.evidence._loader import load

MODULE = load("scripts/evidence/collect-report-integration-evidence.py", "phase7_collect")


class PhaseResolutionTests(unittest.TestCase):
    def test_missing_phase_is_optional(self):
        with tempfile.TemporaryDirectory() as temp:
            self.assertIsNone(MODULE.resolve_phase_dir(Path(temp), "02-tests"))

    def test_required_missing_phase_raises(self):
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaises(FileNotFoundError):
                MODULE.resolve_phase_dir(Path(temp), "02-tests", required=True)

    def test_latest_pointer_is_portable(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            run = root / "02-tests" / "run-1"
            run.mkdir(parents=True)
            (root / "02-tests" / "LATEST.txt").write_text("run-1\n", encoding="utf-8")
            self.assertEqual(run.resolve(), MODULE.resolve_phase_dir(root, "02-tests"))


if __name__ == "__main__":
    unittest.main()
