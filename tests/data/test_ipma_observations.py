import importlib.util
import json
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / "scripts/data/poll-ipma-observations.py"

def module():
    spec = importlib.util.spec_from_file_location("ipma", SCRIPT)
    value = importlib.util.module_from_spec(spec)
    assert spec.loader
    spec.loader.exec_module(value)
    return value

class IpmaObservationTests(unittest.TestCase):
    def test_normalizes_five_metrics_for_two_stations(self):
        value = module()
        observations = json.loads((REPO / "tests/fixtures/ipma-observations.json").read_text())
        stations = json.loads((REPO / "tests/fixtures/ipma-stations.json").read_text())
        config = json.loads((REPO / "config/external-data/ipma.json").read_text())
        lines, seen, _ = value.normalize(observations, stations, config["metrics"], {})
        self.assertEqual(len(lines), 10)
        self.assertEqual(len(seen), 2)
        self.assertTrue(all("source_kind=EXTERNAL" in line for line in lines))
        self.assertTrue(all("raw_payload_hash=" in line for line in lines))
        index = value.station_index(stations)
        self.assertTrue(index)
        first = next(iter(index.values()))
        self.assertIsNotNone(first["latitude"])
        self.assertIsNotNone(first["longitude"])

    def test_deduplicates_by_station_time_and_metric(self):
        value = module()
        observations = json.loads((REPO / "tests/fixtures/ipma-observations.json").read_text())
        stations = json.loads((REPO / "tests/fixtures/ipma-stations.json").read_text())
        config = json.loads((REPO / "config/external-data/ipma.json").read_text())
        _, seen, _ = value.normalize(observations, stations, config["metrics"], {})
        lines, _, _ = value.normalize(observations, stations, config["metrics"], seen)
        self.assertEqual(lines, [])

if __name__ == "__main__":
    unittest.main()
