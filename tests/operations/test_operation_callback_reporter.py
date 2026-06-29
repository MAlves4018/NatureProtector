from __future__ import annotations

import hashlib
import importlib.util
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / "scripts/operations/report-operation-callback.py"

spec = importlib.util.spec_from_file_location("operation_callback_reporter", SCRIPT)
assert spec and spec.loader
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class OperationCallbackReporterTests(unittest.TestCase):
    def test_aggregate_hash_is_deterministic_and_path_sensitive(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "b.txt").write_text("second", encoding="utf-8")
            (root / "a.txt").write_text("first", encoding="utf-8")
            first = module.aggregate_artifact(root, "evidence", "package", "https://example.invalid/run")
            second = module.aggregate_artifact(root, "evidence", "package", "https://example.invalid/run")
            self.assertEqual(first, second)
            self.assertEqual(len(first["sha256"]), hashlib.sha256().digest_size * 2)
            self.assertGreater(first["sizeBytes"], 0)

    def test_unconfigured_callback_is_truthfully_skipped(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            receipt = Path(temporary) / "receipt.json"
            environment = os.environ.copy()
            environment.pop("NP_OPERATIONS_CALLBACK_URL", None)
            environment.pop("NP_OPERATIONS_CALLBACK_SECRET", None)
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    "--operation-id",
                    "00000000-0000-0000-0000-000000000001",
                    "--status",
                    "Queued",
                    "--receipt",
                    str(receipt),
                ],
                env=environment,
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            payload = json.loads(receipt.read_text(encoding="utf-8"))
            self.assertEqual(payload["reportStatus"], "SKIPPED_UNCONFIGURED")

    def test_non_https_non_loopback_callback_is_rejected(self) -> None:
        environment = os.environ.copy()
        environment["NP_OPERATIONS_CALLBACK_URL"] = "http://example.invalid/callback"
        environment["NP_OPERATIONS_CALLBACK_SECRET"] = "not-empty"
        result = subprocess.run(
            [
                sys.executable,
                str(SCRIPT),
                "--operation-id",
                "00000000-0000-0000-0000-000000000001",
                "--status",
                "Failed",
            ],
            env=environment,
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertEqual(result.returncode, 2)
        self.assertIn("REJECTED_CONFIGURATION", result.stdout)


if __name__ == "__main__":
    unittest.main()
