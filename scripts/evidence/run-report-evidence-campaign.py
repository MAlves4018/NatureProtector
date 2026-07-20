#!/usr/bin/env python3
"""Run a controlled NatureProtector report-evidence campaign.

The campaign orchestrates the evidence collectors introduced in Phases 1-7 and the Phase 9 NP_score validation collector. It
is deliberately explicit about live/runtime actions: no test, database, API,
performance or reliability action is executed unless --execute is supplied and
its profile/flags select that action. Credentials are read only from named
environment variables and are never written to the campaign output.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import platform
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request
from dataclasses import dataclass, asdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

SCRIPT_VERSION = "1.4.0"
PHASE_DIRS = {
    "phase1": "01-inventory",
    "phase2": "02-tests",
    "phase3": "03-database",
    "phase4": "04-runtime",
    "phase5": "05-performance",
    "phase6": "06-reliability",
    "phase7": "07-report-integration",
    "phase9": "09-np-score-validation",
    "phase11": "11-evidence-gap-closure",
}
PHASE_ORDER = ("phase1", "phase2", "phase3", "phase4", "phase5", "phase6", "phase9", "phase11", "phase7")
PROFILE_STEPS = {
    "plan": [],
    "static": ["phase1", "phase3", "phase9", "phase11", "phase7"],
    "quality": ["phase1", "phase2", "phase3", "phase9", "phase11", "phase7"],
    "full": ["phase1", "phase2", "phase3", "phase4", "phase5", "phase6", "phase9", "phase11", "phase7"],
}

SUCCESS_STEP_STATUSES = {"PASS", "PASS_COMPLETE_REPORT_PACKAGE", "PASS_PARTIAL_REPORT_PACKAGE", "PASS_EXPLORATORY_VALIDATION", "PASS_GAP_CLOSURE_READY", "PASS_EVIDENCE_COMPLETE", "PLAN_READY_EVIDENCE_INCOMPLETE", "PASS_WITH_LIMITATIONS"}


def campaign_status(selected_results, safety_errors, execute, profile):
    if safety_errors:
        return "BLOCKED_SAFETY"
    if not execute or profile == "plan":
        return "PLAN_ONLY"
    if selected_results and all(row.status in SUCCESS_STEP_STATUSES for row in selected_results):
        return (
            "PASS_PARTIAL_REPORT_PACKAGE"
            if any(row.status == "PASS_PARTIAL_REPORT_PACKAGE" for row in selected_results)
            else "PASS"
        )
    if any(row.status in SUCCESS_STEP_STATUSES for row in selected_results):
        return "PARTIAL"
    return "FAIL"


SECRET_ENV_NAMES = {
    "NATUREPROTECTOR_POSTGRES_DSN",
    "DATABASE_URL",
    "NATUREPROTECTOR_RUNTIME_BEARER_TOKEN",
    "NATUREPROTECTOR_RUNTIME_USERNAME",
    "NATUREPROTECTOR_RUNTIME_PASSWORD",
    "NP_RELIABILITY_AUTH_TOKEN",
    "NP_PERFORMANCE_AUTH_TOKEN",
    "NP_PERFORMANCE_USERNAME",
    "NP_PERFORMANCE_PASSWORD",
    "NP_POSTGRES_CONNECTION_STRING",
}


def utc_stamp() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def utc_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_csv(path: Path, rows: list[dict[str, Any]], fieldnames: list[str] | None = None) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if fieldnames is None:
        fieldnames = list(rows[0]) if rows else []
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_hash_manifest(root: Path) -> int:
    manifest = root / "SHA256SUMS.txt"
    files = sorted(p for p in root.rglob("*") if p.is_file() and p != manifest)
    manifest.write_text(
        "\n".join(f"{sha256(path)}  {path.relative_to(root).as_posix()}" for path in files) + "\n",
        encoding="utf-8",
    )
    return len(files)


def command_version(command: str, *args: str) -> dict[str, Any]:
    resolved = shutil.which(command)
    if not resolved:
        return {"available": False, "command": command, "path": None, "version": None}
    try:
        process = subprocess.run(
            [resolved, *args],
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            timeout=20,
            check=False,
        )
        output = (process.stdout or "").strip().splitlines()
        return {
            "available": process.returncode == 0,
            "command": command,
            "path": resolved,
            "version": output[0] if output else None,
            "exitCode": process.returncode,
        }
    except Exception as exc:  # defensive preflight
        return {"available": False, "command": command, "path": resolved, "version": None, "error": str(exc)}


def probe_api(base_url: str, timeout: float = 2.0) -> dict[str, Any]:
    url = base_url.rstrip("/") + "/health"
    request = urllib.request.Request(url, method="GET")
    started = time.perf_counter()
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            body = response.read(4096).decode("utf-8", errors="replace")
            return {
                "available": 200 <= response.status < 500,
                "url": url,
                "statusCode": response.status,
                "durationMs": round((time.perf_counter() - started) * 1000, 2),
                "bodyPreview": body[:300],
            }
    except urllib.error.HTTPError as exc:
        return {
            "available": True,
            "url": url,
            "statusCode": exc.code,
            "durationMs": round((time.perf_counter() - started) * 1000, 2),
            "bodyPreview": "",
        }
    except Exception as exc:
        return {
            "available": False,
            "url": url,
            "statusCode": None,
            "durationMs": round((time.perf_counter() - started) * 1000, 2),
            "error": str(exc),
        }


def safe_env_status() -> dict[str, bool]:
    return {name: bool(os.getenv(name)) for name in sorted(SECRET_ENV_NAMES)}


def redact_command(command: list[str], secret_values: list[str]) -> str:
    rendered = " ".join(command)
    for value in sorted((v for v in secret_values if v), key=len, reverse=True):
        rendered = rendered.replace(value, "<redacted>")
    rendered = re.sub(r"(?i)(password|token|dsn|connectionstring)=([^\s]+)", r"\1=<redacted>", rendered)
    return rendered


@dataclass
class StepResult:
    step: str
    selected: bool
    status: str
    startedAtUtc: str | None = None
    finishedAtUtc: str | None = None
    durationSeconds: float | None = None
    exitCode: int | None = None
    outputDirectory: str | None = None
    command: str | None = None
    reason: str | None = None


def run_step(
    step: str,
    commands: list[list[str]],
    repo: Path,
    logs: Path,
    output_dir: Path | None,
    secret_values: list[str],
) -> StepResult:
    """Run a collector/verifier command chain and preserve combined logs."""
    started_iso = utc_iso()
    started = time.perf_counter()
    stdout_path = logs / f"{step}.stdout.txt"
    stderr_path = logs / f"{step}.stderr.txt"
    safe_commands = [redact_command(command, secret_values) for command in commands]
    stdout_chunks: list[str] = []
    stderr_chunks: list[str] = []
    last_exit = 0
    try:
        for index, command in enumerate(commands, start=1):
            heading = f"===== command {index}/{len(commands)}: {safe_commands[index - 1]} =====\n"
            process = subprocess.run(
                command,
                cwd=repo,
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                check=False,
            )
            stdout_chunks.append(heading + (process.stdout or ""))
            stderr_chunks.append(heading + (process.stderr or ""))
            last_exit = process.returncode
            if process.returncode != 0:
                break
        stdout_path.write_text("\n".join(stdout_chunks), encoding="utf-8")
        stderr_path.write_text("\n".join(stderr_chunks), encoding="utf-8")
        return StepResult(
            step=step,
            selected=True,
            status="PASS" if last_exit == 0 else "FAIL",
            startedAtUtc=started_iso,
            finishedAtUtc=utc_iso(),
            durationSeconds=round(time.perf_counter() - started, 3),
            exitCode=last_exit,
            outputDirectory=str(output_dir) if output_dir else None,
            command=" && ".join(safe_commands),
            reason=None if last_exit == 0 else f"See {stderr_path.name} and {stdout_path.name}",
        )
    except Exception as exc:
        stderr_path.write_text("\n".join(stderr_chunks + [str(exc) + "\n"]), encoding="utf-8")
        return StepResult(
            step=step,
            selected=True,
            status="FAIL_TO_START",
            startedAtUtc=started_iso,
            finishedAtUtc=utc_iso(),
            durationSeconds=round(time.perf_counter() - started, 3),
            exitCode=None,
            outputDirectory=str(output_dir) if output_dir else None,
            command=" && ".join(safe_commands),
            reason=str(exc),
        )


def phase_output(baseline_root: Path, step: str, run_id: str) -> Path:
    if step == "phase1":
        return baseline_root / PHASE_DIRS[step]
    return baseline_root / PHASE_DIRS[step] / run_id


def build_commands(
    args: argparse.Namespace, repo: Path, baseline_root: Path
) -> dict[str, tuple[list[list[str]], Path]]:
    python = str(Path(sys.executable).resolve())
    scripts = repo / "scripts" / "evidence"
    run_id = args.run_id
    commands: dict[str, tuple[list[list[str]], Path]] = {}

    out1 = phase_output(baseline_root, "phase1", run_id)
    collect1 = [
        python,
        str(scripts / "collect-report-inventory.py"),
        "--repo",
        str(repo),
        "--baseline-id",
        args.baseline_id,
        "--output",
        str(out1),
    ]
    verify1 = [python, str(scripts / "verify-report-inventory.py"), "--inventory-root", str(out1)]
    commands["phase1"] = ([collect1, verify1], out1)

    out2 = phase_output(baseline_root, "phase2", run_id)
    collect2 = [
        python,
        str(scripts / "collect-test-quality-evidence.py"),
        "--repo",
        str(repo),
        "--baseline-id",
        args.baseline_id,
        "--run-id",
        run_id,
        "--output-root",
        str(out2),
        "--timeout-seconds",
        str(args.test_timeout_seconds),
    ]
    if args.include_e2e:
        collect2.append("--include-e2e")
    if args.skip_npm_ci:
        collect2.append("--skip-npm-ci")
    if args.no_restore:
        collect2.append("--no-restore")
    if args.no_build:
        collect2.append("--no-build")
    verify2 = [python, str(scripts / "verify-test-quality-evidence.py"), "--evidence-root", str(out2)]
    commands["phase2"] = ([collect2, verify2], out2)

    out3 = phase_output(baseline_root, "phase3", run_id)
    collect3 = [
        python,
        str(scripts / "collect-database-architecture-evidence.py"),
        "--repo",
        str(repo),
        "--baseline-id",
        args.baseline_id,
        "--run-id",
        run_id,
        "--output",
        str(out3),
    ]
    if args.require_live_database:
        collect3.append("--require-live")
    verify3 = [python, str(scripts / "verify-database-architecture-evidence.py"), str(out3)]
    if args.require_live_database:
        verify3.append("--require-live")
    commands["phase3"] = ([collect3, verify3], out3)

    out4 = phase_output(baseline_root, "phase4", run_id)
    collect4 = [
        python,
        str(scripts / "collect-integrated-runtime-evidence.py"),
        "--repo",
        str(repo),
        "--baseline-id",
        args.baseline_id,
        "--run-id",
        run_id,
        "--output",
        str(out4),
        "--api-base-url",
        args.api_base_url,
        "--live",
        "--postgres-dsn-env",
        args.postgres_dsn_env,
    ]
    if args.require_live_runtime:
        collect4.append("--require-live")
    if args.reset_runtime:
        collect4.append("--reset-runtime")
    verify4 = [python, str(scripts / "verify-integrated-runtime-evidence.py"), str(out4)]
    if args.require_live_runtime:
        verify4.append("--require-live")
    if args.require_database_trace:
        verify4.append("--require-database-trace")
    commands["phase4"] = ([collect4, verify4], out4)

    out5 = phase_output(baseline_root, "phase5", run_id)
    collect5 = [
        python,
        str(scripts / "collect-performance-evidence.py"),
        "--repo",
        str(repo),
        "--baseline-id",
        args.baseline_id,
        "--run-id",
        run_id,
        "--output",
        str(out5),
        "--api-base-url",
        args.api_base_url,
        "--http-profile",
        args.http_profile,
        "--benchmark-profile",
        args.benchmark_profile,
    ]
    if args.run_http:
        collect5.append("--run-http")
    if args.include_web:
        collect5.append("--include-web")
    if args.run_microbenchmarks:
        collect5.append("--run-microbenchmarks")
    if args.system_run_directory:
        collect5 += ["--system-run-directory", str(Path(args.system_run_directory).resolve())]
    if args.require_http:
        collect5.append("--require-http")
    if args.require_microbenchmarks:
        collect5.append("--require-microbenchmarks")
    if args.require_system:
        collect5.append("--require-system")
    verify5 = [python, str(scripts / "verify-performance-evidence.py"), str(out5)]
    if args.require_http:
        verify5.append("--require-http")
    if args.require_microbenchmarks:
        verify5.append("--require-microbenchmarks")
    if args.require_system:
        verify5.append("--require-system")
    commands["phase5"] = ([collect5, verify5], out5)

    out6 = phase_output(baseline_root, "phase6", run_id)
    collect6 = [
        python,
        str(scripts / "collect-reliability-evidence.py"),
        "--repo",
        str(repo),
        "--baseline-id",
        args.baseline_id,
        "--run-id",
        run_id,
        "--output",
        str(out6),
        "--api-base-url",
        args.api_base_url,
        "--timeout-seconds",
        str(args.reliability_timeout_seconds),
    ]
    if args.execute_p3:
        collect6.append("--execute-p3")
    if args.acknowledge_non_production:
        collect6.append("--acknowledge-non-production")
    if args.p3_run_label:
        collect6 += ["--p3-run-label", args.p3_run_label]
    if args.audit_directory:
        collect6 += ["--audit-directory", str(Path(args.audit_directory).resolve())]
    if args.require_p3:
        collect6.append("--require-p3")
    if args.require_audit:
        collect6.append("--require-audit")
    verify6 = [python, str(scripts / "verify-reliability-evidence.py"), str(out6)]
    if args.require_p3:
        verify6.append("--require-p3")
    if args.require_audit:
        verify6.append("--require-audit")
    commands["phase6"] = ([collect6, verify6], out6)

    out7 = phase_output(baseline_root, "phase7", run_id)
    collect7 = [
        python,
        str(scripts / "collect-report-integration-evidence.py"),
        "--repo",
        str(repo),
        "--baseline-id",
        args.baseline_id,
        "--run-id",
        run_id,
        "--output-root",
        str(out7),
    ]
    verify7 = [
        python,
        str(scripts / "verify-report-integration-evidence.py"),
        "--repo",
        str(repo),
        "--baseline-id",
        args.baseline_id,
        "--run-id",
        run_id,
        "--evidence-root",
        str(out7),
    ]
    commands["phase7"] = ([collect7, verify7], out7)

    out9 = phase_output(baseline_root, "phase9", run_id)
    collect9 = [
        python,
        str(scripts / "collect-np-score-validation.py"),
        "--repo",
        str(repo),
        "--baseline-id",
        args.baseline_id,
        "--run-id",
        run_id,
        "--config",
        args.np_score_config,
        "--output",
        str(out9),
        "--bootstrap-iterations",
        str(args.np_score_bootstrap_iterations),
        "--overwrite",
    ]
    for evidence_root in (out4, out5, out6):
        collect9 += ["--runtime-evidence-root", str(evidence_root)]
    verify9 = [python, str(scripts / "verify-np-score-validation.py"), str(out9), "--require-complete"]
    commands["phase9"] = ([collect9, verify9], out9)

    out11 = phase_output(baseline_root, "phase11", run_id)
    collect11 = [
        python,
        str(scripts / "collect-evidence-gap-closure.py"),
        "--repo",
        str(repo),
        "--baseline-id",
        args.baseline_id,
        "--run-id",
        run_id,
        "--config",
        args.evidence_closure_config,
        "--output",
        str(out11),
        "--overwrite",
    ]
    verify11 = [python, str(scripts / "verify-evidence-gap-closure.py"), str(out11)]
    commands["phase11"] = ([collect11, verify11], out11)
    return commands


def normalize_latest_pointer(baseline_root: Path, step: str, run_id: str) -> None:
    """Write a portable run-id pointer instead of an environment-specific absolute path."""
    if step == "phase1":
        return
    phase_root = baseline_root / PHASE_DIRS[step]
    phase_root.mkdir(parents=True, exist_ok=True)
    (phase_root / "LATEST.txt").write_text(run_id + "\n", encoding="utf-8")


def validate_safety(args: argparse.Namespace) -> list[str]:
    errors: list[str] = []
    if args.execute_p3 and not args.acknowledge_non_production:
        errors.append("--execute-p3 requires --acknowledge-non-production")
    if args.require_p3 and not args.execute_p3 and not args.audit_directory:
        errors.append("--require-p3 requires --execute-p3 or an imported --audit-directory")
    if args.require_audit and not args.audit_directory:
        errors.append("--require-audit requires --audit-directory")
    if args.require_system and not args.system_run_directory:
        errors.append("--require-system requires --system-run-directory")
    if args.reset_runtime and not args.acknowledge_non_production:
        errors.append("--reset-runtime requires --acknowledge-non-production")
    return errors


def build_preflight(args: argparse.Namespace, repo: Path, baseline_root: Path) -> dict[str, Any]:
    tools = {
        "python": {"available": True, "path": sys.executable, "version": platform.python_version()},
        "dotnet": command_version("dotnet", "--version"),
        "node": command_version("node", "--version"),
        "npm": command_version("npm", "--version"),
        "docker": command_version("docker", "--version"),
        "powershell": command_version("pwsh", "--version")
        if shutil.which("pwsh")
        else command_version("powershell", "-NoProfile", "-Command", "$PSVersionTable.PSVersion.ToString()"),
    }
    return {
        "generatedAtUtc": utc_iso(),
        "scriptVersion": SCRIPT_VERSION,
        "repositoryRoot": str(repo),
        "baselineId": args.baseline_id,
        "baselineExists": baseline_root.exists(),
        "solutionExists": (repo / "NatureProtector.sln").exists(),
        "tools": tools,
        "api": probe_api(args.api_base_url),
        "environmentVariablesPresent": safe_env_status(),
        "postgresDsnEnvironmentVariable": args.postgres_dsn_env,
        "postgresDsnPresent": bool(os.getenv(args.postgres_dsn_env)) if args.postgres_dsn_env else False,
        "profile": args.profile,
        "npScoreValidationConfigExists": (repo / args.np_score_config).is_file(),
        "npScoreBootstrapIterations": args.np_score_bootstrap_iterations,
        "evidenceClosureConfigExists": (repo / args.evidence_closure_config).is_file(),
        "executeRequested": bool(args.execute),
        "nonProductionAcknowledged": bool(args.acknowledge_non_production),
    }


def write_markdown_summary(path: Path, summary: dict[str, Any]) -> None:
    lines = [
        "# NatureProtector — campanha canónica de evidência",
        "",
        f"- GeneratedAtUtc: `{summary['generatedAtUtc']}`",
        f"- BaselineId: `{summary['baselineId']}`",
        f"- RunId: `{summary['runId']}`",
        f"- Profile: `{summary['profile']}`",
        f"- Mode: `{summary['mode']}`",
        f"- Status: **{summary['status']}**",
        "",
        "## Passos",
        "",
        "| Passo | Selecionado | Estado | Duração (s) | Output |",
        "|---|---:|---|---:|---|",
    ]
    for row in summary["steps"]:
        lines.append(
            f"| {row['step']} | {'sim' if row['selected'] else 'não'} | {row['status']} | "
            f"{row.get('durationSeconds') if row.get('durationSeconds') is not None else ''} | "
            f"{row.get('outputDirectory') or ''} |"
        )
    lines += [
        "",
        "## Interpretação",
        "",
        "- `PLAN_ONLY` significa que apenas o plano e o preflight foram produzidos.",
        "- `PASS` significa que todos os passos selecionados terminaram com código de saída zero.",
        "- `PARTIAL` significa que alguns passos passaram e outros falharam ou ficaram bloqueados.",
        "- A campanha não transforma evidência histórica ou estática em execução atual; essa classificação continua a ser definida pelos coletores de cada fase.",
        "- A Fase 9 mede validação exploratória retrospectiva do NP_score e mantém explícito que o score não é uma probabilidade calibrada.",
        "- A Fase 11 admite fontes históricas verificadas e prepara o fecho das lacunas; comandos planeados não contam como evidência recolhida.",
        "",
    ]
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path.cwd())
    parser.add_argument("--baseline-id", required=True)
    parser.add_argument("--run-id", default=utc_stamp())
    parser.add_argument("--profile", choices=tuple(PROFILE_STEPS), default="plan")
    parser.add_argument(
        "--execute",
        action="store_true",
        help="Actually execute the selected profile. Without it, only plan/preflight is generated.",
    )
    parser.add_argument("--continue-on-error", action="store_true")
    parser.add_argument("--api-base-url", default="http://localhost:5254")
    parser.add_argument("--postgres-dsn-env", default="NATUREPROTECTOR_POSTGRES_DSN")
    parser.add_argument("--test-timeout-seconds", type=int, default=1800)
    parser.add_argument("--include-e2e", action="store_true")
    parser.add_argument("--skip-npm-ci", action="store_true")
    parser.add_argument("--no-restore", action="store_true")
    parser.add_argument("--no-build", action="store_true")
    parser.add_argument("--require-live-database", action="store_true")
    parser.add_argument("--require-live-runtime", action="store_true")
    parser.add_argument("--require-database-trace", action="store_true")
    parser.add_argument("--reset-runtime", action="store_true")
    parser.add_argument("--run-http", action="store_true")
    parser.add_argument("--http-profile", choices=("Calibration", "B0", "B1", "B2"), default="B1")
    parser.add_argument("--include-web", action="store_true")
    parser.add_argument("--run-microbenchmarks", action="store_true")
    parser.add_argument("--benchmark-profile", choices=("B0", "B1", "B2"), default="B1")
    parser.add_argument("--system-run-directory")
    parser.add_argument("--require-http", action="store_true")
    parser.add_argument("--require-microbenchmarks", action="store_true")
    parser.add_argument("--require-system", action="store_true")
    parser.add_argument("--execute-p3", action="store_true")
    parser.add_argument("--acknowledge-non-production", action="store_true")
    parser.add_argument("--p3-run-label")
    parser.add_argument("--audit-directory")
    parser.add_argument("--require-p3", action="store_true")
    parser.add_argument("--require-audit", action="store_true")
    parser.add_argument("--reliability-timeout-seconds", type=int, default=300)
    parser.add_argument("--np-score-config", default="config/evidence/np-score-validation.json")
    parser.add_argument("--np-score-bootstrap-iterations", type=int, default=500)
    parser.add_argument("--evidence-closure-config", default="config/evidence/evidence-gap-closure.json")
    args = parser.parse_args()

    repo = args.repo.resolve()
    baseline_root = repo / "artifacts" / "report-evidence" / args.baseline_id
    campaign_root = baseline_root / "08-campaign" / args.run_id
    logs = campaign_root / "logs"
    logs.mkdir(parents=True, exist_ok=True)
    (baseline_root / "08-campaign" / "LATEST.txt").write_text(args.run_id + "\n", encoding="utf-8")

    safety_errors = validate_safety(args)
    preflight = build_preflight(args, repo, baseline_root)
    preflight["safetyErrors"] = safety_errors
    write_json(campaign_root / "preflight.json", preflight)

    selected_steps = PROFILE_STEPS[args.profile]
    commands = build_commands(args, repo, baseline_root)
    secret_values = [os.getenv(name, "") for name in SECRET_ENV_NAMES]
    plan_rows: list[dict[str, Any]] = []
    for name in PHASE_ORDER:
        command_chain, output = commands[name]
        plan_rows.append(
            {
                "step": name,
                "selected": name in selected_steps,
                "command": " && ".join(redact_command(command, secret_values) for command in command_chain),
                "outputDirectory": str(output),
            }
        )
    write_json(campaign_root / "execution-plan.json", plan_rows)
    write_csv(campaign_root / "execution-plan.csv", plan_rows, ["step", "selected", "command", "outputDirectory"])

    step_results: list[StepResult] = []
    if safety_errors:
        for name in selected_steps:
            step_results.append(
                StepResult(step=name, selected=True, status="BLOCKED_SAFETY", reason="; ".join(safety_errors))
            )
    elif not args.execute or args.profile == "plan":
        for name in PHASE_ORDER:
            step_results.append(
                StepResult(
                    step=name,
                    selected=name in selected_steps,
                    status="PLANNED" if name in selected_steps else "NOT_SELECTED",
                    outputDirectory=str(commands[name][1]) if name in selected_steps else None,
                    command=" && ".join(redact_command(command, secret_values) for command in commands[name][0])
                    if name in selected_steps
                    else None,
                )
            )
    else:
        failed = False
        for name in PHASE_ORDER:
            if name not in selected_steps:
                step_results.append(StepResult(step=name, selected=False, status="NOT_SELECTED"))
                continue
            if failed and not args.continue_on_error:
                step_results.append(
                    StepResult(
                        step=name, selected=True, status="SKIPPED_DEPENDENCY", reason="A previous selected step failed."
                    )
                )
                continue
            command_chain, output = commands[name]
            result = run_step(name, command_chain, repo, logs, output, secret_values)
            if result.status == "PASS":
                summary_candidates = {
                    "phase7": ("phase7-summary.json", {"PASS_COMPLETE_REPORT_PACKAGE", "PASS_PARTIAL_REPORT_PACKAGE"}),
                    "phase9": ("phase9-summary.json", {"PASS_EXPLORATORY_VALIDATION", "PARTIAL_INSUFFICIENT_EVENTS"}),
                    "phase11": ("phase11-summary.json", {"PASS_EVIDENCE_COMPLETE", "PLAN_READY_EVIDENCE_INCOMPLETE", "PASS_WITH_LIMITATIONS", "NEEDS_REVISION"}),
                }
                if name in summary_candidates:
                    filename, allowed = summary_candidates[name]
                    summary_path = output / filename
                    if summary_path.is_file():
                        reported = json.loads(summary_path.read_text(encoding="utf-8")).get("status")
                        if reported in allowed:
                            result.status = reported
            step_results.append(result)
            if result.status in SUCCESS_STEP_STATUSES:
                normalize_latest_pointer(baseline_root, name, args.run_id)
            else:
                failed = True

    selected_results = [row for row in step_results if row.selected]
    status = campaign_status(selected_results, safety_errors, args.execute, args.profile)

    summary = {
        "generatedAtUtc": utc_iso(),
        "scriptVersion": SCRIPT_VERSION,
        "baselineId": args.baseline_id,
        "runId": args.run_id,
        "repositoryRoot": str(repo),
        "profile": args.profile,
        "mode": "EXECUTE" if args.execute else "PLAN",
        "status": status,
        "selectedSteps": selected_steps,
        "safetyErrors": safety_errors,
        "steps": [asdict(row) for row in step_results],
        "claimBoundary": "The campaign orchestrates evidence collection; each phase summary remains authoritative for evidence class and claim ceiling.",
    }
    write_json(campaign_root / "campaign-summary.json", summary)
    write_csv(campaign_root / "step-results.csv", [asdict(row) for row in step_results])
    write_markdown_summary(campaign_root / "campaign-summary.md", summary)
    hashed = write_hash_manifest(campaign_root)
    print(f"PHASE_8_CAMPAIGN_STATUS={status}")
    print(f"PHASE_8_PROFILE={args.profile}")
    print(f"PHASE_8_RUN_ID={args.run_id}")
    print(f"PHASE_8_OUTPUT={campaign_root}")
    print(f"PHASE_8_HASHED_FILES={hashed}")
    return 0 if status in {"PASS", "PASS_PARTIAL_REPORT_PACKAGE", "PLAN_ONLY"} else 1


if __name__ == "__main__":
    raise SystemExit(main())
