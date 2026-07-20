from __future__ import annotations

import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
COLLECTOR = ROOT / "scripts/evidence/collect-test-quality-evidence.py"


class Phase2NpmResolutionTests(unittest.TestCase):
    def test_frontend_commands_use_resolved_npm_executable(self):
        source = COLLECTOR.read_text(encoding="utf-8")
        self.assertIn('npm_executable = tool_path("npm")', source)
        for command in (
            '"ci"',
            '"check:toolchain"',
            '"typecheck"',
            '"lint"',
            '"format:check"',
            '"test:coverage"',
            '"build"',
        ):
            self.assertIn(f'[npm_executable or "npm", ', source)
            self.assertIn(command, source)
        self.assertNotIn('["npm", "ci"]', source)


if __name__ == "__main__":
    unittest.main()
