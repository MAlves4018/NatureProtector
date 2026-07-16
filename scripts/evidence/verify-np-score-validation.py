#!/usr/bin/env python3
"""Verify the integrity and claim boundaries of a Phase 9 NP_score evidence run."""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import re
from pathlib import Path

REQUIRED_FILES = [
    "formula-contract.json",
    "source-inventory.csv",
    "provenance.json",
    "data-quality.json",
    "territorial-components.csv",
    "daily-score-dataset.csv",
    "matched-controls.csv",
    "model-comparison.csv",
    "threshold-analysis.csv",
    "temporal-validation.csv",
    "component-correlations.csv",
    "event-source-stratification.csv",
    "lag-analysis.csv",
    "territorial-added-value.json",
    "sensitivity-analysis.csv",
    "extent-association.json",
    "existing-evidence-import.json",
    "scenario-evidence-records.csv",
    "scenario-comparison.csv",
    "phase9-summary.json",
    "phase9-summary.md",
    "figures/np-score-distribution.svg",
    "figures/roc-comparison.svg",
    "figures/precision-recall-comparison.svg",
    "figures/threshold-tradeoff.svg",
    "figures/sensitivity-stability.svg",
    "SHA256SUMS.txt",
]
SECRET_PATTERNS = [
    re.compile(r"postgres(?:ql)?://[^\s]+:[^\s]+@", re.I),
    re.compile(r"(?:password|passwd|pwd)\s*[:=]\s*[^\s,;]+", re.I),
    re.compile(r"bearer\s+[A-Za-z0-9._~+/=-]{16,}", re.I),
    re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def as_float(value: str | None) -> float | None:
    try:
        parsed = float(value) if value not in (None, "") else None
    except ValueError:
        return None
    return parsed if parsed is not None and math.isfinite(parsed) else None


def verify_hashes(root: Path) -> None:
    manifest = root / "SHA256SUMS.txt"
    lines = [line for line in manifest.read_text(encoding="utf-8").splitlines() if line.strip()]
    if not lines:
        raise SystemExit("Empty SHA256SUMS.txt")
    seen: set[str] = set()
    for line in lines:
        try:
            expected, relative = line.split("  ", 1)
        except ValueError as exc:
            raise SystemExit(f"Malformed hash line: {line}") from exc
        if relative in seen:
            raise SystemExit(f"Duplicate hash entry: {relative}")
        seen.add(relative)
        path = root / relative
        if not path.is_file():
            raise SystemExit(f"Missing hashed file: {relative}")
        actual = sha256(path)
        if actual != expected:
            raise SystemExit(f"Hash mismatch for {relative}: expected {expected}, got {actual}")
    expected_files = {path.relative_to(root).as_posix() for path in root.rglob("*") if path.is_file() and path != manifest}
    if seen != expected_files:
        missing = sorted(expected_files - seen)
        stale = sorted(seen - expected_files)
        raise SystemExit(f"Hash coverage mismatch; unhashed={missing}, stale={stale}")


def verify_no_secrets(root: Path) -> None:
    for path in root.rglob("*"):
        if not path.is_file() or path.suffix.lower() in {".png", ".parquet", ".gpkg"}:
            continue
        try:
            text = path.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        for pattern in SECRET_PATTERNS:
            if pattern.search(text):
                raise SystemExit(f"Possible secret material in {path.relative_to(root)}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence_root", type=Path)
    parser.add_argument("--require-complete", action="store_true")
    args = parser.parse_args()
    root = args.evidence_root.resolve()
    missing = [relative for relative in REQUIRED_FILES if not (root / relative).is_file()]
    if missing:
        raise SystemExit("Missing required Phase 9 files: " + ", ".join(missing))

    verify_hashes(root)
    verify_no_secrets(root)

    contract = json.loads((root / "formula-contract.json").read_text(encoding="utf-8"))
    if contract.get("status") != "PASS" or not contract.get("checks") or not all(item.get("match") for item in contract["checks"]):
        raise SystemExit("Formula constants contract was not verified against the C# source.")

    summary = json.loads((root / "phase9-summary.json").read_text(encoding="utf-8"))
    allowed_statuses = {"PASS_EXPLORATORY_VALIDATION", "PARTIAL_INSUFFICIENT_EVENTS"}
    if summary.get("status") not in allowed_statuses:
        raise SystemExit(f"Unexpected Phase 9 status: {summary.get('status')}")
    if args.require_complete and summary.get("status") != "PASS_EXPLORATORY_VALIDATION":
        raise SystemExit(f"Complete Phase 9 evidence required, got {summary.get('status')}")

    ceiling = summary.get("claimCeiling", {})
    if ceiling.get("calibratedProbability") is not False:
        raise SystemExit("Claim ceiling must explicitly deny calibrated probability.")
    if ceiling.get("causalValidation") is not False:
        raise SystemExit("Claim ceiling must explicitly deny causal validation.")
    if ceiling.get("externalGeneralisation") is not False:
        raise SystemExit("Claim ceiling must explicitly deny external generalisation.")
    for denied in ("localIgnitionPrediction", "territorialAddedValueValidated", "prospectiveForecastValidated"):
        if ceiling.get(denied) is not False:
            raise SystemExit(f"Claim ceiling must explicitly deny {denied}.")
    if summary.get("constantsContractStatus") != "PASS" or summary.get("crossLanguageParityStatus") != "NOT_EXECUTED":
        raise SystemExit("Formula constants and cross-language parity statuses are not explicit.")
    if ceiling.get("evidenceClass") != "exploratory-retrospective":
        raise SystemExit("Unexpected evidence class.")

    daily = read_csv(root / "daily-score-dataset.csv")
    if not daily:
        raise SystemExit("daily-score-dataset.csv is empty")
    dates = [row.get("date_local") for row in daily]
    if len(dates) != len(set(dates)):
        raise SystemExit("daily-score-dataset.csv contains duplicate dates")
    for row in daily:
        score = as_float(row.get("np_score_v1"))
        if score is None or not 0.0 <= score <= 1.0:
            raise SystemExit(f"Invalid NP_score for {row.get('date_local')}: {row.get('np_score_v1')}")

    outside = [row for row in daily if row.get("event_label_eligible") == "0"]
    if not outside or any(row.get("event_label") not in {"", None} for row in outside):
        raise SystemExit("Dates outside event-source coverage must be present but unlabeled.")

    comparisons = read_csv(root / "model-comparison.csv")
    np_rows = [row for row in comparisons if row.get("population") == "seasonal_population" and row.get("model") == "np_score_v1"]
    if len(np_rows) != 1:
        raise SystemExit("Expected exactly one seasonal NP_score comparison row")
    for column in ("roc_auc", "average_precision"):
        value = as_float(np_rows[0].get(column))
        if value is not None and not 0.0 <= value <= 1.0:
            raise SystemExit(f"Invalid {column}: {value}")

    thresholds = read_csv(root / "threshold-analysis.csv")
    threshold_values = {round(as_float(row.get("threshold")) or -1, 8) for row in thresholds}
    for required in (0.50, 0.60, 0.70, 0.80):
        if round(required, 8) not in threshold_values:
            raise SystemExit(f"Missing operational threshold {required}")

    quality = json.loads((root / "data-quality.json").read_text(encoding="utf-8"))
    if quality.get("qualityAssessment") != "SHARE_WITH_CAVEATS":
        raise SystemExit("Data quality assessment must remain SHARE_WITH_CAVEATS")
    if quality.get("seasonalEventDatesAnalyzed") != summary.get("headlineMetrics", {}).get("eventDates"):
        raise SystemExit("Seasonal event count mismatch between data quality and summary")
    if quality.get("eventDatesWithWeather", 0) < quality.get("seasonalEventDatesAnalyzed", 0):
        raise SystemExit("Seasonal event count cannot exceed eligible event dates with weather")
    coverage = quality.get("eventSourceCoverage", {})
    if not coverage.get("startDate") or not coverage.get("endDate"):
        raise SystemExit("Event-source coverage window must be explicit")
    if quality.get("seasonalRowsOutsideEventCoverage", 0) != summary.get("headlineMetrics", {}).get("seasonalWeatherRowsOutsideEventCoverage", 0):
        raise SystemExit("Unlabeled seasonal row count mismatch")
    limitations = quality.get("territorialLimitationCounts", {})
    headline = summary.get("headlineMetrics", {})
    if headline.get("territorialCellsUsingAltitudeDefault", 0) != limitations.get("altitude_missing_candidate_default", 0):
        raise SystemExit("Altitude default count mismatch")

    lag_rows = read_csv(root / "lag-analysis.csv")
    if {row.get("lag_days") for row in lag_rows} != {"0", "1", "2"}:
        raise SystemExit("Lag analysis must contain D0, D-1 and D-2")
    source_rows = read_csv(root / "event-source-stratification.csv")
    if not any(row.get("event_definition") == "municipality_intersection" for row in source_rows):
        raise SystemExit("Event-source stratification is incomplete")
    territory = json.loads((root / "territorial-added-value.json").read_text(encoding="utf-8"))
    if territory.get("temporalAddedValueDemonstrated") is not False:
        raise SystemExit("Static territory profile must not be promoted as validated added value")

    for relative in [item for item in REQUIRED_FILES if item.endswith(".svg")]:
        text = (root / relative).read_text(encoding="utf-8")
        if "<svg" not in text or "<title" not in text or "<desc" not in text or 'fill="white"' not in text:
            raise SystemExit(f"Invalid or inaccessible SVG: {relative}")

    print(f"PHASE_9_VERIFICATION=PASS")
    print(f"PHASE_9_STATUS={summary['status']}")
    print(f"PHASE_9_OUTPUT={root}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
