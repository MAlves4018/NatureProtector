#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts/cloud/Build-G81Release.sh"

errors: list[str] = []
checks = 0


def check(condition: bool, message: str) -> None:
    global checks
    checks += 1
    if not condition:
        errors.append(message)


def resolve_bash() -> str | None:
    for candidate in (
        Path("C:/Program Files/Git/bin/bash.exe"),
        Path("C:/Program Files/Git/usr/bin/bash.exe"),
    ):
        if candidate.is_file():
            return str(candidate)
    path = shutil.which("bash")
    if path:
        return path
    return None


def run_bash(command: str) -> subprocess.CompletedProcess[str]:
    bash = resolve_bash()
    if bash is None:
        return subprocess.CompletedProcess(command, 127, "", "bash not found")
    env = os.environ.copy()
    env.pop("GITHUB_RUN_ATTEMPT", None)
    return subprocess.run(
        [bash, "-lc", command],
        cwd=ROOT,
        env=env,
        capture_output=True,
        text=True,
        check=False,
    )


def assert_stdout(command: str, expected: str, message: str) -> None:
    result = run_bash(command)
    check(result.returncode == 0, f"{message}:exit:{result.returncode}:{result.stderr.strip()}")
    check(result.stdout.strip() == expected, f"{message}:stdout:{result.stdout.strip()}")


def assert_invalid(command: str, message: str) -> None:
    result = run_bash(command)
    check(result.returncode != 0, f"{message}:accepted")
    check("Invalid GitHub run attempt" in result.stderr, f"{message}:missing-error")


source = "source scripts/cloud/Build-G81Release.sh"
assert_stdout(
    f"{source}; unset GITHUB_RUN_ATTEMPT; resolve_release_run_attempt",
    "1",
    "unset-attempt-defaults-to-one",
)
assert_stdout(
    f"{source}; GITHUB_RUN_ATTEMPT=2; resolve_release_run_attempt",
    "2",
    "attempt-two-is-preserved",
)
assert_stdout(
    f"{source}; GITHUB_RUN_ATTEMPT=2; attempt=$(resolve_release_run_attempt); "
    'build_release_tag "abc123" "28398458582" "$attempt"',
    "git-abc123-run-28398458582-attempt-2",
    "tag-includes-sha-run-id-and-attempt",
)
for value, name in [
    ("0", "zero-attempt-rejected"),
    ("-1", "negative-attempt-rejected"),
    ("", "empty-attempt-rejected"),
    ("abc", "text-attempt-rejected"),
]:
    assert_invalid(f"{source}; GITHUB_RUN_ATTEMPT='{value}'; resolve_release_run_attempt", name)

text = SCRIPT.read_text(encoding="utf-8")
check('tag="git-${GITHUB_SHA}-run-${GITHUB_RUN_ID}"' not in text, "old-run-id-only-tag-formula-present")
check("attempt-%s" in text, "attempt-suffix-missing")
check('reference="${registry}/${component}@${digest}"' in text, "final-reference-not-digest-bound")
check('cosign sign --yes "$reference"' in text, "cosign-signing-not-digest-bound")
check("--immutable-tags=false" not in text.lower(), "tag-immutability-disabled")
check("delete-tag" not in text.lower(), "tag-deletion-present")

payload = {
    "phase": "BUILD_G81_RELEASE_STATIC",
    "status": "PASS" if not errors else "FAIL",
    "checks_total": checks,
    "checks_failed": len(errors),
    "errors": errors,
    "cloud_mutation": False,
}
print(json.dumps(payload, indent=2))
sys.exit(1 if errors else 0)
