#!/usr/bin/env python3
"""Compare two report-evidence baselines without mixing runs or volatile metadata."""

from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path
from typing import Any


def read_json(path: Path, default: Any = None) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return default


def latest_dir(root: Path, phase_dir: str) -> Path | None:
    phase = root / phase_dir
    latest = phase / "LATEST.txt"
    if latest.is_file():
        candidate = phase / Path(latest.read_text(encoding="utf-8").strip()).name
        if candidate.is_dir():
            return candidate
    runs = sorted((p for p in phase.iterdir() if p.is_dir()), key=lambda p: p.name) if phase.is_dir() else []
    return runs[-1] if runs else None


def get_path(data: Any, path: str) -> Any:
    current = data
    for part in path.split("."):
        if not isinstance(current, dict) or part not in current:
            return None
        current = current[part]
    return current


def put_selected(output: dict[str, Any], prefix: str, data: dict[str, Any], paths: list[str]) -> None:
    for path in paths:
        value = get_path(data, path)
        if value is not None:
            output[f"{prefix}.{path}"] = value


def collect_snapshot(root: Path) -> dict[str, Any]:
    result: dict[str, Any] = {}
    campaign_dir = latest_dir(root, "08-campaign")
    phase7_dir = latest_dir(root, "07-report-integration")
    phase9_dir = latest_dir(root, "09-np-score-validation")
    phase10_dir = latest_dir(root, "10-evidence-intelligence")
    campaign = read_json(campaign_dir / "campaign-summary.json", {}) if campaign_dir else {}
    phase7 = read_json(phase7_dir / "phase7-summary.json", {}) if phase7_dir else {}
    phase9 = read_json(phase9_dir / "phase9-summary.json", {}) if phase9_dir else {}
    phase10 = read_json(phase10_dir / "phase10-summary.json", {}) if phase10_dir else {}

    put_selected(result, "campaign", campaign, ["status", "profile", "mode"])
    for row in campaign.get("steps", []) if isinstance(campaign, dict) else []:
        if isinstance(row, dict) and row.get("step"):
            result[f"campaign.step.{row['step']}.selected"] = row.get("selected")
            result[f"campaign.step.{row['step']}.status"] = row.get("status")

    put_selected(result, "phase7", phase7, ["status"])
    for group in ("counts", "claimClassCounts", "promotionState", "sourceAvailability"):
        values = phase7.get(group, {}) if isinstance(phase7, dict) else {}
        if isinstance(values, dict):
            for key, value in values.items():
                result[f"phase7.{group}.{key}"] = value

    put_selected(result, "phase9", phase9, [
        "status",
        "formulaContractStatus",
        "headlineMetrics.dailyRows",
        "headlineMetrics.seasonalRows",
        "headlineMetrics.eventDates",
        "headlineMetrics.npScoreRocAuc",
        "headlineMetrics.npScoreRocAucLower95",
        "headlineMetrics.npScoreRocAucUpper95",
        "headlineMetrics.npScoreAveragePrecision",
        "headlineMetrics.warningSensitivity",
        "headlineMetrics.alarmSensitivity",
        "headlineMetrics.maximumNpScore",
        "headlineMetrics.extentSpearman",
        "headlineMetrics.scenarioMetricsImported",
        "bestBaseline.model",
        "bestBaseline.roc_auc",
        "bestBaseline.roc_auc_lower95",
        "bestBaseline.roc_auc_upper95",
        "bestBaseline.average_precision",
        "bestBaseline.average_precision_lower95",
        "bestBaseline.average_precision_upper95",
        "claimCeiling.formulaReproduced",
        "claimCeiling.retrospectiveDiscriminationMeasured",
        "claimCeiling.calibratedProbability",
        "claimCeiling.causalValidation",
        "claimCeiling.externalGeneralisation",
        "claimCeiling.runtimeScenarioComparison",
    ])

    put_selected(result, "phase10", phase10, ["status", "overallScore"])
    for group in ("componentScores", "counts"):
        values = phase10.get(group, {}) if isinstance(phase10, dict) else {}
        if isinstance(values, dict):
            for key, value in values.items():
                result[f"phase10.{group}.{key}"] = value

    claims_dir = phase7_dir / "claims" if phase7_dir else None
    claims = read_json(claims_dir / "claim-evidence-register.json", []) if claims_dir else []
    claim_signature = sorted(
        (str(row.get("claim_id", "")), str(row.get("evidence_class", "")))
        for row in claims if isinstance(row, dict)
    )
    result["claims.signature"] = json.dumps(claim_signature, ensure_ascii=False)
    return result


