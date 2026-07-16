#!/usr/bin/env python3
"""Core helpers for Phase 9 NP_score exploratory validation.

The module intentionally uses only the Python standard library so the static
analysis can run in the same evidence environments as the existing collectors.
It mirrors CandidateParameterSetV1 and fails when the configured formula
contract drifts from the C# source.
"""
from __future__ import annotations

import csv
import hashlib
import json
import math
import random
import re
import statistics
import unicodedata
from collections import defaultdict
from dataclasses import dataclass
from datetime import date, datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Iterable, Sequence

SCRIPT_VERSION = "1.1.0"


def utc_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def clamp(value: float) -> float:
    return max(0.0, min(1.0, float(value)))


def as_float(value: Any) -> float | None:
    if value is None:
        return None
    text = str(value).strip()
    if not text:
        return None
    try:
        parsed = float(text.replace(",", "."))
    except ValueError:
        return None
    return parsed if math.isfinite(parsed) else None


def as_date(value: Any) -> date | None:
    text = str(value or "").strip()
    if not text:
        return None
    try:
        return date.fromisoformat(text[:10])
    except ValueError:
        return None


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_csv(path: Path, rows: Sequence[dict[str, Any]], fieldnames: Sequence[str] | None = None) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if fieldnames is None:
        fieldnames = list(rows[0]) if rows else []
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(fieldnames), extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)


def parse_csharp_constants(path: Path) -> dict[str, float | str]:
    text = path.read_text(encoding="utf-8")
    values: dict[str, float | str] = {}
    for match in re.finditer(r"public\s+const\s+(?:double|int|string)\s+(\w+)\s*=\s*([^;]+);", text):
        name, raw = match.group(1), match.group(2).strip()
        if raw.startswith('"') and raw.endswith('"'):
            values[name] = raw[1:-1]
            continue
        try:
            values[name] = float(raw)
        except ValueError:
            continue
    return values


def validate_formula_contract(config: dict[str, Any], source_constants: dict[str, float | str]) -> list[dict[str, Any]]:
    checks: list[dict[str, Any]] = []
    expected_version = config["formulaContract"]["version"]
    actual_version = source_constants.get("Version")
    checks.append({"name": "Version", "expected": expected_version, "actual": actual_version, "match": actual_version == expected_version})
    for name, expected in config["formulaContract"]["constants"].items():
        actual = source_constants.get(name)
        match = isinstance(actual, (int, float)) and math.isclose(float(actual), float(expected), rel_tol=0, abs_tol=1e-12)
        checks.append({"name": name, "expected": expected, "actual": actual, "match": match})
    return checks


def normalize_text(value: Any) -> str:
    text = str(value or "").strip()
    if not text:
        return ""
    decomposed = unicodedata.normalize("NFD", text)
    out: list[str] = []
    previous_separator = True
    for character in decomposed:
        if unicodedata.category(character) == "Mn":
            continue
        if character.isalnum():
            out.append(character.lower())
            previous_separator = False
        elif not previous_separator:
            out.append(" ")
            previous_separator = True
    return "".join(out).strip()


HAZARD_ALIASES = {
    "muito alta": 0.90, "very high": 0.90, "extreme": 0.90,
    "alta": 0.75, "high": 0.75,
    "moderada": 0.50, "media": 0.50, "medium": 0.50,
    "baixa": 0.25, "low": 0.25,
    "muito baixa": 0.10, "very low": 0.10,
}
FUEL_ALIASES = {
    "florestas de eucalipto": 0.85, "eucalipto": 0.85, "eucaliptal": 0.85,
    "florestas de pinheiro bravo": 0.85, "pinheiro bravo": 0.85, "pinhal": 0.85,
    "pine": 0.85, "florestas de resinosas": 0.85, "resinosas": 0.85,
    "matos": 0.80, "mato": 0.80, "mato denso": 0.80, "matos densos": 0.80,
    "shrub": 0.80, "shrubs": 0.80, "scrub": 0.80,
    "florestas de outras folhosas": 0.75, "outras folhosas": 0.75,
    "florestas": 0.75, "floresta": 0.75, "forest": 0.75, "wood": 0.75, "woodland": 0.75,
    "culturas temporarias de sequeiro e regadio": 0.40,
    "culturas temporarias e ou pastagens melhoradas associadas a olival": 0.40,
    "mosaicos culturais e parcelares complexos": 0.40, "olivais": 0.40, "olival": 0.40,
    "agricultura": 0.40, "agriculture": 0.40, "pastagem": 0.40, "pastagens": 0.40,
    "pasture": 0.40, "herbaceas": 0.40, "herbaceous": 0.40,
    "albufeiras de barragens": 0.15, "corpos de agua": 0.15, "agua": 0.15,
    "water": 0.15, "urbano": 0.15, "urban": 0.15, "artificial": 0.15,
    "areas artificiais": 0.15,
}


