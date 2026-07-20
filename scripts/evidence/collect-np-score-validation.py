#!/usr/bin/env python3
"""Collect Phase 9 exploratory validation evidence for the NatureProtector NP_score."""
from __future__ import annotations

import argparse
import bisect
import json
import math
import shutil
import statistics
from collections import Counter, defaultdict
from datetime import timedelta
from pathlib import Path
from typing import Any, Sequence

from np_score_validation import (
    SCRIPT_VERSION,
    as_date,
    as_float,
    average_precision,
    bootstrap_metric,
    cell_territory,
    discover_external_evidence,
    histogram_svg,
    line_chart_svg,
    mann_whitney,
    matched_control_dates,
    parse_csharp_constants,
    pearson,
    precision_recall_curve,
    read_csv,
    roc_auc,
    roc_curve,
    score_day,
    sha256,
    spearman,
    summarize_scenarios,
    threshold_metrics,
    utc_iso,
    validate_formula_contract,
    write_csv,
    write_json,
)

MODEL_FIELDS = [
    "np_score_v1",
    "simple_weather_risk_score",
    "fire_index_reference_score",
    "simple_weather_trainfit_score",
    "fire_index_trainfit_score",
    "fwi_normalized",
    "kbdi_normalized",
    "meteorology_metric_only",
    "meteorology_with_fwi",
    "np_without_territory",
    "np_equal_weights",
]

PRIMARY_MODEL_SPECS = {
    "np_score_v1": {
        "role": "candidate_score",
        "fitWindow": "Candidate Parameter Set V1.0; no statistical fit in this collector.",
    },
    "simple_weather_risk_score": {
        "role": "retrospective_weather_baseline",
        "fitWindow": "Full-period retrospective percentile transforms from the source dataset.",
    },
    "simple_weather_trainfit_score": {
        "role": "temporally_fitted_weather_baseline",
        "fitWindow": "Empirical percentile transforms fitted through the configured fitEndYear and applied unchanged afterwards.",
        "formula": "0.35*temperature_max_percentile + 0.30*(100-relative_humidity_min)_percentile + 0.20*wind_max_percentile + 0.15*gust_max_percentile",
    },
    "fire_index_reference_score": {
        "role": "retrospective_fire_index_reference",
        "fitWindow": "Full-period retrospective FWI/KBDI percentile combination; not a simple weather model.",
    },
    "fire_index_trainfit_score": {
        "role": "temporally_fitted_fire_index_reference",
        "fitWindow": "FWI/KBDI percentile transforms fitted through the configured fitEndYear and applied unchanged afterwards.",
    },
}


def finite_or_none(value: Any) -> float | None:
    return float(value) if isinstance(value, (int, float)) and math.isfinite(float(value)) else None


def temporal_split(day: Any, start_year: int, fit_end_year: int, coverage_end_year: int) -> str:
    if day.year <= fit_end_year:
        return f"exploration_{start_year}_{fit_end_year}"
    if day.year <= coverage_end_year:
        return f"holdout_{fit_end_year + 1}_{coverage_end_year}"
    raise ValueError(f"Date {day} is outside the eligible event-label coverage")


def empirical_percentile(sorted_values: Sequence[float], value: float | None) -> float | None:
    if value is None or not sorted_values:
        return None
    left = bisect.bisect_left(sorted_values, value)
    right = bisect.bisect_right(sorted_values, value)
    return ((left + 1) + right) / (2.0 * len(sorted_values))


def fit_baseline_reference(weather_rows: list[dict[str, str]], end_year: int = 2022) -> dict[str, list[float]]:
    fields = {
        "temperature": ("temperature_max_c", False),
        "dryness": ("relative_humidity_min_pct", True),
        "wind": ("wind_speed_max_ms", False),
        "gust": ("wind_gust_max_ms", False),
        "fwi": ("fwi_reference", False),
        "kbdi": ("kbdi_reference", False),
    }
    fitted: dict[str, list[float]] = {}
    for name, (field, invert) in fields.items():
        values = []
        for row in weather_rows:
            day = as_date(row.get("date_local"))
            value = as_float(row.get(field))
            if day is None or day.year > end_year or value is None:
                continue
            values.append(100.0 - value if invert else value)
        fitted[name] = sorted(values)
    return fitted


def apply_trainfit_baselines(row: dict[str, str], fitted: dict[str, list[float]]) -> dict[str, float | None]:
    temperature = empirical_percentile(fitted["temperature"], as_float(row.get("temperature_max_c")))
    humidity = as_float(row.get("relative_humidity_min_pct"))
    dryness = empirical_percentile(fitted["dryness"], 100.0 - humidity if humidity is not None else None)
    wind = empirical_percentile(fitted["wind"], as_float(row.get("wind_speed_max_ms")))
    gust = empirical_percentile(fitted["gust"], as_float(row.get("wind_gust_max_ms")))
    simple = None if None in (temperature, dryness, wind, gust) else 0.35 * temperature + 0.30 * dryness + 0.20 * wind + 0.15 * gust
    fwi = empirical_percentile(fitted["fwi"], as_float(row.get("fwi_reference")))
    kbdi = empirical_percentile(fitted["kbdi"], as_float(row.get("kbdi_reference")))
    fire_index = None if fwi is None or kbdi is None else 0.70 * fwi + 0.30 * kbdi
    return {"simple_weather_trainfit_score": simple, "fire_index_trainfit_score": fire_index}


