from __future__ import annotations

import csv
import hashlib
import json
import os
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable

SECRET_NAME_RE = re.compile(r"(?i)(password|passwd|secret|token|api[_-]?key|connection[_-]?string|dsn)")
SECRET_VALUE_RE = re.compile(r"(?i)(bearer\s+[A-Za-z0-9._~+\-/=]+|password\s*=\s*[^;\s]+|token\s*=\s*[^;\s]+)")


def utc_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def utc_stamp() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def read_json(path: Path, default: Any = None) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return default


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_csv(path: Path, rows: Iterable[dict[str, Any]], fields: list[str] | None = None) -> None:
    materialized = list(rows)
    path.parent.mkdir(parents=True, exist_ok=True)
    if fields is None:
        fields = list(materialized[0]) if materialized else []
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(materialized)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_hash_manifest(root: Path, name: str = "SHA256SUMS.txt") -> int:
    target = root / name
    files = sorted(path for path in root.rglob("*") if path.is_file() and path != target)
    target.write_text(
        "\n".join(f"{sha256(path)}  {path.relative_to(root).as_posix()}" for path in files) + "\n",
        encoding="utf-8",
    )
    return len(files)


def verify_hash_manifest(root: Path, name: str = "SHA256SUMS.txt") -> list[str]:
    errors: list[str] = []
    target = root / name
    if not target.is_file():
        return [f"Missing {name}"]
    listed: dict[str, str] = {}
    for line in target.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        try:
            expected, relative = line.split("  ", 1)
        except ValueError:
            errors.append(f"Malformed hash line: {line[:120]}")
            continue
        listed[relative] = expected.lower()
    observed = {
        path.relative_to(root).as_posix()
        for path in root.rglob("*")
        if path.is_file() and path != target
    }
    for relative in sorted(observed - set(listed)):
        errors.append(f"Unlisted file: {relative}")
    for relative in sorted(set(listed) - observed):
        errors.append(f"Missing hashed file: {relative}")
    for relative, expected in listed.items():
        path = root / relative
        if path.is_file() and sha256(path) != expected:
            errors.append(f"Hash mismatch: {relative}")
    return errors


def safe_relative(path: Path | None, repo: Path) -> str:
    if path is None:
        return ""
    try:
        return path.resolve().relative_to(repo.resolve()).as_posix()
    except ValueError:
        return str(path.resolve())


def redact_text(value: str, extra_secrets: Iterable[str] = ()) -> str:
    result = value
    for secret in extra_secrets:
        if secret:
            result = result.replace(secret, "[REDACTED]")
    return SECRET_VALUE_RE.sub("[REDACTED]", result)


def safe_environment() -> dict[str, Any]:
    values: dict[str, Any] = {}
    for name, value in sorted(os.environ.items()):
        if name.startswith(("NATUREPROTECTOR_", "NP_", "RABBITMQ_", "INFLUXDB_", "GRAFANA_")):
            values[name] = "[SET_REDACTED]" if SECRET_NAME_RE.search(name) and value else bool(value)
    return values


def find_latest_run(phase_root: Path) -> Path | None:
    latest = phase_root / "LATEST.txt"
    if latest.is_file():
        raw = latest.read_text(encoding="utf-8").strip()
        candidate = phase_root / Path(raw).name
        if candidate.is_dir():
            return candidate
    runs = sorted(path for path in phase_root.iterdir() if path.is_dir()) if phase_root.is_dir() else []
    return runs[-1] if runs else None