def resolve_hazard(row: dict[str, str]) -> tuple[float, str | None]:
    value = normalize_text(row.get("structural_hazard"))
    if not value:
        return 0.50, "hazard_missing_candidate_default"
    if value in HAZARD_ALIASES:
        return HAZARD_ALIASES[value], None
    return 0.50, "hazard_unmapped_candidate_default"


def resolve_fuel(row: dict[str, str]) -> tuple[float, str | None]:
    labels = [normalize_text(row.get(name)) for name in ("dominant_forest_type", "dominant_fuel_model", "land_cover_class")]
    mapped = [FUEL_ALIASES[label] for label in labels if label in FUEL_ALIASES]
    if mapped:
        return max(mapped), None
    density = as_float(row.get("tree_cover_density"))
    if density is not None:
        return clamp(density / 100.0), None
    return 0.50, "fuel_missing_or_unmapped_candidate_default"


def resolve_geomorphology(row: dict[str, str]) -> tuple[float, str | None]:
    slope = as_float(row.get("slope_deg"))
    aspect = as_float(row.get("aspect_deg"))
    altitude = as_float(row.get("altitude_m"))
    if slope is None and aspect is None and altitude is None:
        return 0.50, "geomorphology_missing_candidate_default"
    slope_score = clamp(slope / 35.0) if slope is not None else 0.50
    if aspect is None:
        aspect_score = 0.50
    else:
        normalized = aspect % 360.0
        aspect_score = 0.80 if 135.0 <= normalized <= 270.0 else 0.55 if (90.0 <= normalized < 135.0 or 270.0 < normalized <= 315.0) else 0.30
    altitude_score = clamp(altitude / 1000.0) if altitude is not None else 0.50
    limitations = []
    if slope is None: limitations.append("slope_missing_candidate_default")
    if aspect is None: limitations.append("aspect_missing_candidate_default")
    if altitude is None: limitations.append("altitude_missing_candidate_default")
    return clamp(0.70 * slope_score + 0.20 * aspect_score + 0.10 * altitude_score), ";".join(limitations) or None


def cell_territory(row: dict[str, str]) -> dict[str, Any]:
    hazard, h_lim = resolve_hazard(row)
    fuel, f_lim = resolve_fuel(row)
    geomorphology, g_lim = resolve_geomorphology(row)
    territory = clamp(0.50 * hazard + 0.30 * fuel + 0.20 * geomorphology)
    limitations = ";".join(x for x in (h_lim, f_lim, g_lim) if x)
    return {
        "cell_id": row.get("cell_id"),
        "hazard": hazard,
        "fuel": fuel,
        "geomorphology": geomorphology,
        "territory": territory,
        "limitations": limitations,
    }


def metric_risk(metric: str, value: float) -> float:
    if metric == "temperature":
        return 0.10 if value < 20 else 0.20 if value < 25 else 0.40 if value < 30 else 0.65 if value < 35 else 0.85 if value < 40 else 1.00
    if metric == "humidity":
        return 0.05 if value >= 70 else 0.20 if value >= 50 else 0.40 if value >= 35 else 0.70 if value >= 20 else 0.95
    if metric == "wind":
        return 0.10 if value < 5 else 0.30 if value < 10 else 0.55 if value < 15 else 0.75 if value < 20 else 0.95
    return 0.20


