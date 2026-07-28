from __future__ import annotations

import copy
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
CONFIG = json.loads((REPO / "config/acceptance/p0-runtime-coverage.json").read_text(encoding="utf-8"))
VERIFIER = REPO / "scripts/acceptance/verify_scenario_profile_matrix.py"


SENSORS = [
    ("00000000-0000-0000-0000-000000000001", "Temperature", 31.0),
    ("00000000-0000-0000-0000-000000000002", "Temperature", 31.2),
    ("00000000-0000-0000-0000-000000000003", "Humidity", 25.0),
    ("00000000-0000-0000-0000-000000000004", "Humidity", 25.2),
    ("00000000-0000-0000-0000-000000000005", "WindSpeed", 3.78),
    ("00000000-0000-0000-0000-000000000006", "WindSpeed", 3.98),
]
CYCLES = 6


def event_id(index: int) -> str:
    return f"10000000-0000-0000-0000-{index:012d}"


def baseline_readings() -> list[dict]:
    rows: list[dict] = []
    index = 1
    for cycle in range(CYCLES):
        for sensor_id, metric, base in SENSORS:
            natural = {"Temperature": 0.01, "Humidity": 0.02, "WindSpeed": 0.005}[metric] * cycle
            rows.append(
                {
                    "eventId": event_id(index),
                    "sensorId": sensor_id,
                    "metricType": metric,
                    "cycleIndex": cycle,
                    "value": base + natural,
                    "ingestDelaySeconds": 0.0,
                }
            )
            index += 1
    return rows


def publish_events(readings: list[dict]) -> list[dict]:
    return [
        {
            "eventId": row["eventId"],
            "sensorId": row["sensorId"],
            "cycleIndex": row["cycleIndex"],
            "correlationId": f"run-{row['cycleIndex']:04d}",
        }
        for row in readings
    ]


def common_run(case_id: str, profile: str, readings: list[dict], *, profiles: list[str] | None = None) -> dict:
    accepted = len(readings)
    expected = len(SENSORS) * CYCLES
    run_id = f"20000000-0000-0000-0000-{abs(hash(case_id)) % 10**12:012d}"
    return {
        "caseId": case_id,
        "primaryProfile": profile,
        "profiles": profiles or [profile],
        "seed": CONFIG["scenarioMatrix"]["seed"],
        "sensorCount": len(SENSORS),
        "numberOfCycles": CYCLES,
        "expectedEvents": expected,
        "requestId": "30000000-0000-0000-0000-000000000001",
        "operationId": "40000000-0000-0000-0000-000000000001",
        "runId": run_id,
        "runStatus": "Completed",
        "resolvedProfiles": profiles or [profile],
        "operation": {
            "simulationRunId": run_id,
            "state": "SystemCompleted",
            "terminalOutcome": "Succeeded",
            "accounting": {
                "settled": True,
                "pendingInbox": 0,
                "processingInbox": 0,
                "retryPendingInbox": 0,
            },
        },
        "audit": {
            "expectedEvents": expected,
            "acceptedReadings": accepted,
            "missingEvents": expected - accepted,
            "rejected": 0,
            "quarantined": 0,
            "retryAttempts": 0,
            "riskAssessments": accepted,
        },
        "readings": readings,
        "inbox": [],
        "attempts": [],
        "cycleObservations": [
            {"eventId": row["eventId"], "outcome": "Eligible"} for row in readings
        ],
        "publishEvents": publish_events(readings),
    }


