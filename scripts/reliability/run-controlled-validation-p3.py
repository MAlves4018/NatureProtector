#!/usr/bin/env python3
"""Safely invoke NatureProtector controlled validation P3.

Default behaviour is a dry-run that prepares a bounded request. Actual execution
requires both --execute and --acknowledge-non-production, and the API must report
that it is running in Development or Evidence. Authentication is read only from
NP_RELIABILITY_AUTH_TOKEN and is never written to evidence.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

ALLOWED_ENVIRONMENTS = {"development", "evidence"}
RUN_LABEL_PREFIX = "controlled-validation-p3-negative-pipeline-"


def utc_compact() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def request_json(
    url: str, method: str, token: str | None, payload: dict[str, Any] | None, timeout: int
) -> tuple[int, Any]:
    headers = {"Accept": "application/json"}
    data = None
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if payload is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(url=url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            raw = response.read().decode("utf-8", errors="replace")
            try:
                body = json.loads(raw) if raw else {}
            except json.JSONDecodeError:
                body = {"raw": raw}
            return response.status, body
    except urllib.error.HTTPError as exc:
        raw = exc.read().decode("utf-8", errors="replace")
        try:
            body = json.loads(raw) if raw else {}
        except json.JSONDecodeError:
            body = {"raw": raw}
        return exc.code, body


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--api-base-url", default="http://localhost:5254")
    parser.add_argument("--run-label", default=f"{RUN_LABEL_PREFIX}{utc_compact()}-phase6")
    parser.add_argument("--timeout-seconds", type=int, default=300)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--execute", action="store_true")
    parser.add_argument("--acknowledge-non-production", action="store_true")
    args = parser.parse_args()

    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    token = os.environ.get("NP_RELIABILITY_AUTH_TOKEN")
    base = args.api_base_url.rstrip("/")
    availability_url = f"{base}/api/dev/controlled-validation/p3"
    run_url = f"{base}/api/dev/controlled-validation/p3/run"
    payload = {
        "runLabel": args.run_label,
        "waitForCompletion": True,
        "collectEvidence": True,
        "runAuditAfterCompletion": False,
        "timeoutSeconds": max(5, min(args.timeout_seconds, 3600)),
    }
    request_record = {
        "apiBaseUrl": base,
        "availabilityUrl": availability_url,
        "runUrl": run_url,
        "payload": payload,
        "authenticationSource": "NP_RELIABILITY_AUTH_TOKEN" if token else "not_configured",
        "executionRequested": args.execute,
        "nonProductionAcknowledged": args.acknowledge_non_production,
    }
    write_json(output / "request.json", request_record)

    if not args.execute:
        status = {
            "status": "DRY_RUN_PASS",
            "message": "Request prepared; no HTTP request or runtime mutation was performed.",
            "runLabel": args.run_label,
        }
        write_json(output / "status.json", status)
        print("CONTROLLED_VALIDATION_P3=DRY_RUN_PASS")
        print(f"RUN_LABEL={args.run_label}")
        return 0

    if not args.acknowledge_non_production:
        write_json(output / "status.json", {"status": "BLOCKED_ACKNOWLEDGEMENT_REQUIRED"})
        print("CONTROLLED_VALIDATION_P3=BLOCKED_ACKNOWLEDGEMENT_REQUIRED", file=sys.stderr)
        return 2
    if not token:
        write_json(output / "status.json", {"status": "BLOCKED_AUTH_TOKEN_MISSING"})
        print("CONTROLLED_VALIDATION_P3=BLOCKED_AUTH_TOKEN_MISSING", file=sys.stderr)
        return 2
    if not args.run_label.startswith(RUN_LABEL_PREFIX):
        write_json(output / "status.json", {"status": "BLOCKED_INVALID_RUN_LABEL_PREFIX"})
        print("CONTROLLED_VALIDATION_P3=BLOCKED_INVALID_RUN_LABEL_PREFIX", file=sys.stderr)
        return 2

    try:
        availability_code, availability = request_json(
            availability_url, "GET", token, None, timeout=min(30, args.timeout_seconds)
        )
    except (urllib.error.URLError, TimeoutError, OSError) as exc:
        write_json(output / "status.json", {"status": "BLOCKED_API_UNAVAILABLE", "error": str(exc)})
        print("CONTROLLED_VALIDATION_P3=BLOCKED_API_UNAVAILABLE", file=sys.stderr)
        return 3
    write_json(output / "availability.json", {"httpStatus": availability_code, "body": availability})

    environment = str(availability.get("environment", "")) if isinstance(availability, dict) else ""
    available = bool(availability.get("available", False)) if isinstance(availability, dict) else False
    if availability_code != 200:
        write_json(output / "status.json", {"status": "BLOCKED_AVAILABILITY_HTTP", "httpStatus": availability_code})
        print(f"CONTROLLED_VALIDATION_P3=BLOCKED_AVAILABILITY_HTTP_{availability_code}", file=sys.stderr)
        return 3
    if environment.lower() not in ALLOWED_ENVIRONMENTS or not available:
        write_json(
            output / "status.json",
            {"status": "BLOCKED_NON_ALLOWED_ENVIRONMENT", "environment": environment, "available": available},
        )
        print("CONTROLLED_VALIDATION_P3=BLOCKED_NON_ALLOWED_ENVIRONMENT", file=sys.stderr)
        return 3

    try:
        response_code, response = request_json(
            run_url,
            "POST",
            token,
            payload,
            timeout=max(10, args.timeout_seconds + 30),
        )
    except (urllib.error.URLError, TimeoutError, OSError) as exc:
        write_json(output / "status.json", {"status": "FAILED_EXECUTION_REQUEST", "error": str(exc)})
        print("CONTROLLED_VALIDATION_P3=FAILED_EXECUTION_REQUEST", file=sys.stderr)
        return 4
    write_json(output / "response.json", {"httpStatus": response_code, "body": response})

    response_status = str(response.get("status", "unknown")) if isinstance(response, dict) else "unknown"
    accepted_statuses = {"Completed", "Started", "Validated", "Succeeded"}
    final = "PASS_AUDIT_REQUIRED" if response_code < 300 and response_status in accepted_statuses else "FAIL"
    write_json(
        output / "status.json",
        {
            "status": final,
            "httpStatus": response_code,
            "runtimeStatus": response_status,
            "runLabel": args.run_label,
            "auditRequired": True,
        },
    )
    print(f"CONTROLLED_VALIDATION_P3={final}")
    print(f"RUN_LABEL={args.run_label}")
    print("AUDIT_REQUIRED=YES")
    return 0 if final.startswith("PASS") else 4


if __name__ == "__main__":
    raise SystemExit(main())