def meteorology_components(row: dict[str, str], constants: dict[str, float]) -> dict[str, float | None]:
    temp = as_float(row.get("noon_temperature_c"))
    humidity = as_float(row.get("noon_relative_humidity_pct"))
    wind = as_float(row.get("noon_wind_speed_ms"))
    weighted: list[tuple[float, float]] = []
    if temp is not None: weighted.append((constants["TemperatureMetricWeight"], metric_risk("temperature", temp)))
    if humidity is not None: weighted.append((constants["HumidityMetricWeight"], metric_risk("humidity", humidity)))
    if wind is not None: weighted.append((constants["WindMetricWeight"], metric_risk("wind", wind)))
    if not weighted:
        return {"metric": None, "fwi": None, "meteorology": None}
    metric = clamp(sum(w * score for w, score in weighted) / sum(w for w, _ in weighted))
    fwi_raw = as_float(row.get("fwi_reference"))
    normalized_fwi = clamp(fwi_raw / constants["FireWeatherIndexNormalizationReference"]) if fwi_raw is not None else None
    meteorology = metric if normalized_fwi is None else clamp(constants["FireWeatherIndexMetricBlendWeight"] * metric + constants["FireWeatherIndexBlendWeight"] * normalized_fwi)
    return {"metric": metric, "fwi": normalized_fwi, "meteorology": meteorology}


def nearest_rank(values: Sequence[float], percentile: float) -> float:
    ordered = sorted(values)
    if not ordered:
        raise ValueError("nearest_rank requires values")
    rank = math.ceil(percentile * len(ordered))
    return ordered[max(0, min(len(ordered) - 1, rank - 1))]


def aggregate_area(scores: Sequence[float], aggregation: dict[str, Any]) -> float:
    p = nearest_rank(scores, float(aggregation["percentile"]))
    return clamp(float(aggregation["percentileWeight"]) * p + float(aggregation["maximumWeight"]) * max(scores))


def score_day(row: dict[str, str], cells: Sequence[dict[str, Any]], constants: dict[str, float], aggregation: dict[str, Any], weights: tuple[float, float, float] | None = None, fwi_blend: float | None = None, fwi_reference: float | None = None) -> dict[str, Any] | None:
    local = dict(constants)
    if fwi_blend is not None:
        local["FireWeatherIndexBlendWeight"] = fwi_blend
        local["FireWeatherIndexMetricBlendWeight"] = 1.0 - fwi_blend
    if fwi_reference is not None:
        local["FireWeatherIndexNormalizationReference"] = fwi_reference
    components = meteorology_components(row, local)
    if components["meteorology"] is None:
        return None
    kbdi = as_float(row.get("kbdi_reference"))
    drought = clamp(kbdi / local["KeetchByramDroughtIndexMaximum"]) if kbdi is not None else 0.50
    wm, wd, wt = weights or (local["MeteorologyWeight"], local["DroughtWeight"], local["TerritoryWeight"])
    total = wm + wd + wt
    if total <= 0:
        raise ValueError("weights must have positive sum")
    wm, wd, wt = wm / total, wd / total, wt / total
    cell_scores = [clamp(wm * float(components["meteorology"]) + wd * drought + wt * float(cell["territory"])) for cell in cells]
    score = aggregate_area(cell_scores, aggregation)
    no_territory_total = wm + wd
    no_territory = clamp((wm / no_territory_total) * float(components["meteorology"]) + (wd / no_territory_total) * drought)
    return {
        "np_score_v1": score,
        "meteorology_metric_only": components["metric"],
        "fwi_normalized": components["fwi"],
        "meteorology_with_fwi": components["meteorology"],
        "kbdi_normalized": drought,
        "np_without_territory": no_territory,
        "np_equal_weights": aggregate_area([clamp((float(components["meteorology"]) + drought + float(cell["territory"])) / 3.0) for cell in cells], aggregation),
        "simple_weather_risk_score": as_float(row.get("simple_weather_risk_score")),
        "fire_index_reference_score": as_float(row.get("fire_index_reference_score")),
        "territory_p80": nearest_rank([float(cell["territory"]) for cell in cells], 0.80),
        "territory_max": max(float(cell["territory"]) for cell in cells),
    }


def ranks(values: Sequence[float]) -> list[float]:
    order = sorted(range(len(values)), key=lambda i: values[i])
    result = [0.0] * len(values)
    index = 0
    while index < len(order):
        end = index
        while end + 1 < len(order) and values[order[end + 1]] == values[order[index]]:
            end += 1
        average = (index + end + 2) / 2.0
        for cursor in range(index, end + 1):
            result[order[cursor]] = average
        index = end + 1
    return result


def pearson(x: Sequence[float], y: Sequence[float]) -> float | None:
    if len(x) != len(y) or len(x) < 2:
        return None
    mx, my = statistics.fmean(x), statistics.fmean(y)
    dx, dy = [v - mx for v in x], [v - my for v in y]
    denominator = math.sqrt(sum(v * v for v in dx) * sum(v * v for v in dy))
    return sum(a * b for a, b in zip(dx, dy)) / denominator if denominator else None