def metric_rows(
    rows: list[dict[str, Any]],
    population: str,
    iterations: int,
    seed: int,
    bootstrap_models: set[str] | None = None,
) -> list[dict[str, Any]]:
    output: list[dict[str, Any]] = []
    for index, model in enumerate(MODEL_FIELDS):
        pairs = [(int(row["event_label"]), finite_or_none(row.get(model))) for row in rows]
        pairs = [(label, score) for label, score in pairs if score is not None]
        labels = [label for label, _ in pairs]
        scores = [float(score) for _, score in pairs]
        positives, negatives = sum(labels), len(labels) - sum(labels)
        if positives < 2 or negatives < 2:
            output.append({
                "population": population, "model": model, "rows": len(labels),
                "positives": positives, "negatives": negatives, "roc_auc": None,
                "roc_auc_lower95": None, "roc_auc_upper95": None,
                "average_precision": None, "average_precision_lower95": None,
                "average_precision_upper95": None, "event_mean": None, "control_mean": None,
                "event_median": None, "control_median": None, "mann_whitney_u": None,
                "mann_whitney_p_approx": None, "cliffs_delta": None,
            })
            continue
        model_iterations = iterations if bootstrap_models is None or model in bootstrap_models else 0
        auc_ci = bootstrap_metric(labels, scores, roc_auc, model_iterations, seed + index * 17)
        ap_ci = bootstrap_metric(labels, scores, average_precision, model_iterations, seed + index * 17 + 1)
        event_scores = [score for label, score in zip(labels, scores) if label]
        control_scores = [score for label, score in zip(labels, scores) if not label]
        mw = mann_whitney(labels, scores)
        output.append({
            "population": population,
            "model": model,
            "rows": len(labels),
            "positives": positives,
            "negatives": negatives,
            "roc_auc": auc_ci["estimate"],
            "roc_auc_lower95": auc_ci["lower95"],
            "roc_auc_upper95": auc_ci["upper95"],
            "average_precision": ap_ci["estimate"],
            "average_precision_lower95": ap_ci["lower95"],
            "average_precision_upper95": ap_ci["upper95"],
            "event_mean": statistics.fmean(event_scores),
            "control_mean": statistics.fmean(control_scores),
            "event_median": statistics.median(event_scores),
            "control_median": statistics.median(control_scores),
            "mann_whitney_u": mw["u"],
            "mann_whitney_p_approx": mw["pApprox"],
            "cliffs_delta": mw["cliffsDelta"],
        })
    return output


def temporal_rows(rows: list[dict[str, Any]], coverage_end_year: int) -> list[dict[str, Any]]:
    output: list[dict[str, Any]] = []
    holdout_end = min(coverage_end_year, 2025)
    periods = {
        "exploration_2017_2022": set(range(2017, 2023)),
        f"holdout_2023_{holdout_end}": set(range(2023, holdout_end + 1)),
    }
    for year in sorted({row["date"].year for row in rows}):
        periods[f"year_{year}"] = {year}
    for period, years in periods.items():
        selected = [row for row in rows if row["date"].year in years]
        labels = [int(row["event_label"]) for row in selected]
        scores = [float(row["np_score_v1"]) for row in selected]
        output.append({
            "period": period,
            "start_year": min(years),
            "end_year": max(years),
            "rows": len(selected),
            "event_dates": sum(labels),
            "roc_auc": roc_auc(labels, scores),
            "average_precision": average_precision(labels, scores),
            "score_mean": statistics.fmean(scores) if scores else None,
            "score_max": max(scores) if scores else None,
        })
    return output


def event_source_rows(rows: list[dict[str, Any]], iterations: int, seed: int) -> list[dict[str, Any]]:
    definitions = {
        "any_eligible_event": lambda row: bool(row.get("event_label")),
        "municipality_intersection": lambda row: "municipality_intersection" in str(row.get("event_proximity_bases", "")).split(";"),
        "nearby_municipality_seed": lambda row: "nearby_municipality_seed" in str(row.get("event_proximity_bases", "")).split(";"),
        "icnf_burned_area_intersection": lambda row: "icnf_burned_area_intersection" in str(row.get("event_history_kinds", "")).split(";"),
        "large_fire_progression": lambda row: "large_fire_progression" in str(row.get("event_history_kinds", "")).split(";"),
    }
    output: list[dict[str, Any]] = []
    for index, (definition, predicate) in enumerate(definitions.items()):
        labels = [int(predicate(row)) for row in rows]
        scores = [float(row["np_score_v1"]) for row in rows]
        positives = sum(labels)
        auc_ci = bootstrap_metric(labels, scores, roc_auc, iterations, seed + index * 101) if positives >= 2 else {"estimate": None, "lower95": None, "upper95": None}
        ap_ci = bootstrap_metric(labels, scores, average_precision, iterations, seed + index * 101 + 1) if positives >= 2 else {"estimate": None, "lower95": None, "upper95": None}
        output.append({
            "event_definition": definition,
            "rows": len(rows),
            "positive_dates": positives,
            "prevalence": positives / len(rows) if rows else None,
            "roc_auc": auc_ci.get("estimate"),
            "roc_auc_lower95": auc_ci.get("lower95"),
            "roc_auc_upper95": auc_ci.get("upper95"),
            "average_precision": ap_ci.get("estimate"),
            "average_precision_lower95": ap_ci.get("lower95"),
            "average_precision_upper95": ap_ci.get("upper95"),
            "claim_boundary": "Target-specific retrospective association; non-target event dates remain in the comparison population.",
        })
    return output


def lag_analysis_rows(rows: list[dict[str, Any]], lags: Sequence[int], iterations: int, seed: int) -> list[dict[str, Any]]:
    by_date = {row["date"]: row for row in rows}
    output: list[dict[str, Any]] = []
    for lag in lags:
        pairs = []
        for target in rows:
            source = by_date.get(target["date"] - timedelta(days=lag))
            if source is not None:
                pairs.append((int(target["event_label"]), float(source["np_score_v1"])))
        labels = [label for label, _ in pairs]
        scores = [score for _, score in pairs]
        positives = sum(labels)
        auc_ci = bootstrap_metric(labels, scores, roc_auc, iterations, seed + lag * 211) if positives >= 2 else {"estimate": None, "lower95": None, "upper95": None}
        ap_ci = bootstrap_metric(labels, scores, average_precision, iterations, seed + lag * 211 + 1) if positives >= 2 else {"estimate": None, "lower95": None, "upper95": None}
        output.append({
            "lag_days": lag,
            "interpretation": "same_day_concurrent_association" if lag == 0 else f"score_available_{lag}_day_before_event_date",
            "rows": len(pairs),
            "event_dates": positives,
            "roc_auc": auc_ci.get("estimate"),
            "roc_auc_lower95": auc_ci.get("lower95"),
            "roc_auc_upper95": auc_ci.get("upper95"),
            "average_precision": ap_ci.get("estimate"),
            "average_precision_lower95": ap_ci.get("lower95"),
            "average_precision_upper95": ap_ci.get("upper95"),
        })
    return output


