#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path

from g82_common import assert_commit, read_json, validate_schema, write_json


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--expected-run-id", required=True, type=int)
    parser.add_argument("--expected-workflow-path", required=True)
    parser.add_argument("--expected-source-commit", required=True)
    parser.add_argument("--expected-branch", required=True)
    parser.add_argument("--expected-repository", default="MAlves4018/NatureProtector")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    raw = read_json(args.input)
    repository = (raw.get("repository") or {}).get("full_name")
    actor = (raw.get("actor") or {}).get("login")
    expected_commit = assert_commit(args.expected_source_commit)
    checks = {
        "run_id": raw.get("id") == args.expected_run_id,
        "workflow_path": raw.get("path") == args.expected_workflow_path,
        "source_commit": raw.get("head_sha") == expected_commit,
        "branch": raw.get("head_branch") == args.expected_branch,
        "repository": repository == args.expected_repository,
        "event": raw.get("event") == "workflow_dispatch",
        "completed_successfully": raw.get("status") == "completed" and raw.get("conclusion") == "success",
        "actor": isinstance(actor, str) and bool(actor),
    }
    if not all(checks.values()):
        failures = [name for name, passed in checks.items() if not passed]
        raise SystemExit("run metadata validation failed: " + ", ".join(failures))
    output = {
        "schema_version": 2,
        "phase": "G8.2",
        "run_id": raw["id"],
        "run_attempt": raw.get("run_attempt", 1),
        "repository": repository,
        "workflow_path": raw["path"],
        "head_sha": raw["head_sha"],
        "head_branch": raw["head_branch"],
        "event": raw["event"],
        "status": raw["status"],
        "conclusion": raw["conclusion"],
        "actor": actor,
        "created_at": raw["created_at"],
        "updated_at": raw["updated_at"],
        "validated": True,
    }
    validate_schema(output, "g8-2-run-metadata.schema.json")
    write_json(args.output, output)
    print(args.output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
