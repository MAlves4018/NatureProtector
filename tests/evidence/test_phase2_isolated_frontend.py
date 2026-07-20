from __future__ import annotations
import importlib.util
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / "scripts" / "evidence" / "collect-test-quality-evidence.py"


def load_module():
    spec = importlib.util.spec_from_file_location("phase2_isolated", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class IsolatedFrontendWorkspaceTests(unittest.TestCase):
    def test_excludes_runtime_and_stale_output_directories(self):
        module = load_module()
        root = Path(tempfile.mkdtemp())
        try:
            source = root / "webUI"
            (source / "src").mkdir(parents=True)
            (source / "src" / "main.tsx").write_text("export {};", encoding="utf-8")
            (source / "package.json").write_text("{}", encoding="utf-8")
            for name in ("node_modules", "coverage", "test-results", "dist"):
                directory = source / name
                directory.mkdir()
                (directory / "stale.txt").write_text("stale", encoding="utf-8")
            target, temp_root = module.prepare_isolated_frontend_workspace(source)
            self.assertTrue((target / "src" / "main.tsx").is_file())
            self.assertTrue((target / "package.json").is_file())
            for name in ("node_modules", "coverage", "test-results", "dist"):
                self.assertFalse((target / name).exists())
            shutil.rmtree(temp_root, ignore_errors=True)
        finally:
            shutil.rmtree(root, ignore_errors=True)


if __name__ == "__main__":
    unittest.main()
