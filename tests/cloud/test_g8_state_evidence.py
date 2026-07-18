from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts" / "cloud"))

from g8_state_evidence import load_required_json, validate_g8_state_document  # noqa: E402


class G8StateEvidenceTests(unittest.TestCase):
    def test_missing_file_returns_controlled_failure(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            path = root / "docs" / "evidence" / "g8-1-state.json"

            result = load_required_json(path, root)

            self.assertIsNone(result.data)
            self.assertEqual("missing:docs/evidence/g8-1-state.json", result.error)

    def test_invalid_json_returns_controlled_failure(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            path = root / "docs" / "evidence" / "g8-2-state.json"
            path.parent.mkdir(parents=True)
            path.write_text("{invalid", encoding="utf-8")

            result = load_required_json(path, root)

            self.assertIsNone(result.data)
            self.assertIsNotNone(result.error)
            self.assertTrue(result.error.startswith("json:docs/evidence/g8-2-state.json:"))

    def test_valid_json_is_loaded(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            path = root / "docs" / "evidence" / "g8-1-state.json"
            path.parent.mkdir(parents=True)
            path.write_text('{"schema_version":1,"phase":"G8.1"}', encoding="utf-8")

            result = load_required_json(path, root)

            self.assertIsNone(result.error)
            self.assertEqual({"schema_version": 1, "phase": "G8.1"}, result.data)

    def test_repository_g8_state_files_are_valid_static_contracts(self) -> None:
        expectations = {
            "G8.1": ROOT / "docs" / "evidence" / "g8-1-state.json",
            "G8.2": ROOT / "docs" / "evidence" / "g8-2-state.json",
        }
        for phase, path in expectations.items():
            with self.subTest(phase=phase):
                result = load_required_json(path, ROOT)
                self.assertIsNone(result.error)
                self.assertEqual([], validate_g8_state_document(result.data, phase))

    def test_empty_state_document_is_not_accepted_as_fixture(self) -> None:
        self.assertIn("state:G8.1:phase", validate_g8_state_document({}, "G8.1"))


if __name__ == "__main__":
    unittest.main()
