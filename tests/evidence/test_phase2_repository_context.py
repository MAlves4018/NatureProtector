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
    spec = importlib.util.spec_from_file_location("phase2_repository_context", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class IsolatedFrontendRepositoryContextTests(unittest.TestCase):
    def test_copies_repository_workflows_required_by_toolchain_check(self):
        module = load_module()
        root = Path(tempfile.mkdtemp())
        try:
            source = root / "webUI"
            source.mkdir(parents=True)
            (source / "package.json").write_text("{}", encoding="utf-8")

            workflows = root / ".github" / "workflows"
            workflows.mkdir(parents=True)
            names = (
                "engineering-foundations.yml",
                "release-candidate.yml",
                "security.yml",
            )
            for name in names:
                (workflows / name).write_text(
                    'steps:\n  - uses: actions/setup-node@v4\n    with:\n      node-version: "20.17.0"\n',
                    encoding="utf-8",
                )

            target, temp_root = module.prepare_isolated_frontend_workspace(source)
            self.assertTrue(target.is_dir())
            for name in names:
                copied = temp_root / ".github" / "workflows" / name
                self.assertTrue(copied.is_file(), name)
                self.assertEqual(copied.read_text(encoding="utf-8"), (workflows / name).read_text(encoding="utf-8"))
            shutil.rmtree(temp_root, ignore_errors=True)
        finally:
            shutil.rmtree(root, ignore_errors=True)


if __name__ == "__main__":
    unittest.main()
