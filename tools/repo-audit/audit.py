#!/usr/bin/env python3
"""Deterministic, dependency-free repository maintainability inventory."""

from __future__ import annotations

import argparse
import csv
import fnmatch
import hashlib
import json
import os
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Iterator, Mapping, Sequence

TOOL_VERSION = "1.1.0"

ENV_PATTERNS: tuple[tuple[str, re.Pattern[str]], ...] = (
    ("powershell", re.compile(r"\$env:([A-Z][A-Z0-9_]*)", re.IGNORECASE)),
    (
        "dotnet",
        re.compile(
            r"Environment\.GetEnvironmentVariable\(\s*[\"']([A-Z][A-Z0-9_]*)[\"']",
            re.IGNORECASE,
        ),
    ),
    (
        "python-getenv",
        re.compile(r"os\.(?:getenv|environ\.get)\(\s*[\"']([A-Z][A-Z0-9_]*)[\"']", re.IGNORECASE),
    ),
    (
        "python-environ",
        re.compile(r"os\.environ\[\s*[\"']([A-Z][A-Z0-9_]*)[\"']\s*\]", re.IGNORECASE),
    ),
    ("node", re.compile(r"(?:process|import\.meta)\.env\.([A-Z][A-Z0-9_]*)", re.IGNORECASE)),
    ("interpolation", re.compile(r"\$\{([A-Z][A-Z0-9_]*)(?:(?::[-?])[^}]*)?\}", re.IGNORECASE)),
)
ENV_DEFINITION = re.compile(r"^\s*(?:export\s+)?([A-Z][A-Z0-9_]*)\s*=", re.IGNORECASE)


@dataclass(frozen=True)
class FileRecord:
    path: str
    size_bytes: int
    sha256: str
    extension: str
    language: str
    is_text: bool
    lines: int | None
    category: str


@dataclass(frozen=True)
class ScriptReference:
    source_path: str
    reference_path: str
    context: str
    match_kind: str


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Create a deterministic maintainability inventory without modifying the repository."
    )
    parser.add_argument("--repo", type=Path, default=Path.cwd(), help="Repository root to scan.")
    parser.add_argument(
        "--config",
        type=Path,
        default=Path(__file__).with_name("audit-config.json"),
        help="Audit configuration JSON.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("artifacts/repo-audit"),
        help="Directory for generated reports.",
    )
    parser.add_argument(
        "--verify-determinism",
        action="store_true",
        help="Run the scan twice in memory and fail if the normalized model differs.",
    )
    return parser.parse_args(argv)


def read_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as stream:
        value = json.load(stream)
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object: {path}")
    return value


