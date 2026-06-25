#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path

from g82_common import (
    file_entries,
    read_json,
    tree_digest,
    validate_schema,
    write_json,
)


def verify(root: Path, index_path: Path) -> dict:
    document = read_json(index_path)
    validate_schema(document, "g8-2-evidence-index.schema.json")
    actual = file_entries(root)
    expected_by_path = {entry["path"]: entry for entry in document["files"]}
    actual_by_path = {entry["path"]: entry for entry in actual}
    errors: list[str] = []

    expected_paths = set(expected_by_path)
    actual_paths = set(actual_by_path)
    for path in sorted(expected_paths - actual_paths):
        errors.append(f"missing:{path}")
    for path in sorted(actual_paths - expected_paths):
        errors.append(f"extra:{path}")
    for path in sorted(expected_paths & actual_paths):
        expected = expected_by_path[path]
        observed = actual_by_path[path]
        for field in ("sha256", "size_bytes", "media_type"):
            if observed[field] != expected[field]:
                errors.append(f"{field}:{path}")

    if document["file_count"] != len(document["files"]):
        errors.append("declared-file-count")
    if len(expected_by_path) != len(document["files"]):
        errors.append("duplicate-index-path")
    if tree_digest(document["files"]) != document["tree_sha256"]:
        errors.append("index-tree-digest")
    if tree_digest(actual) != document["tree_sha256"]:
        errors.append("actual-tree-digest")

    return {
        "schema_version": 2,
        "phase": "G8.2",
        "qualification_id": document["qualification_id"],
        "status": "passed" if not errors else "failed",
        "files_verified": len(actual),
        "tree_sha256": tree_digest(actual) if actual else "",
        "errors": errors,
        "production_authorized": False,
        "production_deployed": False,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    parser.add_argument("--index", required=True)
    parser.add_argument("--output")
    args = parser.parse_args()
    result = verify(Path(args.root).resolve(), Path(args.index).resolve())
    if args.output:
        write_json(args.output, result)
    import json

    print(json.dumps(result, indent=2))
    return 0 if result["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
