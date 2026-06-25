#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts/cloud/Invoke-G102ProjectBootstrap.ps1"
CONFIRMATION = "CREATE_EMPTY_NATUREPROTECTOR_PROJECTS_AND_LINK_APPROVED_BILLING"
BILLING_ACCOUNT = "0109B8-93144E-B93C1C"


def write_input(path: Path) -> None:
    path.write_text(
        json.dumps(
            {
                "schema_version": 1,
                "repository": "MAlves4018/NatureProtector",
                "repository_id": "1197705051",
                "repository_owner_id": "115478577",
                "default_branch": "master",
                "billing_account_id": BILLING_ACCOUNT,
                "platform_project_id": "np-platform-migkxl-20260624",
                "staging_project_id": "np-staging-migkxl-20260624",
                "production_project_id": "np-production-migkxl-20260624",
                "primary_region": "europe-southwest1",
                "terraform_state_bucket_name": "np-tfstate-migkxl-20260624",
                "evidence_bucket_name": "np-evidence-migkxl-20260624",
                "expected_gcloud_account": "migkxl@gmail.com",
                "qualification_window": {
                    "starts_at": "2026-06-25T09:00:00+02:00",
                    "ends_at": "2026-06-25T18:00:00+02:00",
                    "teardown_owner": "Miguel Alves",
                },
                "cost_guardrails": {
                    "observed_credit_usd": 15.64,
                    "minimum_credit_to_preserve_usd": 8.0,
                    "staging_budget_amount": 1.0,
                    "budget_currency": "EUR",
                    "budget_is_hard_cap": False,
                },
                "execution": {
                    "create_projects": True,
                    "link_billing": True,
                    "create_state_foundation": False,
                    "create_delivery_control_plane": False,
                    "create_data_plane": False,
                    "create_edge": False,
                    "materialize_generated_secrets": False,
                },
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )


def write_fake_gcloud(fake_dir: Path, state_path: Path, calls_path: Path) -> None:
    fake_py = fake_dir / "fake_gcloud.py"
    fake_py.write_text(
        f"""
import json
import sys
from pathlib import Path

STATE = Path({str(state_path)!r})
CALLS = Path({str(calls_path)!r})
BILLING = {BILLING_ACCOUNT!r}

def load():
    return json.loads(STATE.read_text(encoding="utf-8"))

def save(state):
    STATE.write_text(json.dumps(state, indent=2) + "\\n", encoding="utf-8")

def out(value):
    print(json.dumps(value))

args = sys.argv[1:]
with CALLS.open("a", encoding="utf-8") as handle:
    handle.write(json.dumps(args) + "\\n")
state = load()

if args[:2] == ["auth", "list"]:
    print("migkxl@gmail.com")
    raise SystemExit(0)

if args[:3] == ["billing", "accounts", "describe"]:
    out({{"name": "billingAccounts/" + BILLING, "open": True}})
    raise SystemExit(0)

if args[:2] == ["projects", "list"]:
    project_id = ""
    for index, arg in enumerate(args):
        if arg.startswith("--filter=projectId="):
            project_id = arg.split("projectId=", 1)[1]
        elif arg == "--filter" and index + 1 < len(args) and args[index + 1].startswith("projectId="):
            project_id = args[index + 1].split("=", 1)[1]
    if not project_id:
        joined = " ".join(args)
        for candidate in state["projects"]:
            if candidate in joined:
                project_id = candidate
                break
    if project_id in state["projects"]:
        print(project_id)
    raise SystemExit(0)

if args[:2] == ["projects", "create"]:
    project_id = args[2]
    if project_id in state["projects"]:
        print("ALREADY_EXISTS", file=sys.stderr)
        raise SystemExit(1)
    if project_id in state.get("reserved", []):
        print("ALREADY_EXISTS", file=sys.stderr)
        raise SystemExit(1)
    state["projects"][project_id] = {{
        "projectId": project_id,
        "projectNumber": str(len(state["projects"]) + 1000),
        "lifecycleState": "ACTIVE",
        "billingEnabled": False,
        "billingAccountName": "",
    }}
    save(state)
    raise SystemExit(0)

if args[:2] == ["projects", "describe"]:
    project_id = args[2]
    project = state["projects"].get(project_id)
    if project is None:
        print("not found", file=sys.stderr)
        raise SystemExit(1)
    out({{
        "projectId": project_id,
        "projectNumber": project["projectNumber"],
        "lifecycleState": project["lifecycleState"],
    }})
    raise SystemExit(0)

if args[:3] == ["billing", "projects", "describe"]:
    project_id = args[3]
    project = state["projects"].get(project_id)
    if project is None:
        print("not found", file=sys.stderr)
        raise SystemExit(1)
    out({{
        "projectId": project_id,
        "billingEnabled": project["billingEnabled"],
        "billingAccountName": project["billingAccountName"],
    }})
    raise SystemExit(0)

if args[:3] == ["billing", "projects", "link"]:
    project_id = args[3]
    project = state["projects"].get(project_id)
    if project is None:
        print("not found", file=sys.stderr)
        raise SystemExit(1)
    if project["billingEnabled"] and project["billingAccountName"] == "billingAccounts/" + BILLING:
        print("duplicate billing link must not be called", file=sys.stderr)
        raise SystemExit(7)
    account_arg = next((arg for arg in args if arg.startswith("--billing-account=")), "")
    project["billingEnabled"] = True
    project["billingAccountName"] = "billingAccounts/" + account_arg.split("=", 1)[1]
    save(state)
    raise SystemExit(0)

print("unsupported gcloud call: " + " ".join(args), file=sys.stderr)
raise SystemExit(2)
""".lstrip(),
        encoding="utf-8",
    )
    (fake_dir / "gcloud.ps1").write_text(
        f'& "{sys.executable}" "{fake_py}" @args\nexit $LASTEXITCODE\n',
        encoding="utf-8",
    )


def run_bootstrap(work: Path, state: dict) -> tuple[subprocess.CompletedProcess[str], dict, list[list[str]]]:
    state_path = work / "state.json"
    calls_path = work / "calls.jsonl"
    input_path = work / "input.json"
    evidence = work / "evidence"
    fake_dir = work / "fake-bin"
    fake_dir.mkdir()
    state_path.write_text(json.dumps(state, indent=2) + "\n", encoding="utf-8")
    calls_path.write_text("", encoding="utf-8")
    write_input(input_path)
    write_fake_gcloud(fake_dir, state_path, calls_path)
    shell = "pwsh" if os.name != "nt" else "pwsh"
    env = os.environ.copy()
    env["PATH"] = str(fake_dir) + os.pathsep + env.get("PATH", "")
    validation_python = env.get("NATUREPROTECTOR_VALIDATION_PYTHON", sys.executable)
    cmd = [
        shell,
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(SCRIPT),
        "-InputPath",
        str(input_path),
        "-Confirmation",
        CONFIRMATION,
        "-EvidenceDirectory",
        str(evidence),
        "-PythonExecutable",
        validation_python,
        "-Execute",
    ]
    result = subprocess.run(cmd, cwd=ROOT, env=env, text=True, capture_output=True, timeout=120)
    summary_path = evidence / "project-bootstrap-summary.json"
    summary = json.loads(summary_path.read_text(encoding="utf-8")) if summary_path.exists() else {}
    calls = [json.loads(line) for line in calls_path.read_text(encoding="utf-8").splitlines() if line.strip()]
    return result, summary, calls


def initial_state(billing_name: str = f"billingAccounts/{BILLING_ACCOUNT}") -> dict:
    return {
        "projects": {
            "np-platform-migkxl-20260624": {
                "projectId": "np-platform-migkxl-20260624",
                "projectNumber": "915502172228",
                "lifecycleState": "ACTIVE",
                "billingEnabled": True,
                "billingAccountName": billing_name,
            }
        }
    }


def all_projects_with_platform_billing_disabled() -> dict:
    state = {"projects": {}}
    for role, number in (
        ("platform", "915502172228"),
        ("staging", "915502172229"),
        ("production", "915502172230"),
    ):
        project_id = f"np-{role}-migkxl-20260624"
        state["projects"][project_id] = {
            "projectId": project_id,
            "projectNumber": number,
            "lifecycleState": "ACTIVE",
            "billingEnabled": role != "platform",
            "billingAccountName": "" if role == "platform" else f"billingAccounts/{BILLING_ACCOUNT}",
        }
    return state


def assert_success(result: subprocess.CompletedProcess[str], calls: list[list[str]]) -> None:
    if result.returncode != 0:
        raise AssertionError(result.stdout + "\n" + result.stderr + "\nCALLS=" + json.dumps(calls, indent=2))


def main() -> int:
    with tempfile.TemporaryDirectory(prefix="np-g102-idempotency-") as tmp:
        first_dir = Path(tmp) / "first"
        first_dir.mkdir()
        result, summary, calls = run_bootstrap(first_dir, initial_state())
        assert_success(result, calls)
        link_calls = [call for call in calls if call[:3] == ["billing", "projects", "link"]]
        linked_projects = [call[3] for call in link_calls]
        assert "np-platform-migkxl-20260624" not in linked_projects
        assert set(linked_projects) == {"np-staging-migkxl-20260624", "np-production-migkxl-20260624"}
        assert summary["projects_created"] == 2
        assert summary["billing_links_created"] == 2
        platform = next(project for project in summary["projects"] if project["role"] == "platform")
        assert platform["billing_action"] == "NO_OP_ALREADY_COMPLIANT"

        second_dir = Path(tmp) / "second"
        second_dir.mkdir()
        state_after_first = json.loads((first_dir / "state.json").read_text(encoding="utf-8"))
        result, summary, calls = run_bootstrap(second_dir, state_after_first)
        assert_success(result, calls)
        link_calls = [call for call in calls if call[:3] == ["billing", "projects", "link"]]
        assert link_calls == []
        assert summary["projects_created"] == 0
        assert summary["billing_links_created"] == 0

        wrong_dir = Path(tmp) / "wrong-billing"
        wrong_dir.mkdir()
        result, summary, calls = run_bootstrap(wrong_dir, initial_state("billingAccounts/WRONG"))
        assert result.returncode != 0
        link_calls = [call for call in calls if call[:3] == ["billing", "projects", "link"]]
        assert link_calls == []
        assert "unexpected billing account" in summary.get("error", "")

        absent_dir = Path(tmp) / "absent-billing"
        absent_dir.mkdir()
        result, summary, calls = run_bootstrap(absent_dir, all_projects_with_platform_billing_disabled())
        assert_success(result, calls)
        link_calls = [call for call in calls if call[:3] == ["billing", "projects", "link"]]
        assert [call[3] for call in link_calls] == ["np-platform-migkxl-20260624"]
        platform = next(project for project in summary["projects"] if project["role"] == "platform")
        assert platform["billing_action"] == "LINK_BILLING"

        unavailable_dir = Path(tmp) / "unavailable-id"
        unavailable_dir.mkdir()
        result, summary, calls = run_bootstrap(unavailable_dir, {"projects": {}, "reserved": ["np-platform-migkxl-20260624"]})
        assert result.returncode != 0
        create_calls = [call for call in calls if call[:2] == ["projects", "create"]]
        assert create_calls == [["projects", "create", "np-platform-migkxl-20260624", "--name=NatureProtector Platform", "--quiet"]]
        assert summary["projects_created"] == 0

    print(
        json.dumps(
            {
                "phase": "G10.2_PROJECT_BOOTSTRAP_IDEMPOTENCY",
                "status": "PASS",
                "cloud_mutations": False,
                "scenarios": [
                    "existing compliant platform continues to staging and production",
                    "second execution performs no duplicate billing links",
                    "wrong billing account blocks without relink",
                    "existing project without billing links and verifies",
                    "unavailable project ID blocks without alternate ID",
                ],
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