def build_matrix() -> dict:
    base = baseline_readings()
    runs = [common_run("none", "none", copy.deepcopy(base))]

    missing = [row for index, row in enumerate(copy.deepcopy(base)) if index not in {0, 8, 17, 25}]
    missing_run = common_run("missing-readings", "missing-readings", missing)
    runs.append(missing_run)
    repeat = common_run("missing-readings-repeat", "missing-readings", copy.deepcopy(missing))
    repeat["repeatOf"] = "missing-readings"
    runs.append(repeat)

    noise = copy.deepcopy(base)
    for row in noise:
        row["value"] += 0.25
    runs.append(common_run("noise", "noise", noise))

    bias = copy.deepcopy(base)
    for row in bias:
        row["value"] += {"Temperature": 1.5, "Humidity": -4.0, "WindSpeed": 0.8}[row["metricType"]]
    runs.append(common_run("bias", "bias", bias))

    drift = copy.deepcopy(base)
    for row in drift:
        row["value"] += {"Temperature": 0.15, "Humidity": -0.25, "WindSpeed": 0.08}[row["metricType"]] * row["cycleIndex"]
    runs.append(common_run("drift", "drift", drift))

    stuck = copy.deepcopy(base)
    fixed: dict[tuple[str, str], float] = {}
    for row in stuck:
        k = (row["sensorId"], row["metricType"])
        fixed.setdefault(k, row["value"])
        row["value"] = fixed[k]
    runs.append(common_run("stuck-value", "stuck-value", stuck))

    outlier = copy.deepcopy(base)
    outlier[0]["value"] += 12.0
    runs.append(common_run("outlier", "outlier", outlier))

    clipping = copy.deepcopy(base)
    runs.append(common_run("clipping-range", "clipping/range", clipping))
    saturated = copy.deepcopy(base)
    for row in saturated:
        if row["metricType"] == "Temperature":
            row["value"] = 42.0
        elif row["metricType"] == "Humidity":
            row["value"] = 8.0
    supplemental = common_run("clipping-saturation", "clipping/range", saturated, profiles=["outlier", "clipping/range"])
    supplemental["supplemental"] = True
    runs.append(supplemental)

    lag = copy.deepcopy(base)
    for row in lag:
        row["ingestDelaySeconds"] = 15.0
    runs.append(common_run("lag-delay", "lag/delay", lag))

    duplicate = common_run("duplicate", "duplicate", copy.deepcopy(base))
    duplicate["publishEvents"].append(copy.deepcopy(duplicate["publishEvents"][0]))
    runs.append(duplicate)

    out_of_order = common_run("out-of-order", "out-of-order", copy.deepcopy(base))
    grouped: dict[int, list[dict]] = {}
    for row in out_of_order["publishEvents"]:
        grouped.setdefault(row["cycleIndex"], []).append(row)
    out_of_order["publishEvents"] = [row for cycle in sorted(grouped) for row in reversed(grouped[cycle])]
    runs.append(out_of_order)

    retry = common_run("retry-transient", "retry-transient", copy.deepcopy(base))
    retry["audit"]["retryAttempts"] = 1
    retry["attempts"] = [
        {"eventId": retry["readings"][0]["eventId"], "attemptNumber": 1, "outcome": "RetryScheduled"},
        {"eventId": retry["readings"][0]["eventId"], "attemptNumber": 2, "outcome": "Succeeded"},
    ]
    runs.append(retry)
    return {"schemaVersion": 1, "runs": runs}


class ScenarioProfileVerifierTests(unittest.TestCase):
    def run_verifier(self, payload: dict) -> tuple[int, dict]:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            input_path = root / "input.json"
            output = root / "output"
            input_path.write_text(json.dumps(payload), encoding="utf-8")
            completed = subprocess.run(
                [sys.executable, str(VERIFIER), "--config", str(REPO / "config/acceptance/p0-runtime-coverage.json"), "--input", str(input_path), "--output-dir", str(output)],
                cwd=REPO,
                text=True,
                capture_output=True,
                check=False,
            )
            result = json.loads((output / "scenario-matrix-result.json").read_text(encoding="utf-8"))
            return completed.returncode, result

    def test_complete_synthetic_matrix_passes(self) -> None:
        code, result = self.run_verifier(build_matrix())
        self.assertEqual(code, 0, result)
        self.assertEqual(result["status"], "PASS")
        self.assertEqual(result["summary"]["failed"], 0)

    def test_missing_profile_fails_closed(self) -> None:
        payload = build_matrix()
        payload["runs"] = [row for row in payload["runs"] if row.get("primaryProfile") != "noise"]
        code, result = self.run_verifier(payload)
        self.assertEqual(code, 1)
        self.assertTrue(any(row["assertion"] == "profile_supplied" and row["profile"] == "noise" and row["status"] == "FAIL" for row in result["checks"]))

    def test_duplicate_without_duplicate_delivery_fails(self) -> None:
        payload = build_matrix()
        duplicate = next(row for row in payload["runs"] if row.get("primaryProfile") == "duplicate")
        duplicate["publishEvents"] = duplicate["publishEvents"][:-1]
        code, result = self.run_verifier(payload)
        self.assertEqual(code, 1)
        self.assertTrue(any(row["assertion"] == "duplicate_delivery_observed" and row["status"] == "FAIL" for row in result["checks"]))

    def test_clipping_without_observed_saturation_fails(self) -> None:
        payload = build_matrix()
        supplemental = next(row for row in payload["runs"] if row.get("supplemental"))
        supplemental["readings"] = baseline_readings()
        code, result = self.run_verifier(payload)
        self.assertEqual(code, 1)
        self.assertTrue(any(row["assertion"] == "clipping_effect_observed" and row["status"] == "FAIL" for row in result["checks"]))


if __name__ == "__main__":
    unittest.main()
