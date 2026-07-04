#!/usr/bin/env python3
"""Run progressive repository quality gates and emit deterministic evidence."""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def utc_now() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def resolve_executable(name: str) -> str | None:
    if name == "python":
        return sys.executable
    return shutil.which(name)


def command_for_execution(command: list[str], executable: str | None) -> list[str]:
    if command and command[0] == "python":
        return [sys.executable, *command[1:]]
    if command and executable is not None:
        return [executable, *command[1:]]
    return command


def write_markdown(path: Path, payload: dict[str, Any]) -> None:
    lines = [
        "# NatureProtector quality guardrails",
        "",
        f"- Mode: `{payload['mode']}`",
        f"- Status: `{payload['status']}`",
        f"- Generated: `{payload['generated_at_utc']}`",
        "",
        "| Gate | Rollout | Status | Exit | Duration (s) |",
        "|---|---|---|---:|---:|",
    ]
    for result in payload["results"]:
        lines.append(
            f"| `{result['id']}` | `{result['rollout']}` | `{result['status']}` | "
            f"{result.get('exit_code', '')} | {result.get('duration_seconds', 0):.3f} |"
        )
    lines.extend(["", "Detailed stdout and stderr are stored beside this summary.", ""])
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=".")
    parser.add_argument("--config", default="config/quality/quality-gates.json")
    parser.add_argument("--mode", choices=("report", "enforce"))
    parser.add_argument("--output-dir", default="artifacts/quality")
    parser.add_argument("--only", action="append", default=[])
    args = parser.parse_args()

    repo = Path(args.repo).resolve()
    config_path = repo / args.config
    config = json.loads(config_path.read_text(encoding="utf-8"))
    mode = args.mode or config["default_mode"]
    output_dir = (repo / args.output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    selected = set(args.only)

    results: list[dict[str, Any]] = []
    blocking_failure = False
    for gate in config["gates"]:
        gate_id = gate["id"]
        if selected and gate_id not in selected:
            continue
        command = list(gate["command"])
        executable = resolve_executable(command[0])
        result: dict[str, Any] = {
            "id": gate_id,
            "description": gate["description"],
            "rollout": gate["rollout"],
            "command": command,
            "cwd": gate["cwd"],
        }
        if executable is None:
            result.update(status="BLOCKED_MISSING_TOOL", missing_tool=command[0], exit_code=None, duration_seconds=0.0)
        else:
            import time

            started = time.monotonic()
            completed = subprocess.run(
                command_for_execution(command, executable),
                cwd=repo / gate["cwd"],
                text=True,
                encoding="utf-8",
                errors="replace",
                capture_output=True,
                check=False,
                env={**os.environ, "NP_QUALITY_MODE": mode},
            )
            duration = time.monotonic() - started
            result.update(
                status="PASS" if completed.returncode == 0 else "FINDINGS",
                exit_code=completed.returncode,
                duration_seconds=round(duration, 3),
            )
            (output_dir / f"{gate_id}.stdout.txt").write_text(completed.stdout or "", encoding="utf-8")
            (output_dir / f"{gate_id}.stderr.txt").write_text(completed.stderr or "", encoding="utf-8")
        if mode == "enforce" and gate["rollout"] == "enforce" and result["status"] != "PASS":
            blocking_failure = True
        results.append(result)

    payload = {
        "schema_version": 1,
        "generated_at_utc": utc_now(),
        "mode": mode,
        "status": "FAIL" if blocking_failure else "PASS",
        "results": results,
        "summary": {
            "total": len(results),
            "passed": sum(result["status"] == "PASS" for result in results),
            "findings": sum(result["status"] == "FINDINGS" for result in results),
            "blocked": sum(result["status"] == "BLOCKED_MISSING_TOOL" for result in results),
            "enforced_failures": sum(
                mode == "enforce" and result["rollout"] == "enforce" and result["status"] != "PASS"
                for result in results
            ),
        },
    }
    (output_dir / "summary.json").write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    write_markdown(output_dir / "summary.md", payload)
    print(json.dumps(payload, indent=2, sort_keys=True))
    return 1 if blocking_failure else 0


if __name__ == "__main__":
    raise SystemExit(main())
