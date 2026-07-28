#!/usr/bin/env python3
"""Verify Phase 4 UI, rate-limit and bounded-performance evidence."""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def newest_summary(root: Path, pattern: str) -> Path | None:
    candidates = sorted(root.glob(pattern), key=lambda item: item.stat().st_mtime, reverse=True)
    return candidates[0] if candidates else None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--evidence-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    config = read_json(args.config)
    root = args.evidence_root.resolve()
    checks: list[dict[str, Any]] = []

    def check(area: str, name: str, passed: bool, detail: str, evidence: Path | None = None) -> None:
        checks.append(
            {
                "area": area,
                "name": name,
                "status": "PASS" if passed else "FAIL",
                "detail": detail,
                "evidence": str(evidence) if evidence else "",
            }
        )

    for name in ("fixture", "live"):
        report = root / "ui" / name / "playwright-results.json"
        if not report.exists():
            check("ui", f"{name} Playwright report", False, "Report missing.", report)
            continue
        payload = read_json(report)
        stats = payload.get("stats", {})
        unexpected = int(stats.get("unexpected", 0))
        expected = int(stats.get("expected", 0))
        skipped = int(stats.get("skipped", 0))
        check("ui", f"{name} Playwright suite", unexpected == 0 and expected > 0, f"expected={expected}; unexpected={unexpected}; skipped={skipped}", report)

    rate_path = root / "rate-limit" / "rate-limit-result.json"
    if rate_path.exists():
        rate = read_json(rate_path)
        check("rate-limit", "live limiter contract", rate.get("status") == "PASS", f"status={rate.get('status')}", rate_path)
    else:
        check("rate-limit", "live limiter contract", False, "Result missing.", rate_path)

    for spec in config["performance"]["http"]["profiles"]:
        profile = spec["profile"]
        status_path = root / "performance" / "http" / profile / "status.json"
        summary_path = root / "performance" / "http" / profile / "summary.json"
        if not status_path.exists() or not summary_path.exists():
            check("http-performance", profile, False, "Status or summary missing.", status_path)
            continue
        status = read_json(status_path)
        summaries = read_json(summary_path)
        p95_values = [float(row["p95ElapsedMs"]) for row in summaries if row.get("p95ElapsedMs") not in (None, "")]
        measured = int(status.get("measuredAttempts", 0))
        maximum_p95 = max(p95_values) if p95_values else float("inf")
        passed = (
            status.get("status") == "PASS"
            and measured >= int(spec["minimumMeasuredAttempts"])
            and maximum_p95 <= float(spec["maximumP95Milliseconds"])
        )
        check(
            "http-performance",
            profile,
            passed,
            f"status={status.get('status')}; measured={measured}; maxP95Ms={maximum_p95}",
            summary_path,
        )

    zero_fields = config["performance"]["system"]["requiredZeroFields"]
    for spec in config["performance"]["system"]["profiles"]:
        profile = spec["profile"]
        summary_path = newest_summary(root / "performance" / "system" / profile, "system-*/summary.json")
        if summary_path is None:
            check("system-performance", profile, False, "System summary missing.")
            continue
        summary = read_json(summary_path)

        def integer_field(name: str) -> int | None:
            value = summary.get(name)
            if value is None or isinstance(value, bool):
                return None
            try:
                return int(value)
            except (TypeError, ValueError):
                return None

        def nested_number(container: str, name: str) -> float | None:
            value = summary.get(container)
            if not isinstance(value, dict) or value.get(name) is None or isinstance(value.get(name), bool):
                return None
            try:
                return float(value[name])
            except (TypeError, ValueError):
                return None

        zero_values = {field: integer_field(field) for field in zero_fields}
        zero_ok = all(value == 0 for value in zero_values.values())
        expected_events = integer_field("expectedEventsTotal")
        accepted_readings = integer_field("acceptedReadingsTotal")
        risk_assessments = integer_field("riskAssessmentsTotal")
        successful_runs = integer_field("successfulRuns")
        final_queue = nested_number("queueTotalAfter", "final")
        elapsed_p95 = nested_number("elapsedMs", "p95")
        drain_p95 = nested_number("backlogDrainMs", "p95")
        accepted_ok = (
            not config["performance"]["system"]["requireAcceptedEqualsExpected"]
            or (expected_events is not None and accepted_readings == expected_events)
        )
        risk_ok = (
            not config["performance"]["system"]["requireRiskEqualsExpected"]
            or (expected_events is not None and risk_assessments == expected_events)
        )
        queue_ok = (
            not config["performance"]["system"]["requireFinalQueueEmpty"]
            or final_queue == 0
        )
        passed = (
            summary.get("profile") == profile
            and summary.get("status") == "Completed"
            and successful_runs is not None
            and successful_runs >= int(spec["minimumSuccessfulRuns"])
            and zero_ok
            and accepted_ok
            and risk_ok
            and queue_ok
            and elapsed_p95 is not None
            and elapsed_p95 <= float(spec["maximumElapsedP95Milliseconds"])
            and drain_p95 is not None
            and drain_p95 <= float(spec["maximumBacklogDrainP95Milliseconds"])
        )
        check(
            "system-performance",
            profile,
            passed,
            f"profile={summary.get('profile')}; status={summary.get('status')}; successful={successful_runs}; elapsedP95Ms={elapsed_p95}; drainP95Ms={drain_p95}; zeroFields={zero_ok}; accepted={accepted_ok}; risk={risk_ok}; queue={queue_ok}",
            summary_path,
        )

    status = "PASS" if checks and all(item["status"] == "PASS" for item in checks) else "FAIL"
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "generatedAtUtc": utc_now(),
        "status": status,
        "nativeStatus": "UI_AND_BOUNDED_PERFORMANCE_PASS" if status == "PASS" else "UI_AND_BOUNDED_PERFORMANCE_FAIL",
        "claimBoundary": config["performance"]["claimBoundary"],
        "checks": checks,
    }
    output.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"ui_performance_status: {status}")
    return 0 if status == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
