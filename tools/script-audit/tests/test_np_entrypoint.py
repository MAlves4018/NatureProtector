from __future__ import annotations
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]


class CanonicalEntrypointTests(unittest.TestCase):
    def test_evidence_directory_receives_resolved_config(self):
        text = (ROOT / "scripts/np.ps1").read_text(encoding="utf-8-sig")
        self.assertIn("Get-EvidenceDirectory -Operation $operation -Config $config", text)
        self.assertNotIn("$evidence = Get-EvidenceDirectory $operation", text)

    def test_evidence_root_creation_does_not_require_existing_parent(self):
        text = (ROOT / "scripts/np.ps1").read_text(encoding="utf-8-sig")
        self.assertIn("[System.IO.Path]::GetFullPath", text)
        self.assertNotIn("Resolve-Path -LiteralPath (Split-Path -Parent $root)", text)


if __name__ == "__main__":
    unittest.main()
