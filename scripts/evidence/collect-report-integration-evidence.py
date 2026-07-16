#!/usr/bin/env python3
"""Build an honest report-integration package from the evidence phases that exist.

Missing phases are represented explicitly. No numerical result is invented and no
blocked component is promoted by static inference.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import shutil
import sys
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable

SCRIPT_VERSION = "1.3.0"
ALLOWED_EVIDENCE_CLASSES = {
    "CURRENT_EXECUTION",
    "CURRENT_STATIC_VERIFICATION",
    "CURRENT_ANALYTICAL_EVIDENCE",
    "HISTORICAL_EXECUTION",
    "IMPLEMENTED_NOT_EXECUTED",
    "BLOCKED_OR_PENDING",
    "NO_SOURCE_EVIDENCE",
}

_PYPLOT = None


def require_pyplot() -> Any:
    global _PYPLOT
    if _PYPLOT is None:
        try:
            import matplotlib

            matplotlib.use("Agg")
            import matplotlib.pyplot as pyplot
        except ModuleNotFoundError as exc:
            raise RuntimeError(
                "matplotlib is required only when generating report figures. "
                "Install the report/evidence plotting dependencies before running the collector end-to-end."
            ) from exc
        _PYPLOT = pyplot
    return _PYPLOT


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def run_id_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def load_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_csv(path: Path, rows: Iterable[dict[str, Any]], fieldnames: list[str] | None = None) -> None:
    materialized = list(rows)
    path.parent.mkdir(parents=True, exist_ok=True)
    if fieldnames is None:
        fieldnames = list(materialized[0]) if materialized else []
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(materialized)


def md_table(rows: list[dict[str, Any]], columns: list[tuple[str, str]]) -> str:
    if not rows:
        return "_Sem linhas; não existe fonte de evidência para esta tabela._\n"

    def esc(value: Any) -> str:
        return str(value if value is not None else "").replace("|", "\\|").replace("\n", " ")

    output = ["| " + " | ".join(label for _, label in columns) + " |", "|" + "|".join("---" for _ in columns) + "|"]
    output.extend("| " + " | ".join(esc(row.get(key, "")) for key, _ in columns) + " |" for row in rows)
    return "\n".join(output) + "\n"


def tex_escape(value: Any) -> str:
    replacements = {
        "\\": r"\textbackslash{}",
        "&": r"\&",
        "%": r"\%",
        "$": r"\$",
        "#": r"\#",
        "_": r"\_",
        "{": r"\{",
        "}": r"\}",
        "~": r"\textasciitilde{}",
        "^": r"\textasciicircum{}",
    }
    return "".join(replacements.get(char, char) for char in str(value if value is not None else ""))


def tex_table(rows: list[dict[str, Any]], columns: list[tuple[str, str]], caption: str, label: str) -> str:
    colspec = "@{}" + "".join(r">{\raggedright\arraybackslash}X" for _ in columns) + "@{}"
    lines = [
        r"\begin{table}[htbp]",
        r"\centering",
        r"\small",
        rf"\begin{{tabularx}}{{\textwidth}}{{{colspec}}}",
        r"\toprule",
        " & ".join(tex_escape(v) for _, v in columns) + r" \\",
        r"\midrule",
    ]
    if rows:
        lines.extend(" & ".join(tex_escape(row.get(key, "")) for key, _ in columns) + r" \\" for row in rows)
    else:
        lines.append(r"\multicolumn{" + str(len(columns)) + r"}{l}{Sem fonte de evidência disponível.} \\")
    lines.extend(
        [
            r"\bottomrule",
            r"\end{tabularx}",
            rf"\caption{{{tex_escape(caption)}}}",
            rf"\label{{{label}}}",
            r"\end{table}",
            "",
        ]
    )
    return "\n".join(lines)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def resolve_phase_dir(baseline_root: Path, phase_dir_name: str, required: bool = False) -> Path | None:
    phase_root = baseline_root / phase_dir_name
    latest = phase_root / "LATEST.txt"
    if latest.exists():
        raw = latest.read_text(encoding="utf-8").strip()
        candidate = Path(raw)
        if candidate.is_absolute():
            candidate = phase_root / candidate.name
        else:
            candidate = phase_root / raw
        try:
            resolved = candidate.resolve()
            if resolved.exists() and (resolved == phase_root.resolve() or phase_root.resolve() in resolved.parents):
                return resolved
        except OSError:
            pass
    directories = (
        sorted((item for item in phase_root.iterdir() if item.is_dir()), key=lambda item: item.name)
        if phase_root.exists()
        else []
    )
    if directories:
        return directories[-1].resolve()
    if required:
        raise FileNotFoundError(f"No evidence run directory found under {phase_root}")
    return None


def _status_is_pass(status: str) -> bool:
    normalized = status.upper().strip()
    return normalized in {
        "PASS",
        "PASSED",
        "CURRENT_EXECUTION_PASS",
        "PASS_COMPLETE",
        "PASS_WITH_LIMITATIONS",
    }


def component_state(summary: dict[str, Any] | None, component: str) -> dict[str, Any]:
    if summary is None:
        return {"available": False, "status": "NO_SOURCE", "passed": False, "blocked": False}

    if component in {"frontend", "backend"}:
        data = summary.get(component, {})
        status = str(data.get("status", "UNKNOWN")).upper()
        return {
            "available": True,
            "status": status,
            "passed": _status_is_pass(status),
            "blocked": status.startswith("BLOCKED"),
            "data": data,
        }

    if component == "runtime":
        status = str(summary.get("currentRuntimeExecutionStatus", summary.get("status", "UNKNOWN"))).upper()
        passed = bool(summary.get("claimCeiling", {}).get("currentIntegratedExecution")) and _status_is_pass(status)
        return {
            "available": True,
            "status": status,
            "passed": passed,
            "blocked": "BLOCKED" in status,
            "data": summary,
        }

    if component == "performance":
        status = str(summary.get("phaseStatus", summary.get("status", "UNKNOWN"))).upper()
        counts = summary.get("currentResultCounts", {})
        measured = False
        if isinstance(counts, dict):
            try:
                measured = sum(int(value or 0) for value in counts.values()) > 0
            except (TypeError, ValueError):
                measured = False
        claim_ceiling = summary.get("claimCeiling", {})
        measurements_authorized = claim_ceiling.get("currentPerformanceMeasurements")
        if measurements_authorized is None:
            measurements_authorized = summary.get("currentPerformanceMeasurements")
        passed = _status_is_pass(status) and measured and measurements_authorized is not False
        return {
            "available": True,
            "status": status,
            "passed": passed,
            "blocked": "BLOCKED" in status,
            "data": summary,
        }

    if component == "reliability":
        status = str(
            summary.get("currentReliabilityStatus", summary.get("phaseStatus", summary.get("status", "UNKNOWN")))
        ).upper()
        claim_ceiling = summary.get("claimCeiling", {})
        campaign_authorized = claim_ceiling.get("currentReliabilityCampaign")
        if campaign_authorized is None:
            campaign_authorized = summary.get("currentReliabilityCampaign")
        passed = _status_is_pass(status) and campaign_authorized is not False
        return {
            "available": True,
            "status": status,
            "passed": passed,
            "blocked": "BLOCKED" in status,
            "data": summary,
        }

    raise ValueError(component)


def save_chart(fig: Any, base: Path) -> list[str]:
    pyplot = require_pyplot()
    base.parent.mkdir(parents=True, exist_ok=True)
    png, svg = base.with_suffix(".png"), base.with_suffix(".svg")
    fig.tight_layout()
    fig.savefig(png, dpi=180, bbox_inches="tight")
    fig.savefig(svg, bbox_inches="tight")
    pyplot.close(fig)
    return [png.name, svg.name]


def make_bar(
    labels: list[str], values: list[float], title: str, ylabel: str, base: Path, percent: bool = False
) -> None:
    pyplot = require_pyplot()
    fig, ax = pyplot.subplots(figsize=(8.5, 4.8))
    bars = ax.bar(labels, values)
    ax.set_title(title)
    ax.set_ylabel(ylabel)
    ax.grid(axis="y", alpha=0.25)
    if percent:
        ax.set_ylim(0, 100)
    for bar, value in zip(bars, values):
        ax.text(
            bar.get_x() + bar.get_width() / 2,
            bar.get_height(),
            f"{value:g}{'%' if percent else ''}",
            ha="center",
            va="bottom",
            fontsize=9,
        )
    save_chart(fig, base)


def _required_non_negative_int(row: dict[str, str], *keys: str) -> int:
    for key in keys:
        raw = row.get(key)
        if raw not in (None, ""):
            value = int(float(raw))
            if value < 0:
                raise ValueError(f"negative value for {key}")
            return value
    raise ValueError(f"missing numeric field: {keys}")


def historical_rows(path: Path | None) -> list[dict[str, Any]]:
    if path is None or not path.is_file():
        return []

    source_hash = sha256(path)
    result: list[dict[str, Any]] = []
    for row_number, row in enumerate(load_csv(path), start=2):
        scenario_raw = row.get("scenarioCode") or row.get("scenarioId") or row.get("scenario") or row.get("scenario_id")
        scenario = str(scenario_raw or "").strip().lower()
        if scenario not in {"scenario_b", "scenario_c", "b", "c"}:
            continue
        canonical_scenario = "scenario_b" if scenario in {"scenario_b", "b"} else "scenario_c"
        run_id = str(
            row.get("runId") or row.get("run_id") or row.get("simulationRunId") or row.get("simulation_run_id") or ""
        ).strip()
        if not run_id:
            continue
        try:
            expected = _required_non_negative_int(row, "expectedEvents", "expected_events", "expected")
            inbox = _required_non_negative_int(row, "inboxEvents", "inbox_events", "inbox")
            assessments = _required_non_negative_int(row, "riskAssessments", "risk_assessments", "assessments")
            missing = _required_non_negative_int(row, "missingEvents", "missing_events", "missing")
            rejected = _required_non_negative_int(row, "rejected", "rejectedEvents", "rejected_events")
            quarantined = _required_non_negative_int(row, "quarantined", "quarantinedEvents", "quarantined_events")
            rate_raw = (
                row.get("observedEventRatePct") or row.get("observedRatePercent") or row.get("observed_rate_percent")
            )
            observed = (
                float(rate_raw) if rate_raw not in (None, "") else (100.0 * inbox / expected if expected else 0.0)
            )
            if not 0.0 <= observed <= 100.0:
                raise ValueError("observed rate outside [0, 100]")
        except (TypeError, ValueError):
            continue

        result.append(
            {
                "scenario": canonical_scenario,
                "run_id": run_id,
                "expected": expected,
                "inbox": inbox,
                "assessments": assessments,
                "missing": missing,
                "rejected": rejected,
                "quarantined": quarantined,
                "observed_rate_percent": observed,
                "source_path": path.as_posix(),
                "source_sha256": source_hash,
                "source_row": row_number,
                "evidence_class": "HISTORICAL_EXECUTION",
            }
        )
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=".")
    parser.add_argument("--baseline-id", required=True)
    parser.add_argument("--run-id", default=None)
    parser.add_argument("--output-root", default=None)
    args = parser.parse_args()

    repo = Path(args.repo).resolve()
    baseline_root = repo / "artifacts" / "report-evidence" / args.baseline_id
    inventory_path = baseline_root / "01-inventory" / "inventory-summary.json"
    if not inventory_path.is_file():
        raise FileNotFoundError(f"Required inventory summary missing: {inventory_path}")
    run_id = args.run_id or run_id_now()
    output = Path(args.output_root).resolve() if args.output_root else baseline_root / "07-report-integration" / run_id
    output.mkdir(parents=True, exist_ok=True)

    phase_dirs = {
        name: resolve_phase_dir(baseline_root, directory)
        for name, directory in {
            "phase2": "02-tests",
            "phase3": "03-database",
            "phase4": "04-runtime",
            "phase5": "05-performance",
            "phase6": "06-reliability",
            "phase9": "09-np-score-validation",
            "phase11": "11-evidence-gap-closure",
        }.items()
    }
    summary_files = {
        "phase2": "phase2-summary.json",
        "phase3": "phase3-summary.json",
        "phase4": "phase4-summary.json",
        "phase5": "phase5-summary.json",
        "phase6": "phase6-summary.json",
        "phase9": "phase9-summary.json",
        "phase11": "phase11-summary.json",
    }
    summaries: dict[str, dict[str, Any] | None] = {}
    missing_inputs: list[dict[str, str]] = []
    for phase, directory in phase_dirs.items():
        expected = summary_files[phase]
        path = directory / expected if directory else None
        if path and path.is_file():
            summaries[phase] = load_json(path)
        else:
            summaries[phase] = None
            missing_inputs.append({"phase": phase, "expected": expected, "reason": "PHASE_OUTPUT_NOT_AVAILABLE"})

    inventory = load_json(inventory_path)
    counts = inventory.get("counts", {})
    tests, database, runtime, performance, reliability, np_validation = (
        summaries[name] for name in ("phase2", "phase3", "phase4", "phase5", "phase6", "phase9")
    )
    frontend_state, backend_state = component_state(tests, "frontend"), component_state(tests, "backend")
    runtime_state, performance_state, reliability_state = (
        component_state(runtime, "runtime"),
        component_state(performance, "performance"),
        component_state(reliability, "reliability"),
    )

    tables_dir, figures_dir, claims_dir = output / "tables", output / "figures", output / "claims"
    tex_dir, report_dir, assets_dir = output / "latex", output / "report-ready", output / "report-assets"
    for directory in (tables_dir, figures_dir, claims_dir, tex_dir, report_dir, assets_dir):
        directory.mkdir(parents=True, exist_ok=True)

    datasets: dict[str, tuple[list[dict[str, Any]], list[tuple[str, str]], str, str]] = {}
    snapshot_rows = [
        {
            "metric": "Ficheiros do repositório",
            "value": counts.get("repository_files", 0),
            "evidence_class": "CURRENT_STATIC_VERIFICATION",
        },
        {
            "metric": "Linhas de texto reconhecidas",
            "value": counts.get("recognized_text_lines", 0),
            "evidence_class": "CURRENT_STATIC_VERIFICATION",
        },
        {
            "metric": "Projetos .NET",
            "value": counts.get("dotnet_projects", 0),
            "evidence_class": "CURRENT_STATIC_VERIFICATION",
        },
        {
            "metric": "Projetos de testes .NET",
            "value": counts.get("dotnet_test_projects", 0),
            "evidence_class": "CURRENT_STATIC_VERIFICATION",
        },
        {
            "metric": "Endpoints API declarados",
            "value": counts.get("api_endpoints", 0),
            "evidence_class": "CURRENT_STATIC_VERIFICATION",
        },
        {"metric": "Migrações", "value": counts.get("migrations", 0), "evidence_class": "CURRENT_STATIC_VERIFICATION"},
        {
            "metric": "Workflows GitHub Actions",
            "value": counts.get("workflows", 0),
            "evidence_class": "CURRENT_STATIC_VERIFICATION",
        },
    ]
    datasets["version-snapshot"] = (
        snapshot_rows,
        [("metric", "Métrica"), ("value", "Valor"), ("evidence_class", "Classe")],
        "Snapshot factual do repositório",
        "tab:np-version-snapshot",
    )

    test_rows: list[dict[str, Any]] = []
    if tests:
        for component, state in (("Frontend", frontend_state), ("Backend", backend_state)):
            data = state.get("data", {})
            test_data, coverage = data.get("tests", {}), data.get("coverage", {})
            test_count = test_data.get("test_count", test_data.get("test_result_count", 0))
            passed = test_data.get("passed", 0)
            failed = test_data.get("failed", 0) + test_data.get("errors", 0)
            test_rows.append(
                {
                    "area": component,
                    "metric": "Execução",
                    "value": state["status"],
                    "unit": "estado",
                    "status": state["status"],
                    "evidence_class": "CURRENT_EXECUTION" if state["passed"] else "BLOCKED_OR_PENDING",
                }
            )
            if test_count:
                test_rows.extend(
                    [
                        {
                            "area": component,
                            "metric": "Testes executados",
                            "value": test_count,
                            "unit": "testes",
                            "status": state["status"],
                            "evidence_class": "CURRENT_EXECUTION",
                        },
                        {
                            "area": component,
                            "metric": "Testes passados",
                            "value": passed,
                            "unit": "testes",
                            "status": state["status"],
                            "evidence_class": "CURRENT_EXECUTION",
                        },
                        {
                            "area": component,
                            "metric": "Falhas/erros",
                            "value": failed,
                            "unit": "testes",
                            "status": state["status"],
                            "evidence_class": "CURRENT_EXECUTION",
                        },
                    ]
                )
            for key, label in (
                ("lines", "Cobertura de linhas"),
                ("branches", "Cobertura de branches"),
                ("functions", "Cobertura de funções"),
                ("statements", "Cobertura de statements"),
            ):
                item = coverage.get(key, {})
                percent = item.get(
                    "percent", item.get("line_coverage_percent" if key == "lines" else "branch_coverage_percent")
                )
                if percent is not None:
                    test_rows.append(
                        {
                            "area": component,
                            "metric": label,
                            "value": percent,
                            "unit": "%",
                            "status": state["status"],
                            "evidence_class": "CURRENT_EXECUTION",
                        }
                    )
        datasets["test-quality"] = (
            test_rows,
            [
                ("area", "Área"),
                ("metric", "Métrica"),
                ("value", "Valor"),
                ("unit", "Unidade"),
                ("status", "Estado"),
                ("evidence_class", "Classe"),
            ],
            "Execução atual de testes e cobertura",
            "tab:np-test-quality",
        )

    schema_rows: list[dict[str, Any]] = []
    db_counts: dict[str, Any] = database.get("counts", {}) if database else {}
    if database and phase_dirs["phase3"]:
        tables_path = phase_dirs["phase3"] / "static" / "tables.csv"
        if tables_path.is_file():
            aggregate: dict[str, dict[str, int]] = defaultdict(
                lambda: {"tables": 0, "columns": 0, "foreign_keys": 0, "indexes": 0}
            )
            for row in load_csv(tables_path):
                item = aggregate[row["schema"]]
                item["tables"] += 1
                item["columns"] += int(row["column_count"])
                item["foreign_keys"] += int(row["foreign_key_count"])
                item["indexes"] += int(row["index_count_including_unique_constraints"])
            schema_rows = [
                {"schema": schema, **values, "evidence_class": "CURRENT_STATIC_VERIFICATION"}
                for schema, values in sorted(aggregate.items())
            ]
        db_rows = [
            {
                "metric": label,
                "value": db_counts.get(key, 0),
                "evidence_class": "CURRENT_STATIC_VERIFICATION"
                if key != "critical_queries"
                else "IMPLEMENTED_NOT_EXECUTED",
            }
            for key, label in (
                ("schemas", "Schemas"),
                ("tables", "Tabelas"),
                ("columns", "Colunas"),
                ("primary_keys", "Primary keys"),
                ("foreign_keys", "Foreign keys"),
                ("indexes", "Índices/uniques"),
                ("critical_queries", "Queries críticas preparadas"),
            )
        ]
        datasets["database-summary"] = (
            db_rows,
            [("metric", "Métrica"), ("value", "Valor"), ("evidence_class", "Classe")],
            "Resumo do modelo físico reconstruído",
            "tab:np-database-summary",
        )
        datasets["database-by-schema"] = (
            schema_rows,
            [
                ("schema", "Schema"),
                ("tables", "Tabelas"),
                ("columns", "Colunas"),
                ("foreign_keys", "FKs"),
                ("indexes", "Índices"),
                ("evidence_class", "Classe"),
            ],
            "Distribuição do modelo por schema",
            "tab:np-database-schema",
        )

    historical_path = phase_dirs["phase4"] / "historical" / "historical-runs.csv" if phase_dirs["phase4"] else None
    if not historical_path or not historical_path.is_file():
        phase11_historical = phase_dirs["phase11"] / "admitted" / "historical-runs.csv" if phase_dirs.get("phase11") else None
        if phase11_historical and phase11_historical.is_file():
            historical_path = phase11_historical
    bc_rows = historical_rows(historical_path)
    if bc_rows:
        datasets["historical-bc"] = (
            bc_rows,
            [
                ("scenario", "Cenário"),
                ("expected", "Esperados"),
                ("inbox", "Inbox"),
                ("assessments", "Assessments"),
                ("missing", "Ausentes"),
                ("observed_rate_percent", "Taxa observada (%)"),
                ("evidence_class", "Classe"),
            ],
            "Execuções históricas B/C identificadas",
            "tab:np-historical-bc",
        )
    else:
        missing_inputs.append(
            {
                "phase": "phase4-historical-bc",
                "expected": "historical/historical-runs.csv with rows",
                "reason": "NO_SOURCE_EVIDENCE",
            }
        )

    rel_counts = reliability.get("counts", {}) if reliability else {}
    if reliability:
        reliability_rows = [
            {
                "phase": phase,
                "executable": rel_counts.get(f"{phase.lower()}ExecutableCases", 0),
                "blocked": rel_counts.get(f"{phase.lower()}BlockedCases", 0),
                "evidence_class": "CURRENT_EXECUTION" if reliability_state["passed"] else "IMPLEMENTED_NOT_EXECUTED",
            }
            for phase in ("P0", "P1", "P2", "P3")
        ]
        datasets["reliability-contract"] = (
            reliability_rows,
            [("phase", "Fase"), ("executable", "Executáveis"), ("blocked", "Bloqueados"), ("evidence_class", "Classe")],
            "Casos de fiabilidade por fase",
            "tab:np-reliability",
        )

    np_metrics: dict[str, Any] = np_validation.get("headlineMetrics", {}) if np_validation else {}
    np_claim_ceiling: dict[str, Any] = np_validation.get("claimCeiling", {}) if np_validation else {}
    np_validation_passed = bool(
        np_validation
        and np_validation.get("status") == "PASS_EXPLORATORY_VALIDATION"
        and np_validation.get("formulaContractStatus") == "PASS"
    )
    if np_validation:
        np_summary_rows = [
            {"metric": "Dias reconstruídos", "value": np_metrics.get("dailyRows", 0), "unit": "dias"},
            {"metric": "Dias sazonais analisados", "value": np_metrics.get("seasonalRows", 0), "unit": "dias"},
            {"metric": "Datas de evento elegíveis", "value": np_metrics.get("eventDates", 0), "unit": "eventos"},
            {"metric": "Dias sazonais fora da cobertura dos rótulos", "value": np_metrics.get("seasonalWeatherRowsOutsideEventCoverage", 0), "unit": "dias"},
            {"metric": "Fim da cobertura das fontes de evento", "value": np_metrics.get("eventCoverageEndDate", ""), "unit": "data"},
            {"metric": "ROC-AUC do NP_score", "value": np_metrics.get("npScoreRocAuc", ""), "unit": "0–1"},
            {"metric": "Average Precision do NP_score", "value": np_metrics.get("npScoreAveragePrecision", ""), "unit": "0–1"},
            {"metric": "Sensibilidade no limiar de aviso", "value": np_metrics.get("warningSensitivity", ""), "unit": "0–1"},
            {"metric": "Sensibilidade no limiar de alarme", "value": np_metrics.get("alarmSensitivity", ""), "unit": "0–1"},
            {"metric": "NP_score máximo reconstruído", "value": np_metrics.get("maximumNpScore", ""), "unit": "0–1"},
            {"metric": "Correlação com extensão ardida", "value": np_metrics.get("extentSpearman", ""), "unit": "Spearman rho"},
            {"metric": "Métricas runtime importadas", "value": np_metrics.get("scenarioMetricsImported", 0), "unit": "registos"},
            {"metric": "Células territoriais", "value": np_metrics.get("territorialCells", 0), "unit": "células"},
            {"metric": "Células com altitude por defeito", "value": np_metrics.get("territorialCellsUsingAltitudeDefault", 0), "unit": "células"},
            {"metric": "Células com perigosidade por defeito", "value": np_metrics.get("territorialCellsUsingHazardDefault", 0), "unit": "células"},
            {"metric": "Células com combustível por defeito", "value": np_metrics.get("territorialCellsUsingFuelDefault", 0), "unit": "células"},
            {"metric": "Valor territorial acrescentado demonstrado", "value": np_metrics.get("territorialTemporalAddedValueDemonstrated", False), "unit": "booleano"},
            {"metric": "ROC-AUC D-1", "value": np_metrics.get("oneDayLeadRocAuc", ""), "unit": "0–1"},
            {"metric": "ROC-AUC D-2", "value": np_metrics.get("twoDayLeadRocAuc", ""), "unit": "0–1"},
        ]
        for row in np_summary_rows:
            row["evidence_class"] = "CURRENT_ANALYTICAL_EVIDENCE"
        datasets["np-score-validation-summary"] = (
            np_summary_rows,
            [("metric", "Métrica"), ("value", "Valor"), ("unit", "Unidade"), ("evidence_class", "Classe")],
            "Validação exploratória retrospetiva do NP_score",
            "tab:np-score-validation-summary",
        )

        model_path = phase_dirs["phase9"] / "model-comparison.csv" if phase_dirs["phase9"] else None
        if model_path and model_path.is_file():
            model_rows = []
            for row in load_csv(model_path):
                if row.get("population") != "seasonal_population":
                    continue
                model_rows.append(
                    {
                        "model": row.get("model", ""),
                        "rows": row.get("rows", ""),
                        "positives": row.get("positives", ""),
                        "roc_auc": row.get("roc_auc", ""),
                        "roc_auc_lower95": row.get("roc_auc_lower95", ""),
                        "roc_auc_upper95": row.get("roc_auc_upper95", ""),
                        "average_precision": row.get("average_precision", ""),
                        "cliffs_delta": row.get("cliffs_delta", ""),
                        "evidence_class": "CURRENT_ANALYTICAL_EVIDENCE",
                    }
                )
            if model_rows:
                datasets["np-score-model-comparison"] = (
                    model_rows,
                    [
                        ("model", "Modelo"),
                        ("rows", "Linhas"),
                        ("positives", "Eventos"),
                        ("roc_auc", "ROC-AUC"),
                        ("roc_auc_lower95", "IC95 inf."),
                        ("roc_auc_upper95", "IC95 sup."),
                        ("average_precision", "Average Precision"),
                        ("cliffs_delta", "Cliff delta"),
                        ("evidence_class", "Classe"),
                    ],
                    "Comparação do NP_score com baselines na população sazonal",
                    "tab:np-score-model-comparison",
                )

        threshold_path = phase_dirs["phase9"] / "threshold-analysis.csv" if phase_dirs["phase9"] else None
        if threshold_path and threshold_path.is_file():
            selected_thresholds = {"0.5", "0.6", "0.7", "0.8"}
            threshold_rows = []
            for row in load_csv(threshold_path):
                if str(round(float(row.get("threshold", 0)), 2)).rstrip("0").rstrip(".") not in selected_thresholds:
                    continue
                threshold_rows.append(
                    {
                        "threshold": row.get("threshold", ""),
                        "sensitivity": row.get("sensitivity", ""),
                        "specificity": row.get("specificity", ""),
                        "precision": row.get("precision", ""),
                        "non_event_alert_days_per_30": row.get("non_event_alert_days_per_30", row.get("false_alert_days_per_30", "")),
                        "evidence_class": "CURRENT_ANALYTICAL_EVIDENCE",
                    }
                )
            if threshold_rows:
                datasets["np-score-threshold-tradeoff"] = (
                    threshold_rows,
                    [
                        ("threshold", "Limiar"),
                        ("sensitivity", "Sensibilidade"),
                        ("specificity", "Especificidade"),
                        ("precision", "Precisão"),
                        ("non_event_alert_days_per_30", "Dias com alerta sem evento elegível/30"),
                        ("evidence_class", "Classe"),
                    ],
                    "Trade-off exploratório dos limiares do NP_score",
                    "tab:np-score-thresholds",
                )


        lag_path = phase_dirs["phase9"] / "lag-analysis.csv" if phase_dirs["phase9"] else None
        if lag_path and lag_path.is_file():
            lag_rows = [dict(row, evidence_class="CURRENT_ANALYTICAL_EVIDENCE") for row in load_csv(lag_path)]
            if lag_rows:
                datasets["np-score-lag-analysis"] = (
                    lag_rows,
                    [("lag_days", "Antecedência (dias)"), ("interpretation", "Interpretação"), ("event_dates", "Eventos"), ("roc_auc", "ROC-AUC"), ("average_precision", "Average Precision"), ("evidence_class", "Classe")],
                    "Associação concorrente e análise preliminar com D-1/D-2",
                    "tab:np-score-lag-analysis",
                )

        source_path = phase_dirs["phase9"] / "event-source-stratification.csv" if phase_dirs["phase9"] else None
        if source_path and source_path.is_file():
            source_rows = [dict(row, evidence_class="CURRENT_ANALYTICAL_EVIDENCE") for row in load_csv(source_path)]
            if source_rows:
                datasets["np-score-event-source-stratification"] = (
                    source_rows,
                    [("event_definition", "Definição do evento"), ("positive_dates", "Datas positivas"), ("prevalence", "Prevalência"), ("roc_auc", "ROC-AUC"), ("average_precision", "Average Precision"), ("evidence_class", "Classe")],
                    "Resultados por origem e proximidade do rótulo de evento",
                    "tab:np-score-event-source",
                )

    status_rows = [
        {
            "area": "Inventário factual",
            "result": "PASS",
            "evidence_class": "CURRENT_STATIC_VERIFICATION",
            "claim_ceiling": "Estrutura declarada no snapshot avaliado",
        },
        {
            "area": "Frontend: testes e cobertura",
            "result": frontend_state["status"],
            "evidence_class": "CURRENT_EXECUTION"
            if frontend_state["passed"]
            else ("BLOCKED_OR_PENDING" if frontend_state["available"] else "NO_SOURCE_EVIDENCE"),
            "claim_ceiling": "Resultados atuais apenas quando executados",
        },
        {
            "area": "Backend: testes e cobertura",
            "result": backend_state["status"],
            "evidence_class": "CURRENT_EXECUTION"
            if backend_state["passed"]
            else ("BLOCKED_OR_PENDING" if backend_state["available"] else "NO_SOURCE_EVIDENCE"),
            "claim_ceiling": "Resultados atuais apenas quando executados",
        },
        {
            "area": "Modelo PostgreSQL",
            "result": str(
                database.get("phase_status", database.get("phaseStatus", database.get("status", "UNKNOWN")))
                if database
                else "NO_SOURCE"
            ),
            "evidence_class": "CURRENT_STATIC_VERIFICATION" if database else "NO_SOURCE_EVIDENCE",
            "claim_ceiling": "Modelo reconstruído; métricas live exigem execução",
        },
        {
            "area": "Comparação B/C",
            "result": "PASS_HISTORICAL" if bc_rows else "NO_SOURCE",
            "evidence_class": "HISTORICAL_EXECUTION" if bc_rows else "NO_SOURCE_EVIDENCE",
            "claim_ceiling": "Apenas execuções identificadas com fonte e hash",
        },
        {
            "area": "Execução integrada atual",
            "result": runtime_state["status"],
            "evidence_class": "CURRENT_EXECUTION"
            if runtime_state["passed"]
            else ("BLOCKED_OR_PENDING" if runtime_state["available"] else "NO_SOURCE_EVIDENCE"),
            "claim_ceiling": "Cadeia atual apenas quando autorizada pelo summary da Fase 4",
        },
        {
            "area": "Performance atual",
            "result": performance_state["status"],
            "evidence_class": "CURRENT_EXECUTION"
            if performance_state["passed"]
            else ("IMPLEMENTED_NOT_EXECUTED" if performance_state["available"] else "NO_SOURCE_EVIDENCE"),
            "claim_ceiling": "Números apenas quando existem linhas atuais",
        },
        {
            "area": "Fiabilidade atual",
            "result": reliability_state["status"],
            "evidence_class": "CURRENT_EXECUTION"
            if reliability_state["passed"]
            else ("IMPLEMENTED_NOT_EXECUTED" if reliability_state["available"] else "NO_SOURCE_EVIDENCE"),
            "claim_ceiling": "Campanha atual apenas quando summary e auditoria autorizam",
        },
        {
            "area": "Validação exploratória do NP_score",
            "result": np_validation.get("status", "NO_SOURCE") if np_validation else "NO_SOURCE",
            "evidence_class": "CURRENT_ANALYTICAL_EVIDENCE" if np_validation_passed else "NO_SOURCE_EVIDENCE",
            "claim_ceiling": (
                "Discriminação retrospetiva e coerência; não probabilidade calibrada nem validação causal"
                if np_validation
                else "Sem fonte analítica da Fase 9"
            ),
        },
    ]
    datasets["evidence-status"] = (
        status_rows,
        [
            ("area", "Área"),
            ("result", "Resultado"),
            ("evidence_class", "Classe"),
            ("claim_ceiling", "Teto da afirmação"),
        ],
        "Estado e teto das fontes de evidência",
        "tab:np-evidence-status",
    )

    gaps = []
    if not backend_state["passed"]:
        gaps.append(
            {
                "priority": "P0",
                "gap": "Executar backend com resultados e cobertura atuais",
                "needed": "SDK .NET e ambiente compatíveis",
                "report_effect": "Permite claims atuais do backend",
            }
        )
    if not runtime_state["passed"]:
        gaps.append(
            {
                "priority": "P0",
                "gap": "Executar cadeia integrada atual",
                "needed": "API e dependências qualificadas",
                "report_effect": "Eleva a cadeia de implementada para executada",
            }
        )
    if not performance_state["passed"]:
        gaps.append(
            {
                "priority": "P1",
                "gap": "Executar workloads de performance",
                "needed": "Ambiente estável e perfis aprovados",
                "report_effect": "Produz números atuais sem inferência",
            }
        )
    if not reliability_state["passed"]:
        gaps.append(
            {
                "priority": "P1",
                "gap": "Executar campanha de fiabilidade e auditoria",
                "needed": "Ambiente não produtivo qualificado",
                "report_effect": "Demonstra os casos efetivamente executados",
            }
        )
    if not bc_rows:
        gaps.append(
            {
                "priority": "P1",
                "gap": "Fornecer artefacto fonte B/C",
                "needed": "CSV com run IDs e contagens verificáveis",
                "report_effect": "Evita fallback ou números sem fonte",
            }
        )
    if not np_validation_passed:
        gaps.append(
            {
                "priority": "P0",
                "gap": "Executar e verificar a validação exploratória do NP_score",
                "needed": "Fase 9 com contrato da fórmula e datasets disponíveis",
                "report_effect": "Permite apresentar discriminação, baselines, limiares e limitações sem inferência",
            }
        )
    elif not np_claim_ceiling.get("runtimeScenarioComparison", False):
        gaps.append(
            {
                "priority": "P1",
                "gap": "Importar métricas runtime comparáveis dos cenários A/B/C",
                "needed": "Outputs estruturados e verificados das Fases 4–6",
                "report_effect": "Completa a comparação entre validação histórica e comportamento operacional",
            }
        )
    datasets["remaining-gaps"] = (
        gaps,
        [("priority", "Prioridade"), ("gap", "Lacuna"), ("needed", "Necessário"), ("report_effect", "Efeito")],
        "Lacunas restantes",
        "tab:np-gaps",
    )
    datasets["missing-evidence-register"] = (
        missing_inputs,
        [("phase", "Fase/entrada"), ("expected", "Esperado"), ("reason", "Razão")],
        "Registo de fontes ausentes",
        "tab:np-missing-evidence",
    )

    for name, (rows, columns, caption, label) in datasets.items():
        write_csv(tables_dir / f"{name}.csv", rows, [key for key, _ in columns])
        (tables_dir / f"{name}.md").write_text(md_table(rows, columns), encoding="utf-8")
        (tex_dir / f"table-{name}.tex").write_text(tex_table(rows, columns, caption, label), encoding="utf-8")

    generated_figures: list[str] = []
    if frontend_state["passed"]:
        coverage = frontend_state["data"].get("coverage", {})
        labels = []
        values = []
        for key, label in (
            ("lines", "Linhas"),
            ("branches", "Branches"),
            ("functions", "Funções"),
            ("statements", "Statements"),
        ):
            value = coverage.get(key, {}).get("percent")
            if value is not None:
                labels.append(label)
                values.append(float(value))
        if values:
            make_bar(labels, values, "Cobertura frontend atual", "Percentagem", figures_dir / "frontend-coverage", True)
            generated_figures += ["frontend-coverage.svg", "frontend-coverage.png"]
    if schema_rows:
        make_bar(
            [row["schema"] for row in schema_rows],
            [float(row["tables"]) for row in schema_rows],
            "Tabelas por schema",
            "Tabelas",
            figures_dir / "database-tables-by-schema",
        )
        generated_figures += ["database-tables-by-schema.svg", "database-tables-by-schema.png"]
    if bc_rows:
        labels = [row["scenario"] for row in bc_rows]
        pyplot = require_pyplot()
        fig, ax = pyplot.subplots(figsize=(8.5, 4.8))
        x = range(len(labels))
        width = 0.35
        ax.bar([i - width / 2 for i in x], [row["inbox"] for row in bc_rows], width, label="Inbox")
        ax.bar([i + width / 2 for i in x], [row["assessments"] for row in bc_rows], width, label="Assessments")
        ax.set_xticks(list(x), labels)
        ax.set_title("Execuções históricas B/C identificadas")
        ax.set_ylabel("Contagem")
        ax.legend()
        ax.grid(axis="y", alpha=0.25)
        save_chart(fig, figures_dir / "historical-bc-comparison")
        generated_figures += ["historical-bc-comparison.svg", "historical-bc-comparison.png"]
    if reliability:
        rows = datasets["reliability-contract"][0]
        make_bar(
            [row["phase"] for row in rows],
            [float(row["executable"]) for row in rows],
            "Casos de fiabilidade definidos",
            "Casos",
            figures_dir / "reliability-case-coverage",
        )
        generated_figures += ["reliability-case-coverage.svg", "reliability-case-coverage.png"]
    if np_validation and phase_dirs["phase9"]:
        phase9_figures = phase_dirs["phase9"] / "figures"
        for name in (
            "np-score-distribution.svg",
            "roc-comparison.svg",
            "precision-recall-comparison.svg",
            "threshold-tradeoff.svg",
            "sensitivity-stability.svg",
        ):
            source = phase9_figures / name
            if source.is_file():
                shutil.copy2(source, figures_dir / name)
                generated_figures.append(name)

    source_paths = {"inventory": str(inventory_path.relative_to(repo))}
    if phase_dirs.get("phase11"):
        source_paths["phase11"] = str(phase_dirs["phase11"].relative_to(repo))
    for phase, directory in phase_dirs.items():
        if directory and (directory / summary_files[phase]).is_file():
            source_paths[phase] = str((directory / summary_files[phase]).relative_to(repo))
    if historical_path and historical_path.is_file() and bc_rows:
        source_paths["historical_bc"] = str(historical_path.relative_to(repo))

    claims = [
        {
            "claim_id": "RPT-INV-001",
            "claim": f"O snapshot contém {counts.get('dotnet_projects', 0)} projetos .NET e {counts.get('dotnet_test_projects', 0)} projetos de testes.",
            "evidence_class": "CURRENT_STATIC_VERIFICATION",
            "source": source_paths["inventory"],
            "allowed_wording": "contém / foram inventariados",
            "prohibited_wording": "foram todos executados",
        }
    ]
    if tests:
        for label, state in (("Frontend", frontend_state), ("Backend", backend_state)):
            source = source_paths["phase2"]
            data = state.get("data", {})
            test_data = data.get("tests", {})
            count = test_data.get("test_count", test_data.get("test_result_count", 0))
            if state["passed"]:
                claims.append(
                    {
                        "claim_id": f"RPT-TEST-{label[:1]}01",
                        "claim": f"A execução atual de {label.lower()} passou {test_data.get('passed', 0)} de {count} testes registados.",
                        "evidence_class": "CURRENT_EXECUTION",
                        "source": source,
                        "allowed_wording": "execução atual no âmbito recolhido",
                        "prohibited_wording": "toda a plataforma passou",
                    }
                )
            else:
                claims.append(
                    {
                        "claim_id": f"RPT-TEST-{label[:1]}02",
                        "claim": f"A execução atual de {label.lower()} ficou no estado {state['status']}.",
                        "evidence_class": "BLOCKED_OR_PENDING",
                        "source": source,
                        "allowed_wording": "bloqueada, parcial ou não executada",
                        "prohibited_wording": "passou",
                    }
                )
    if database:
        claims.append(
            {
                "claim_id": "RPT-DB-001",
                "claim": f"O modelo estático reconstruído contém {db_counts.get('schemas', 0)} schemas, {db_counts.get('tables', 0)} tabelas e {db_counts.get('columns', 0)} colunas.",
                "evidence_class": "CURRENT_STATIC_VERIFICATION",
                "source": source_paths["phase3"],
                "allowed_wording": "modelo reconstruído",
                "prohibited_wording": "base live contém atualmente",
            }
        )
    if bc_rows:
        claims.append(
            {
                "claim_id": "RPT-RUN-001",
                "claim": "A comparação B/C usa apenas as execuções históricas presentes no artefacto fonte identificado.",
                "evidence_class": "HISTORICAL_EXECUTION",
                "source": source_paths["historical_bc"],
                "allowed_wording": "nas execuções históricas identificadas",
                "prohibited_wording": "taxa universal ou causalidade",
            }
        )
    if runtime:
        claims.append(
            {
                "claim_id": "RPT-RUN-002",
                "claim": (
                    "A campanha contém execução integrada atual autorizada pela Fase 4."
                    if runtime_state["passed"]
                    else f"A execução integrada atual ficou no estado {runtime_state['status']}."
                ),
                "evidence_class": "CURRENT_EXECUTION" if runtime_state["passed"] else "BLOCKED_OR_PENDING",
                "source": source_paths["phase4"],
                "allowed_wording": "estado derivado do summary da Fase 4",
                "prohibited_wording": "cadeia provada por inferência",
            }
        )
    if performance:
        claims.append(
            {
                "claim_id": "RPT-PERF-001",
                "claim": (
                    "A campanha contém medições atuais de performance."
                    if performance_state["passed"]
                    else "A campanha não contém medições atuais de performance autorizadas."
                ),
                "evidence_class": "CURRENT_EXECUTION" if performance_state["passed"] else "IMPLEMENTED_NOT_EXECUTED",
                "source": source_paths["phase5"],
                "allowed_wording": "medições presentes no summary",
                "prohibited_wording": "números inferidos da implementação",
            }
        )
    if reliability:
        claims.append(
            {
                "claim_id": "RPT-REL-001",
                "claim": (
                    "A campanha de fiabilidade atual foi autorizada pelo summary da Fase 6."
                    if reliability_state["passed"]
                    else f"A fiabilidade atual ficou no estado {reliability_state['status']}."
                ),
                "evidence_class": "CURRENT_EXECUTION" if reliability_state["passed"] else "IMPLEMENTED_NOT_EXECUTED",
                "source": source_paths["phase6"],
                "allowed_wording": "estado derivado do summary",
                "prohibited_wording": "campanha passou sem execução",
            }
        )
    if np_validation:
        np_auc = np_metrics.get("npScoreRocAuc")
        np_ap = np_metrics.get("npScoreAveragePrecision")
        events = np_metrics.get("eventDates", 0)
        rows = np_metrics.get("seasonalRows", 0)
        claim_text = (
            f"A reconstrução retrospetiva do NP_score analisou {rows} dias sazonais e {events} datas de evento; "
            f"obteve ROC-AUC {float(np_auc):.3f} e Average Precision {float(np_ap):.3f}."
            if np_auc is not None and np_ap is not None
            else "A Fase 9 produziu uma validação exploratória retrospetiva do NP_score."
        )
        claims.append(
            {
                "claim_id": "RPT-NPS-001",
                "claim": claim_text,
                "evidence_class": "CURRENT_ANALYTICAL_EVIDENCE" if np_validation_passed else "BLOCKED_OR_PENDING",
                "source": source_paths["phase9"],
                "allowed_wording": "validação exploratória retrospetiva, discriminação observada e comparação com baselines",
                "prohibited_wording": "probabilidade calibrada, causalidade, eficácia operacional provada ou generalização externa",
            }
        )
    for claim in claims:
        if claim["evidence_class"] not in ALLOWED_EVIDENCE_CLASSES:
            raise ValueError(claim)
    write_csv(claims_dir / "claim-evidence-register.csv", claims)
    write_json(claims_dir / "claim-evidence-register.json", claims)
    (claims_dir / "claim-evidence-register.md").write_text(
        "# Registo claim–evidência\n\n"
        + md_table(
            claims,
            [
                ("claim_id", "ID"),
                ("claim", "Afirmação"),
                ("evidence_class", "Classe"),
                ("source", "Fonte"),
                ("allowed_wording", "Permitido"),
                ("prohibited_wording", "Proibido"),
            ],
        ),
        encoding="utf-8",
    )

    class_counts = Counter(claim["evidence_class"] for claim in claims)
    pyplot = require_pyplot()
    fig, ax = pyplot.subplots(figsize=(8.5, 4.8))
    labels = list(class_counts)
    values = [class_counts[label] for label in labels]
    bars = ax.barh(labels, values)
    ax.set_title("Afirmações por classe de evidência")
    ax.set_xlabel("Número de afirmações")
    ax.grid(axis="x", alpha=0.25)
    for bar, value in zip(bars, values):
        ax.text(value, bar.get_y() + bar.get_height() / 2, f" {value}", va="center", fontsize=9)
    save_chart(fig, figures_dir / "claims-by-evidence-class")
    generated_figures += ["claims-by-evidence-class.svg", "claims-by-evidence-class.png"]

    asset_manifest = []
    if phase_dirs["phase3"]:
        for ext in ("svg", "png"):
            src = phase_dirs["phase3"] / "diagrams" / f"erd-report-simplified.{ext}"
            if src.is_file():
                shutil.copy2(src, assets_dir / src.name)
        if (assets_dir / "erd-report-simplified.svg").is_file():
            asset_manifest.append(
                {
                    "asset_id": "FIG-DB-ERD",
                    "file": "report-assets/erd-report-simplified.svg",
                    "recommended_location": "Capítulo 4 ou Anexo E5",
                    "evidence_class": "CURRENT_STATIC_VERIFICATION",
                    "caption": "Modelo físico simplificado reconstruído a partir das fontes da Fase 3.",
                }
            )
    if "frontend-coverage.svg" in generated_figures:
        asset_manifest.append(
            {
                "asset_id": "FIG-TEST-COV",
                "file": "figures/frontend-coverage.svg",
                "recommended_location": "Capítulo 8.3 / Anexo H3",
                "evidence_class": "CURRENT_EXECUTION",
                "caption": "Cobertura frontend na execução atual e no âmbito configurado.",
            }
        )
    if "historical-bc-comparison.svg" in generated_figures:
        asset_manifest.append(
            {
                "asset_id": "FIG-BC",
                "file": "figures/historical-bc-comparison.svg",
                "recommended_location": "Anexo F11",
                "evidence_class": "HISTORICAL_EXECUTION",
                "caption": "Comparação das execuções históricas presentes no artefacto fonte.",
            }
        )
    phase9_asset_specs = {
        "np-score-distribution.svg": ("FIG-NPS-DIST", "Distribuição retrospetiva do NP_score em datas com evento e controlo."),
        "roc-comparison.svg": ("FIG-NPS-ROC", "Curvas ROC do NP_score e dos baselines avaliados."),
        "precision-recall-comparison.svg": ("FIG-NPS-PR", "Curvas Precision–Recall na população rara avaliada."),
        "threshold-tradeoff.svg": ("FIG-NPS-THR", "Trade-off exploratório entre limiar, deteção e falsos alertas."),
        "sensitivity-stability.svg": ("FIG-NPS-SENS", "Estabilidade do NP_score perante variações dos pesos candidatos."),
    }
    for figure_name, (asset_id, caption) in phase9_asset_specs.items():
        if figure_name in generated_figures:
            asset_manifest.append(
                {
                    "asset_id": asset_id,
                    "file": f"figures/{figure_name}",
                    "recommended_location": "Capítulo 6 — Validação do NP_score",
                    "evidence_class": "CURRENT_ANALYTICAL_EVIDENCE",
                    "caption": caption,
                }
            )
    write_csv(report_dir / "report-asset-manifest.csv", asset_manifest)
    write_json(report_dir / "report-asset-manifest.json", asset_manifest)

    integration_map = []
    if database:
        integration_map.append(
            {
                "location": "Capítulo 4 / Anexo E5",
                "action": "Atualizar o modelo físico a partir da Fase 3",
                "asset": "FIG-DB-ERD e tabelas database-*",
                "replace_or_extend": "estender sem confundir modelo estático com base live",
            }
        )
    if tests:
        integration_map.append(
            {
                "location": "Capítulo 8.3 / Anexo H3",
                "action": "Atualizar testes e cobertura segundo o estado de cada componente",
                "asset": "table-test-quality.tex",
                "replace_or_extend": "não generalizar frontend para backend",
            }
        )
    if bc_rows:
        integration_map.append(
            {
                "location": "Anexo F11",
                "action": "Ligar B/C ao artefacto histórico fonte",
                "asset": "table-historical-bc.tex e FIG-BC",
                "replace_or_extend": "manter classe histórica",
            }
        )
    if np_validation:
        integration_map.append(
            {
                "location": "Capítulo 6 — Validação e resultados",
                "action": "Integrar a validação exploratória do NP_score, baselines, limiares e análise de sensibilidade",
                "asset": "table-np-score-*.tex e FIG-NPS-*",
                "replace_or_extend": "estender; manter explícito que não é probabilidade calibrada nem validação causal",
            }
        )
    integration_map.append(
        {
            "location": "Capítulo 9 / Anexo L",
            "action": "Registar fontes ausentes e gates abertos",
            "asset": "table-missing-evidence-register.tex e table-remaining-gaps.tex",
            "replace_or_extend": "não preencher por inferência",
        }
    )
    write_csv(report_dir / "integration-map.csv", integration_map)
    write_json(report_dir / "integration-map.json", integration_map)
    (report_dir / "integration-map.md").write_text(
        "# Mapa de integração no relatório\n\n"
        + md_table(
            integration_map,
            [("location", "Local"), ("action", "Ação"), ("asset", "Artefacto"), ("replace_or_extend", "Tratamento")],
        ),
        encoding="utf-8",
    )

    paragraphs = [
        "# Síntese pronta para integração no relatório",
        "",
        "## Estado factual da versão",
        "",
        f"O snapshot contém {counts.get('repository_files', 0)} ficheiros reconhecidos, {counts.get('dotnet_projects', 0)} projetos .NET, {counts.get('dotnet_test_projects', 0)} projetos de testes, {counts.get('api_endpoints', 0)} endpoints declarados, {counts.get('migrations', 0)} migrations e {counts.get('workflows', 0)} workflows. Estas contagens são verificação estática.",
    ]
    if tests:
        paragraphs += [
            "",
            "## Testes e cobertura",
            "",
            f"Frontend: {frontend_state['status']}. Backend: {backend_state['status']}. Os resultados são descritos por componente e não são generalizados à plataforma completa.",
        ]
    else:
        paragraphs += [
            "",
            "## Testes e cobertura",
            "",
            "A Fase 2 não está presente nesta campanha; não são produzidas afirmações atuais sobre testes ou cobertura.",
        ]
    if database:
        paragraphs += [
            "",
            "## Persistência",
            "",
            f"O modelo estático reconstruído contém {db_counts.get('schemas', 0)} schemas, {db_counts.get('tables', 0)} tabelas e {db_counts.get('columns', 0)} colunas. Métricas live continuam separadas.",
        ]
    if bc_rows:
        paragraphs += [
            "",
            "## B/C",
            "",
            "A comparação B/C usa apenas as linhas do ficheiro histórico identificado; não existe fallback numérico.",
        ]
    else:
        paragraphs += [
            "",
            "## B/C",
            "",
            "Não existe artefacto fonte B/C utilizável nesta campanha, pelo que nenhuma contagem é apresentada.",
        ]
    if np_validation:
        paragraphs += [
            "",
            "## Validação exploratória do NP_score",
            "",
            (
                f"A Fase 9 reconstruiu {np_metrics.get('dailyRows', 0)} dias e avaliou "
                f"{np_metrics.get('seasonalRows', 0)} dias sazonais, incluindo "
                f"{np_metrics.get('eventDates', 0)} datas de evento elegíveis. "
                f"O NP_score obteve ROC-AUC {float(np_metrics.get('npScoreRocAuc', 0)):.3f} "
                f"e Average Precision {float(np_metrics.get('npScoreAveragePrecision', 0)):.3f}."
            ),
            (
                f"No limiar de aviso, a sensibilidade observada foi "
                f"{float(np_metrics.get('warningSensitivity', 0)):.3f}; no limiar de alarme foi "
                f"{float(np_metrics.get('alarmSensitivity', 0)):.3f}. O máximo reconstruído foi "
                f"{float(np_metrics.get('maximumNpScore', 0)):.3f}."
            ),
            (
                f"A reconstrução territorial incluiu {np_metrics.get('territorialCells', 0)} células; "
                f"{np_metrics.get('territorialCellsUsingAltitudeDefault', 0)} usaram o valor candidato por defeito para altitude e "
                f"{np_metrics.get('territorialCellsUsingHazardDefault', 0)} para perigosidade. Esta limitação condiciona a interpretação da componente territorial."
            ),
            "Estes resultados medem discriminação retrospetiva no âmbito avaliado. Não transformam o índice numa probabilidade calibrada, não demonstram causalidade e não autorizam generalização para outras regiões ou períodos.",
        ]
    else:
        paragraphs += [
            "",
            "## Validação exploratória do NP_score",
            "",
            "A Fase 9 não está presente nesta campanha; não são apresentadas métricas analíticas do NP_score.",
        ]
    paragraphs += [
        "",
        "## Limite global",
        "",
        "A integração promove resultados apenas quando o summary da fase e o artefacto fonte o autorizam. Fontes ausentes permanecem explicitamente ausentes.",
    ]
    (report_dir / "report-section-draft.md").write_text("\n".join(paragraphs) + "\n", encoding="utf-8")

    (tex_dir / "phase7-tables.tex").write_text(
        "\n".join(f"\\input{{table-{name}.tex}}" for name in datasets) + "\n", encoding="utf-8"
    )
    completeness = "PASS_COMPLETE_REPORT_PACKAGE" if not missing_inputs else "PASS_PARTIAL_REPORT_PACKAGE"
    summary = {
        "phase": 7,
        "collectorVersion": SCRIPT_VERSION,
        "generatedAtUtc": utc_now(),
        "baselineId": args.baseline_id,
        "runId": run_id,
        "status": completeness,
        "phaseInputs": source_paths,
        "sourceAvailability": {phase: bool(summary) for phase, summary in summaries.items()},
        "missingInputs": missing_inputs,
        "generatedTables": list(datasets),
        "generatedFigures": sorted(generated_figures),
        "generatedAssets": [item["file"] for item in asset_manifest],
        "counts": {
            "tables": len(datasets),
            "figures": len([name for name in generated_figures if name.endswith(".svg")]),
            "claims": len(claims),
            "reportAssets": len(asset_manifest),
            "integrationTargets": len(integration_map),
            "remainingGaps": len(gaps),
        },
        "claimClassCounts": dict(class_counts),
        "promotionState": {
            "currentFrontendResults": frontend_state["passed"],
            "currentBackendResults": backend_state["passed"],
            "currentLiveDatabaseMetrics": bool(database and str(database.get("live_status", "")).upper() == "PASS"),
            "currentIntegratedRuntime": runtime_state["passed"],
            "currentPerformanceMeasurements": performance_state["passed"],
            "currentReliabilityCampaign": reliability_state["passed"],
            "historicalBC": bool(bc_rows),
            "currentNpScoreExploratoryValidation": np_validation_passed,
            "currentNpScoreRuntimeScenarioComparison": bool(
                np_validation_passed and np_claim_ceiling.get("runtimeScenarioComparison", False)
            ),
        },
        "limitations": [
            "The package creates no new runtime evidence.",
            "Missing phases remain explicit and do not receive synthetic values.",
            "Historical B/C is included only when a source file contains identifiable rows.",
            "Every promoted current result is derived from its phase summary.",
            "NP_score results are exploratory and retrospective; they are not calibrated probabilities or causal validation.",
            "Runtime scenario comparison is promoted only when Phase 9 imports verified structured evidence from prior phases.",
        ],
    }
    write_json(output / "phase7-summary.json", summary)
    (output / "phase7-summary.md").write_text(
        "# NatureProtector — Phase 7 Report Integration\n\n"
        + md_table(
            [
                {"field": "Status", "value": summary["status"]},
                {"field": "Baseline", "value": args.baseline_id},
                {"field": "Run", "value": run_id},
                {"field": "Tabelas", "value": summary["counts"]["tables"]},
                {"field": "Figuras SVG", "value": summary["counts"]["figures"]},
                {"field": "Claims", "value": summary["counts"]["claims"]},
            ],
            [("field", "Campo"), ("value", "Valor")],
        )
        + "\n## Limitações\n\n"
        + "\n".join(f"- {item}" for item in summary["limitations"])
        + "\n",
        encoding="utf-8",
    )

    excluded = {"SHA256SUMS.txt", "artifact-manifest.csv", "artifact-manifest.json"}
    files = sorted(path for path in output.rglob("*") if path.is_file() and path.name not in excluded)
    manifest = [
        {"path": str(path.relative_to(output)), "bytes": path.stat().st_size, "sha256": sha256(path)} for path in files
    ]
    write_csv(output / "artifact-manifest.csv", manifest)
    write_json(output / "artifact-manifest.json", manifest)
    hash_files = sorted(path for path in output.rglob("*") if path.is_file() and path.name != "SHA256SUMS.txt")
    (output / "SHA256SUMS.txt").write_text(
        "".join(f"{sha256(path)}  {path.relative_to(output).as_posix()}\n" for path in hash_files), encoding="utf-8"
    )
    phase_root = baseline_root / "07-report-integration"
    phase_root.mkdir(parents=True, exist_ok=True)
    (phase_root / "LATEST.txt").write_text(run_id + "\n", encoding="utf-8")
    print(f"PHASE_7_STATUS={completeness}")
    print(f"BASELINE_ID={args.baseline_id}")
    print(f"RUN_ID={run_id}")
    print(f"OUTPUT={output}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"PHASE_7_STATUS=FAILED\nERROR={exc}", file=sys.stderr)
        raise
