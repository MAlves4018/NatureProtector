#!/usr/bin/env python3
"""Report a bounded workflow status to the NatureProtector operations API.

The callback URL and secret are read only from process environment. If either is
missing the reporter records a truthful SKIPPED_UNCONFIGURED receipt and exits
successfully, so operations workflows remain usable before callback plumbing is
configured.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any

ALLOWED_STATUSES = {"Queued", "Running", "Succeeded", "Failed", "Cancelled", "RolledBack"}


def aggregate_artifact(root: Path, name: str, kind: str, reference: str) -> dict[str, Any] | None:
    if not root.exists():
        return None
    files = sorted(path for path in root.rglob("*") if path.is_file()) if root.is_dir() else [root]
    if not files:
        return None
    digest = hashlib.sha256()
    total_size = 0
    for path in files:
        relative = path.relative_to(root).as_posix() if root.is_dir() else path.name
        file_digest = hashlib.sha256(path.read_bytes()).hexdigest()
        size = path.stat().st_size
        total_size += size
        digest.update(relative.encode("utf-8"))
        digest.update(b"\0")
        digest.update(file_digest.encode("ascii"))
        digest.update(b"\0")
        digest.update(str(size).encode("ascii"))
        digest.update(b"\n")
    return {
        "artifactId": f"aggregate-{digest.hexdigest()[:20]}",
        "name": name,
        "kind": kind,
        "reference": reference,
        "sha256": digest.hexdigest(),
        "sizeBytes": total_size,
        "evidenceLevel": "HASHED_WORKFLOW_OUTPUT",
    }


def validate_url(value: str) -> str:
    parsed = urllib.parse.urlparse(value)
    if parsed.scheme != "https" and parsed.hostname not in {"localhost", "127.0.0.1", "::1"}:
        raise ValueError("Callback URL must use HTTPS, except for loopback development endpoints.")
    if not parsed.netloc:
        raise ValueError("Callback URL must be absolute.")
    return value


def write_receipt(path: Path | None, payload: dict[str, Any]) -> None:
    rendered = json.dumps(payload, indent=2, sort_keys=True) + "\n"
    if path:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(rendered, encoding="utf-8")
    print(rendered, end="")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--operation-id", required=True)
    parser.add_argument("--status", required=True, choices=sorted(ALLOWED_STATUSES))
    parser.add_argument("--provider-reference")
    parser.add_argument("--detail")
    parser.add_argument("--artifact-root")
    parser.add_argument("--artifact-name", default="workflow-output")
    parser.add_argument("--artifact-kind", default="evidence-package")
    parser.add_argument("--artifact-reference")
    parser.add_argument("--receipt")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    callback_url = os.environ.get("NP_OPERATIONS_CALLBACK_URL", "").strip()
    callback_secret = os.environ.get("NP_OPERATIONS_CALLBACK_SECRET", "").strip()
    artifacts: list[dict[str, Any]] = []
    if args.artifact_root:
        artifact = aggregate_artifact(
            Path(args.artifact_root),
            args.artifact_name,
            args.artifact_kind,
            args.artifact_reference or args.provider_reference or "unresolved-provider-reference",
        )
        if artifact:
            artifacts.append(artifact)

    request_payload: dict[str, Any] = {
        "operationId": args.operation_id,
        "status": args.status,
        "providerReference": args.provider_reference,
        "artifacts": artifacts,
        "detail": args.detail,
    }
    receipt_path = Path(args.receipt) if args.receipt else None

    if args.dry_run:
        write_receipt(receipt_path, {"reportStatus": "DRY_RUN", "request": request_payload})
        return 0

    if not callback_url or not callback_secret:
        write_receipt(
            receipt_path,
            {
                "reportStatus": "SKIPPED_UNCONFIGURED",
                "reason": "NP_OPERATIONS_CALLBACK_URL and NP_OPERATIONS_CALLBACK_SECRET are required.",
                "request": request_payload,
            },
        )
        return 0

    try:
        endpoint = validate_url(callback_url)
    except ValueError as error:
        write_receipt(receipt_path, {"reportStatus": "REJECTED_CONFIGURATION", "reason": str(error)})
        return 2

    body = json.dumps(request_payload, separators=(",", ":")).encode("utf-8")
    request = urllib.request.Request(
        endpoint,
        data=body,
        method="POST",
        headers={
            "Content-Type": "application/json",
            "Accept": "application/json",
            "User-Agent": "NatureProtector-Operations-Reporter/1.0",
            "X-NatureProtector-Operations-Secret": callback_secret,
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=20) as response:  # noqa: S310 - URL is validated and owner-configured.
            response_body = response.read(4096).decode("utf-8", errors="replace")
            write_receipt(
                receipt_path,
                {
                    "reportStatus": "REPORTED",
                    "httpStatus": response.status,
                    "response": response_body,
                    "artifactCount": len(artifacts),
                },
            )
            return 0
    except urllib.error.HTTPError as error:
        detail = error.read(4096).decode("utf-8", errors="replace")
        write_receipt(
            receipt_path,
            {"reportStatus": "HTTP_ERROR", "httpStatus": error.code, "response": detail},
        )
        return 1
    except urllib.error.URLError as error:
        write_receipt(receipt_path, {"reportStatus": "NETWORK_ERROR", "reason": str(error.reason)})
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
