#!/usr/bin/env python3
"""PowerShell-facing orchestrator for the existing NatureProtector evidence system.

This runner deliberately reuses Phase 1-11 collectors, the existing E1-E6 final
portfolio, the runtime long-run proof and the Playwright live-runtime capture.
It writes only the orchestration/linking layer as Phase 13.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Any

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from final_common import read_json, safe_environment, safe_relative, utc_iso, utc_stamp, write_csv, write_json


@dataclass
class Step:
    stage: str
    status: str
    command: str
    cwd: str
    startedAtUtc: str
    finishedAtUtc: str
    durationSeconds: float
    exitCode: int | None
    stdout: str
    stderr: str
    limitation: str = ""


class Runner:
    def __init__(self, repo: Path, root: Path, resume: bool, continue_on_error: bool):
        self.repo = repo
        self.root = root
        self.resume = resume
        self.continue_on_error = continue_on_error
        self.logs = root / "logs"
        self.states = root / "states"
        self.logs.mkdir(parents=True, exist_ok=True)
        self.states.mkdir(parents=True, exist_ok=True)
        self.steps: list[Step] = []

    def state_path(self, stage: str) -> Path:
        return self.states / f"{stage}.json"

    def run(
        self,
        stage: str,
        command: list[str],
        *,
        cwd: Path | None = None,
        env: dict[str, str] | None = None,
        timeout: int | None = None,
        required: bool = True,
        acceptable_codes: set[int] | None = None,
    ) -> Step:
        cwd = (cwd or self.repo).resolve()
        state_path = self.state_path(stage)
        if self.resume and state_path.is_file():
            prior = read_json(state_path, {})
            if prior.get("status") in {"PASS", "PLANNED"}:
                step = Step(**prior)
                self.steps.append(step)
                return step

        started_iso = utc_iso()
        started = time.monotonic()
        stdout_path = self.logs / f"{stage}.stdout.log"
        stderr_path = self.logs / f"{stage}.stderr.log"
        executable = shutil.which(command[0])
        resolved_command = list(command)
        if executable is not None:
            resolved_command[0] = executable
        if executable is None:
            status = "BLOCKED" if required else "NOT_EXECUTED"
            limitation = f"Executable not found: {command[0]}"
            stdout_path.write_text("", encoding="utf-8")
            stderr_path.write_text(limitation + "\n", encoding="utf-8")
            step = Step(stage, status, self._format(command), str(cwd), started_iso, utc_iso(), 0.0, None, safe_relative(stdout_path, self.repo), safe_relative(stderr_path, self.repo), limitation)
            write_json(state_path, asdict(step))
            self.steps.append(step)
            if required and not self.continue_on_error:
                raise RuntimeError(limitation)
            return step

        merged_env = os.environ.copy()
        if env:
            merged_env.update(env)
        try:
            process = subprocess.run(
                resolved_command,
                cwd=cwd,
                env=merged_env,
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                timeout=timeout,
                check=False,
            )
            stdout_path.write_text(process.stdout, encoding="utf-8", errors="replace")
            stderr_path.write_text(process.stderr, encoding="utf-8", errors="replace")
            allowed = acceptable_codes or {0}
            status = "PASS" if process.returncode in allowed else "FAIL"
            limitation = "" if status == "PASS" else f"Exit code {process.returncode}"
            step = Step(stage, status, self._format(resolved_command), str(cwd), started_iso, utc_iso(), round(time.monotonic() - started, 3), process.returncode, safe_relative(stdout_path, self.repo), safe_relative(stderr_path, self.repo), limitation)
        except subprocess.TimeoutExpired as exc:
            stdout_path.write_text((exc.stdout or "") if isinstance(exc.stdout, str) else "", encoding="utf-8")
            stderr_path.write_text(((exc.stderr or "") if isinstance(exc.stderr, str) else "") + f"\nTimed out after {timeout}s\n", encoding="utf-8")
            step = Step(stage, "FAIL", self._format(resolved_command), str(cwd), started_iso, utc_iso(), round(time.monotonic() - started, 3), None, safe_relative(stdout_path, self.repo), safe_relative(stderr_path, self.repo), f"Timed out after {timeout}s")
        write_json(state_path, asdict(step))
        self.steps.append(step)
        if step.status == "FAIL" and required and not self.continue_on_error:
            stderr_tail = stderr_path.read_text(encoding="utf-8", errors="replace").strip()[-2000:] if stderr_path.is_file() else ""
            stdout_tail = stdout_path.read_text(encoding="utf-8", errors="replace").strip()[-2000:] if stdout_path.is_file() else ""
            detail = stderr_tail or stdout_tail or step.limitation
            raise RuntimeError(f"Stage {stage} failed: {detail}. Logs: {stdout_path}; {stderr_path}")
        return step

    @staticmethod
    def _format(command: list[str]) -> str:
        def quote(value: str) -> str:
            return f'"{value}"' if any(ch.isspace() for ch in value) else value
        return " ".join(quote(value) for value in command)

    def ledger(self, path: Path) -> None:
        write_csv(path, [asdict(step) for step in self.steps], list(Step.__annotations__))


def probe(url: str, timeout: float = 3.0) -> dict[str, Any]:
    try:
        with urllib.request.urlopen(url, timeout=timeout) as response:
            return {"url": url, "ok": 200 <= response.status < 400, "statusCode": response.status, "error": ""}
    except (urllib.error.URLError, TimeoutError, ValueError) as exc:
        return {"url": url, "ok": False, "statusCode": None, "error": str(exc)}


def wait_for_runtime(root: Path, timeout_seconds: int) -> bool:
    deadline = time.monotonic() + timeout_seconds
    observations: list[dict[str, Any]] = []
    urls = [
        "http://127.0.0.1:5254/health/ready",
        "http://127.0.0.1:5260/health/ready",
        "http://127.0.0.1:5173",
    ]
    while time.monotonic() < deadline:
        current = [{**probe(url), "capturedAtUtc": utc_iso()} for url in urls]
        observations.extend(current)
        if all(item["ok"] for item in current):
            write_json(root / "runtime-readiness.json", observations)
            return True
        time.sleep(3)
    write_json(root / "runtime-readiness.json", observations)
    return False


def runtime_token(api_base_url: str) -> str:
    token = os.getenv("NATUREPROTECTOR_RUNTIME_BEARER_TOKEN", "").strip()
    if token:
        return token
    username = os.getenv("NATUREPROTECTOR_RUNTIME_USERNAME", "").strip() or os.getenv("NATUREPROTECTOR_USERNAME", "").strip()
    password = os.getenv("NATUREPROTECTOR_RUNTIME_PASSWORD", "").strip() or os.getenv("NATUREPROTECTOR_PASSWORD", "").strip()
    if not username or not password:
        raise RuntimeError(
            "Runtime preflight requires NATUREPROTECTOR_RUNTIME_BEARER_TOKEN or "
            "NATUREPROTECTOR_RUNTIME_USERNAME/NATUREPROTECTOR_RUNTIME_PASSWORD."
        )
    payload = json.dumps({"usernameOrEmail": username, "password": password}).encode("utf-8")
    request = urllib.request.Request(
        api_base_url.rstrip("/") + "/api/users-roles/login",
        data=payload,
        method="POST",
        headers={"Accept": "application/json", "Content-Type": "application/json"},
    )
    with urllib.request.urlopen(request, timeout=10) as response:
        body = json.loads(response.read().decode("utf-8"))
    token = str((body or {}).get("token") or "").strip()
    if not token:
        raise RuntimeError("Runtime preflight login returned no bearer token.")
    return token


def authenticated_json(
    api_base_url: str,
    token: str,
    method: str,
    path: str,
    payload: dict[str, Any] | None = None,
) -> dict[str, Any]:
    headers = {"Accept": "application/json", "Authorization": f"Bearer {token}"}
    data = None
    if payload is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(api_base_url.rstrip("/") + path, data=data, headers=headers, method=method)
    with urllib.request.urlopen(request, timeout=15) as response:
        raw = response.read().decode("utf-8")
    return json.loads(raw) if raw else {}


def run_preflight(output: Path, api_base_url: str) -> bool:
    result: dict[str, Any] = {"capturedAtUtc": utc_iso(), "apiBaseUrl": api_base_url, "checks": []}
    try:
        ready = probe(api_base_url.rstrip("/") + "/health/ready", timeout=5.0)
        result["checks"].append({"name": "health-ready", **ready})
        if not ready["ok"]:
            raise RuntimeError("Existing runtime is not ready.")
        token = runtime_token(api_base_url)
        current = authenticated_json(api_base_url, token, "GET", "/api/control/runtime/operations/current")
        result["checks"].append({"name": "runtime-current", "ok": True, "status": current.get("status")})
        reset = authenticated_json(
            api_base_url,
            token,
            "POST",
            "/api/control/runtime/reset",
            {
                "scope": "runtime-only",
                "confirm": "RESET_RUNTIME_STATE",
                "dryRun": True,
                "requireExternalStores": True,
                "reconcileTerminalOrphans": True,
            },
        )
        result["checks"].append({"name": "systemic-reset-dry-run", "ok": True, "status": reset.get("status")})
        result["status"] = "PASS"
    except Exception as exc:
        result["status"] = "FAIL"
        result["errorType"] = type(exc).__name__
        result["error"] = str(exc)
    write_json(output / "preflight.json", result)
    return result["status"] == "PASS"


def ensure_postgres_dsn_environment() -> None:
    if os.getenv("NATUREPROTECTOR_POSTGRES_DSN") or os.getenv("DATABASE_URL"):
        return
    host = os.getenv("POSTGRES_HOST", "").strip()
    port = os.getenv("POSTGRES_PORT", "5432").strip()
    database = os.getenv("POSTGRES_DB", "").strip()
    username = os.getenv("POSTGRES_USER", "").strip()
    password = os.getenv("POSTGRES_PASSWORD", "").strip()
    if not (host and port and database and username and password):
        return
    quoted_user = urllib.parse.quote(username, safe="")
    quoted_password = urllib.parse.quote(password, safe="")
    quoted_host = host.strip("[]")
    os.environ["NATUREPROTECTOR_POSTGRES_DSN"] = (
        f"postgresql://{quoted_user}:{quoted_password}@{quoted_host}:{port}/{database}"
    )


def discover_created(root: Path, before: set[Path]) -> Path | None:
    after = {path for path in root.iterdir() if path.is_dir()} if root.is_dir() else set()
    created = sorted(after - before, key=lambda path: path.name)
    return created[-1] if created else (sorted(after, key=lambda path: path.name)[-1] if after else None)


def register_screenshots(runner: Runner, python: str, repo: Path, phase13: Path, raw_root: Path, baseline_id: str, run_id: str) -> int:
    registrar = repo / "scripts/evidence/register-evidence-capture.py"
    count = 0
    page_map = {
        "run": "/simulation",
        "query": "/query",
        "comparison": "/comparison",
        "reset-blocked": "/admin",
        "reset-completed": "/admin",
    }
    for image in sorted(raw_root.rglob("*.png")):
        stem = image.stem.lower()
        source_page = next((page for key, page in page_map.items() if key in stem), "/")
        scenario = "scenario_c" if "missing" in stem else ("scenario_b" if "nominal" in stem else "")
        cmd = [
            python,
            str(registrar),
            "--image", str(image),
            "--evidence-root", str(phase13),
            "--capture-id", f"P13-{run_id}-{image.stem}",
            "--title", image.stem.replace("-", " ").title(),
            "--purpose", "Demonstrate the run-scoped local interface using evidence generated by the same Playwright flow.",
            "--chapter-target", "5,6",
            "--baseline-id", baseline_id,
            "--run-id", run_id,
            "--source-page", source_page,
            "--scenario", scenario,
            "--interpretation", "The capture shows the local experimental interface and its run context.",
            "--limitations", "No external operator validation; SimulationRunId remains in the captured page but is not inferred from the filename.",
        ]
        step = runner.run(f"register-screenshot-{count+1:02d}", cmd, required=False)
        if step.status == "PASS":
            count += 1
    return count


def tool_version(command: list[str], cwd: Path) -> str:
    try:
        result = subprocess.run(command, cwd=cwd, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=20)
        return (result.stdout or "").strip()[:500]
    except Exception as exc:
        return f"unavailable: {exc}"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--mode", choices=("plan", "quick", "full", "analyze"), default="plan")
    parser.add_argument("--baseline-id", required=True)
    parser.add_argument("--run-id", default=utc_stamp())
    parser.add_argument("--api-base-url", default="http://localhost:5254")
    parser.add_argument("--python", default=sys.executable)
    parser.add_argument("--pwsh", default="pwsh")
    parser.add_argument("--config", type=Path, default=Path("config/evidence/final-execution.json"))
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--continue-on-error", action="store_true")
    parser.add_argument("--use-existing-runtime", action="store_true")
    parser.add_argument("--skip-infrastructure", action="store_true")
    parser.add_argument("--keep-services-running", action="store_true")
    parser.add_argument("--skip-long-run", action="store_true")
    parser.add_argument("--skip-screenshots", action="store_true")
    parser.add_argument("--skip-final-portfolio", action="store_true")
    parser.add_argument("--allow-reviewed-commands", action="store_true")
    parser.add_argument("--acknowledge-non-production", action="store_true")
    parser.add_argument("--require-live", action="store_true")
    parser.add_argument("--preflight-only", action="store_true")
    parser.add_argument("--include-e2e", action="store_true")
    parser.add_argument("--bootstrap-iterations", type=int, default=500)
    parser.add_argument("--runtime-timeout-seconds", type=int, default=240)
    parser.add_argument("--input-phase8-root", type=Path)
    parser.add_argument("--input-portfolio-root", type=Path)
    parser.add_argument("--input-long-run-root", type=Path)
    parser.add_argument("--input-screenshots-root", type=Path)
    args = parser.parse_args()

    repo = args.repo.resolve()
    config_path = args.config if args.config.is_absolute() else repo / args.config
    config = read_json(config_path, {})
    if not config or config.get("phaseId") != "phase13":
        raise RuntimeError(f"Invalid Phase 13 config: {config_path}")
    if not (repo / "NatureProtector.sln").is_file():
        raise RuntimeError(f"Not a NatureProtector repository: {repo}")

    phase_root = repo / "artifacts/report-evidence" / args.baseline_id / "13-final-execution"
    output = phase_root / args.run_id
    output.mkdir(parents=True, exist_ok=True)
    work_root = repo / "artifacts" / "evidence-orchestration" / args.baseline_id / args.run_id
    work_root.mkdir(parents=True, exist_ok=True)
    runner = Runner(repo, work_root, args.resume, args.continue_on_error)
    write_json(
        output / "environment.json",
        {
            "schemaVersion": 2,
            "baselineId": args.baseline_id,
            "runId": args.run_id,
            "mode": args.mode,
            "capturedAtUtc": utc_iso(),
            "repo": str(repo),
            "safeEnvironment": safe_environment(),
            "tools": {
                "python": tool_version([args.python, "--version"], repo),
                "pwsh": tool_version([args.pwsh, "--version"], repo),
                "dotnet": tool_version(["dotnet", "--version"], repo),
                "docker": tool_version(["docker", "--version"], repo),
                "node": tool_version(["node", "--version"], repo),
                "npm": tool_version(["npm", "--version"], repo),
                "git": tool_version(["git", "rev-parse", "HEAD"], repo),
            },
        },
    )

    mode_cfg = config["modes"][args.mode]
    phase8_root: Path | None = args.input_phase8_root.resolve() if args.input_phase8_root else None
    portfolio_root: Path | None = args.input_portfolio_root.resolve() if args.input_portfolio_root else None
    long_run_root: Path | None = args.input_long_run_root.resolve() if args.input_long_run_root else None
    screenshots_root: Path | None = args.input_screenshots_root.resolve() if args.input_screenshots_root else None
    started_services = False
    execution_error: str | None = None

    try:
        if args.mode == "full" and not args.use_existing_runtime and not args.skip_infrastructure:
            runner.run("infrastructure-start", [args.pwsh, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(repo / "scripts/docker/Start-LocalInfrastructure.ps1")], timeout=600)
            runner.run("runtime-start", [args.pwsh, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(repo / "scripts/runtime/Start-LocalRuntime.ps1"), "-NoBrowser"], timeout=600)
            started_services = True
            if not wait_for_runtime(output, args.runtime_timeout_seconds):
                step = Step("runtime-readiness", "BLOCKED", "HTTP readiness polling", str(repo), utc_iso(), utc_iso(), float(args.runtime_timeout_seconds), None, safe_relative(output / "runtime-readiness.json", repo), "", "Runtime did not become ready")
                runner.steps.append(step)
                write_json(runner.state_path("runtime-readiness"), asdict(step))
                if not args.continue_on_error:
                    raise RuntimeError("Runtime did not become ready. See runtime-readiness.json")
        elif args.mode == "full":
            ready = wait_for_runtime(output, min(args.runtime_timeout_seconds, 30))
            step = Step("runtime-readiness", "PASS" if ready else "BLOCKED", "HTTP readiness polling", str(repo), utc_iso(), utc_iso(), 0.0, None, safe_relative(output / "runtime-readiness.json", repo), "", "" if ready else "Existing runtime is not ready")
            runner.steps.append(step)
            write_json(runner.state_path("runtime-readiness"), asdict(step))
            if not ready and not args.continue_on_error:
                raise RuntimeError("Existing runtime is not ready")

        if args.mode == "full" and args.require_live:
            ensure_postgres_dsn_environment()
            preflight_ok = run_preflight(output, args.api_base_url)
            if args.preflight_only:
                print(f"PHASE_13_OUTPUT={output}")
                print(f"PHASE_13_PREFLIGHT_STATUS={'PASS' if preflight_ok else 'FAIL'}")
                return 0 if preflight_ok else 1
            if not preflight_ok and not args.continue_on_error:
                raise RuntimeError(f"Live preflight failed. See {output / 'preflight.json'}")

        if args.mode != "analyze":
            phase8_root = repo / "artifacts/report-evidence" / args.baseline_id / "08-campaign" / args.run_id
            phase8_cmd = [
                args.python,
                str(repo / "scripts/evidence/run-report-evidence-campaign.py"),
                "--repo", str(repo),
                "--baseline-id", args.baseline_id,
                "--run-id", args.run_id,
                "--profile", str(mode_cfg["phase8Profile"]),
                "--api-base-url", args.api_base_url,
                "--np-score-bootstrap-iterations", str(args.bootstrap_iterations),
            ]
            if mode_cfg.get("executePhase8"):
                phase8_cmd.append("--execute")
            if args.continue_on_error:
                phase8_cmd.append("--continue-on-error")
            if args.include_e2e:
                phase8_cmd.append("--include-e2e")
            if args.mode == "full":
                phase8_cmd += ["--require-live-runtime", "--require-live-database", "--run-http", "--http-profile", "B1", "--require-http", "--run-microbenchmarks", "--benchmark-profile", "B1", "--require-microbenchmarks"]
                if args.acknowledge_non_production:
                    phase8_cmd += ["--execute-p3", "--acknowledge-non-production", "--p3-run-label", f"phase13-{args.run_id}"]
            runner.run("phase8-campaign", phase8_cmd, timeout=7200, required=True)
            if phase8_root.is_dir():
                runner.run("phase8-verify", [args.python, str(repo / "scripts/evidence/verify-report-evidence-campaign.py"), str(phase8_root)], required=True)

        portfolio_mode = str(mode_cfg.get("runFinalPortfolio", "none"))
        if not args.skip_final_portfolio and portfolio_mode != "none" and args.mode != "analyze":
            portfolio_parent = output / "final-portfolio"
            portfolio_parent.mkdir(parents=True, exist_ok=True)
            before = {path for path in portfolio_parent.iterdir() if path.is_dir()}
            portfolio_cmd = [
                args.python,
                str(repo / "scripts/evidence/run-final-evidence-campaign.py"),
                "--repo", str(repo),
                "--config", str(repo / config["canonicalInputs"]["finalPortfolioConfig"]),
                "--output-root", str(portfolio_parent),
                "--mode", portfolio_mode,
                "--api-base-url", args.api_base_url,
            ]
            if args.allow_reviewed_commands or (args.mode == "full" and args.require_live):
                portfolio_cmd.append("--allow-commands")
            runner.run("final-portfolio", portfolio_cmd, timeout=7200, required=args.mode == "full")
            portfolio_root = discover_created(portfolio_parent, before)
            if portfolio_root:
                verify_cmd = [args.python, str(repo / "scripts/evidence/verify-final-evidence-campaign.py"), str(portfolio_root)]
                if args.require_live:
                    verify_cmd.append("--require-live")
                runner.run("final-portfolio-verify", verify_cmd, required=args.mode == "full")

        if args.mode == "full" and mode_cfg.get("runLongRun") and not args.skip_long_run:
            long_run_root = output / "long-run"
            runner.run(
                "long-run",
                [
                    args.python,
                    str(repo / config["canonicalInputs"]["longRunRunner"]),
                    "--base-url", args.api_base_url,
                    "--matrix", str(repo / config["canonicalInputs"]["longRunMatrix"]),
                    "--output", str(long_run_root),
                ],
                timeout=3600,
                required=True,
            )
            runner.run(
                "long-run-verify",
                [
                    args.python,
                    str(repo / config["canonicalInputs"]["longRunVerifier"]),
                    str(long_run_root),
                    "--matrix", str(repo / config["canonicalInputs"]["longRunMatrix"]),
                ],
                required=True,
            )

        if args.mode == "full" and mode_cfg.get("runScreenshots") and not args.skip_screenshots:
            screenshots_root = output / "screenshots"
            raw_root = screenshots_root / "raw"
            raw_root.mkdir(parents=True, exist_ok=True)
            for profile in config["screenshots"]["profiles"]:
                profile_root = raw_root / profile
                profile_root.mkdir(parents=True, exist_ok=True)
                env = {
                    "LIVE_RUNTIME": "1",
                    "LIVE_PROFILE": profile,
                    "NP_EVIDENCE_RUN_ID": args.run_id,
                    "UI_REVISION_SCREENSHOTS": str(profile_root),
                    "UI_REVISION_RUNS": str(screenshots_root / "playwright" / profile),
                }
                runner.run(
                    f"screenshots-{profile}",
                    [
                        "npm", "run", "test:e2e", "--",
                        "--project", str(config["screenshots"]["project"]),
                        "--grep", str(config["screenshots"]["testPattern"]),
                    ],
                    cwd=repo / "webUI",
                    env=env,
                    timeout=900,
                    required=False,
                )
            register_screenshots(runner, args.python, repo, screenshots_root, raw_root, args.baseline_id, args.run_id)

    except Exception as exc:
        execution_error = f"{type(exc).__name__}: {exc}"
        write_json(work_root / "execution-error.json", {"capturedAtUtc": utc_iso(), "error": execution_error})
    finally:
        runner.ledger(work_root / "command-ledger.csv")
        if started_services and not args.keep_services_running:
            runner.run("runtime-stop", [args.pwsh, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(repo / "scripts/runtime/Stop-LocalRuntime.ps1")], required=False, timeout=120)
            runner.run("infrastructure-stop", [args.pwsh, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(repo / "infra/scripts/down.ps1")], required=False, timeout=300)
            runner.ledger(work_root / "command-ledger.csv")

    collect_cmd = [
        args.python,
        str(repo / "scripts/evidence/final/collect_final_execution.py"),
        "--repo", str(repo),
        "--baseline-id", args.baseline_id,
        "--run-id", args.run_id,
        "--mode", args.mode,
        "--output", str(output),
        "--command-ledger", str(work_root / "command-ledger.csv"),
    ]
    if phase8_root:
        collect_cmd += ["--phase8-root", str(phase8_root)]
    if portfolio_root:
        collect_cmd += ["--portfolio-root", str(portfolio_root)]
    if long_run_root:
        collect_cmd += ["--long-run-root", str(long_run_root)]
    if screenshots_root:
        collect_cmd += ["--screenshots-root", str(screenshots_root)]
    if args.require_live:
        collect_cmd.append("--require-live")
    runner.run("phase13-collect", collect_cmd, required=False)
    verify_cmd = [args.python, str(repo / "scripts/evidence/final/verify_final_execution.py"), str(output)]
    if args.require_live:
        verify_cmd.append("--require-live")
    runner.run("phase13-verify", verify_cmd, required=False)
    runner.ledger(work_root / "command-ledger.csv")

    # Phase 10 is deliberately last so its cross-phase scan includes Phase 13.
    phase10 = repo / "artifacts/report-evidence" / args.baseline_id / "10-evidence-intelligence" / args.run_id
    runner.run(
        "phase10-refresh",
        [
            args.python,
            str(repo / config["canonicalInputs"]["phase10Collector"]),
            "--repo", str(repo),
            "--baseline-id", args.baseline_id,
            "--run-id", args.run_id,
            "--output", str(phase10),
            "--overwrite",
        ],
        required=False,
        timeout=900,
    )
    if phase10.is_dir():
        runner.run("phase10-verify", [args.python, str(repo / config["canonicalInputs"]["phase10Verifier"]), str(phase10)], required=False)
    runner.ledger(work_root / "command-ledger.csv")

    write_json(work_root / "orchestration-summary.json", {"baselineId": args.baseline_id, "runId": args.run_id, "phase13Output": safe_relative(output, repo), "phase10Output": safe_relative(phase10, repo), "executionError": execution_error, "steps": [asdict(step) for step in runner.steps], "finishedAtUtc": utc_iso()})
    summary = read_json(output / "phase13-summary.json", {})
    status = str(summary.get("status", "FAIL"))
    print(f"PHASE_13_OUTPUT={output}")
    print(f"PHASE_13_STATUS={status}")
    if execution_error:
        print(f"PHASE_13_EXECUTION_ERROR={execution_error}")
    return 1 if status == "FAIL" else 0


if __name__ == "__main__":
    raise SystemExit(main())
