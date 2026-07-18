from __future__ import annotations
import unittest
from tests.evidence._loader import load

MODULE = load("scripts/evidence/run-report-evidence-campaign.py", "campaign_redaction")


class RedactionTests(unittest.TestCase):
    def test_secret_value_is_redacted(self):
        self.assertNotIn("supersecret", MODULE.redact_command(["tool", "--token=supersecret"], ["supersecret"]))

    def test_dsn_password_is_redacted(self):
        self.assertIn("password=<redacted>", MODULE.redact_command(["tool", "password=secret"], []).lower())


if __name__ == "__main__":
    unittest.main()