def spearman(x: Sequence[float], y: Sequence[float]) -> float | None:
    return pearson(ranks(x), ranks(y)) if len(x) == len(y) and len(x) >= 2 else None


def roc_curve(labels: Sequence[int], scores: Sequence[float]) -> list[dict[str, float]]:
    pairs = sorted(zip(scores, labels), reverse=True)
    positives = sum(labels)
    negatives = len(labels) - positives
    if positives == 0 or negatives == 0:
        return []
    tp = fp = 0
    points = [{"threshold": float("inf"), "tpr": 0.0, "fpr": 0.0}]
    last: float | None = None
    for score, label in pairs:
        if last is not None and score != last:
            points.append({"threshold": last, "tpr": tp / positives, "fpr": fp / negatives})
        if label: tp += 1
        else: fp += 1
        last = score
    points.append({"threshold": last if last is not None else 0.0, "tpr": tp / positives, "fpr": fp / negatives})
    return points


def precision_recall_curve(labels: Sequence[int], scores: Sequence[float]) -> list[dict[str, float]]:
    """Return threshold-grouped precision/recall points.

    Equal scores are processed as one threshold.  Processing tied observations
    one row at a time makes Average Precision depend on the arbitrary ordering
    of labels inside a tie and can materially overstate stepwise models.
    """
    pairs = sorted(zip(scores, labels), key=lambda item: item[0], reverse=True)
    positives = sum(labels)
    if positives == 0:
        return []
    tp = fp = 0
    points = [{"threshold": float("inf"), "precision": 1.0, "recall": 0.0}]
    index = 0
    while index < len(pairs):
        threshold = pairs[index][0]
        tied_positive = tied_negative = 0
        while index < len(pairs) and pairs[index][0] == threshold:
            if pairs[index][1]:
                tied_positive += 1
            else:
                tied_negative += 1
            index += 1
        tp += tied_positive
        fp += tied_negative
        points.append({"threshold": threshold, "precision": tp / (tp + fp), "recall": tp / positives})
    return points


def auc_from_curve(points: Sequence[dict[str, float]], x: str, y: str) -> float | None:
    if len(points) < 2:
        return None
    ordered = sorted(points, key=lambda p: p[x])
    return sum((b[x] - a[x]) * (a[y] + b[y]) / 2.0 for a, b in zip(ordered, ordered[1:]))


def roc_auc(labels: Sequence[int], scores: Sequence[float]) -> float | None:
    return auc_from_curve(roc_curve(labels, scores), "fpr", "tpr")


def average_precision(labels: Sequence[int], scores: Sequence[float]) -> float | None:
    points = precision_recall_curve(labels, scores)
    if len(points) < 2:
        return None
    total = 0.0
    previous_recall = 0.0
    for point in points[1:]:
        recall = point["recall"]
        total += max(0.0, recall - previous_recall) * point["precision"]
        previous_recall = recall
    return total


def threshold_metrics(labels: Sequence[int], scores: Sequence[float], threshold: float) -> dict[str, float | int | None]:
    tp = sum(1 for label, score in zip(labels, scores) if label and score >= threshold)
    fn = sum(1 for label, score in zip(labels, scores) if label and score < threshold)
    fp = sum(1 for label, score in zip(labels, scores) if not label and score >= threshold)
    tn = sum(1 for label, score in zip(labels, scores) if not label and score < threshold)
    return {
        "threshold": threshold, "tp": tp, "fp": fp, "tn": tn, "fn": fn,
        "sensitivity": tp / (tp + fn) if tp + fn else None,
        "specificity": tn / (tn + fp) if tn + fp else None,
        "precision": tp / (tp + fp) if tp + fp else None,
        "false_positive_rate": fp / (fp + tn) if fp + tn else None,
        "positive_day_fraction": (tp + fp) / len(labels) if labels else None,
        "non_event_alert_days_per_30": 30.0 * fp / max(1, fp + tn),
        # Deprecated compatibility alias. These are not confirmed false alarms:
        # the negative class means no eligible recorded event start on that date.
        "false_alert_days_per_30": 30.0 * fp / max(1, fp + tn),
    }


