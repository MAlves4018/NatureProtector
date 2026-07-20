from __future__ import annotations

import csv
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts/performance/aggregate-runtime-metrics.py"
spec = importlib.util.spec_from_file_location("aggregate_runtime_metrics", SCRIPT)
assert spec and spec.loader
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value), encoding="utf-8")


def write_csv(path: Path, rows: list[dict[str, object]], fieldnames: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


class AggregateRuntimeMetricsTests(unittest.TestCase):
    def test_percentile_nearest_rank_is_deterministic(self) -> None:
        values = [10.0, 20.0, 30.0, 40.0, 50.0]

        self.assertEqual(30.0, module.percentile_nearest_rank(values, 50))
        self.assertEqual(50.0, module.percentile_nearest_rank(values, 95))
        self.assertIsNone(module.percentile_nearest_rank([], 95))

    def test_system_run_does_not_calculate_processing_throughput_from_total_duration(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            run = root / "system-B0"
            output = root / "out"
            write_json(run / "summary.json", {"profile": "B0"})
            write_json(run / "workload.json", {"profile": "B0"})
            write_csv(
                run / "measurements.csv",
                [
                    {
                        "profile": "B0",
                        "simulationRunId": "run-1",
                        "elapsedMs": 10000,
                        "expectedEvents": 8,
                        "acceptedReadings": 8,
                        "riskAssessments": 8,
                        "rejected": 0,
                        "quarantined": 0,
                        "lostEvents": 0,
                        "backlogDrainTimeMs": 100,
                        "queueReadyAfter": 0,
                        "queueUnacknowledgedAfter": 0,
                        "queueTotalAfter": 0,
                        "queueConsumersAfter": 1,
                    }
                ],
                [
                    "profile",
                    "simulationRunId",
                    "elapsedMs",
                    "expectedEvents",
                    "acceptedReadings",
                    "riskAssessments",
                    "rejected",
                    "quarantined",
                    "lostEvents",
                    "backlogDrainTimeMs",
                    "queueReadyAfter",
                    "queueUnacknowledgedAfter",
                    "queueTotalAfter",
                    "queueConsumersAfter",
                ],
            )

            args = module.parse_args(["--output-root", str(output), "--system-run-dir", str(run)])
            summary = module.aggregate(args)
            throughput = read_csv(output / "06-throughput" / "THROUGHPUT_RESULTS.csv")
            latency = read_csv(output / "05-latency" / "LATENCY_SUMMARY.csv")

        self.assertEqual("PASS", summary["status"])
        self.assertTrue(any(row["metric"] == "pipeline_processing_throughput" and row["status"] == "UNSUPPORTED" for row in throughput))
        self.assertTrue(any(row["stage"] == "publish_to_receive" and row["status"] == "UNSUPPORTED" for row in latency))

    def test_system_run_uses_persisted_publish_to_receive_samples_when_present(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            run = root / "system-B0"
            output = root / "out"
            write_json(run / "summary.json", {"profile": "B0"})
            write_json(run / "workload.json", {"profile": "B0"})
            write_csv(
                run / "measurements.csv",
                [
                    {
                        "profile": "B0",
                        "simulationRunId": "run-1",
                        "elapsedMs": 10000,
                        "expectedEvents": 8,
                        "acceptedReadings": 8,
                        "riskAssessments": 8,
                        "rejected": 0,
                        "quarantined": 0,
                        "lostEvents": 0,
                        "backlogDrainTimeMs": 100,
                        "queueReadyAfter": 0,
                        "queueUnacknowledgedAfter": 0,
                        "queueTotalAfter": 0,
                        "queueConsumersAfter": 1,
                        "timeToFirstPublishedMs": 15.25,
                        "publishToFirstInboxMs": 12.5,
                        "publishToLastProcessingFinishedMs": 90.75,
                    }
                ],
                [
                    "profile",
                    "simulationRunId",
                    "elapsedMs",
                    "expectedEvents",
                    "acceptedReadings",
                    "riskAssessments",
                    "rejected",
                    "quarantined",
                    "lostEvents",
                    "backlogDrainTimeMs",
                    "queueReadyAfter",
                    "queueUnacknowledgedAfter",
                    "queueTotalAfter",
                    "queueConsumersAfter",
                    "timeToFirstPublishedMs",
                    "publishToFirstInboxMs",
                    "publishToLastProcessingFinishedMs",
                ],
            )

            args = module.parse_args(["--output-root", str(output), "--system-run-dir", str(run)])
            summary = module.aggregate(args)
            latency = read_csv(output / "05-latency" / "LATENCY_SUMMARY.csv")
            raw_latency = read_csv(output / "05-latency" / "RAW_LATENCY_SAMPLES.csv")
            throughput = read_csv(output / "06-throughput" / "THROUGHPUT_RESULTS.csv")
            windows = read_csv(output / "06-throughput" / "THROUGHPUT_WINDOWS.csv")

        publish_rows = [row for row in latency if row["stage"] == "publish_to_receive"]
        pipeline_rows = [row for row in throughput if row["metric"] == "pipeline_processing_throughput"]
        self.assertEqual("PASS", summary["status"])
        self.assertEqual(1, len(publish_rows))
        self.assertEqual("MEASURED", publish_rows[0]["status"])
        self.assertEqual("1", publish_rows[0]["count"])
        self.assertEqual("12.5", publish_rows[0]["p95"])
        self.assertTrue(any(row["stage"] == "publish_to_receive" and row["durationMs"] == "12.5" for row in raw_latency))
        self.assertEqual(1, len(pipeline_rows))
        self.assertEqual("MEASURED", pipeline_rows[0]["status"])
        self.assertEqual("88.15427", pipeline_rows[0]["value"])
        self.assertTrue(any(row["window"] == "first_published_to_last_processing_finished" and row["validForProcessingThroughput"] == "true" for row in windows))

    def test_parse_float_accepts_powershell_decimal_comma(self) -> None:
        self.assertEqual(48.719, module.parse_float("48,719"))

    def test_http_run_generates_api_and_observed_request_rate(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            run = root / "http-B0"
            output = root / "out"
            write_json(run / "run-manifest.json", {"profile": "B0"})
            write_json(run / "status.json", {"measuredAttempts": 2, "measuredWallSeconds": 1.0, "aggregateObservedRequestsPerSecond": 2.0})
            write_json(run / "summary.json", [])
            write_csv(
                run / "measurements.csv",
                [
                    {
                        "phase": "measured",
                        "surface": "api",
                        "probe": "health",
                        "url": "http://localhost/health",
                        "statusCode": 200,
                        "expectedStatusObserved": True,
                        "elapsedMs": 10,
                        "byteCount": 5,
                        "errorKind": "",
                    },
                    {
                        "phase": "measured",
                        "surface": "api",
                        "probe": "health",
                        "url": "http://localhost/health",
                        "statusCode": 200,
                        "expectedStatusObserved": True,
                        "elapsedMs": 20,
                        "byteCount": 5,
                        "errorKind": "",
                    },
                ],
                [
                    "phase",
                    "surface",
                    "probe",
                    "url",
                    "statusCode",
                    "expectedStatusObserved",
                    "elapsedMs",
                    "byteCount",
                    "errorKind",
                ],
            )

            args = module.parse_args(["--output-root", str(output), "--http-run-dir", str(run)])
            summary = module.aggregate(args)
            api = read_csv(output / "10-api" / "API_ROUTE_SUMMARY.csv")
            throughput = read_csv(output / "06-throughput" / "THROUGHPUT_RESULTS.csv")

        self.assertEqual("PASS", summary["status"])
        self.assertEqual("20.0", api[0]["p95ElapsedMs"])
        self.assertTrue(any(row["metric"] == "observed_http_requests_per_second" and row["value"] == "2.0" for row in throughput))


if __name__ == "__main__":
    unittest.main()
