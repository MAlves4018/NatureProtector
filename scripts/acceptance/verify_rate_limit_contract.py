#!/usr/bin/env python3
"""Exercise the live authentication limiter and verify its fail-closed contract."""

from __future__ import annotations

import argparse
import json
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def request_json(url: str, method: str = "GET", body: dict[str, Any] | None = None) -> dict[str, Any]:
    data = json.dumps(body).encode("utf-8") if body is not None else None
    request = Request(
        url,
        data=data,
        method=method,
        headers={"Accept": "application/json", "Content-Type": "application/json", "User-Agent": "NP-RateLimit-Acceptance/1.0"},
    )
    started = time.perf_counter()
    try:
        with urlopen(request, timeout=15) as response:
            raw = response.read().decode("utf-8", errors="replace")
            return {
                "status": int(response.status),
                "headers": dict(response.headers.items()),
                "body": parse_json(raw),
                "rawBody": raw,
                "elapsedMs": round((time.perf_counter() - started) * 1000, 3),
            }
    except HTTPError as exc:
        raw = exc.read().decode("utf-8", errors="replace")
        return {
            "status": int(exc.code),
            "headers": dict(exc.headers.items()),
            "body": parse_json(raw),
            "rawBody": raw,
            "elapsedMs": round((time.perf_counter() - started) * 1000, 3),
        }
    except (URLError, TimeoutError, OSError) as exc:
        return {
            "status": None,
            "headers": {},
            "body": None,
            "rawBody": "",
            "elapsedMs": round((time.perf_counter() - started) * 1000, 3),
            "error": f"{type(exc).__name__}: {exc}",
        }


def parse_json(value: str) -> Any:
    try:
        return json.loads(value) if value.strip() else None
    except json.JSONDecodeError:
        return None


def header(headers: dict[str, str], name: str) -> str:
    return next((value for key, value in headers.items() if key.lower() == name.lower()), "")


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    config = json.loads(args.config.read_text(encoding="utf-8"))
    runtime = config["runtime"]
    contract = config["rateLimiting"]
    base = runtime["apiBaseUrl"].rstrip("/")
    attempts: list[dict[str, Any]] = []
    limited: dict[str, Any] | None = None

    for sequence in range(1, int(contract["maximumAttempts"]) + 1):
        result = request_json(
            base + contract["probePath"],
            method="POST",
            body={"usernameOrEmail": f"rate-limit-invalid-{sequence}", "password": "invalid"},
        )
        row = {"sequence": sequence, **result}
        attempts.append(row)
        if result["status"] == int(contract["expectedLimitStatus"]):
            limited = row
            break

    checks: list[dict[str, Any]] = []

    def check(name: str, passed: bool, detail: str) -> None:
        checks.append({"name": name, "status": "PASS" if passed else "FAIL", "detail": detail})

    pre_limit = [row for row in attempts if row["status"] == int(contract["expectedPreLimitStatus"])]
    check("pre-limit rejection observed", bool(pre_limit), f"count={len(pre_limit)}")
    check("rate limit reached", limited is not None, f"attempts={len(attempts)}")

    if limited is not None:
        policy = header(limited["headers"], "X-RateLimit-Policy")
        retry_after = header(limited["headers"], "Retry-After")
        body = limited.get("body") if isinstance(limited.get("body"), dict) else {}
        check("policy header", policy == contract["expectedPolicy"], f"observed={policy!r}")
        check("retry-after header", retry_after.isdigit() and int(retry_after) >= 1, f"observed={retry_after!r}")
        check("problem details status", body.get("status") == int(contract["expectedLimitStatus"]), f"observed={body.get('status')!r}")
        check("problem details policy", body.get("policy") == contract["expectedPolicy"], f"observed={body.get('policy')!r}")
    else:
        for name in ("policy header", "retry-after header", "problem details status", "problem details policy"):
            check(name, False, "No HTTP 429 response was observed.")

    health_results = []
    for path in contract["healthPathsAfterLimit"]:
        result = request_json(base + path)
        health_results.append({"path": path, **result})
        check(f"health unrestricted {path}", result["status"] == 200, f"observed={result['status']!r}")

    status = "PASS" if checks and all(item["status"] == "PASS" for item in checks) else "FAIL"
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    write_json(output / "attempts.json", attempts)
    write_json(output / "health-after-limit.json", health_results)
    write_json(output / "rate-limit-result.json", {"generatedAtUtc": utc_now(), "status": status, "checks": checks})
    (output / "SUMMARY.md").write_text(
        "# Live rate-limit contract\n\n"
        f"- Status: **{status}**\n"
        f"- Attempts until terminal observation: `{len(attempts)}`\n"
        f"- Pre-limit 401 responses: `{len(pre_limit)}`\n\n"
        + "\n".join(f"- {item['status']}: {item['name']} — {item['detail']}" for item in checks)
        + "\n",
        encoding="utf-8",
    )
    print(f"rate_limit_status: {status}")
    return 0 if status == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