def stable_json(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def normalize_path(path: Path) -> str:
    value = path.as_posix()
    while value.startswith("./"):
        value = value[2:]
    return value


def matches_any(path: str, patterns: Iterable[str]) -> bool:
    normalized = path.replace("\\", "/")
    return any(fnmatch.fnmatchcase(normalized, pattern) for pattern in patterns)


def classify_path(path: str, config: Mapping[str, Any]) -> str:
    if matches_any(path, config.get("generated_paths", [])):
        return "generated"
    if matches_any(path, config.get("dataset_paths", [])):
        return "dataset"
    if matches_any(path, config.get("historical_paths", [])):
        return "historical"
    return "source"


def extension_for(path: Path) -> str:
    lower_name = path.name.lower()
    if lower_name.endswith(".tfvars.json"):
        return ".tfvars.json"
    if lower_name.endswith(".schema.json"):
        return ".schema.json"
    return path.suffix.lower()


def is_text_path(relative: str, path: Path, config: Mapping[str, Any]) -> bool:
    extension = extension_for(path)
    configured_extensions = {str(item).lower() for item in config.get("text_extensions", [])}
    configured_names = {str(item) for item in config.get("text_filenames", [])}
    if extension in configured_extensions or path.name in configured_names:
        return True
    if path.name.startswith(".env"):
        return True
    if path.name.startswith("Dockerfile"):
        return True
    return False


def decode_text(path: Path) -> str:
    data = path.read_bytes()
    try:
        return data.decode("utf-8-sig")
    except UnicodeDecodeError:
        return data.decode("utf-8", errors="replace")


def count_lines(text: str) -> int:
    if not text:
        return 0
    return len(text.splitlines())


def count_file_lines(path: Path) -> int:
    newline_count = 0
    last_byte: int | None = None
    size = 0
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            size += len(chunk)
            newline_count += chunk.count(b"\n")
            last_byte = chunk[-1]
    if size == 0:
        return 0
    return newline_count if last_byte == 10 else newline_count + 1


def iter_repository_files(repo: Path, config: Mapping[str, Any]) -> Iterator[tuple[str, Path]]:
    excluded = config.get("excluded_paths", [])
    candidates: list[tuple[str, Path]] = []
    for root, directories, filenames in os.walk(repo, topdown=True, followlinks=False):
        root_path = Path(root)
        relative_root = normalize_path(root_path.relative_to(repo)) if root_path != repo else ""
        directories[:] = sorted(
            directory
            for directory in directories
            if not matches_any(
                f"{relative_root}/{directory}".lstrip("/") + "/",
                excluded,
            )
            and not matches_any(f"{relative_root}/{directory}".lstrip("/"), excluded)
        )
        for filename in sorted(filenames):
            absolute = root_path / filename
            if not absolute.is_file() or absolute.is_symlink():
                continue
            relative = normalize_path(absolute.relative_to(repo))
            if matches_any(relative, excluded):
                continue
            candidates.append((relative, absolute))
    yield from sorted(candidates, key=lambda item: item[0])


def build_file_records(repo: Path, config: Mapping[str, Any]) -> tuple[list[FileRecord], dict[str, str]]:
    language_by_extension = {
        str(key).lower(): str(value) for key, value in config.get("language_by_extension", {}).items()
    }
    records: list[FileRecord] = []
    text_by_path: dict[str, str] = {}
    for relative, absolute in iter_repository_files(repo, config):
        extension = extension_for(absolute)
        text_file = is_text_path(relative, absolute, config)
        text: str | None = None
        lines: int | None = None
        category = classify_path(relative, config)
        if text_file:
            if category in {"dataset", "generated"}:
                lines = count_file_lines(absolute)
            else:
                text = decode_text(absolute)
                lines = count_lines(text)
                text_by_path[relative] = text
        records.append(
            FileRecord(
                path=relative,
                size_bytes=absolute.stat().st_size,
                sha256=sha256_file(absolute),
                extension=extension,
                language=language_by_extension.get(extension, "Other"),
                is_text=text_file,
                lines=lines,
                category=category,
            )
        )
    return records, text_by_path


def make_duplicate_groups(records: Sequence[FileRecord], max_bytes: int) -> list[dict[str, Any]]:
    grouped: dict[tuple[int, str], list[str]] = defaultdict(list)
    for record in records:
        if 0 < record.size_bytes <= max_bytes:
            grouped[(record.size_bytes, record.sha256)].append(record.path)
    groups: list[dict[str, Any]] = []
    for (size_bytes, digest), paths in sorted(grouped.items(), key=lambda item: (-item[0][0], item[0][1])):
        if len(paths) < 2:
            continue
        groups.append(
            {
                "group_id": f"DUP-{len(groups) + 1:04d}",
                "size_bytes": size_bytes,
                "sha256": digest,
                "paths": sorted(paths),
                "wasted_bytes": size_bytes * (len(paths) - 1),
            }
        )
    return groups


def context_for_path(path: str) -> str:
    if path.startswith(".github/workflows/"):
        return "workflow"
    if path.startswith("scripts/") or path.startswith("tools/"):
        return "automation"
    if path.startswith("docs/") or path.endswith("README.md") or path == "README.md":
        return "documentation"
    if path.startswith("tests/"):
        return "test"
    return "source"


def find_script_references(
    records: Sequence[FileRecord], text_by_path: Mapping[str, str], config: Mapping[str, Any]
) -> tuple[list[dict[str, Any]], list[ScriptReference]]:
    script_extensions = {str(item).lower() for item in config.get("script_extensions", [])}
    scripts = [
        record for record in records if record.path.startswith("scripts/") and record.extension in script_extensions
    ]
    scripts_by_path = {script.path.lower(): script.path for script in scripts}
    scripts_by_basename: dict[str, list[str]] = defaultdict(list)
    for script in scripts:
        scripts_by_basename[Path(script.path).name.lower()].append(script.path)

    extension_expression = "|".join(re.escape(extension.lstrip(".")) for extension in sorted(script_extensions))
    token_pattern = re.compile(
        rf"(?<![A-Za-z0-9_])([A-Za-z0-9_.$@{{}}()\-/:\\]+\.(?:{extension_expression}))(?![A-Za-z0-9_])",
        re.IGNORECASE,
    )
    scripts_by_stem: dict[str, list[str]] = defaultdict(list)
    for script in scripts:
        if script.extension == ".py":
            scripts_by_stem[Path(script.path).stem.lower()].append(script.path)

    references_by_script: dict[str, dict[tuple[str, str], ScriptReference]] = defaultdict(dict)

    def add_reference(script_path: str, source_path: str, match_kind: str) -> None:
        if source_path == script_path:
            return
        key = (source_path, match_kind)
        references_by_script[script_path][key] = ScriptReference(
            source_path=source_path,
            reference_path=script_path,
            context=context_for_path(source_path),
            match_kind=match_kind,
        )

    python_import_pattern = re.compile(
        r"^\s*(?:from\s+([A-Za-z_][A-Za-z0-9_.]*)\s+import|import\s+([A-Za-z_][A-Za-z0-9_.]*))",
        re.MULTILINE,
    )

    for source_path, text in sorted(text_by_path.items()):
        normalized_text = text.replace("\\", "/")
        for match in token_pattern.finditer(normalized_text):
            raw_token = match.group(1).strip("\"'`")
            normalized_token = raw_token.lower().replace("\\", "/")
            basename = normalized_token.rsplit("/", 1)[-1]
            matches: list[tuple[str, str]] = []
            if normalized_token in scripts_by_path:
                matches.append((scripts_by_path[normalized_token], "relative-path"))
            for script_path in scripts_by_basename.get(basename, []):
                if script_path.lower() != normalized_token:
                    matches.append((script_path, "basename"))
            for script_path, match_kind in matches:
                add_reference(script_path, source_path, match_kind)

        if source_path.lower().endswith(".py"):
            source_directory = Path(source_path).parent
            for import_match in python_import_pattern.finditer(text):
                module = (import_match.group(1) or import_match.group(2) or "").split(".")[0]
                if not module:
                    continue
                same_directory = (source_directory / f"{module}.py").as_posix().lower()
                if same_directory in scripts_by_path:
                    add_reference(scripts_by_path[same_directory], source_path, "python-import")
                    continue
                candidates = scripts_by_stem.get(module.lower(), [])
                if len(candidates) == 1:
                    add_reference(candidates[0], source_path, "python-import")

    references: list[ScriptReference] = []
    inventory: list[dict[str, Any]] = []
    for script in sorted(scripts, key=lambda item: item.path):
        script_refs = sorted(
            references_by_script.get(script.path, {}).values(),
            key=lambda item: (item.source_path, item.match_kind),
        )
        references.extend(script_refs)
        contexts = Counter(reference.context for reference in script_refs)
        if contexts.get("workflow", 0):
            status = "WORKFLOW_REFERENCED"
        elif contexts.get("automation", 0):
            status = "AUTOMATION_REFERENCED"
        elif contexts.get("documentation", 0):
            status = "DOCUMENTED"
        elif script_refs:
            status = "OTHER_STATIC_REFERENCE"
        else:
            status = "NO_STATIC_REFERENCE_FOUND"
        inventory.append(
            {
                "path": script.path,
                "lines": script.lines or 0,
                "size_bytes": script.size_bytes,
                "status": status,
                "reference_count": len(script_refs),
                "workflow_references": contexts.get("workflow", 0),
                "automation_references": contexts.get("automation", 0),
                "documentation_references": contexts.get("documentation", 0),
                "other_references": sum(
                    count
                    for context, count in contexts.items()
                    if context not in {"workflow", "automation", "documentation"}
                ),
            }
        )
    return inventory, sorted(
        references,
        key=lambda item: (item.reference_path, item.source_path, item.match_kind),
    )


def find_environment_variables(text_by_path: Mapping[str, str]) -> list[dict[str, Any]]:
    definitions: dict[str, set[str]] = defaultdict(set)
    references: dict[str, list[tuple[str, str]]] = defaultdict(list)
    for path, text in sorted(text_by_path.items()):
        if Path(path).name.startswith(".env"):
            for line in text.splitlines():
                match = ENV_DEFINITION.match(line)
                if match:
                    definitions[match.group(1).upper()].add(path)
        for pattern_name, pattern in ENV_PATTERNS:
            for match in pattern.finditer(text):
                references[match.group(1).upper()].append((path, pattern_name))

    rows: list[dict[str, Any]] = []
    for variable in sorted(set(definitions) | set(references)):
        refs = references.get(variable, [])
        reference_paths = sorted({path for path, _ in refs})
        pattern_names = sorted({pattern_name for _, pattern_name in refs})
        rows.append(
            {
                "variable": variable,
                "definition_count": len(definitions.get(variable, set())),
                "definition_paths": sorted(definitions.get(variable, set())),
                "reference_count": len(refs),
                "reference_paths": reference_paths,
                "detection_patterns": pattern_names,
            }
        )
    return rows


def dotted_get(value: Any, dotted_key: str) -> Any:
    current = value
    for segment in dotted_key.split("."):
        if not isinstance(current, Mapping) or segment not in current:
            raise KeyError(dotted_key)
        current = current[segment]
    return current


def load_configuration_literals(
    repo: Path, config: Mapping[str, Any], text_by_path: Mapping[str, str]
) -> list[dict[str, Any]]:
    literals: list[dict[str, Any]] = []
    for source in config.get("configuration_sources", []):
        source_path = str(source["path"])
        absolute = repo / source_path
        if not absolute.is_file():
            literals.append(
                {
                    "source_path": source_path,
                    "key": "<missing-source>",
                    "value": "",
                    "occurrence_count": 0,
                    "occurrence_paths": [],
                    "status": "SOURCE_NOT_FOUND",
                }
            )
            continue
        parsed_json: dict[str, Any] | None = None
        for key in source.get("keys", []):
            try:
                if key == "$text":
                    literal_value = decode_text(absolute).strip()
                else:
                    if parsed_json is None:
                        parsed_json = read_json(absolute)
                    literal_value = dotted_get(parsed_json, str(key))
            except (KeyError, ValueError, json.JSONDecodeError):
                literals.append(
                    {
                        "source_path": source_path,
                        "key": str(key),
                        "value": "",
                        "occurrence_count": 0,
                        "occurrence_paths": [],
                        "status": "KEY_NOT_FOUND",
                    }
                )
                continue
            if not isinstance(literal_value, (str, int, float)):
                continue
            literal = str(literal_value)
            occurrence_paths: list[str] = []
            occurrence_count = 0
            for path, text in text_by_path.items():
                count = text.count(literal)
                if count:
                    occurrence_count += count
                    occurrence_paths.append(path)
            literals.append(
                {
                    "source_path": source_path,
                    "key": str(key),
                    "value": literal,
                    "occurrence_count": occurrence_count,
                    "occurrence_paths": sorted(occurrence_paths),
                    "status": "FOUND",
                }
            )
    return sorted(literals, key=lambda item: (item["source_path"], item["key"]))


def make_observations(
    records: Sequence[FileRecord],
    duplicate_groups: Sequence[dict[str, Any]],
    script_inventory: Sequence[dict[str, Any]],
    environment_variables: Sequence[dict[str, Any]],
    configuration_literals: Sequence[dict[str, Any]],
    config: Mapping[str, Any],
) -> list[dict[str, Any]]:
    observations: list[dict[str, Any]] = []
    hotspot_threshold = int(config.get("hotspot_line_threshold", 400))
    large_file_bytes = int(config.get("large_file_bytes", 1_000_000))

    for record in records:
        if record.is_text and record.lines is not None and record.lines >= hotspot_threshold:
            observations.append(
                {
                    "kind": "LARGE_TEXT_FILE",
                    "subject": record.path,
                    "metric": record.lines,
                    "unit": "lines",
                    "classification": "MEASUREMENT_ONLY",
                }
            )
        if record.size_bytes >= large_file_bytes:
            observations.append(
                {
                    "kind": "LARGE_FILE",
                    "subject": record.path,
                    "metric": record.size_bytes,
                    "unit": "bytes",
                    "classification": "MEASUREMENT_ONLY",
                }
            )

    for group in duplicate_groups:
        observations.append(
            {
                "kind": "EXACT_DUPLICATE_GROUP",
                "subject": group["group_id"],
                "metric": group["wasted_bytes"],
                "unit": "duplicate-bytes",
                "classification": "REQUIRES_INTENT_REVIEW",
            }
        )

    for script in script_inventory:
        if script["status"] == "NO_STATIC_REFERENCE_FOUND":
            observations.append(
                {
                    "kind": "SCRIPT_WITHOUT_STATIC_REFERENCE",
                    "subject": script["path"],
                    "metric": 0,
                    "unit": "references",
                    "classification": "NOT_PROOF_OF_DEAD_CODE",
                }
            )

    for variable in environment_variables:
        if variable["reference_count"] and not variable["definition_count"]:
            observations.append(
                {
                    "kind": "ENVIRONMENT_VARIABLE_WITHOUT_REPOSITORY_EXAMPLE",
                    "subject": variable["variable"],
                    "metric": variable["reference_count"],
                    "unit": "references",
                    "classification": "MAY_BE_CI_SECRET_OR_RUNTIME_INPUT",
                }
            )

    for literal in configuration_literals:
        if literal["status"] == "FOUND" and len(literal["occurrence_paths"]) > 1:
            observations.append(
                {
                    "kind": "CANONICAL_LITERAL_REPEATED",
                    "subject": f"{literal['source_path']}::{literal['key']}",
                    "metric": literal["occurrence_count"],
                    "unit": "occurrences",
                    "classification": "REQUIRES_AUTHORITY_REVIEW",
                }
            )

    for index, observation in enumerate(
        sorted(observations, key=lambda item: (item["kind"], item["subject"])), start=1
    ):
        observation["observation_id"] = f"OBS-{index:05d}"
    return observations


def summarize(
    records: Sequence[FileRecord],
    duplicate_groups: Sequence[dict[str, Any]],
    script_inventory: Sequence[dict[str, Any]],
    environment_variables: Sequence[dict[str, Any]],
    configuration_literals: Sequence[dict[str, Any]],
    observations: Sequence[dict[str, Any]],
    config_digest: str,
) -> dict[str, Any]:
    lines_by_language: Counter[str] = Counter()
    files_by_category: Counter[str] = Counter()
    bytes_by_category: Counter[str] = Counter()
    for record in records:
        files_by_category[record.category] += 1
        bytes_by_category[record.category] += record.size_bytes
        if record.lines is not None:
            lines_by_language[record.language] += record.lines

    script_statuses = Counter(item["status"] for item in script_inventory)
    observation_kinds = Counter(item["kind"] for item in observations)
    return {
        "schema_version": 1,
        "audit_tool_version": TOOL_VERSION,
        "repository_root": ".",
        "configuration_sha256": config_digest,
        "files": {
            "total": len(records),
            "text": sum(1 for record in records if record.is_text),
            "binary_or_unclassified": sum(1 for record in records if not record.is_text),
            "bytes": sum(record.size_bytes for record in records),
            "by_category": dict(sorted(files_by_category.items())),
            "bytes_by_category": dict(sorted(bytes_by_category.items())),
        },
        "lines_by_language": dict(sorted(lines_by_language.items())),
        "scripts": {
            "total": len(script_inventory),
            "by_static_reference_status": dict(sorted(script_statuses.items())),
        },
        "exact_duplicates": {
            "groups": len(duplicate_groups),
            "potential_duplicate_bytes": sum(group["wasted_bytes"] for group in duplicate_groups),
        },
        "environment_variables": {
            "total": len(environment_variables),
            "referenced_without_repository_example": sum(
                1 for item in environment_variables if item["reference_count"] and not item["definition_count"]
            ),
        },
        "configuration_literals": {
            "tracked": len(configuration_literals),
            "repeated_across_multiple_files": sum(
                1 for item in configuration_literals if item["status"] == "FOUND" and len(item["occurrence_paths"]) > 1
            ),
        },
        "observations": {
            "total": len(observations),
            "by_kind": dict(sorted(observation_kinds.items())),
        },
    }


def build_model(repo: Path, config: Mapping[str, Any], config_digest: str) -> dict[str, Any]:
    records, text_by_path = build_file_records(repo, config)
    duplicate_groups = make_duplicate_groups(records, max_bytes=int(config.get("duplicate_max_bytes", 25_000_000)))
    script_inventory, script_references = find_script_references(records, text_by_path, config)
    environment_variables = find_environment_variables(text_by_path)
    configuration_literals = load_configuration_literals(repo, config, text_by_path)
    observations = make_observations(
        records,
        duplicate_groups,
        script_inventory,
        environment_variables,
        configuration_literals,
        config,
    )
    return {
        "summary": summarize(
            records,
            duplicate_groups,
            script_inventory,
            environment_variables,
            configuration_literals,
            observations,
            config_digest,
        ),
        "files": [record.__dict__ for record in records],
        "duplicate_groups": duplicate_groups,
        "script_inventory": script_inventory,
        "script_references": [reference.__dict__ for reference in script_references],
        "environment_variables": environment_variables,
        "configuration_literals": configuration_literals,
        "observations": observations,
    }


def write_csv(path: Path, fieldnames: Sequence[str], rows: Iterable[Mapping[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            flattened = {
                key: ";".join(str(value) for value in row.get(key, []))
                if isinstance(row.get(key), list)
                else row.get(key, "")
                for key in fieldnames
            }
            writer.writerow(flattened)


def write_markdown_report(path: Path, model: Mapping[str, Any], config: Mapping[str, Any]) -> None:
    summary = model["summary"]
    limit = int(config.get("max_report_rows", 100))
    largest_text = sorted(
        (record for record in model["files"] if record["lines"] is not None),
        key=lambda record: (-record["lines"], record["path"]),
    )[:20]
    script_statuses = summary["scripts"]["by_static_reference_status"]
    lines = [
        "# Repository maintainability inventory",
        "",
        "This report is a deterministic static inventory. It identifies review candidates; it does not prove that code is dead, duplicated semantically, or safe to remove.",
        "",
        "## Summary",
        "",
        f"- Files: **{summary['files']['total']}** ({summary['files']['bytes']} bytes).",
        f"- Text files: **{summary['files']['text']}**.",
        f"- Executable scripts under `scripts/`: **{summary['scripts']['total']}**.",
        f"- Exact duplicate groups: **{summary['exact_duplicates']['groups']}**.",
        f"- Potential exact-duplicate bytes: **{summary['exact_duplicates']['potential_duplicate_bytes']}**.",
        f"- Environment variable names inventoried: **{summary['environment_variables']['total']}**.",
        f"- Static observations: **{summary['observations']['total']}**.",
        "",
        "## Lines by language",
        "",
        "| Language | Lines |",
        "|---|---:|",
    ]
    for language, count in summary["lines_by_language"].items():
        lines.append(f"| {language} | {count} |")
    lines.extend(["", "## Largest text files", "", "| Path | Lines | Category |", "|---|---:|---|"])
    for record in largest_text:
        lines.append(f"| `{record['path']}` | {record['lines']} | {record['category']} |")
    lines.extend(["", "## Script static-reference status", "", "| Status | Scripts |", "|---|---:|"])
    for status, count in script_statuses.items():
        lines.append(f"| {status} | {count} |")
    lines.extend(
        [
            "",
            "## Interpretation rules",
            "",
            "- `NO_STATIC_REFERENCE_FOUND` is a review candidate, not proof of dead code.",
            "- Exact duplicates may be deliberate fixtures, generated output, or historical evidence.",
            "- Environment variables without a repository example may legitimately be CI secrets or runtime inputs.",
            "- Large files and high line counts are measurements, not quality verdicts.",
            "",
            f"The CSV and JSON outputs contain the complete inventories. Markdown tables are intentionally capped at {limit} rows where applicable.",
        ]
    )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_outputs(output: Path, model: Mapping[str, Any], config: Mapping[str, Any]) -> None:
    output.mkdir(parents=True, exist_ok=True)
    (output / "summary.json").write_text(stable_json(model["summary"]), encoding="utf-8")
    (output / "observations.json").write_text(stable_json(model["observations"]), encoding="utf-8")
    write_csv(
        output / "file-inventory.csv",
        ["path", "size_bytes", "sha256", "extension", "language", "is_text", "lines", "category"],
        model["files"],
    )
    write_csv(
        output / "hotspots.csv",
        ["path", "lines", "size_bytes", "language", "category"],
        sorted(
            (record for record in model["files"] if record["lines"] is not None),
            key=lambda record: (-record["lines"], record["path"]),
        ),
    )
    duplicate_rows: list[dict[str, Any]] = []
    for group in model["duplicate_groups"]:
        for path in group["paths"]:
            duplicate_rows.append(
                {
                    "group_id": group["group_id"],
                    "size_bytes": group["size_bytes"],
                    "sha256": group["sha256"],
                    "wasted_bytes": group["wasted_bytes"],
                    "path": path,
                }
            )
    write_csv(
        output / "exact-duplicates.csv",
        ["group_id", "size_bytes", "sha256", "wasted_bytes", "path"],
        duplicate_rows,
    )
    write_csv(
        output / "script-inventory.csv",
        [
            "path",
            "lines",
            "size_bytes",
            "status",
            "reference_count",
            "workflow_references",
            "automation_references",
            "documentation_references",
            "other_references",
        ],
        model["script_inventory"],
    )
    write_csv(
        output / "script-references.csv",
        ["reference_path", "source_path", "context", "match_kind"],
        model["script_references"],
    )
    write_csv(
        output / "environment-variables.csv",
        [
            "variable",
            "definition_count",
            "definition_paths",
            "reference_count",
            "reference_paths",
            "detection_patterns",
        ],
        model["environment_variables"],
    )
    write_csv(
        output / "configuration-literals.csv",
        ["source_path", "key", "value", "occurrence_count", "occurrence_paths", "status"],
        model["configuration_literals"],
    )
    write_markdown_report(output / "report.md", model, config)

    report_files = sorted(path for path in output.iterdir() if path.is_file() and path.name != "manifest.json")
    manifest = {
        "schema_version": 1,
        "audit_tool_version": TOOL_VERSION,
        "files": [
            {
                "path": path.name,
                "size_bytes": path.stat().st_size,
                "sha256": sha256_file(path),
            }
            for path in report_files
        ],
    }
    (output / "manifest.json").write_text(stable_json(manifest), encoding="utf-8")


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    repo = args.repo.resolve()
    config_path = args.config.resolve()
    output = args.output.resolve()
    if not repo.is_dir():
        print(f"error: repository root does not exist: {repo}", file=sys.stderr)
        return 2
    if not config_path.is_file():
        print(f"error: configuration does not exist: {config_path}", file=sys.stderr)
        return 2
    try:
        config = read_json(config_path)
        config_digest = sha256_file(config_path)
        model = build_model(repo, config, config_digest)
        if args.verify_determinism:
            second_model = build_model(repo, config, config_digest)
            if stable_json(model) != stable_json(second_model):
                print("error: repeated in-memory scans were not deterministic", file=sys.stderr)
                return 3
        write_outputs(output, model, config)
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 2
    print(stable_json(model["summary"]), end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
