#!/usr/bin/env python3
"""Close admissible evidence gaps and produce an executable completion plan.

Phase 11 is deliberately conservative. It can admit existing historical B/C
execution evidence after schema and reconciliation checks, inventory current
phase outputs, and produce a machine-readable runbook for missing current
evidence. A planned command never counts as collected evidence.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import shutil
import subprocess
import sys
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable

SCRIPT_VERSION = "1.1.0"
CLOSED_STATES = {"CLOSED_CURRENT", "CLOSED_STATIC", "CLOSED_ANALYTICAL", "CLOSED_HISTORICAL"}
PASS_VALUES = {
    "PASS", "PASSED", "PASS_COMPLETE_REPORT_PACKAGE", "PASS_PARTIAL_REPORT_PACKAGE",
    "PASS_EXPLORATORY_VALIDATION", "PASS_WITH_LIMITATIONS", "CURRENT_EXECUTION_PASS",
}
SECRET_PATTERN = re.compile(r"(?i)(password|secret|token|connectionstring|dsn)\s*[=:]\s*[^\s]+")


def utc_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_json(path: Path, default: Any = None) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return default


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
        for row in materialized:
            writer.writerow({key: normalize(row.get(key)) for key in fieldnames})


def normalize(value: Any) -> Any:
    if value is None:
        return ""
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (list, tuple, set)):
        return "; ".join(str(v) for v in value)
    if isinstance(value, dict):
        return json.dumps(value, ensure_ascii=False, sort_keys=True)
    return value


def md_table(rows: list[dict[str, Any]], columns: list[tuple[str, str]]) -> str:
    if not rows:
        return "_Sem registos._\n"
    def esc(value: Any) -> str:
        return str(value if value is not None else "").replace("|", "\\|").replace("\n", " ")
    lines = [
        "| " + " | ".join(label for _, label in columns) + " |",
        "|" + "|".join("---" for _ in columns) + "|",
    ]
    lines.extend("| " + " | ".join(esc(row.get(key, "")) for key, _ in columns) + " |" for row in rows)
    return "\n".join(lines) + "\n"


def resolve_latest(phase_root: Path) -> Path | None:
    latest = phase_root / "LATEST.txt"
    if latest.is_file():
        raw = latest.read_text(encoding="utf-8").strip()
        candidate = phase_root / Path(raw).name
        if candidate.is_dir():
            return candidate
    runs = sorted(p for p in phase_root.iterdir() if p.is_dir()) if phase_root.is_dir() else []
    return runs[-1] if runs else None


def find_summary(run_root: Path | None, candidates: list[str]) -> Path | None:
    if not run_root:
        return None
    for name in candidates:
        path = run_root / name
        if path.is_file():
            return path
    for name in candidates:
        matches = sorted(run_root.rglob(name))
        if matches:
            return matches[0]
    return None


def summary_status(summary: dict[str, Any] | None) -> str:
    if not isinstance(summary, dict):
        return "NO_SUMMARY"
    for key in (
        "status", "overall_status", "phaseStatus", "phase_status",
        "currentRuntimeExecutionStatus", "currentReliabilityStatus",
    ):
        value = summary.get(key)
        if value is not None:
            return str(value).upper()
    return "UNKNOWN"


def command_available(name: str) -> bool:
    if name == "python":
        return True
    aliases = {"pwsh": ("pwsh", "powershell")}
    return any(shutil.which(candidate) for candidate in aliases.get(name, (name,)))


def environment_snapshot() -> dict[str, Any]:
    tools = {}
    for tool in ("python", "dotnet", "node", "npm", "docker", "pwsh"):
        available = command_available(tool)
        tools[tool] = {"available": available}
        if available:
            command = [sys.executable, "--version"] if tool == "python" else [tool, "--version"]
            if tool == "pwsh" and not shutil.which("pwsh"):
                command = ["powershell", "-NoProfile", "-Command", "$PSVersionTable.PSVersion.ToString()"]
            try:
                result = subprocess.run(command, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=15, check=False)
                tools[tool]["version"] = (result.stdout or "").strip().splitlines()[0] if result.stdout else None
                tools[tool]["exitCode"] = result.returncode
            except Exception as exc:
                tools[tool]["version"] = None
                tools[tool]["error"] = str(exc)
    return {"generatedAtUtc": utc_iso(), "tools": tools}


def valid_uuid(value: Any) -> bool:
    try:
        uuid.UUID(str(value))
        return True
    except (ValueError, TypeError, AttributeError):
        return False


def parse_historical_sql_extract(path: Path) -> dict[str, Any] | None:
    if not path.is_file():
        return None
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    run = re.search(r"run\s*\|\s*([0-9a-f-]{36})\s*\|\s*(scenario_[bc])", text, re.I)
    inbox = re.search(r"inbox\s*\|\s*(\d+)", text, re.I)
    assessments = re.search(r"risk_assessments\s*\|\s*(\d+)\s*\|\s*(\d+)", text, re.I)
    rejected = re.search(r"rejected\s*\|\s*(\d+)", text, re.I)
    quarantined = re.search(r"quarantined\s*\|\s*(\d+)", text, re.I)
    if not all((run, inbox, assessments, rejected, quarantined)):
        return None
    return {
        "simulationRunId": run.group(1),
        "scenario": run.group(2).lower(),
        "inbox": int(inbox.group(1)),
        "assessments": int(assessments.group(1)),
        "sensors": int(assessments.group(2)),
        "rejected": int(rejected.group(1)),
        "quarantined": int(quarantined.group(1)),
        "source": path.as_posix(),
        "sha256": sha256(path),
    }


def admit_historical_bc(repo: Path, requirement: dict[str, Any], output: Path) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    source = repo / requirement.get("historicalSource", "")
    manifests = {key: repo / value for key, value in requirement.get("scenarioManifests", {}).items()}
    sql_extracts = {key: repo / value for key, value in requirement.get("sqlSummaryExtracts", {}).items()}
    issues: list[str] = []
    payload = read_json(source)
    if not source.is_file() or not isinstance(payload, dict):
        return [], {"status": "MISSING_SOURCE", "issues": [f"Missing or invalid source: {source}"]}
    runs = payload.get("runs")
    comparison = payload.get("comparison")
    if not isinstance(runs, dict) or not isinstance(comparison, dict):
        issues.append("Source must contain runs and comparison objects.")
        runs = {}
        comparison = {}
    rows: list[dict[str, Any]] = []
    for scenario in ("scenario_b", "scenario_c"):
        row = runs.get(scenario, {}) if isinstance(runs, dict) else {}
        manifest_path = manifests.get(scenario)
        manifest = read_json(manifest_path) if manifest_path else None
        if not isinstance(row, dict):
            issues.append(f"Missing run object: {scenario}")
            continue
        expected = int(row.get("expectedEvents", -1))
        inbox = int(row.get("inboxEvents", -1))
        assessments = int(row.get("riskAssessments", -1))
        missing = int(row.get("missingEvents", -1))
        rejected = int(row.get("rejected", -1))
        quarantined = int(row.get("quarantined", -1))
        if str(row.get("status", "")).upper() != "COMPLETED":
            issues.append(f"{scenario} is not Completed.")
        if min(expected, inbox, assessments, missing, rejected, quarantined) < 0:
            issues.append(f"{scenario} has absent or negative counters.")
        if expected != inbox + missing:
            issues.append(f"{scenario}: expectedEvents != inboxEvents + missingEvents.")
        if assessments > inbox:
            issues.append(f"{scenario}: riskAssessments exceeds inboxEvents.")
        if not valid_uuid(row.get("simulationRunId")):
            issues.append(f"{scenario} has an invalid SimulationRunId.")
        if not isinstance(manifest, dict):
            issues.append(f"Missing or invalid scenario manifest: {manifest_path}")
        else:
            if manifest.get("scenarioCode") != scenario:
                issues.append(f"Manifest scenario mismatch for {scenario}.")
            manifest_expected = int(manifest.get("sensorCount", 0)) * int(manifest.get("numberOfCycles", 0))
            if manifest_expected != expected:
                issues.append(f"{scenario}: manifest sensorCount*cycles does not equal expectedEvents.")
        sql_path = sql_extracts.get(scenario)
        sql_data = parse_historical_sql_extract(sql_path) if sql_path else None
        if not isinstance(sql_data, dict):
            issues.append(f"Missing or unparsable historical SQL summary: {sql_path}")
        else:
            expected_sql = {
                "simulationRunId": row.get("simulationRunId"), "scenario": scenario,
                "inbox": inbox, "assessments": assessments, "rejected": rejected, "quarantined": quarantined,
            }
            for key, expected_value in expected_sql.items():
                if sql_data.get(key) != expected_value:
                    issues.append(f"{scenario}: SQL extract mismatch for {key}.")
            if isinstance(manifest, dict) and sql_data.get("sensors") != int(manifest.get("sensorCount", 0)):
                issues.append(f"{scenario}: SQL sensor count does not match the manifest.")
        rows.append({
            "scenario": scenario,
            "simulationRunId": row.get("simulationRunId"),
            "status": row.get("status"),
            "expected": expected,
            "inbox": inbox,
            "assessments": assessments,
            "missing": missing,
            "rejected": rejected,
            "quarantined": quarantined,
            "degradationProfile": row.get("degradationProfile"),
            "generatedAtUtc": payload.get("generatedAtUtc"),
            "evidenceClass": "HISTORICAL_EXECUTION",
            "source": source.relative_to(repo).as_posix(),
            "sourceSha256": sha256(source),
            "manifest": manifest_path.relative_to(repo).as_posix() if manifest_path and manifest_path.is_file() else "",
            "manifestSha256": sha256(manifest_path) if manifest_path and manifest_path.is_file() else "",
            "sqlSummary": sql_path.relative_to(repo).as_posix() if sql_path and sql_path.is_file() else "",
            "sqlSummarySha256": sha256(sql_path) if sql_path and sql_path.is_file() else "",
        })
    if len({row.get("simulationRunId") for row in rows}) != len(rows):
        issues.append("Historical scenarios must have distinct SimulationRunId values.")
    b = next((row for row in rows if row["scenario"] == "scenario_b"), None)
    c = next((row for row in rows if row["scenario"] == "scenario_c"), None)
    if b and (b["missing"] != 0 or b["inbox"] != b["expected"]):
        issues.append("Scenario B is not a complete nominal reference.")
    if c and c["missing"] <= 0:
        issues.append("Scenario C does not demonstrate missing-event degradation.")
    if not bool(comparison.get("scenarioCShowsControlledDegradation")):
        issues.append("Comparison does not declare controlled degradation.")
    admitted = not issues and len(rows) == 2
    admitted_dir = output / "admitted"
    sources_dir = output / "sources"
    admitted_dir.mkdir(parents=True, exist_ok=True)
    sources_dir.mkdir(parents=True, exist_ok=True)
    if admitted:
        shutil.copy2(source, sources_dir / source.name)
        for path in manifests.values():
            shutil.copy2(path, sources_dir / path.name)
        for path in sql_extracts.values():
            shutil.copy2(path, sources_dir / path.name)
        write_json(admitted_dir / "historical-runs.json", rows)
        write_csv(admitted_dir / "historical-runs.csv", rows)
    audit = {
        "status": "ADMITTED_HISTORICAL" if admitted else "INVALID_SOURCE",
        "source": source.relative_to(repo).as_posix(),
        "sourceSha256": sha256(source),
        "scenarioRows": len(rows),
        "sqlExtractsReconciled": bool(sql_extracts) and all(parse_historical_sql_extract(path) for path in sql_extracts.values()),
        "issues": issues,
        "claimCeiling": "Historical summarized SQL execution evidence only; the original full run directories are absent, so this is not a fully reproducible current runtime package.",
    }
    write_json(output / "historical-admission-audit.json", audit)
    return rows if admitted else [], audit


def component_result(requirement: dict[str, Any], summary: dict[str, Any] | None) -> tuple[str, str]:
    component = requirement.get("component")
    if component and isinstance(summary, dict):
        data = summary.get(component)
        if isinstance(data, dict):
            status = str(data.get("status", "UNKNOWN")).upper()
            if status in PASS_VALUES:
                return "CLOSED_CURRENT", status
            if status.startswith("PARTIAL"):
                return "PARTIAL", status
            if "BLOCKED" in status:
                return "BLOCKED_ENVIRONMENT", status
            return "MISSING_SOURCE", status
    status = summary_status(summary)
    if status in PASS_VALUES:
        evidence_class = requirement.get("evidenceClassWhenClosed")
        if evidence_class == "CURRENT_STATIC_VERIFICATION":
            return "CLOSED_STATIC", status
        if evidence_class == "CURRENT_ANALYTICAL_EVIDENCE":
            return "CLOSED_ANALYTICAL", status
        return "CLOSED_CURRENT", status
    if "PARTIAL" in status:
        return "PARTIAL", status
    if "BLOCKED" in status:
        return "BLOCKED_ENVIRONMENT", status
    return "MISSING_SOURCE", status


def build_svg(path: Path, title: str, rows: list[tuple[str, float]], subtitle: str) -> None:
    width, height = 1050, max(360, 150 + 66 * len(rows))
    left, right, top = 330, 80, 125
    plot = width - left - right
    lines = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">',
        '<rect width="100%" height="100%" fill="white"/>',
        f'<text x="42" y="48" font-family="Arial,sans-serif" font-size="28" font-weight="700">{title}</text>',
        f'<text x="42" y="80" font-family="Arial,sans-serif" font-size="16" fill="#444">{subtitle}</text>',
    ]
    for i, (label, value) in enumerate(rows):
        y = top + i * 66
        bounded = max(0.0, min(100.0, float(value)))
        lines += [
            f'<text x="{left-16}" y="{y+23}" text-anchor="end" font-family="Arial,sans-serif" font-size="17">{label}</text>',
            f'<rect x="{left}" y="{y}" width="{plot}" height="32" rx="5" fill="#e8edf2"/>',
            f'<rect x="{left}" y="{y}" width="{plot*bounded/100:.1f}" height="32" rx="5" fill="#52677c"/>',
            f'<text x="{min(left+plot*bounded/100+10,width-65):.1f}" y="{y+23}" font-family="Arial,sans-serif" font-size="16" font-weight="700">{bounded:.1f}%</text>',
        ]
    lines.append("</svg>")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def png_fallback(svg: Path) -> bool:
    try:
        import cairosvg  # type: ignore
        cairosvg.svg2png(url=str(svg), write_to=str(svg.with_suffix(".png")), output_width=1900)
        return svg.with_suffix(".png").is_file()
    except Exception:
        return False


def make_runbooks(output: Path, matrix: list[dict[str, Any]], baseline: str, run_id: str) -> None:
    open_rows = [row for row in matrix if row["closureState"] not in CLOSED_STATES]
    ps_lines = [
        "[CmdletBinding()]", "param(", f'  [string]$BaselineId = "{baseline}",',
        f'  [string]$RunId = "{run_id}"', ")", "$ErrorActionPreference = 'Stop'", "",
        "# Review each command and run only in a qualified non-production environment.",
    ]
    sh_lines = ["#!/usr/bin/env bash", "set -euo pipefail", f'BASELINE_ID="${{BASELINE_ID:-{baseline}}}"', f'RUN_ID="${{RUN_ID:-{run_id}}}"', ""]
    for row in open_rows:
        command = str(row.get("closureCommand", "")).replace("<BASELINE>", "$BaselineId").replace("<RUN>", "$RunId")
        safe_command = SECRET_PATTERN.sub(r"\1=<redacted>", command)
        ps_lines += [f"# {row['priority']} — {row['title']}", f"# Estado atual: {row['closureState']}", f"# {safe_command}", ""]
        shell_command = str(row.get("closureCommand", "")).replace("<BASELINE>", "${BASELINE_ID}").replace("<RUN>", "${RUN_ID}")
        safe_shell_command = SECRET_PATTERN.sub(r"\1=<redacted>", shell_command)
        sh_lines += [f"# {row['priority']} — {row['title']}", f"# Estado atual: {row['closureState']}", f"# {safe_shell_command}", ""]
    handoff = output / "handoff"
    handoff.mkdir(parents=True, exist_ok=True)
    (handoff / "windows-closure-runbook.ps1").write_text("\n".join(ps_lines) + "\n", encoding="utf-8")
    (handoff / "unix-closure-runbook.sh").write_text("\n".join(sh_lines) + "\n", encoding="utf-8")
    checklist = ["# Checklist de fecho das evidências", ""]
    for row in matrix:
        marker = "x" if row["closureState"] in CLOSED_STATES else " "
        checklist.append(f"- [{marker}] **{row['title']}** — `{row['closureState']}`. {row['nextAction']}")
    checklist += ["", "Uma linha marcada significa que existe evidência admitida; não significa que todas as limitações da área foram eliminadas.", ""]
    (handoff / "closure-checklist.md").write_text("\n".join(checklist), encoding="utf-8")


def write_hash_manifest(root: Path) -> int:
    manifest = root / "SHA256SUMS.txt"
    files = sorted(path for path in root.rglob("*") if path.is_file() and path != manifest)
    manifest.write_text("\n".join(f"{sha256(path)}  {path.relative_to(root).as_posix()}" for path in files) + "\n", encoding="utf-8")
    return len(files)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path.cwd())
    parser.add_argument("--baseline-id", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--config", default="config/evidence/evidence-gap-closure.json")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--overwrite", action="store_true")
    args = parser.parse_args()

    repo = args.repo.resolve()
    baseline = repo / "artifacts" / "report-evidence" / args.baseline_id
    if not baseline.is_dir():
        raise SystemExit(f"Baseline not found: {baseline}")
    config_path = (repo / args.config).resolve()
    config = read_json(config_path)
    if not isinstance(config, dict):
        raise SystemExit(f"Invalid configuration: {config_path}")
    output = (args.output or baseline / "11-evidence-gap-closure" / args.run_id).resolve()
    if output.exists():
        if not args.overwrite:
            raise SystemExit(f"Output already exists: {output}; use --overwrite")
        shutil.rmtree(output)
    output.mkdir(parents=True)
    phase_root = baseline / "11-evidence-gap-closure"
    phase_root.mkdir(parents=True, exist_ok=True)
    (phase_root / "LATEST.txt").write_text(args.run_id + "\n", encoding="utf-8")

    env = environment_snapshot()
    write_json(output / "environment-readiness.json", env)
    write_csv(output / "environment-readiness.csv", [
        {"tool": tool, "available": values.get("available"), "version": values.get("version"), "error": values.get("error")}
        for tool, values in env["tools"].items()
    ])

    historical_req = next((r for r in config.get("requirements", []) if r.get("id") == "historical-bc"), None)
    historical_rows, historical_audit = admit_historical_bc(repo, historical_req or {}, output)

    matrix: list[dict[str, Any]] = []
    for requirement in config.get("requirements", []):
        req_id = str(requirement.get("id"))
        source_file: Path | None = None
        source_hash = ""
        source_status = "UNKNOWN"
        if req_id == "historical-bc":
            state = "CLOSED_HISTORICAL" if historical_rows else historical_audit.get("status", "INVALID_SOURCE")
            source_status = historical_audit.get("status", "UNKNOWN")
            source_file = output / "admitted" / "historical-runs.csv" if historical_rows else None
        else:
            phase_dir = baseline / str(requirement.get("phaseDirectory", ""))
            run_root = phase_dir if req_id == "inventory" else resolve_latest(phase_dir)
            source_file = find_summary(run_root, list(requirement.get("summaryCandidates", [])))
            summary = read_json(source_file) if source_file else None
            if req_id == "inventory" and source_file and isinstance(summary, dict):
                state, source_status = "CLOSED_STATIC", "PASS"
            else:
                state, source_status = component_result(requirement, summary)
        if source_file and source_file.is_file():
            source_hash = sha256(source_file)
        required_tools = list(requirement.get("requiredTools", []))
        missing_tools = [tool for tool in required_tools if not env["tools"].get(tool, {}).get("available")]
        if state not in CLOSED_STATES and state != "PARTIAL":
            if missing_tools:
                state = "BLOCKED_ENVIRONMENT"
            elif source_file is None:
                state = "READY_TO_EXECUTE"
        evidence_class = requirement.get("evidenceClassWhenClosed") if state in CLOSED_STATES else (
            "IMPLEMENTED_NOT_EXECUTED" if state in {"READY_TO_EXECUTE", "BLOCKED_ENVIRONMENT"} else "BLOCKED_OR_PENDING"
        )
        if state in CLOSED_STATES:
            next_action = "Preservar a fonte, o hash e o teto de afirmação."
        elif state == "PARTIAL":
            next_action = "Executar a componente ainda ausente sem substituir os resultados já recolhidos."
        elif missing_tools:
            next_action = "Executar num ambiente com: " + ", ".join(missing_tools) + "."
        else:
            next_action = "Executar o comando de fecho e verificar o novo pacote."
        command = str(requirement.get("closureCommand", ""))
        if SECRET_PATTERN.search(command):
            command = SECRET_PATTERN.sub(r"\1=<redacted>", command)
        matrix.append({
            "requirementId": req_id,
            "title": requirement.get("title"),
            "priority": requirement.get("priority"),
            "sourcePhase": requirement.get("sourcePhase"),
            "closureState": state,
            "sourceStatus": source_status,
            "evidenceClass": evidence_class,
            "sourceFile": source_file.relative_to(repo).as_posix() if source_file and source_file.is_file() else "",
            "sourceSha256": source_hash,
            "requiredTools": required_tools,
            "missingTools": missing_tools,
            "requiredServices": requirement.get("requiredServices", []),
            "closureCommand": command,
            "nextAction": next_action,
            "countsAsEvidence": state in CLOSED_STATES,
            "hasExecutableClosurePlan": bool(command) and state not in CLOSED_STATES,
        })

    total = len(matrix)
    closed = sum(1 for row in matrix if row["closureState"] in CLOSED_STATES)
    partial = sum(1 for row in matrix if row["closureState"] == "PARTIAL")
    blocked = sum(1 for row in matrix if row["closureState"] == "BLOCKED_ENVIRONMENT")
    ready = sum(1 for row in matrix if row["closureState"] == "READY_TO_EXECUTE")
    invalid = sum(1 for row in matrix if row["closureState"] == "INVALID_SOURCE")
    evidence_coverage = 100.0 * closed / total if total else 0.0
    planned = sum(1 for row in matrix if row["closureState"] in CLOSED_STATES or row["hasExecutableClosurePlan"])
    closure_readiness = 100.0 * planned / total if total else 0.0
    weights = config.get("scoring", {})
    composite = evidence_coverage * float(weights.get("evidenceCoverageWeight", 0.7)) + closure_readiness * float(weights.get("closureReadinessWeight", 0.3))
    if invalid:
        status = "NEEDS_REVISION"
    elif evidence_coverage == 100.0:
        status = "PASS_EVIDENCE_COMPLETE"
    elif closure_readiness == 100.0:
        status = "PLAN_READY_EVIDENCE_INCOMPLETE"
    else:
        status = "PASS_WITH_LIMITATIONS"

    write_json(output / "closure-matrix.json", matrix)
    write_csv(output / "closure-matrix.csv", matrix)
    report_ready = output / "report-ready"
    tables = report_ready / "tables"
    figures = report_ready / "figures"
    tables.mkdir(parents=True)
    figures.mkdir(parents=True)
    (tables / "evidence-closure-matrix.md").write_text(md_table(matrix, [
        ("priority", "Prioridade"), ("title", "Área"), ("closureState", "Estado"),
        ("evidenceClass", "Classe"), ("missingTools", "Ferramentas em falta"), ("nextAction", "Próxima ação")
    ]), encoding="utf-8")
    write_csv(tables / "evidence-closure-matrix.csv", matrix)
    coverage_rows = [
        {"metric": "Evidência efetivamente presente", "percent": round(evidence_coverage, 1), "meaning": "Requisitos com fonte admitida e verificável."},
        {"metric": "Prontidão do plano de fecho", "percent": round(closure_readiness, 1), "meaning": "Requisitos fechados ou com comando e pré-requisitos explícitos."},
        {"metric": "Cobertura potencial após execução", "percent": 100.0 if closure_readiness == 100 else round(closure_readiness, 1), "meaning": "Meta; não é resultado alcançado."},
    ]
    write_csv(tables / "completion-readiness.csv", coverage_rows)
    (tables / "completion-readiness.md").write_text(md_table(coverage_rows, [("metric", "Métrica"), ("percent", "Percentagem"), ("meaning", "Interpretação")]), encoding="utf-8")
    svg = figures / "evidence-completeness-and-readiness.svg"
    build_svg(svg, "Completude real e prontidão para fecho", [(row["metric"], float(row["percent"])) for row in coverage_rows], "A cobertura potencial é uma meta e não deve ser apresentada como evidência recolhida.")
    png_fallback(svg)
    make_runbooks(output, matrix, args.baseline_id, args.run_id)

    summary = {
        "schemaVersion": "1.0",
        "scriptVersion": SCRIPT_VERSION,
        "baselineId": args.baseline_id,
        "runId": args.run_id,
        "generatedAtUtc": utc_iso(),
        "status": status,
        "evidenceCoveragePercent": round(evidence_coverage, 1),
        "closureReadinessPercent": round(closure_readiness, 1),
        "compositeReadinessScore": round(composite, 1),
        "potentialCoverageAfterExecutionPercent": 100.0 if closure_readiness == 100 else round(closure_readiness, 1),
        "counts": {"requirements": total, "closed": closed, "partial": partial, "blockedEnvironment": blocked, "readyToExecute": ready, "invalid": invalid, "historicalRunsAdmitted": len(historical_rows)},
        "claimBoundary": config.get("claimBoundary", {}),
    }
    write_json(output / "phase11-summary.json", summary)
    summary_md = [
        "# Fase 11 — fecho de lacunas e readiness gate", "",
        f"- Estado: **{status}**",
        f"- Evidência efetivamente presente: **{evidence_coverage:.1f}%** ({closed}/{total})",
        f"- Prontidão do plano de fecho: **{closure_readiness:.1f}%**",
        f"- Score composto de readiness: **{composite:.1f}/100**",
        f"- Execuções históricas B/C admitidas: **{len(historical_rows)}**", "",
        "## Interpretação", "",
        "A Fase 11 aumenta a evidência real apenas quando encontra ou executa uma fonte admissível. Um comando preparado aumenta a prontidão do fecho, mas não aumenta a cobertura de evidência.", "",
        "## Matriz", "", md_table(matrix, [("priority", "Prioridade"), ("title", "Área"), ("closureState", "Estado"), ("evidenceClass", "Classe"), ("nextAction", "Próxima ação")]),
    ]
    (output / "phase11-summary.md").write_text("\n".join(summary_md), encoding="utf-8")
    (report_ready / "report-integration-note.md").write_text(
        "# Integração no relatório\n\n"
        "Apresentar separadamente a percentagem de evidência presente e a prontidão do plano de fecho. "
        "A comparação B/C admitida nesta fase é histórica e deve manter data, SimulationRunId, fonte e limitação de versão.\n",
        encoding="utf-8",
    )
    hashed = write_hash_manifest(output)
    print(f"PHASE_11_STATUS={status}")
    print(f"PHASE_11_EVIDENCE_COVERAGE={evidence_coverage:.1f}")
    print(f"PHASE_11_CLOSURE_READINESS={closure_readiness:.1f}")
    print(f"PHASE_11_HASHED_FILES={hashed}")
    print(f"PHASE_11_OUTPUT={output}")
    return 0 if status in {"PASS_EVIDENCE_COMPLETE", "PLAN_READY_EVIDENCE_INCOMPLETE", "PASS_WITH_LIMITATIONS"} else 1


if __name__ == "__main__":
    raise SystemExit(main())
