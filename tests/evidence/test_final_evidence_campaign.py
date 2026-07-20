from __future__ import annotations

from pathlib import Path
import unittest

from _loader import ROOT, load

MODULE = load("scripts/evidence/run-final-evidence-campaign.py", "final_evidence_campaign")
CONFIG = ROOT / "config" / "evidence" / "final-evidence-campaign.json"


def campaign_by_id(config: dict, campaign_id: str) -> dict:
    return next(campaign for campaign in config["campaigns"] if campaign["id"] == campaign_id)


class FinalEvidenceCampaignTests(unittest.TestCase):
    def test_e1_uses_canonical_scenario_b_nominal_contract(self):
        config = MODULE.load_config(CONFIG)
        e1 = campaign_by_id(config, "E1")
        cases = {case["id"]: case for case in e1["cases"]}

        self.assertEqual("scenario_b", cases["nominal-short"]["scenarioCode"])
        self.assertEqual("scenario_b", cases["nominal-long"]["scenarioCode"])
        self.assertEqual(6, cases["nominal-short"]["sensorCount"])
        self.assertEqual(6, cases["nominal-long"]["sensorCount"])
        self.assertEqual(5, cases["nominal-short"]["numberOfCycles"])
        self.assertEqual(6, cases["nominal-long"]["numberOfCycles"])
        self.assertEqual(5, cases["nominal-short"]["intervalSeconds"])
        self.assertEqual(60, cases["nominal-long"]["intervalSeconds"])
        self.assertEqual(12345, cases["nominal-short"]["seed"])
        self.assertEqual(12345, cases["nominal-long"]["seed"])

    def test_e1_empty_profiles_materialize_explicit_none_payload(self):
        config = MODULE.load_config(CONFIG)
        e1 = campaign_by_id(config, "E1")

        for case in e1["cases"]:
            payload = MODULE.build_api_run_payload({**case, "campaignId": "E1"})
            self.assertEqual("none", payload["degradationProfile"])
            self.assertEqual(["none"], payload["degradationProfiles"])

    def test_degradation_cases_preserve_explicit_profile_payload(self):
        config = MODULE.load_config(CONFIG)
        e3 = campaign_by_id(config, "E3")
        case = {**e3["cases"][0], "campaignId": "E3"}

        payload = MODULE.build_api_run_payload(case)

        self.assertEqual("missing+out-of-order+duplicate", payload["degradationProfile"])
        self.assertEqual(["missing", "out-of-order", "duplicate"], payload["degradationProfiles"])

    def test_external_config_path_can_be_reported_in_manifest(self):
        external = Path(ROOT).parent / "outside-repo-config.json"

        self.assertEqual(str(external.resolve()), MODULE.safe_relative_to(external, ROOT))

    def test_reset_busy_message_takes_precedence_over_external_store_words(self):
        class BusyClient:
            def __init__(self):
                self.calls = 0

            def request(self, *_args, **_kwargs):
                self.calls += 1
                if self.calls == 1:
                    raise RuntimeError(
                        "HTTP 400 for /api/control/runtime/reset: "
                        "Systemic reset requires quiescent and configured RabbitMQ and InfluxDB stores."
                    )
                return {"status": "Completed"}

        client = BusyClient()

        result = MODULE.reset_runtime_when_quiescent(client, poll_seconds=0.0, max_wait_seconds=1.0)

        self.assertEqual({"status": "Completed"}, result)
        self.assertEqual(2, client.calls)

    def test_reset_active_operation_message_is_retryable(self):
        class ActiveOperationClient:
            def __init__(self):
                self.calls = 0

            def request(self, *_args, **_kwargs):
                self.calls += 1
                if self.calls == 1:
                    raise RuntimeError(
                        "HTTP 400 for /api/control/runtime/reset: "
                        "Reset requires quiescence; active operations=1, pending/processing/retry=0."
                    )
                return {"status": "Completed"}

        client = ActiveOperationClient()

        result = MODULE.reset_runtime_when_quiescent(client, poll_seconds=0.0, max_wait_seconds=1.0)

        self.assertEqual({"status": "Completed"}, result)
        self.assertEqual(2, client.calls)


if __name__ == "__main__":
    unittest.main()
