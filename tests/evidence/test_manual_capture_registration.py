from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "evidence" / "register-evidence-capture.py"


class ManualCaptureRegistrationTests(unittest.TestCase):
    def test_capture_creates_sidecar_register_and_hash(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            image = root / "source.svg"
            image.write_text('<svg xmlns="http://www.w3.org/2000/svg"></svg>\n', encoding="utf-8")
            evidence = root / "evidence"
            process = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    "--image", str(image),
                    "--evidence-root", str(evidence),
                    "--capture-id", "CAP-001",
                    "--title", "Timeline",
                    "--purpose", "Evidence",
                    "--chapter-target", "Chapter 6",
                    "--baseline-id", "base",
                    "--run-id", "run",
                    "--source-page", "/operations",
                ],
                text=True,
                capture_output=True,
                check=False,
            )
            self.assertEqual(0, process.returncode, process.stderr)
            metadata = json.loads((evidence / "manual-captures" / "CAP-001" / "metadata.json").read_text(encoding="utf-8"))
            self.assertEqual("CAP-001", metadata["captureId"])
            self.assertTrue(metadata["sha256"])
            self.assertTrue((evidence / "manual-captures" / "capture-register.csv").is_file())


if __name__ == "__main__":
    unittest.main()