def mann_whitney(labels: Sequence[int], scores: Sequence[float]) -> dict[str, float | bool | None]:
    """Mann-Whitney U with tie-corrected asymptotic variance.

    The p-value remains an asymptotic approximation, but unlike the previous
    implementation it accounts for the many tied values created by the
    piecewise candidate formula and applies the usual continuity correction.
    """
    positive = [score for label, score in zip(labels, scores) if label]
    negative = [score for label, score in zip(labels, scores) if not label]
    if not positive or not negative:
        return {"u": None, "pApprox": None, "cliffsDelta": None, "tieCorrected": True}
    combined = positive + negative
    rank_values = ranks(combined)
    n1, n0, total = len(positive), len(negative), len(combined)
    u1 = sum(rank_values[:n1]) - n1 * (n1 + 1) / 2.0
    mean_u = n1 * n0 / 2.0
    tie_counts: dict[float, int] = defaultdict(int)
    for value in combined:
        tie_counts[value] += 1
    tie_term = sum(count ** 3 - count for count in tie_counts.values())
    correction = tie_term / (total * (total - 1)) if total > 1 else 0.0
    variance = n1 * n0 * ((total + 1) - correction) / 12.0
    sigma = math.sqrt(max(0.0, variance))
    distance = abs(u1 - mean_u)
    z = max(0.0, distance - 0.5) / sigma if sigma else 0.0
    p = math.erfc(z / math.sqrt(2.0))
    delta = (2.0 * u1) / (n1 * n0) - 1.0
    return {"u": u1, "pApprox": p, "cliffsDelta": delta, "tieCorrected": True}


def bootstrap_metric(labels: Sequence[int], scores: Sequence[float], metric, iterations: int, seed: int) -> dict[str, float | int | None]:
    positive = [(l, s) for l, s in zip(labels, scores) if l]
    negative = [(l, s) for l, s in zip(labels, scores) if not l]
    estimate = metric(labels, scores)
    if not positive or not negative or iterations <= 0:
        return {"estimate": estimate, "lower95": None, "upper95": None, "iterations": 0}
    rng = random.Random(seed)
    values: list[float] = []
    for _ in range(iterations):
        sample = [rng.choice(positive) for _ in positive] + [rng.choice(negative) for _ in negative]
        rng.shuffle(sample)
        value = metric([x[0] for x in sample], [x[1] for x in sample])
        if value is not None and math.isfinite(value): values.append(float(value))
    values.sort()
    if not values:
        return {"estimate": estimate, "lower95": None, "upper95": None, "iterations": 0}
    lo = values[max(0, math.floor(0.025 * (len(values) - 1)))]
    hi = values[min(len(values) - 1, math.ceil(0.975 * (len(values) - 1)))]
    return {"estimate": estimate, "lower95": lo, "upper95": hi, "iterations": len(values)}


def matched_control_dates(event_dates: Sequence[date], eligible_dates: Sequence[date], exclusion_dates: set[date], per_event: int, seed: int) -> dict[date, list[date]]:
    rng = random.Random(seed)
    mapping: dict[date, list[date]] = {}
    for event in sorted(event_dates):
        candidates = [candidate for candidate in eligible_dates if candidate.month == event.month and candidate not in exclusion_dates and candidate != event]
        candidates.sort()
        if len(candidates) <= per_event:
            chosen = candidates
        else:
            chosen = sorted(rng.sample(candidates, per_event))
        mapping[event] = chosen
    return mapping


