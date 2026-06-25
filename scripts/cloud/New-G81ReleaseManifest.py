#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser(description="Create the G8.1 build-once release manifest.")
    parser.add_argument("--images", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--build-run-id", required=True, type=int)
    parser.add_argument("--platform-project", required=True)
    parser.add_argument("--engineering-run-id", required=True, type=int)
    parser.add_argument("--security-run-id", required=True, type=int)
    parser.add_argument("--policy-run-id", required=True, type=int)
    args = parser.parse_args()

    images = json.loads(args.images.read_text(encoding="utf-8"))
    manifest = {
        "schema_version": 1,
        "repository": args.repository,
        "source_commit": args.commit,
        "build_run_id": args.build_run_id,
        "generated_at": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "images": images,
        "quality_gates": [
            {"name": "Engineering foundations", "run_id": args.engineering_run_id, "conclusion": "success"},
            {"name": "Security", "run_id": args.security_run_id, "conclusion": "success"},
            {"name": "G8.1 cloud production policy", "run_id": args.policy_run_id, "conclusion": "success"},
        ],
        "delivery": {
            "platform_project": args.platform_project,
            "region": "europe-southwest1",
            "pipelines": [
                "natureprotector-api",
                "natureprotector-frontend",
                "natureprotector-prevention",
            ],
        },
        "production_authorized": False,
        "production_deployed": False,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