def correlation_rows(rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    fields = ["np_score_v1", "simple_weather_risk_score", "fire_index_reference_score", "fwi_normalized", "kbdi_normalized", "meteorology_metric_only", "meteorology_with_fwi"]
    output: list[dict[str, Any]] = []
    for i, left in enumerate(fields):
        for right in fields[i + 1:]:
            pairs = [(finite_or_none(row.get(left)), finite_or_none(row.get(right))) for row in rows]
            pairs = [(a, b) for a, b in pairs if a is not None and b is not None]
            x, y = [float(a) for a, _ in pairs], [float(b) for _, b in pairs]
            output.append({"left": left, "right": right, "rows": len(pairs), "pearson": pearson(x, y), "spearman": spearman(x, y)})
    return output


def sensitivity_rows(weather_rows: list[dict[str, str]], cells: list[dict[str, Any]], constants: dict[str, float], aggregation: dict[str, Any], labels_by_date: dict[Any, int], baseline_scores: dict[Any, float], delta: float) -> list[dict[str, Any]]:
    variants: list[tuple[str, dict[str, Any]]] = []
    baseline_weights = (constants["MeteorologyWeight"], constants["DroughtWeight"], constants["TerritoryWeight"])
    for index, name in enumerate(("meteorology", "drought", "territory")):
        for direction, factor in (("minus", 1.0 - delta), ("plus", 1.0 + delta)):
            weights = list(baseline_weights)
            weights[index] *= factor
            variants.append((f"{name}_{direction}_{int(delta*100)}pct", {"weights": tuple(weights)}))
    variants += [
        (f"fwi_blend_minus_{int(delta*100)}pct", {"fwi_blend": max(0.0, constants["FireWeatherIndexBlendWeight"] * (1.0 - delta))}),
        (f"fwi_blend_plus_{int(delta*100)}pct", {"fwi_blend": min(1.0, constants["FireWeatherIndexBlendWeight"] * (1.0 + delta))}),
        (f"fwi_reference_minus_{int(delta*100)}pct", {"fwi_reference": constants["FireWeatherIndexNormalizationReference"] * (1.0 - delta)}),
        (f"fwi_reference_plus_{int(delta*100)}pct", {"fwi_reference": constants["FireWeatherIndexNormalizationReference"] * (1.0 + delta)}),
    ]
    output: list[dict[str, Any]] = []
    baseline_dates = sorted(baseline_scores)
    baseline = [baseline_scores[d] for d in baseline_dates]
    baseline_classes = [score >= constants["WarningOpenThreshold"] for score in baseline]
    for name, parameters in variants:
        values: dict[Any, float] = {}
        for row in weather_rows:
            day = as_date(row.get("date_local"))
            if day not in baseline_scores:
                continue
            result = score_day(row, cells, constants, aggregation, **parameters)
            if result:
                values[day] = float(result["np_score_v1"])
        dates = [d for d in baseline_dates if d in values]
        scores = [values[d] for d in dates]
        labels = [labels_by_date[d] for d in dates]
        changed = sum((score >= constants["WarningOpenThreshold"]) != baseline_classes[baseline_dates.index(d)] for d, score in zip(dates, scores))
        output.append({
            "variant": name,
            "rows": len(scores),
            "roc_auc": roc_auc(labels, scores),
            "average_precision": average_precision(labels, scores),
            "mean_score": statistics.fmean(scores) if scores else None,
            "max_score": max(scores) if scores else None,
            "spearman_vs_baseline": spearman([baseline_scores[d] for d in dates], scores),
            "warning_class_changes": changed,
            "warning_class_change_fraction": changed / len(scores) if scores else None,
        })
    return output


def write_summary_markdown(path: Path, summary: dict[str, Any], best_baseline: dict[str, Any] | None) -> None:
    headline = summary["headlineMetrics"]
    lines = [
        "# Fase 9 — validação exploratória do NP_score",
        "",
        f"- Estado: **{summary['status']}**",
        f"- Gerado em UTC: `{summary['generatedAtUtc']}`",
        f"- Área: `{summary['areaCode']}`",
        f"- Período: `{summary['analysisPeriod']['startDate']}` a `{summary['analysisPeriod']['endDate']}`",
        f"- Classe de evidência: `{summary['claimCeiling']['evidenceClass']}`",
        "",
        "## Resultado principal",
        "",
        f"O NP_score foi reconstruído a partir da fórmula Candidate V1 e avaliado em {headline['seasonalRows']} dias sazonais, incluindo {headline['eventDates']} datas de evento elegíveis com contexto meteorológico disponível.",
        f"A ROC-AUC exploratória foi `{headline.get('npScoreRocAuc')}` e a average precision foi `{headline.get('npScoreAveragePrecision')}`. Estes resultados medem ordenação retrospectiva nesta amostra; não transformam o score numa probabilidade nem demonstram eficácia operacional.",
    ]
    if best_baseline:
        lines += [
            "",
            "## Comparação com baselines",
            "",
            f"O melhor baseline na população sazonal foi `{best_baseline['model']}` com ROC-AUC `{best_baseline['roc_auc']}` e average precision `{best_baseline['average_precision']}`. A comparação deve ser interpretada considerando redundância entre variáveis, dimensão reduzida da classe positiva e seleção dos eventos disponíveis.",
        ]
    lines += [
        "",
        "## Limites de afirmação",
        "",
        "- O resultado é exploratório e retrospectivo.",
        "- O NP_score permanece um índice relativo, não uma probabilidade calibrada.",
        "- FWI e KBDI não são referências totalmente independentes, porque entram na própria fórmula.",
        "- A ausência de um evento elegível não garante ausência de incêndio; os rótulos representam a cobertura das fontes configuradas.",
        "- A análise D0 usa informação meteorológica do próprio dia e mede associação concorrente, não previsão anterior à ignição.",
        "- Resultados runtime e comparações A/B/C só são promovidos quando foram importados de evidência atual verificável.",
        "",
        "## Artefactos principais",
        "",
        "- `daily-score-dataset.csv`: reconstrução diária e labels.",
        "- `model-comparison.csv`: discriminação, incerteza e efeito por modelo/população.",
        "- `threshold-analysis.csv`: trade-off operacional dos limiares.",
        "- `sensitivity-analysis.csv`: estabilidade perante alterações de parâmetros.",
        "- `temporal-validation.csv`: exploração, holdout e cortes anuais.",
        "- `scenario-comparison.csv`: métricas importadas das restantes fases, quando disponíveis.",
        "- `figures/`: representações SVG prontas para revisão e integração no relatório.",
        "",
    ]
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path.cwd())
    parser.add_argument("--config", type=Path, default=Path("config/evidence/np-score-validation.json"))
    parser.add_argument("--baseline-id", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--runtime-evidence-root", type=Path, action="append", default=[])
    parser.add_argument("--bootstrap-iterations", type=int)
    parser.add_argument("--overwrite", action="store_true")
    args = parser.parse_args()

    repo = args.repo.resolve()
    config_path = args.config if args.config.is_absolute() else repo / args.config
    output = args.output.resolve()
    if output.exists() and any(output.iterdir()):
        if not args.overwrite:
            raise SystemExit(f"Output directory is not empty: {output}; use --overwrite for this run-scoped directory.")
        shutil.rmtree(output)
    output.mkdir(parents=True, exist_ok=True)
    figures = output / "figures"
    figures.mkdir(parents=True, exist_ok=True)

    config = json.loads(config_path.read_text(encoding="utf-8"))
    source_paths = {name: repo / relative for name, relative in config["sources"].items()}
    missing_sources = [name for name, path in source_paths.items() if not path.is_file()]
    if missing_sources:
        raise SystemExit("Missing Phase 9 sources: " + ", ".join(missing_sources))

    source_constants = parse_csharp_constants(source_paths["parameterSet"])
    contract_checks = validate_formula_contract(config, source_constants)
    contract_ok = all(check["match"] for check in contract_checks)
    write_json(output / "formula-contract.json", {
        "generatedAtUtc": utc_iso(), "status": "PASS" if contract_ok else "FAIL",
        "checks": contract_checks,
        "sourceHashes": {name: sha256(path) for name, path in source_paths.items() if path.suffix.lower() in {".cs", ".json"}},
    })
    if not contract_ok:
        raise SystemExit("Configured NP_score formula contract does not match the C# source.")

    constants = {name: float(source_constants[name]) for name in config["formulaContract"]["constants"]}
    aggregation = config["formulaContract"]["areaAggregation"]
    weather = read_csv(source_paths["weatherDaily"])
    baseline_fit_end_year = int(config.get("baselineComparison", {}).get("fitEndYear", 2022))
    fitted_baselines = fit_baseline_reference(weather, baseline_fit_end_year)
    fires = read_csv(source_paths["fireHistory"])
    raw_cells = read_csv(source_paths["cellAttributes"])
    cells = [cell_territory(row) for row in raw_cells]
    write_csv(output / "territorial-components.csv", cells)

    period = config["analysisPeriod"]
    start, end = as_date(period["startDate"]), as_date(period["endDate"])
    months = set(int(month) for month in period["fireSeasonMonths"])
    event_config = config["eventDefinition"]
    event_coverage_start = as_date(event_config.get("coverageStartDate") or period["startDate"])
    event_coverage_end = as_date(event_config.get("coverageEndDate") or period["endDate"])
    if event_coverage_start is None or event_coverage_end is None or event_coverage_start > event_coverage_end:
        raise SystemExit("Invalid event source coverage window in Phase 9 configuration.")
    eligible_kinds = set(event_config["eligibleHistoryKinds"])
    events_by_date: dict[Any, list[dict[str, str]]] = defaultdict(list)
    for fire in fires:
        day = as_date(fire.get(event_config["dateField"]))
        if day and event_coverage_start <= day <= event_coverage_end and fire.get("history_kind") in eligible_kinds:
            events_by_date[day].append(fire)

    daily: list[dict[str, Any]] = []
    weather_dates: set[Any] = set()
    duplicate_weather_dates: list[str] = []
    missing_critical = Counter()
    for row in weather:
        day = as_date(row.get("date_local"))
        if day is None or not (start <= day <= end):
            continue
        if day in weather_dates: duplicate_weather_dates.append(day.isoformat())
        weather_dates.add(day)
        for field in ("noon_temperature_c", "noon_relative_humidity_pct", "noon_wind_speed_ms", "fwi_reference", "kbdi_reference"):
            if as_float(row.get(field)) is None: missing_critical[field] += 1
        result = score_day(row, cells, constants, aggregation)
        if result is None:
            continue
        label_eligible = event_coverage_start <= day <= event_coverage_end
        event_records = events_by_date.get(day, []) if label_eligible else []
        extent_values = [as_float(item.get("extent_ha")) for item in event_records]
        extent_values = [value for value in extent_values if value is not None]
        record = {
            "date": day,
            "date_local": day.isoformat(),
            "year": day.year,
            "month": day.month,
            "in_fire_season": int(day.month in months),
            "event_label_eligible": int(label_eligible),
            "event_label": int(bool(event_records)) if label_eligible else None,
            "event_label_status": "ELIGIBLE_SOURCE_COVERAGE" if label_eligible else "OUTSIDE_EVENT_SOURCE_COVERAGE",
            "incident_count": len(event_records),
            "event_history_kinds": ";".join(sorted({item.get("history_kind", "") for item in event_records if item.get("history_kind")})),
            "event_proximity_bases": ";".join(sorted({item.get("proximity_basis", "") for item in event_records if item.get("proximity_basis")})),
            "event_extent_ha_sum": sum(extent_values) if extent_values else None,
            "event_extent_ha_max": max(extent_values) if extent_values else None,
            "source_datasets": ";".join(sorted({item.get("source_dataset", "") for item in event_records if item.get("source_dataset")})),
            "temperature_c": as_float(row.get("noon_temperature_c")),
            "humidity_pct": as_float(row.get("noon_relative_humidity_pct")),
            "wind_ms": as_float(row.get("noon_wind_speed_ms")),
            "fwi_reference": as_float(row.get("fwi_reference")),
            "kbdi_reference": as_float(row.get("kbdi_reference")),
            **apply_trainfit_baselines(row, fitted_baselines),
            **result,
        }
        daily.append(record)

    seasonal_all = [row for row in daily if row["in_fire_season"]]
    seasonal = [row for row in seasonal_all if row["event_label_eligible"]]
    unlabeled_seasonal = [row for row in seasonal_all if not row["event_label_eligible"]]
    event_dates = sorted(row["date"] for row in seasonal if row["event_label"])
    exclusion_window = int(event_config["negativeExclusionWindowDays"])
    exclusion_dates = {event + timedelta(days=offset) for event in event_dates for offset in range(-exclusion_window, exclusion_window + 1)}
    controls = matched_control_dates(
        event_dates,
        [row["date"] for row in seasonal],
        exclusion_dates,
        int(config["matchedControls"]["controlsPerEvent"]),
        int(config["matchedControls"]["seed"]),
    )
    by_date = {row["date"]: row for row in seasonal}
    matched: list[dict[str, Any]] = []
    matched_rows: list[dict[str, Any]] = []
    for event in event_dates:
        if event in by_date:
            matched_rows.append(by_date[event])
        for control in controls.get(event, []):
            if control in by_date:
                matched_rows.append(dict(by_date[control], matched_event_date=event.isoformat()))
                matched.append({"event_date": event.isoformat(), "control_date": control.isoformat(), "calendar_month": event.month})
    write_csv(output / "matched-controls.csv", matched)

    serializable_daily = [{key: (value.isoformat() if hasattr(value, "isoformat") else value) for key, value in row.items() if key != "date"} for row in daily]
    write_csv(output / "daily-score-dataset.csv", serializable_daily)

    iterations = args.bootstrap_iterations if args.bootstrap_iterations is not None else int(config["statistics"]["bootstrapIterations"])
    seed = int(config["statistics"]["bootstrapSeed"])
    holdout_rows = [row for row in seasonal if baseline_fit_end_year < row["date"].year <= event_coverage_end.year]
    for population_name, population_rows in (("seasonal", seasonal), ("holdout", holdout_rows)):
        population_labels = [int(row["event_label"]) for row in population_rows]
        positives = sum(population_labels)
        negatives = len(population_labels) - positives
        if positives == 0 or negatives == 0:
            raise SystemExit(
                f"Phase 9 {population_name} population must contain positive and negative labels; "
                f"observed rows={len(population_rows)}, positives={positives}, negatives={negatives}."
            )

    prediction_models = list(PRIMARY_MODEL_SPECS)
    predictions = [
        {
            "date": row["date"].isoformat(),
            "label": int(row["event_label"]),
            "split": temporal_split(row["date"], start.year, baseline_fit_end_year, event_coverage_end.year),
            "source_datasets": row.get("source_datasets", ""),
            **{model: finite_or_none(row.get(model)) for model in prediction_models},
        }
        for row in seasonal
    ]
    write_csv(
        output / "predictions.csv",
        predictions,
        ["date", "label", "split", "source_datasets", *prediction_models],
    )
    write_json(
        output / "reference-model-spec.json",
        {
            "schemaVersion": 1,
            "fitStartDate": config["baselineComparison"]["fitStartDate"],
            "fitEndYear": baseline_fit_end_year,
            "holdoutStartYear": int(config["baselineComparison"]["holdoutStartYear"]),
            "eventCoverageEndDate": event_coverage_end.isoformat(),
            "splitField": "split",
            "provenanceField": "source_datasets",
            "models": PRIMARY_MODEL_SPECS,
        },
    )
    bootstrap_models = set(config.get("statistics", {}).get("bootstrapModels", ["np_score_v1"]))
    comparisons = (
        metric_rows(seasonal, "seasonal_population", iterations, seed, bootstrap_models)
        + metric_rows(matched_rows, "matched_case_control", iterations, seed + 10000, {"np_score_v1"})
        + metric_rows(holdout_rows, f"holdout_{baseline_fit_end_year + 1}_{event_coverage_end.year}", iterations, seed + 40000, bootstrap_models)
    )
    write_csv(output / "model-comparison.csv", comparisons)
    bootstrap_summary = [
        row
        for row in comparisons
        if row.get("roc_auc_lower95") is not None or row.get("average_precision_lower95") is not None
    ]
    write_csv(output / "bootstrap-summary.csv", bootstrap_summary)
    write_json(
        output / "metrics.json",
        {
            "schemaVersion": 1,
            "populationDefinition": "in_fire_season == 1 and event_label_eligible == 1",
            "resamplingUnit": "calendar date",
            "bootstrapIterations": iterations,
            "bootstrapSeed": seed,
            "seasonalPopulation": {
                "rows": len(seasonal),
                "positives": sum(int(row["event_label"]) for row in seasonal),
                "negatives": len(seasonal) - sum(int(row["event_label"]) for row in seasonal),
                "prevalence": sum(int(row["event_label"]) for row in seasonal) / len(seasonal),
            },
            "holdoutPopulation": {
                "rows": len(holdout_rows),
                "positives": sum(int(row["event_label"]) for row in holdout_rows),
                "negatives": len(holdout_rows) - sum(int(row["event_label"]) for row in holdout_rows),
                "prevalence": sum(int(row["event_label"]) for row in holdout_rows) / len(holdout_rows),
            },
            "modelMetrics": comparisons,
        },
    )

    labels = [int(row["event_label"]) for row in seasonal]
    scores = [float(row["np_score_v1"]) for row in seasonal]
    thresholds = {round(index * float(config["statistics"]["thresholdStep"]), 10) for index in range(int(1 / float(config["statistics"]["thresholdStep"])) + 1)}
    thresholds.update(float(constants[name]) for name in ("WarningOpenThreshold", "WarningCloseThreshold", "AlarmOpenThreshold", "AlarmCloseThreshold"))
    threshold_rows = [threshold_metrics(labels, scores, threshold) for threshold in sorted(thresholds)]
    write_csv(output / "threshold-analysis.csv", threshold_rows)

    temporal = temporal_rows(seasonal, event_coverage_end.year)
    write_csv(output / "temporal-validation.csv", temporal)
    event_sources = event_source_rows(seasonal, iterations, seed + 20000)
    write_csv(output / "event-source-stratification.csv", event_sources)
    lag_analysis = lag_analysis_rows(seasonal, [0, 1, 2], iterations, seed + 30000)
    write_csv(output / "lag-analysis.csv", lag_analysis)
    correlations = correlation_rows(seasonal)
    write_csv(output / "component-correlations.csv", correlations)
    no_territory_row = next((row for row in comparisons if row["population"] == "seasonal_population" and row["model"] == "np_without_territory"), None)
    np_comparison_row = next((row for row in comparisons if row["population"] == "seasonal_population" and row["model"] == "np_score_v1"), None)
    territory_rank_correlation = spearman([float(row["np_score_v1"]) for row in seasonal], [float(row["np_without_territory"]) for row in seasonal])
    territory_diagnostic = {
        "staticTerritorialProfileAppliedToAllDates": True,
        "spearmanNpVsNoTerritory": territory_rank_correlation,
        "rocAucNp": np_comparison_row.get("roc_auc") if np_comparison_row else None,
        "rocAucNoTerritory": no_territory_row.get("roc_auc") if no_territory_row else None,
        "rocAucDelta": (np_comparison_row.get("roc_auc") - no_territory_row.get("roc_auc")) if np_comparison_row and no_territory_row and np_comparison_row.get("roc_auc") is not None and no_territory_row.get("roc_auc") is not None else None,
        "temporalAddedValueDemonstrated": False,
        "reason": "One static territorial profile is applied to every date; it changes level but cannot establish spatial or temporal territorial discrimination.",
        "requiredFutureEvidence": "Cell-level fire labels and cell-specific meteorology across multiple cells/areas.",
    }
    write_json(output / "territorial-added-value.json", territory_diagnostic)

    labels_by_date = {row["date"]: int(row["event_label"]) for row in seasonal}
    baseline_scores = {row["date"]: float(row["np_score_v1"]) for row in seasonal}
    sensitivity = sensitivity_rows(weather, cells, constants, aggregation, labels_by_date, baseline_scores, float(config["statistics"]["sensitivityWeightDelta"]))
    write_csv(output / "sensitivity-analysis.csv", sensitivity)

    extent_pairs = [(float(row["np_score_v1"]), float(row["event_extent_ha_max"])) for row in seasonal if row["event_label"] and row["event_extent_ha_max"] is not None]
    extent_analysis = {
        "eventDatesWithExtent": len(extent_pairs),
        "pearson": pearson([x for x, _ in extent_pairs], [y for _, y in extent_pairs]) if extent_pairs else None,
        "spearman": spearman([x for x, _ in extent_pairs], [y for _, y in extent_pairs]) if extent_pairs else None,
        "interpretationBoundary": "Association with final burned extent is exploratory and is not a severity prediction validation.",
    }
    write_json(output / "extent-association.json", extent_analysis)

    evidence_roots = [path.resolve() for path in args.runtime_evidence_root]
    imported_inventory, imported_records = discover_external_evidence(evidence_roots)
    scenario_summary = summarize_scenarios(imported_records)
    write_json(output / "existing-evidence-import.json", {"roots": imported_inventory, "recordCount": len(imported_records), "claimPromoted": bool(scenario_summary)})
    write_csv(output / "scenario-evidence-records.csv", imported_records)
    write_csv(output / "scenario-comparison.csv", scenario_summary)

    np_roc = roc_curve(labels, scores)
    np_pr = precision_recall_curve(labels, scores)
    baseline_field = "simple_weather_risk_score"
    baseline_pairs = [(int(row["event_label"]), finite_or_none(row.get(baseline_field))) for row in seasonal]
    baseline_pairs = [(label, score) for label, score in baseline_pairs if score is not None]
    baseline_roc = roc_curve([label for label, _ in baseline_pairs], [float(score) for _, score in baseline_pairs])
    baseline_pr = precision_recall_curve([label for label, _ in baseline_pairs], [float(score) for _, score in baseline_pairs])
    (figures / "np-score-distribution.svg").write_text(histogram_svg([float(row["np_score_v1"]) for row in seasonal if row["event_label"]], [float(row["np_score_v1"]) for row in seasonal if not row["event_label"]], "Distribuição do NP_score em eventos e controlos"), encoding="utf-8")
    prevalence = sum(labels) / len(labels) if labels else 0.0
    (figures / "roc-comparison.svg").write_text(line_chart_svg([("NP_score", [(p["fpr"], p["tpr"]) for p in np_roc]), ("Baseline meteorológico retrospetivo", [(p["fpr"], p["tpr"]) for p in baseline_roc])], "Curva ROC — população sazonal elegível", "Taxa de positivos em dias sem evento elegível", "Sensibilidade", x_bounds=(0.0, 1.0), y_bounds=(0.0, 1.0)), encoding="utf-8")
    (figures / "precision-recall-comparison.svg").write_text(line_chart_svg([("NP_score", [(p["recall"], p["precision"]) for p in np_pr]), ("Baseline meteorológico retrospetivo", [(p["recall"], p["precision"]) for p in baseline_pr])], "Curva Precision–Recall — população sazonal elegível", "Recall", "Precisão", x_bounds=(0.0, 1.0), y_bounds=(0.0, 1.0), horizontal_references=((f"Prevalência {prevalence:.3f}", prevalence),)), encoding="utf-8")
    (figures / "threshold-tradeoff.svg").write_text(line_chart_svg([("Sensibilidade", [(float(row["threshold"]), float(row["sensitivity"] or 0)) for row in threshold_rows]), ("Taxa em dias sem evento elegível", [(float(row["threshold"]), float(row["false_positive_rate"] or 0)) for row in threshold_rows])], "Trade-off dos limiares do NP_score", "Limiar", "Proporção", x_bounds=(0.0, 1.0), y_bounds=(0.0, 1.0)), encoding="utf-8")
    (figures / "sensitivity-stability.svg").write_text(line_chart_svg([("ROC-AUC", [(float(index), float(row["roc_auc"] or 0)) for index, row in enumerate(sensitivity)]), ("Correlação com baseline", [(float(index), float(row["spearman_vs_baseline"] or 0)) for index, row in enumerate(sensitivity)])], "Estabilidade das variantes de sensibilidade", "Índice da variante", "Métrica"), encoding="utf-8")

    source_inventory = []
    for name, path in source_paths.items():
        source_inventory.append({"name": name, "path": path.relative_to(repo).as_posix(), "bytes": path.stat().st_size, "sha256": sha256(path)})
    write_csv(output / "source-inventory.csv", source_inventory)

    data_quality = {
        "generatedAtUtc": utc_iso(),
        "weatherRowsSource": len(weather),
        "weatherRowsInPeriod": len(daily),
        "seasonalRows": len(seasonal),
        "seasonalRowsTotalWeather": len(seasonal_all),
        "seasonalRowsOutsideEventCoverage": len(unlabeled_seasonal),
        "eventSourceCoverage": {"startDate": event_coverage_start.isoformat(), "endDate": event_coverage_end.isoformat()},
        "duplicateWeatherDates": duplicate_weather_dates,
        "missingCriticalFields": dict(missing_critical),
        "fireHistoryRows": len(fires),
        "eligibleFireRecords": sum(len(items) for items in events_by_date.values()),
        "eligibleEventDates": len(events_by_date),
        "eventDatesWithWeather": sum(1 for day in events_by_date if day in weather_dates),
        "seasonalEventDatesAnalyzed": len(event_dates),
        "eventDatesWithoutWeather": sorted(day.isoformat() for day in events_by_date if day not in weather_dates),
        "cellRows": len(cells),
        "territorialLimitationCounts": dict(Counter(part for cell in cells for part in str(cell["limitations"]).split(";") if part)),
        "matchedControlRows": len(matched),
        "negativeDefinition": "No eligible recorded event start on a date inside the configured event-source coverage; dates outside coverage are unlabeled and excluded.",
        "weatherProvenance": {
            "referenceKinds": sorted({str(row.get("reference_kind", "")) for row in weather if row.get("reference_kind")}),
            "sourceDatasets": sorted({str(row.get("source_dataset", "")) for row in weather if row.get("source_dataset")}),
            "sourceModels": sorted({str(row.get("source_model", "")) for row in weather if row.get("source_model")}),
            "requestedModels": sorted({str(row.get("requested_model", "")) for row in weather if row.get("requested_model")}),
            "interpretation": "Model/reanalysis data at one reference point; not direct observations from an IPMA station and not cell-specific meteorology.",
        },
        "qualityAssessment": "SHARE_WITH_CAVEATS",
    }
    write_json(output / "data-quality.json", data_quality)

    territorial_limitations = data_quality["territorialLimitationCounts"]
    required_caveats = [
        "Rare-event sample and limited geographic scope.",
        "Negative dates mean no eligible recorded event start inside source coverage, not guaranteed absence of fire.",
        f"Dates after {event_coverage_end.isoformat()} are unlabeled and excluded from event metrics because the configured fire source coverage ends there.",
        "Most event labels are nearby-municipality progression seeds; they support regional association, not confirmed local ignition prediction.",
        "D0 uses same-day noon/reanalysis inputs and is a concurrent retrospective association; D-1/D-2 are the relevant preliminary early-warning checks.",
        "Weather is model/reanalysis data for one reference point, not observed IPMA-station data or cell-specific weather.",
        "The simple weather and fire-index baselines use full-period retrospective percentile transforms and are not leakage-free prospective baselines.",
        "The static territory profile cannot demonstrate territorial added value in a date-only validation.",
        "FWI and KBDI are internal components and therefore not independent gold standards.",
        "Burned extent depends on response, suppression and post-ignition conditions.",
        "This run does not tune or replace Candidate Parameter Set V1.0.",
    ]
    altitude_defaults = int(territorial_limitations.get("altitude_missing_candidate_default", 0))
    hazard_defaults = int(territorial_limitations.get("hazard_missing_candidate_default", 0))
    unmapped_hazard_defaults = int(territorial_limitations.get("hazard_unmapped_candidate_default", 0))
    fuel_defaults = int(territorial_limitations.get("fuel_missing_or_unmapped_candidate_default", 0))
    if altitude_defaults:
        required_caveats.append(
            f"Altitude was unavailable for {altitude_defaults} territorial cells and the documented candidate default was used."
        )
    if hazard_defaults or unmapped_hazard_defaults:
        required_caveats.append(
            f"Structural hazard used a candidate default in {hazard_defaults + unmapped_hazard_defaults} territorial cells."
        )
    if fuel_defaults:
        required_caveats.append(
            f"Fuel used a candidate default in {fuel_defaults} territorial cells."
        )

    np_result = next((row for row in comparisons if row["population"] == "seasonal_population" and row["model"] == "np_score_v1"), None)
    baselines = [row for row in comparisons if row["population"] == "seasonal_population" and row["model"] != "np_score_v1" and row["roc_auc"] is not None]
    best_baseline = max(baselines, key=lambda row: (row["roc_auc"], row["average_precision"] or 0)) if baselines else None
    threshold_lookup = {round(float(row["threshold"]), 8): row for row in threshold_rows}
    status = "PASS_EXPLORATORY_VALIDATION" if len(event_dates) >= int(event_config["minimumEventDates"]) else "PARTIAL_INSUFFICIENT_EVENTS"
    summary = {
        "generatedAtUtc": utc_iso(),
        "scriptVersion": SCRIPT_VERSION,
        "status": status,
        "baselineId": args.baseline_id,
        "runId": args.run_id,
        "areaCode": config["areaCode"],
        "analysisPeriod": period,
        "formulaContractStatus": "PASS",
        "constantsContractStatus": "PASS",
        "crossLanguageParityStatus": "NOT_EXECUTED",
        "headlineMetrics": {
            "dailyRows": len(daily),
            "seasonalRows": len(seasonal),
            "seasonalWeatherRowsOutsideEventCoverage": len(unlabeled_seasonal),
            "eventDates": len(event_dates),
            "eventCoverageEndDate": event_coverage_end.isoformat(),
            "npScoreRocAuc": np_result["roc_auc"] if np_result else None,
            "npScoreRocAucLower95": np_result["roc_auc_lower95"] if np_result else None,
            "npScoreRocAucUpper95": np_result["roc_auc_upper95"] if np_result else None,
            "npScoreAveragePrecision": np_result["average_precision"] if np_result else None,
            "warningSensitivity": threshold_lookup.get(round(constants["WarningOpenThreshold"], 8), {}).get("sensitivity"),
            "alarmSensitivity": threshold_lookup.get(round(constants["AlarmOpenThreshold"], 8), {}).get("sensitivity"),
            "maximumNpScore": max(scores) if scores else None,
            "extentSpearman": extent_analysis["spearman"],
            "scenarioMetricsImported": len(scenario_summary),
            "territorialCells": len(cells),
            "territorialCellsUsingAltitudeDefault": altitude_defaults,
            "territorialCellsUsingHazardDefault": hazard_defaults + unmapped_hazard_defaults,
            "territorialCellsUsingFuelDefault": fuel_defaults,
            "territorialTemporalAddedValueDemonstrated": territory_diagnostic["temporalAddedValueDemonstrated"],
            "sameDayAssociationRocAuc": next((row["roc_auc"] for row in lag_analysis if row["lag_days"] == 0), None),
            "oneDayLeadRocAuc": next((row["roc_auc"] for row in lag_analysis if row["lag_days"] == 1), None),
            "twoDayLeadRocAuc": next((row["roc_auc"] for row in lag_analysis if row["lag_days"] == 2), None),
        },
        "bestBaseline": best_baseline,
        "claimCeiling": {
            **config["claimBoundary"],
            "formulaReproduced": True,
            "retrospectiveDiscriminationMeasured": bool(np_result and np_result["roc_auc"] is not None),
            "calibratedProbability": False,
            "causalValidation": False,
            "externalGeneralisation": False,
            "runtimeScenarioComparison": bool(scenario_summary),
            "localIgnitionPrediction": False,
            "territorialAddedValueValidated": False,
            "prospectiveForecastValidated": False,
        },
        "requiredCaveats": required_caveats,
    }
    write_json(output / "phase9-summary.json", summary)
    write_summary_markdown(output / "phase9-summary.md", summary, best_baseline)

    provenance = {
        "generatedAtUtc": utc_iso(),
        "collector": "scripts/evidence/collect-np-score-validation.py",
        "collectorVersion": SCRIPT_VERSION,
        "config": config_path.relative_to(repo).as_posix(),
        "configSha256": sha256(config_path),
        "sourceInventory": source_inventory,
        "runtimeEvidenceRoots": [str(path) for path in evidence_roots],
        "reproducibility": {"bootstrapIterations": iterations, "bootstrapSeed": seed, "matchedControlSeed": config["matchedControls"]["seed"], "baselineFitEndYear": baseline_fit_end_year, "bootstrapModels": sorted(bootstrap_models)},
    }
    write_json(output / "provenance.json", provenance)

    files = sorted(path for path in output.rglob("*") if path.is_file() and path.name != "SHA256SUMS.txt")
    (output / "SHA256SUMS.txt").write_text("\n".join(f"{sha256(path)}  {path.relative_to(output).as_posix()}" for path in files) + "\n", encoding="utf-8")
    print(f"PHASE_9_STATUS={status}")
    print(f"PHASE_9_OUTPUT={output}")
    print(f"PHASE_9_EVENT_DATES={len(event_dates)}")
    print(f"PHASE_9_NP_SCORE_ROC_AUC={summary['headlineMetrics']['npScoreRocAuc']}")
    return 0 if status == "PASS_EXPLORATORY_VALIDATION" else 2


if __name__ == "__main__":
    raise SystemExit(main())