def svg_document(width: int, height: int, body: str, title: str, description: str) -> str:
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" role="img" aria-labelledby="title desc">
<title id="title">{title}</title><desc id="desc">{description}</desc>
<style>text{{font-family:Arial,sans-serif;fill:#222}}.axis{{stroke:#555;stroke-width:1}}.grid{{stroke:#ddd;stroke-width:1}}.series1{{fill:none;stroke:#1565c0;stroke-width:2.5}}.series2{{fill:none;stroke:#c62828;stroke-width:2.5}}.reference{{fill:none;stroke:#666;stroke-width:1.5;stroke-dasharray:7 5}}.bar1{{fill:#1565c0;opacity:.7}}.bar2{{fill:#c62828;opacity:.7}}.label{{font-size:12px}}.title{{font-size:18px;font-weight:bold}}</style>
<rect width="100%" height="100%" fill="white"/>
{body}</svg>'''


def line_chart_svg(
    series: Sequence[tuple[str, Sequence[tuple[float, float]]]],
    title: str,
    x_label: str,
    y_label: str,
    width: int = 900,
    height: int = 520,
    x_bounds: tuple[float, float] | None = None,
    y_bounds: tuple[float, float] | None = None,
    horizontal_references: Sequence[tuple[str, float]] = (),
) -> str:
    left, right, top, bottom = 75, 25, 65, 65
    plot_w, plot_h = width - left - right, height - top - bottom
    all_points = [point for _, points in series for point in points]
    if not all_points:
        all_points = [(0.0, 0.0), (1.0, 1.0)]
    min_x, max_x = x_bounds or (min(p[0] for p in all_points), max(p[0] for p in all_points))
    min_y, max_y = y_bounds or (min(p[1] for p in all_points), max(p[1] for p in all_points))
    if math.isclose(min_x, max_x):
        max_x = min_x + 1.0
    if math.isclose(min_y, max_y):
        max_y = min_y + 1.0
    def px(x: float) -> float: return left + (x - min_x) / (max_x - min_x) * plot_w
    def py(y: float) -> float: return top + plot_h - (y - min_y) / (max_y - min_y) * plot_h
    body = [f'<text x="{left}" y="28" class="title">{title}</text>']
    for i in range(6):
        x = left + i * plot_w / 5
        y = top + i * plot_h / 5
        body += [f'<line x1="{x:.1f}" y1="{top}" x2="{x:.1f}" y2="{top+plot_h}" class="grid"/>', f'<line x1="{left}" y1="{y:.1f}" x2="{left+plot_w}" y2="{y:.1f}" class="grid"/>']
        body += [f'<text x="{x:.1f}" y="{top+plot_h+22}" text-anchor="middle" class="label">{min_x+i*(max_x-min_x)/5:.2f}</text>', f'<text x="{left-10}" y="{y+4:.1f}" text-anchor="end" class="label">{max_y-i*(max_y-min_y)/5:.2f}</text>']
    body += [f'<line x1="{left}" y1="{top+plot_h}" x2="{left+plot_w}" y2="{top+plot_h}" class="axis"/>', f'<line x1="{left}" y1="{top}" x2="{left}" y2="{top+plot_h}" class="axis"/>']
    legend_x = left + plot_w - 230
    for idx, (name, points) in enumerate(series):
        cls = "series1" if idx % 2 == 0 else "series2"
        visible = [(x, y) for x, y in points if min_x <= x <= max_x and min_y <= y <= max_y]
        coords = " ".join(f"{px(x):.1f},{py(y):.1f}" for x, y in visible)
        if coords:
            body.append(f'<polyline points="{coords}" class="{cls}"/>')
        ly = top + 14 + idx * 20
        body.append(f'<line x1="{legend_x}" y1="{ly-4}" x2="{legend_x+28}" y2="{ly-4}" class="{cls}"/>')
        body.append(f'<text x="{legend_x+36}" y="{ly}" class="label">{name}</text>')
    for idx, (name, value) in enumerate(horizontal_references):
        if min_y <= value <= max_y:
            y = py(value)
            body.append(f'<line x1="{left}" y1="{y:.1f}" x2="{left+plot_w}" y2="{y:.1f}" class="reference"/>')
            body.append(f'<text x="{left+plot_w-4}" y="{y-5:.1f}" text-anchor="end" class="label">{name}</text>')
    body += [f'<text x="{left+plot_w/2}" y="{height-18}" text-anchor="middle" class="label">{x_label}</text>', f'<text x="18" y="{top+plot_h/2}" transform="rotate(-90 18 {top+plot_h/2})" text-anchor="middle" class="label">{y_label}</text>']
    return svg_document(width, height, "\n".join(body), title, f"Line chart of {y_label} against {x_label}.")


def histogram_svg(positive: Sequence[float], negative: Sequence[float], title: str, bins: int = 20, width: int = 900, height: int = 520) -> str:
    left, right, top, bottom = 75, 25, 55, 65
    plot_w, plot_h = width-left-right, height-top-bottom
    def counts(values: Sequence[float]) -> list[int]:
        result=[0]*bins
        for value in values: result[min(bins-1,max(0,int(clamp(value)*bins)))] += 1
        return result
    pos, neg = counts(positive), counts(negative)
    pos_total, neg_total = max(1,len(positive)), max(1,len(negative))
    pos_rate, neg_rate = [x/pos_total for x in pos], [x/neg_total for x in neg]
    max_y=max(pos_rate+neg_rate+[1e-9])
    bar_w=plot_w/bins
    body=[f'<text x="{left}" y="28" class="title">{title}</text>']
    for i in range(6):
        y=top+i*plot_h/5
        body += [f'<line x1="{left}" y1="{y:.1f}" x2="{left+plot_w}" y2="{y:.1f}" class="grid"/>', f'<text x="{left-10}" y="{y+4:.1f}" text-anchor="end" class="label">{max_y*(1-i/5):.2f}</text>']
    for i,(p,n) in enumerate(zip(pos_rate,neg_rate)):
        x=left+i*bar_w
        body += [f'<rect x="{x:.1f}" y="{top+plot_h-(n/max_y)*plot_h:.1f}" width="{bar_w*.9:.1f}" height="{(n/max_y)*plot_h:.1f}" class="bar1"/>', f'<rect x="{x+bar_w*.15:.1f}" y="{top+plot_h-(p/max_y)*plot_h:.1f}" width="{bar_w*.6:.1f}" height="{(p/max_y)*plot_h:.1f}" class="bar2"/>']
    for i in range(6):
        x = left + i * plot_w / 5
        body.append(f'<text x="{x:.1f}" y="{top+plot_h+22}" text-anchor="middle" class="label">{i/5:.1f}</text>')
    body += [f'<line x1="{left}" y1="{top+plot_h}" x2="{left+plot_w}" y2="{top+plot_h}" class="axis"/>', f'<text x="{left+plot_w/2}" y="{height-18}" text-anchor="middle" class="label">NP_score</text>', f'<text x="{left+10}" y="{top+18}" class="label">Blue: controls; red: event dates</text>']
    return svg_document(width,height,"\n".join(body),title,"Normalized histogram comparing event and control dates.")


def flatten_records(value: Any, inherited: dict[str, Any] | None = None) -> Iterable[dict[str, Any]]:
    inherited = dict(inherited or {})
    if isinstance(value, dict):
        context = dict(inherited)
        for key, item in value.items():
            if not isinstance(item, (dict, list)) and len(str(item)) < 1000:
                context[key] = item
        yield context
        for item in value.values():
            if isinstance(item, (dict, list)):
                yield from flatten_records(item, context)
    elif isinstance(value, list):
        for item in value:
            yield from flatten_records(item, inherited)


def discover_external_evidence(roots: Sequence[Path]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    inventory: list[dict[str, Any]] = []
    records: list[dict[str, Any]] = []
    scenario_keys = ("scenarioCode", "scenario_code", "scenario", "ScenarioCode")
    metric_keys = ("baseRisk", "BaseRisk", "adjustedScore", "AdjustedScore", "riskScore", "RiskScore", "confidenceFactor", "ConfidenceFactor", "integrityFactor", "IntegrityFactor", "latencyMs", "durationMs", "coverage", "missing", "missingCount")
    for root in roots:
        if not root.exists():
            inventory.append({"root": str(root), "status": "MISSING", "files": 0})
            continue
        files = [p for p in root.rglob("*") if p.is_file() and p.suffix.lower() in {".json", ".csv"}]
        inventory.append({"root": str(root), "status": "AVAILABLE", "files": len(files)})
        for path in files:
            try:
                if path.suffix.lower() == ".json": candidates = flatten_records(json.loads(path.read_text(encoding="utf-8-sig")))
                else: candidates = read_csv(path)
                for candidate in candidates:
                    scenario = next((candidate.get(key) for key in scenario_keys if candidate.get(key) not in (None, "")), None)
                    if scenario is None: continue
                    found = False
                    row = {"source_file": str(path), "scenario": str(scenario)}
                    for key in metric_keys:
                        value = as_float(candidate.get(key))
                        if value is not None:
                            row[key] = value; found = True
                    if found: records.append(row)
            except (OSError, ValueError, json.JSONDecodeError):
                continue
    return inventory, records


def summarize_scenarios(records: Sequence[dict[str, Any]]) -> list[dict[str, Any]]:
    buckets: dict[tuple[str,str], list[float]] = defaultdict(list)
    for row in records:
        for key, value in row.items():
            if key not in {"source_file", "scenario"} and isinstance(value, (int,float)):
                buckets[(str(row["scenario"]), key)].append(float(value))
    return [{"scenario": scenario, "metric": metric, "count": len(values), "minimum": min(values), "mean": statistics.fmean(values), "maximum": max(values)} for (scenario,metric),values in sorted(buckets.items())]
