from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from _loader import load

MODULE = load("scripts/evidence/collect-evidence-intelligence.py", "evidence_intelligence")


class EvidenceIntelligenceTests(unittest.TestCase):
    def test_artifact_roles_are_stable(self):
        self.assertEqual("summary", MODULE.artifact_role(Path("phase9-summary.json")))
        self.assertEqual("figure", MODULE.artifact_role(Path("figures/roc.svg")))
        self.assertEqual("claim_register", MODULE.artifact_role(Path("claims/claim-evidence-register.json")))
        self.assertEqual("integrity_manifest", MODULE.artifact_role(Path("SHA256SUMS.txt")))

    def test_integrity_manifest_verifies_and_detects_mismatch(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            run = root / "09-np-score-validation" / "20260715T120000Z"
            excluded = root / "10-evidence-intelligence" / "20260715T120000Z"
            run.mkdir(parents=True)
            target = run / "summary.json"
            target.write_text('{"status":"PASS"}\n', encoding="utf-8")
            digest = hashlib.sha256(target.read_bytes()).hexdigest()
            (run / "SHA256SUMS.txt").write_text(f"{digest}  summary.json\n", encoding="utf-8")
            coverage, audit = MODULE.parse_integrity_manifests(root, excluded)
            rel = target.relative_to(root).as_posix()
            self.assertEqual("VERIFIED", coverage[rel]["status"])
            target.write_text('{"status":"CHANGED"}\n', encoding="utf-8")
            coverage, audit = MODULE.parse_integrity_manifests(root, excluded)
            self.assertEqual("MISMATCH", coverage[rel]["status"])

    def test_source_resolution_under_baseline(self):
        with tempfile.TemporaryDirectory() as temp:
            repo = Path(temp) / "repo"
            baseline = repo / "artifacts" / "report-evidence" / "base"
            source = baseline / "09-np-score-validation" / "run" / "phase9-summary.json"
            source.parent.mkdir(parents=True)
            source.write_text("{}", encoding="utf-8")
            resolved = MODULE.resolve_source_path(
                repo,
                baseline,
                "artifacts/report-evidence/base/09-np-score-validation/run/phase9-summary.json",
            )
            self.assertEqual(source, resolved)

    def test_svg_bar_is_created(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "score.svg"
            MODULE.build_svg_bar(path, "Score", [("Integrity", 100), ("Presentation", 75)])
            text = path.read_text(encoding="utf-8")
            self.assertIn("Integrity", text)
            self.assertIn("75.0", text)


if __name__ == "__main__":
    unittest.main()
