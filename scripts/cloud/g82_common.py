#!/usr/bin/env python3
"""Shared fail-closed helpers for the G8.2 qualification/governance chain."""
from __future__ import annotations

import hashlib
import json
import mimetypes
import os
import re
import unicodedata
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
from typing import Any, Iterable

try:
    from jsonschema import Draft202012Validator, FormatChecker
except Exception as exc:  # pragma: no cover - exercised by owner gate
    raise SystemExit(
        "jsonschema is required for G8.2. Install scripts/cloud/requirements-g82.txt"
    ) from exc

ROOT = Path(__file__).resolve().parents[2]
SCHEMA_ROOT = ROOT / "infra" / "gcp" / "contracts"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
SHA1_RE = re.compile(r"^[0-9a-f]{40}$")
QUALIFICATION_ID_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{2,79}$")


def read_json(path: str | os.PathLike[str]) -> Any:
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def write_json(path: str | os.PathLike[str], value: Any) -> None:
    destination = Path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(
        json.dumps(value, indent=2, sort_keys=False, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: str | os.PathLike[str]) -> str:
    return sha256_bytes(Path(path).read_bytes())


def utc_now() -> datetime:
    return datetime.now(timezone.utc)


def iso_utc(value: datetime | None = None) -> str:
    current = value or utc_now()
    return current.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def parse_datetime(value: Any, *, field: str) -> datetime:
    if not isinstance(value, str) or not value:
        raise ValueError(f"{field}: expected a non-empty date-time string")
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        raise ValueError(f"{field}: timezone is required")
    return parsed.astimezone(timezone.utc)


def validate_schema(document: Any, schema_name: str) -> None:
    schema_path = SCHEMA_ROOT / schema_name
    schema = read_json(schema_path)
    validator = Draft202012Validator(schema, format_checker=FormatChecker())
    errors = sorted(validator.iter_errors(document), key=lambda item: list(item.absolute_path))
    if not errors:
        return
    messages: list[str] = []
    for error in errors[:50]:
        location = "/".join(str(part) for part in error.absolute_path) or "$"
        messages.append(f"{location}: {error.message}")
    raise ValueError(f"schema validation failed for {schema_name}: " + "; ".join(messages))


def assert_sha256(value: Any, *, field: str) -> str:
    if not isinstance(value, str) or not SHA256_RE.fullmatch(value):
        raise ValueError(f"{field}: expected lowercase SHA-256")
    return value


def assert_commit(value: Any, *, field: str = "source_commit") -> str:
    if not isinstance(value, str) or not SHA1_RE.fullmatch(value):
        raise ValueError(f"{field}: expected lowercase 40-character git commit")
    return value


def assert_qualification_id(value: Any) -> str:
    if not isinstance(value, str) or not QUALIFICATION_ID_RE.fullmatch(value):
        raise ValueError("qualification_id: invalid")
    return value


def normalize_relative_path(value: str) -> str:
    if not isinstance(value, str) or not value:
        raise ValueError("empty path")
    if "\\" in value or "\x00" in value:
        raise ValueError(f"non-portable path: {value!r}")
    normalized = unicodedata.normalize("NFC", value)
    if normalized != value:
        raise ValueError(f"path is not NFC normalized: {value!r}")
    pure = PurePosixPath(value)
    if pure.is_absolute() or any(part in {"", ".", ".."} for part in pure.parts):
        raise ValueError(f"unsafe relative path: {value!r}")
    return pure.as_posix()


def media_type_for(path: Path) -> str:
    suffix = path.suffix.lower()
    overrides = {
        ".json": "application/json",
        ".jsonl": "application/x-ndjson",
        ".yaml": "application/yaml",
        ".yml": "application/yaml",
        ".md": "text/markdown",
        ".txt": "text/plain",
        ".log": "text/plain",
        ".csv": "text/csv",
        ".zip": "application/zip",
        ".xml": "application/xml",
        ".trx": "application/xml",
    }
    return overrides.get(suffix) or mimetypes.guess_type(path.name)[0] or "application/octet-stream"


def enumerate_regular_files(root: Path) -> list[Path]:
    root = root.resolve()
    if not root.is_dir():
        raise ValueError(f"evidence root does not exist: {root}")
    files: list[Path] = []
    for path in sorted(root.rglob("*"), key=lambda item: item.as_posix()):
        if path.is_symlink():
            raise ValueError(f"symbolic links are forbidden in evidence: {path.relative_to(root)}")
        if path.is_dir():
            continue
        if not path.is_file():
            raise ValueError(f"non-regular evidence entry: {path.relative_to(root)}")
        resolved = path.resolve()
        resolved.relative_to(root)
        normalize_relative_path(path.relative_to(root).as_posix())
        files.append(path)
    return files


def file_entries(root: Path) -> list[dict[str, Any]]:
    root = root.resolve()
    entries: list[dict[str, Any]] = []
    lower_paths: set[str] = set()
    for path in enumerate_regular_files(root):
        relative = normalize_relative_path(path.relative_to(root).as_posix())
        folded = relative.casefold()
        if folded in lower_paths:
            raise ValueError(f"case-insensitive duplicate evidence path: {relative}")
        lower_paths.add(folded)
        payload = path.read_bytes()
        entries.append(
            {
                "path": relative,
                "sha256": sha256_bytes(payload),
                "size_bytes": len(payload),
                "media_type": media_type_for(path),
            }
        )
    return entries


def tree_digest(entries: Iterable[dict[str, Any]]) -> str:
    lines = [
        f"{entry['sha256']}\t{entry['size_bytes']}\t{entry['media_type']}\t{entry['path']}"
        for entry in entries
    ]
    return sha256_bytes(("\n".join(lines) + "\n").encode("utf-8"))


def finite_number(value: Any, *, field: str, minimum: float | None = None) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{field}: expected number")
    number = float(value)
    if not (number == number and abs(number) != float("inf")):
        raise ValueError(f"{field}: expected finite number")
    if minimum is not None and number < minimum:
        raise ValueError(f"{field}: must be >= {minimum}")
    return number


def percentile_from_histograms(histograms: Iterable[dict[str, Any]], percentile: float) -> float:
    """Compute a conservative quantile from cumulative histograms."""
    if not 0 < percentile <= 1:
        raise ValueError("percentile must be in (0, 1]")
    bucket_totals: dict[float, int] = {}
    total = 0
    for index, histogram in enumerate(histograms):
        count = int(histogram["count"])
        total += count
        previous = -1
        for bucket in histogram["buckets"]:
            upper = float(bucket["le"])
            cumulative = int(bucket["cumulative_count"])
            if cumulative < previous or cumulative > count:
                raise ValueError(f"histogram[{index}] cumulative counts are invalid")
            previous = cumulative
            bucket_totals[upper] = bucket_totals.get(upper, 0) + cumulative
        if not histogram["buckets"] or previous != count:
            raise ValueError(f"histogram[{index}] final bucket must equal count")
    if total <= 0:
        raise ValueError("histograms contain no observations")
    threshold = total * percentile
    for upper in sorted(bucket_totals):
        if bucket_totals[upper] >= threshold:
            return upper
    raise ValueError("histogram does not contain a terminal bucket")
