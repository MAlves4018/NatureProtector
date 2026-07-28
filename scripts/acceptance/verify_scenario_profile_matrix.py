#!/usr/bin/env python3
"""Verify persisted runtime evidence for every supported degradation profile.

The PowerShell runtime harness owns service lifecycle and evidence collection. This
verifier is deliberately side-effect free: it consumes one JSON document, applies
the versioned contract in config/acceptance/p0-runtime-coverage.json and emits a
closed PASS/FAIL result with row-level evidence.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import statistics
import sys
from collections import defaultdict
from dataclasses import dataclass, asdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


PASS = "PASS"
FAIL = "FAIL"


@dataclass(frozen=True)
class Check:
    case_id: str
    profile: str
    assertion: str
    status: str
    detail: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", required=True)
    parser.add_argument("--input", required=True)
    parser.add_argument("--output-dir", required=True)
    return parser.parse_args()


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def number(value: Any, default: float | None = None) -> float | None:
    if value is None or value == "":
        return default
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def integer(value: Any, default: int = 0) -> int:
    parsed = number(value)
    return default if parsed is None else int(parsed)


def boolean(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    return str(value).strip().lower() in {"true", "1", "yes"}


def check(rows: list[Check], case_id: str, profile: str, assertion: str, condition: bool, detail: str) -> None:
    rows.append(Check(case_id, profile, assertion, PASS if condition else FAIL, detail))


def key(reading: dict[str, Any]) -> tuple[str, str, int]:
    return (
        str(reading.get("sensorId", "")).lower(),
        str(reading.get("metricType", "")),
        integer(reading.get("cycleIndex"), -1),
    )


def matched_values(run: dict[str, Any], baseline: dict[str, Any]) -> list[tuple[dict[str, Any], dict[str, Any], float]]:
    baseline_by_key = {key(row): row for row in baseline.get("readings", [])}
    result: list[tuple[dict[str, Any], dict[str, Any], float]] = []
    for row in run.get("readings", []):
        other = baseline_by_key.get(key(row))
        current = number(row.get("value"))
        base = number(other.get("value")) if other else None
        if other is not None and current is not None and base is not None:
            result.append((row, other, current - base))
    return result


def mean_delta_by_metric(run: dict[str, Any], baseline: dict[str, Any]) -> dict[str, float]:
    groups: dict[str, list[float]] = defaultdict(list)
    for current, _, delta in matched_values(run, baseline):
        groups[str(current.get("metricType"))].append(delta)
    return {metric: statistics.fmean(values) for metric, values in groups.items() if values}


def regression_slope(points: Iterable[tuple[float, float]]) -> float | None:
    values = list(points)
    if len(values) < 2:
        return None
    xs = [x for x, _ in values]
    ys = [y for _, y in values]
    x_mean = statistics.fmean(xs)
    y_mean = statistics.fmean(ys)
    denominator = sum((x - x_mean) ** 2 for x in xs)
    if denominator == 0:
        return None
    return sum((x - x_mean) * (y - y_mean) for x, y in values) / denominator


def assert_physical_bounds(checks: list[Check], run: dict[str, Any], profile: str) -> None:
    limits = {
        "Temperature": (-20.0, 60.0),
        "Humidity": (0.0, 100.0),
        "WindSpeed": (0.0, 35.0),
    }
    invalid: list[str] = []
    for row in run.get("readings", []):
        metric = str(row.get("metricType"))
        value = number(row.get("value"))
        if metric in limits and value is not None:
            low, high = limits[metric]
            if value < low - 1e-9 or value > high + 1e-9:
                invalid.append(f"{metric}:{value}")
    check(checks, run["caseId"], profile, "physical_bounds", not invalid, f"invalid={invalid[:5]}; total={len(invalid)}")


def assert_common(checks: list[Check], run: dict[str, Any], configured: dict[str, Any]) -> None:
    case_id = str(run["caseId"])
    profile = str(run["primaryProfile"])
    expected = integer(run.get("expectedEvents"))
    readings = list(run.get("readings", []))
    audit = run.get("audit") or {}
    operation = run.get("operation") or {}
    accounting = operation.get("accounting") or {}
    resolved = {str(item).lower() for item in run.get("resolvedProfiles", [])}
    requested = {str(item).lower() for item in run.get("profiles", [])}

    check(checks, case_id, profile, "run_completed", str(run.get("runStatus")) == "Completed", f"status={run.get('runStatus')}")
    operation_state = str(operation.get("state", ""))
    terminal = str(operation.get("terminalOutcome", ""))
    check(
        checks,
        case_id,
        profile,
        "operation_success_terminal",
        operation_state in {"SystemCompleted", "Completed", "Succeeded"} or terminal in {"Succeeded", "Success", "Completed"},
        f"state={operation_state}; terminalOutcome={terminal}",
    )
    check(checks, case_id, profile, "accounting_settled", boolean(accounting.get("settled")), f"accounting={accounting}")
    for field in ("pendingInbox", "processingInbox", "retryPendingInbox"):
        check(checks, case_id, profile, f"{field}_zero", integer(accounting.get(field)) == 0, f"{field}={accounting.get(field)}")
    check(checks, case_id, profile, "request_id_present", bool(str(run.get("requestId", "")).strip()), f"requestId={run.get('requestId')}")
    check(checks, case_id, profile, "operation_id_present", bool(str(run.get("operationId", "")).strip()), f"operationId={run.get('operationId')}")
    check(checks, case_id, profile, "run_id_present", bool(str(run.get("runId", "")).strip()), f"runId={run.get('runId')}")
    check(
        checks,
        case_id,
        profile,
        "operation_run_correlation",
        str(operation.get("simulationRunId", "")).lower() == str(run.get("runId", "")).lower(),
        f"operation.simulationRunId={operation.get('simulationRunId')}; runId={run.get('runId')}",
    )
    check(checks, case_id, profile, "resolved_profiles", requested.issubset(resolved), f"requested={sorted(requested)}; resolved={sorted(resolved)}")
    check(checks, case_id, profile, "expected_formula", expected == configured["sensorCount"] * configured["numberOfCycles"], f"expected={expected}")
    check(checks, case_id, profile, "audit_reading_count", integer(audit.get("acceptedReadings")) == len(readings), f"audit={audit.get('acceptedReadings')}; rows={len(readings)}")
    check(checks, case_id, profile, "audit_expected_count", integer(audit.get("expectedEvents")) == expected, f"audit={audit.get('expectedEvents')}; expected={expected}")
    event_ids = [str(row.get("eventId", "")).lower() for row in readings]
    check(checks, case_id, profile, "accepted_event_ids_unique", len(event_ids) == len(set(event_ids)), f"rows={len(event_ids)}; unique={len(set(event_ids))}")
    risk_count = integer(audit.get("riskAssessments"))
    check(checks, case_id, profile, "risk_not_greater_than_accepted", 0 <= risk_count <= len(readings), f"risk={risk_count}; accepted={len(readings)}")
    observations = run.get("cycleObservations", [])
    if observations:
        obs_ids = {str(row.get("eventId", "")).lower() for row in observations}
        check(checks, case_id, profile, "cycle_observation_event_coverage", set(event_ids).issubset(obs_ids), f"accepted={len(set(event_ids))}; observed={len(obs_ids)}")
    assert_physical_bounds(checks, run, profile)


def assert_profile(checks: list[Check], run: dict[str, Any], baseline: dict[str, Any], config: dict[str, Any], all_runs: list[dict[str, Any]]) -> None:
    profile = str(run["primaryProfile"])
    case_id = str(run["caseId"])
    expected = integer(run.get("expectedEvents"))
    audit = run.get("audit") or {}
    accepted = integer(audit.get("acceptedReadings"))
    risk = integer(audit.get("riskAssessments"))
    missing = integer(audit.get("missingEvents"))
    thresholds = config["scenarioMatrix"]["effectThresholds"]

    if profile == "none":
        check(checks, case_id, profile, "accepted_equals_expected", accepted == expected, f"accepted={accepted}; expected={expected}")
        check(checks, case_id, profile, "risk_equals_accepted", risk == accepted, f"risk={risk}; accepted={accepted}")
        check(checks, case_id, profile, "missing_zero", missing == 0, f"missing={missing}")
        check(checks, case_id, profile, "rejected_zero", integer(audit.get("rejected")) == 0, f"rejected={audit.get('rejected')}")
        check(checks, case_id, profile, "quarantined_zero", integer(audit.get("quarantined")) == 0, f"quarantined={audit.get('quarantined')}")
        return

    if profile == "missing-readings":
        check(checks, case_id, profile, "accepted_between_zero_and_expected", 0 < accepted < expected, f"accepted={accepted}; expected={expected}")
        check(checks, case_id, profile, "missing_arithmetic", missing == expected - accepted, f"missing={missing}; expected-accepted={expected-accepted}")
        repeat = next((item for item in all_runs if item.get("repeatOf") == case_id), None)
        if repeat:
            omitted = set(key(row) for row in baseline.get("readings", [])) - set(key(row) for row in run.get("readings", []))
            repeat_omitted = set(key(row) for row in baseline.get("readings", [])) - set(key(row) for row in repeat.get("readings", []))
            check(checks, case_id, profile, "same_seed_same_omission_pattern", omitted == repeat_omitted and bool(omitted), f"omitted={len(omitted)}; repeat={len(repeat_omitted)}")
        else:
            check(checks, case_id, profile, "same_seed_same_omission_pattern", False, "repeat run was not supplied")
        return

    matches = matched_values(run, baseline)
    check(checks, case_id, profile, "baseline_matches_available", bool(matches), f"matched={len(matches)}")

    if profile == "noise":
        changed = sum(1 for _, _, delta in matches if abs(delta) > 1e-9)
        ratio = changed / len(matches) if matches else 0.0
        minimum = float(thresholds["noiseMinimumChangedRatio"])
        check(checks, case_id, profile, "values_differ_from_baseline", ratio >= minimum, f"changedRatio={ratio:.3f}; minimum={minimum}")
        return

    if profile == "bias":
        deltas = mean_delta_by_metric(run, baseline)
        check(checks, case_id, profile, "temperature_delta_positive", deltas.get("Temperature", -math.inf) >= float(thresholds["biasTemperatureMinimumMeanDelta"]), f"meanDeltas={deltas}")
        check(checks, case_id, profile, "humidity_delta_negative", deltas.get("Humidity", math.inf) <= float(thresholds["biasHumidityMaximumMeanDelta"]), f"meanDeltas={deltas}")
        check(checks, case_id, profile, "wind_delta_positive", deltas.get("WindSpeed", -math.inf) >= float(thresholds["biasWindMinimumMeanDelta"]), f"meanDeltas={deltas}")
        return

    if profile == "drift":
        grouped: dict[str, list[tuple[float, float]]] = defaultdict(list)
        for current, _, delta in matches:
            grouped[str(current.get("metricType"))].append((float(integer(current.get("cycleIndex"))), delta))
        slopes = {metric: regression_slope(points) for metric, points in grouped.items()}
        minimum = float(thresholds["driftMinimumAbsoluteSlope"])
        conditions = {
            "Temperature": slopes.get("Temperature") is not None and slopes["Temperature"] >= minimum,
            "Humidity": slopes.get("Humidity") is not None and slopes["Humidity"] <= -minimum,
            "WindSpeed": slopes.get("WindSpeed") is not None and slopes["WindSpeed"] >= minimum,
        }
        check(checks, case_id, profile, "delta_changes_with_cycle_index", all(conditions.values()), f"slopes={slopes}; minimum={minimum}")
        return

    if profile == "stuck-value":
        groups: dict[tuple[str, str], set[float]] = defaultdict(set)
        for row in run.get("readings", []):
            value = number(row.get("value"))
            if value is not None:
                groups[(str(row.get("sensorId")), str(row.get("metricType")))].add(round(value, 9))
        non_stuck = {str(group): len(values) for group, values in groups.items() if len(values) != 1}
        check(checks, case_id, profile, "repeated_value_across_cycles", bool(groups) and not non_stuck, f"groups={len(groups)}; nonStuck={non_stuck}")
        return

    if profile == "outlier":
        deltas: dict[str, list[float]] = defaultdict(list)
        for current, _, delta in matches:
            deltas[str(current.get("metricType"))].append(delta)
        observed = (
            max(deltas.get("Temperature", [-math.inf])) >= float(thresholds["outlierTemperatureMinimumDelta"])
            or max((abs(value) for value in deltas.get("Humidity", [])), default=-math.inf) >= float(thresholds["outlierHumidityMinimumAbsoluteDelta"])
            or max(deltas.get("WindSpeed", [-math.inf])) >= float(thresholds["outlierWindMinimumDelta"])
        )
        outlier_maxima = {key: max((abs(value) for value in values), default=0.0) for key, values in deltas.items() if values}
        check(checks, case_id, profile, "deterministic_material_outlier", observed, f"maxDeltas={outlier_maxima}")
        outcomes = {str(row.get("outcome")) for row in run.get("cycleObservations", [])}
        check(checks, case_id, profile, "classification_consistent", not outcomes or outcomes.issubset({"Eligible", "Blocked", "Missing", "CarriedForward"}), f"outcomes={sorted(outcomes)}")
        return

    if profile == "clipping/range":
        caps = thresholds["clippingCaps"]
        violations: list[str] = []
        for row in run.get("readings", []):
            metric = str(row.get("metricType"))
            value = number(row.get("value"))
            if value is None or metric not in caps:
                continue
            rule = caps[metric]
            if "maximum" in rule and value > float(rule["maximum"]) + 1e-9:
                violations.append(f"{metric}>{rule['maximum']}:{value}")
            if "minimum" in rule and value < float(rule["minimum"]) - 1e-9:
                violations.append(f"{metric}<{rule['minimum']}:{value}")
        check(checks, case_id, profile, "profile_caps_respected", not violations, f"violations={violations[:5]}")
        supplemental = next((item for item in all_runs if item.get("primaryProfile") == profile and item.get("supplemental")), None)
        cap_hits = 0
        if supplemental:
            for row in supplemental.get("readings", []):
                metric = str(row.get("metricType"))
                value = number(row.get("value"))
                rule = caps.get(metric, {})
                targets = [number(rule.get("maximum")), number(rule.get("minimum"))]
                if value is not None and any(target is not None and abs(value - target) <= 1e-9 for target in targets):
                    cap_hits += 1
        check(checks, case_id, profile, "clipping_effect_observed", cap_hits > 0, f"supplementalCapHits={cap_hits}")
        return

    if profile == "lag/delay":
        minimum = float(thresholds["lagMinimumSeconds"])
        delays = [number(row.get("ingestDelaySeconds")) for row in run.get("readings", [])]
        delays = [value for value in delays if value is not None]
        check(checks, case_id, profile, "configured_delay_persisted", bool(delays) and min(delays) >= minimum, f"minDelay={min(delays) if delays else None}; required={minimum}")
        return

    if profile == "duplicate":
        published = [str(row.get("eventId", "")).lower() for row in run.get("publishEvents", [])]
        duplicate_count = len(published) - len(set(published))
        check(checks, case_id, profile, "duplicate_delivery_observed", duplicate_count > 0, f"published={len(published)}; duplicateDeliveries={duplicate_count}")
        check(checks, case_id, profile, "idempotent_accepted_count", accepted == expected, f"accepted={accepted}; expected={expected}")
        return

    if profile == "out-of-order":
        baseline_by_cycle: dict[int, list[str]] = defaultdict(list)
        current_by_cycle: dict[int, list[str]] = defaultdict(list)
        for row in baseline.get("publishEvents", []):
            baseline_by_cycle[integer(row.get("cycleIndex"), -1)].append(str(row.get("sensorId", "")).lower())
        for row in run.get("publishEvents", []):
            current_by_cycle[integer(row.get("cycleIndex"), -1)].append(str(row.get("sensorId", "")).lower())
        comparable = [cycle for cycle in baseline_by_cycle if cycle in current_by_cycle and len(baseline_by_cycle[cycle]) > 1]
        reversed_cycles = [cycle for cycle in comparable if current_by_cycle[cycle] == list(reversed(baseline_by_cycle[cycle]))]
        check(checks, case_id, profile, "delivery_order_changed", bool(comparable) and len(reversed_cycles) == len(comparable), f"comparable={comparable}; reversed={reversed_cycles}")
        check(checks, case_id, profile, "all_events_converged", accepted == expected, f"accepted={accepted}; expected={expected}")
        return

    if profile == "retry-transient":
        attempts = run.get("attempts", [])
        retry_rows = [row for row in attempts if str(row.get("outcome")) == "RetryScheduled" or integer(row.get("attemptNumber")) > 1]
        check(checks, case_id, profile, "transient_retry_observed", bool(retry_rows), f"retryRows={len(retry_rows)}")
        check(checks, case_id, profile, "eventually_processed", accepted == expected and integer(audit.get("quarantined")) == 0, f"accepted={accepted}; expected={expected}; quarantined={audit.get('quarantined')}")
        return

    check(checks, case_id, profile, "known_profile", False, f"Unsupported profile {profile}")


def write_outputs(output_dir: Path, checks: list[Check], payload: dict[str, Any]) -> int:
    output_dir.mkdir(parents=True, exist_ok=True)
    failed = [item for item in checks if item.status == FAIL]
    status = PASS if not failed else FAIL
    result = {
        "schemaVersion": 1,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z"),
        "status": status,
        "nativeStatus": "SCENARIO_PROFILE_MATRIX_PASS" if status == PASS else "SCENARIO_PROFILE_MATRIX_FAIL",
        "checks": [asdict(item) for item in checks],
        "summary": {
            "total": len(checks),
            "passed": len(checks) - len(failed),
            "failed": len(failed),
            "runCount": len(payload.get("runs", [])),
        },
    }
    (output_dir / "scenario-matrix-result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    with (output_dir / "scenario-matrix-checks.csv").open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=["case_id", "profile", "assertion", "status", "detail"])
        writer.writeheader()
        writer.writerows(asdict(item) for item in checks)
    lines = [
        "# Scenario profile matrix",
        "",
        f"- Status: **{status}**",
        f"- Runs: {len(payload.get('runs', []))}",
        f"- Checks: {len(checks)}",
        f"- Failed: {len(failed)}",
        "",
        "## Failed checks",
        "",
    ]
    if failed:
        lines.extend(f"- `{item.case_id}` / `{item.assertion}`: {item.detail}" for item in failed)
    else:
        lines.append("- None.")
    (output_dir / "SCENARIO-MATRIX.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    return 0 if status == PASS else 1


def main() -> int:
    args = parse_args()
    config = load_json(Path(args.config))
    payload = load_json(Path(args.input))
    runs = list(payload.get("runs", []))
    checks: list[Check] = []

    configured_profiles = list(config["scenarioMatrix"]["profiles"])
    supplied_primary = {str(run.get("primaryProfile")) for run in runs if not run.get("supplemental") and not run.get("repeatOf")}
    for profile in configured_profiles:
        check(checks, "matrix", profile, "profile_supplied", profile in supplied_primary, f"supplied={sorted(supplied_primary)}")

    baseline = next((run for run in runs if run.get("primaryProfile") == config["scenarioMatrix"]["baselineProfile"] and not run.get("repeatOf") and not run.get("supplemental")), None)
    check(checks, "matrix", "none", "baseline_supplied", baseline is not None, "baseline profile none")
    if baseline is None:
        return write_outputs(Path(args.output_dir), checks, payload)

    runtime = config["runtime"]
    for run in runs:
        if run.get("repeatOf") or run.get("supplemental"):
            continue
        assert_common(checks, run, runtime)
        assert_profile(checks, run, baseline, config, runs)

    return write_outputs(Path(args.output_dir), checks, payload)


if __name__ == "__main__":
    sys.exit(main())