def numeric(value: Any) -> float | None:
    if isinstance(value, bool):
        return None
    if isinstance(value, (int, float)):
        return float(value)
    return None


def expected_direction(metric: str) -> str:
    lower = metric.lower()
    if any(token in lower for token in ("latency", "duration", "missing", "error", "backlog", "retry", "gap", "default")):
        return "LOWER_IS_BETTER"
    if any(token in lower for token in ("coverage", "throughput", "auc", "averageprecision", "average_precision", "sensitivity", "traceable", "score")):
        return "HIGHER_IS_BETTER"
    return "CONTEXT_DEPENDENT"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--left", type=Path, required=True)
    parser.add_argument("--right", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    left, right = args.left.resolve(), args.right.resolve()
    if not left.is_dir() or not right.is_dir():
        raise SystemExit("Both --left and --right must be baseline directories")
    output = args.output.resolve(); output.mkdir(parents=True, exist_ok=True)
    ldata, rdata = collect_snapshot(left), collect_snapshot(right)
    rows: list[dict[str, Any]] = []
    for key in sorted(set(ldata) | set(rdata)):
        lv, rv = ldata.get(key), rdata.get(key)
        ln, rn = numeric(lv), numeric(rv)
        delta = round(rn - ln, 8) if ln is not None and rn is not None else ""
        rows.append({
            "metric": key,
            "left": lv,
            "right": rv,
            "delta": delta,
            "changed": lv != rv,
            "expectedDirection": expected_direction(key),
        })
    changed_rows = [row for row in rows if row["changed"]]
    summary = {
        "leftBaseline": left.name,
        "rightBaseline": right.name,
        "metricsCompared": len(rows),
        "metricsChanged": len(changed_rows),
        "status": "NO_CHANGES" if not changed_rows else "CHANGES_FOUND",
        "ignoredVolatileFields": ["generatedAtUtc", "runId", "baselineId", "repositoryRoot", "commands", "output paths"],
        "interpretation": "Only decision-relevant fields are compared. Expected direction is advisory; the utility does not classify a delta as a regression without materiality and comparability rules.",
    }
    (output / "comparison.json").write_text(json.dumps({"summary": summary, "rows": rows}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    with (output / "comparison.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["metric", "left", "right", "delta", "changed", "expectedDirection"])
        writer.writeheader(); writer.writerows(rows)
    lines = [
        "# Comparação de campanhas de evidência", "",
        f"- Left: `{left.name}`", f"- Right: `{right.name}`",
        f"- Estado: **{summary['status']}**",
        f"- Métricas estáveis comparadas: **{len(rows)}**",
        f"- Métricas alteradas: **{len(changed_rows)}**", "",
        "Campos voláteis, caminhos locais e comandos foram excluídos da comparação.", "", "## Alterações", "",
    ]
    if changed_rows:
        lines += ["| Métrica | Left | Right | Delta | Direção esperada |", "|---|---:|---:|---:|---|"]
        for row in changed_rows:
            lines.append(f"| {row['metric']} | {row['left']} | {row['right']} | {row['delta']} | {row['expectedDirection']} |")
    else:
        lines.append("_Não foram encontradas alterações nos snapshots comparados._")
    lines += ["", "A classificação final como melhoria, regressão ou resultado inconclusivo exige o mesmo cenário, seed, população, duração, configuração e um limiar de materialidade definido.", ""]
    (output / "comparison.md").write_text("\n".join(lines), encoding="utf-8")
    print(f"EVIDENCE_COMPARISON_STATUS={summary['status']}")
    print(f"EVIDENCE_COMPARISON_OUTPUT={output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
