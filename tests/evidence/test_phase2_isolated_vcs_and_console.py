from __future__ import annotations

import importlib.util
import io
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / "scripts" / "evidence" / "collect-test-quality-evidence.py"


def load_module():
    spec = importlib.util.spec_from_file_location("phase2_isolated_vcs_console", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class IsolatedFrontendVcsAndConsoleTests(unittest.TestCase):
    def test_copies_root_gitignore_required_by_biome_vcs_root(self):
        module = load_module()
        root = Path(tempfile.mkdtemp())
        try:
            source = root / "webUI"
            source.mkdir(parents=True)
            (source / "package.json").write_text("{}", encoding="utf-8")
            (root / ".gitignore").write_text("webUI/node_modules/\nwebUI/coverage/\n", encoding="utf-8")

            target, temp_root = module.prepare_isolated_frontend_workspace(source)
            self.assertTrue(target.is_dir())
            copied = temp_root / ".gitignore"
            self.assertTrue(copied.is_file())
            self.assertEqual(
                copied.read_text(encoding="utf-8"),
                (root / ".gitignore").read_text(encoding="utf-8"),
            )
            shutil.rmtree(temp_root, ignore_errors=True)
        finally:
            shutil.rmtree(root, ignore_errors=True)

    def test_console_echo_does_not_fail_on_cp1252_incompatible_biome_output(self):
        module = load_module()
        raw = io.BytesIO()
        stream = io.TextIOWrapper(raw, encoding="cp1252", errors="strict", write_through=True)
        original = sys.stdout
        try:
            sys.stdout = stream
            module.safe_console_write("Biome diagnostic ━━━━━━━━━━\n")
            stream.flush()
            rendered = raw.getvalue().decode("cp1252")
        finally:
            sys.stdout = original
            stream.detach()
        self.assertIn("Biome diagnostic", rendered)
        self.assertIn("\\u2501", rendered)


if __name__ == "__main__":
    unittest.main()
