#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path

from g82_common import (
    assert_commit,
    assert_qualification_id,
    assert_sha256,
    file_entries,
    iso_utc,
    tree_digest,
    validate_schema,
    write_json,
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--qualification-id", required=True)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--candidate-manifest-sha256", required=True)
    parser.add_argument("--root-label", default="g82-qualification-evidence")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    output = Path(args.output).resolve()
    if output == root or root in output.parents:
        raise SystemExit("evidence index must be written outside the indexed evidence root")
    entries = file_entries(root)
    if not entries:
        raise SystemExit("no evidence files found")
    document = {
        "schema_version": 2,
        "phase": "G8.2",
        "qualification_id": assert_qualification_id(args.qualification_id),
        "source_commit": assert_commit(args.source_commit),
        "candidate_manifest_sha256": assert_sha256(
            args.candidate_manifest_sha256, field="candidate_manifest_sha256"
        ),
        "generated_at": iso_utc(),
        "root_label": args.root_label,
        "file_count": len(entries),
        "files": entries,
        "tree_sha256": tree_digest(entries),
        "production_authorized": False,
        "production_deployed": False,
    }
    validate_schema(document, "g8-2-evidence-index.schema.json")
    write_json(output, document)
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
