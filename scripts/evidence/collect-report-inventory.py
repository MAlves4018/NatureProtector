#!/usr/bin/env python3
"""Generate a reproducible, static NatureProtector report inventory.

The collector deliberately does not execute the application, tests, Docker, cloud,
or databases. It inspects the repository snapshot and exports machine-readable
inventories that can be cited as static evidence in the report.

No third-party Python packages are required.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import re
import subprocess
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterator, Sequence

SCRIPT_VERSION = "1.0.0"
DEFAULT_EXCLUDED_DIRS = {
    ".git",
    ".idea",
    ".vs",
    ".vscode",
    "bin",
    "obj",
    "node_modules",
    "dist",
    "build",
    "coverage",
    "TestResults",
    "artifacts",
    ".nuget",
    ".terraform",
    "playwright-report",
    "test-results",
    "__pycache__",
}
TEXT_EXTENSIONS = {
    ".cs",
    ".csproj",
    ".props",
    ".targets",
    ".sln",
    ".json",
    ".jsonc",
    ".yml",
    ".yaml",
    ".xml",
    ".md",
    ".txt",
    ".ps1",
    ".sh",
    ".py",
    ".sql",
    ".tf",
    ".tfvars",
    ".ts",
    ".tsx",
    ".js",
    ".jsx",
    ".mjs",
    ".cjs",
    ".css",
    ".scss",
    ".html",
    ".toml",
    ".ini",
    ".conf",
    ".config",
    ".runsettings",
    ".dockerignore",
    ".gitignore",
    ".gitattributes",
}


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def rel(path: Path, root: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8-sig", errors="replace")
    except OSError:
        return ""


def iter_files(root: Path, roots: Sequence[str] | None = None) -> Iterator[Path]:
    start_paths = [root / item for item in roots] if roots else [root]
    for start in start_paths:
        if not start.exists():
            continue
        if start.is_file():
            yield start
            continue
        for current, dirs, files in os.walk(start):
            dirs[:] = sorted(d for d in dirs if d not in DEFAULT_EXCLUDED_DIRS)
            for name in sorted(files):
                path = Path(current) / name
                if not any(part in DEFAULT_EXCLUDED_DIRS for part in path.relative_to(root).parts):
                    yield path


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_json(path: Path, value: Any) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_csv(path: Path, rows: Sequence[dict[str, Any]], fieldnames: Sequence[str] | None = None) -> None:
    if fieldnames is None:
        fieldnames = list(rows[0].keys()) if rows else []
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow({key: normalize_csv_value(row.get(key)) for key in fieldnames})


def normalize_csv_value(value: Any) -> Any:
    if value is None:
        return ""
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (list, tuple, set)):
        return "; ".join(str(item) for item in value)
    if isinstance(value, dict):
        return json.dumps(value, ensure_ascii=False, sort_keys=True)
    return value


def local_name(tag: str) -> str:
    return tag.split("}")[-1]


def find_first_text(root: ET.Element, names: Sequence[str]) -> str:
    for element in root.iter():
        if local_name(element.tag) in names and element.text and element.text.strip():
            return element.text.strip()
    return ""


def all_items(root: ET.Element, item_name: str, attr: str = "Include") -> list[str]:
    result: list[str] = []
    for element in root.iter():
        if local_name(element.tag) == item_name and element.attrib.get(attr):
            result.append(element.attrib[attr])
    return sorted(set(result))


def project_group(path: Path, repo: Path) -> str:
    relative = path.relative_to(repo).parts
    return relative[0] if relative else "root"


def collect_projects(repo: Path) -> list[dict[str, Any]]:
    default_tf = ""
    props = repo / "Directory.Build.props"
    if props.exists():
        try:
            default_tf = find_first_text(ET.parse(props).getroot(), ["TargetFramework", "TargetFrameworks"])
        except ET.ParseError:
            pass

    rows: list[dict[str, Any]] = []
    for path in sorted(iter_files(repo, ["src", "tests", "benchmarks"])):
        if path.suffix.lower() != ".csproj":
            continue
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            root = ET.Element("Project")
        name = path.stem
        group = project_group(path, repo)
        sdk = root.attrib.get("Sdk", "")
        target = find_first_text(root, ["TargetFramework", "TargetFrameworks"]) or default_tf
        output_type = find_first_text(root, ["OutputType"]) or ("Exe" if sdk.endswith(".Web") else "Library")
        packages = all_items(root, "PackageReference")
        project_refs = [Path(item.replace("\\", "/")).as_posix() for item in all_items(root, "ProjectReference")]
        is_test = (
            group == "tests" or name.endswith(".Tests") or any(p.lower() == "microsoft.net.test.sdk" for p in packages)
        )
        is_benchmark = group == "benchmarks" or any("benchmarkdotnet" in p.lower() for p in packages)
        rows.append(
            {
                "path": rel(path, repo),
                "group": group,
                "project_name": name,
                "sdk": sdk,
                "target_framework": target,
                "output_type": output_type,
                "is_test_project": is_test,
                "is_benchmark_project": is_benchmark,
                "package_reference_count": len(packages),
                "package_references": packages,
                "project_reference_count": len(project_refs),
                "project_references": project_refs,
            }
        )
    return rows


def source_kind(path: Path, repo: Path) -> str:
    parts = path.relative_to(repo).parts
    if not parts:
        return "root"
    first = parts[0]
    if first == "webUI" and len(parts) > 1:
        return f"webUI/{parts[1]}"
    return first


def is_probably_text(path: Path) -> bool:
    return path.suffix.lower() in TEXT_EXTENSIONS or path.name in {
        "Dockerfile",
        "Makefile",
        "NuGet.Config",
        "global.json",
        ".nvmrc",
        ".node-version",
    }


def collect_source_inventory(repo: Path) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    by_key: dict[tuple[str, str], dict[str, Any]] = {}
    all_files = list(iter_files(repo))
    text_files = 0
    binary_files = 0
    total_lines = 0
    nonblank_lines = 0
    total_bytes = 0
    for path in all_files:
        size = path.stat().st_size
        total_bytes += size
        extension = path.suffix.lower() or path.name
        kind = source_kind(path, repo)
        key = (kind, extension)
        bucket = by_key.setdefault(
            key,
            {
                "area": kind,
                "extension": extension,
                "file_count": 0,
                "bytes": 0,
                "physical_lines": 0,
                "nonblank_lines": 0,
            },
        )
        bucket["file_count"] += 1
        bucket["bytes"] += size
        if is_probably_text(path):
            text_files += 1
            text = read_text(path)
            lines = text.splitlines()
            bucket["physical_lines"] += len(lines)
            bucket["nonblank_lines"] += sum(1 for line in lines if line.strip())
            total_lines += len(lines)
            nonblank_lines += sum(1 for line in lines if line.strip())
        else:
            binary_files += 1
    rows = sorted(by_key.values(), key=lambda row: (row["area"], row["extension"]))
    summary = {
        "file_count": len(all_files),
        "text_file_count": text_files,
        "binary_file_count": binary_files,
        "total_bytes": total_bytes,
        "physical_lines_in_recognized_text_files": total_lines,
        "nonblank_lines_in_recognized_text_files": nonblank_lines,
        "excluded_directory_names": sorted(DEFAULT_EXCLUDED_DIRS),
    }
    return rows, summary


TEST_ATTRS = {
    "fact_attributes": r"\[(?:Xunit\.)?Fact(?:Attribute)?(?:\s*\([^\]]*\))?\]",
    "theory_attributes": r"\[(?:Xunit\.)?Theory(?:Attribute)?(?:\s*\([^\]]*\))?\]",
    "inline_data_attributes": r"\[(?:Xunit\.)?InlineData(?:Attribute)?\s*\(",
    "member_data_attributes": r"\[(?:Xunit\.)?MemberData(?:Attribute)?\s*\(",
    "class_data_attributes": r"\[(?:Xunit\.)?ClassData(?:Attribute)?\s*\(",
    "property_attributes": r"\[(?:FsCheck\.Xunit\.)?Property(?:Attribute)?(?:\s*\([^\]]*\))?\]",
    "skipped_declarations": r"\bSkip\s*=\s*\"",
}


def parse_csproj_packages(path: Path) -> list[str]:
    try:
        return all_items(ET.parse(path).getroot(), "PackageReference")
    except ET.ParseError:
        return []


def collect_tests(repo: Path) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    totals = Counter()
    tests_root = repo / "tests"
    if tests_root.exists():
        for project in sorted(tests_root.rglob("*.csproj")):
            project_dir = project.parent
            cs_files = [p for p in iter_files(project_dir) if p.suffix.lower() == ".cs"]
            counts = Counter()
            files_with_tests = 0
            categories: set[str] = set()
            for file in cs_files:
                text = read_text(file)
                file_has_test = False
                for key, pattern in TEST_ATTRS.items():
                    count = len(re.findall(pattern, text, flags=re.MULTILINE))
                    counts[key] += count
                    if key in {"fact_attributes", "theory_attributes", "property_attributes"} and count:
                        file_has_test = True
                for match in re.finditer(r"\[(?:Trait|Xunit\.Trait)\s*\(\s*\"Category\"\s*,\s*\"([^\"]+)\"", text):
                    categories.add(match.group(1))
                if file_has_test:
                    files_with_tests += 1
            packages = parse_csproj_packages(project)
            row = {
                "project": project.stem,
                "path": rel(project, repo),
                "source_files": len(cs_files),
                "files_with_test_declarations": files_with_tests,
                **{key: counts[key] for key in TEST_ATTRS},
                "categories": sorted(categories),
                "test_packages": [
                    p
                    for p in packages
                    if any(token in p.lower() for token in ("xunit", "test", "coverlet", "fscheck", "fluentassert"))
                ],
                "evidence_class": "static_declaration_inventory",
            }
            rows.append(row)
            totals.update({key: counts[key] for key in TEST_ATTRS})

    frontend = repo / "webUI"
    frontend_files: list[Path] = []
    if frontend.exists():
        for path in iter_files(frontend):
            lower = path.name.lower()
            if any(token in lower for token in (".test.", ".spec.")) and path.suffix.lower() in {
                ".ts",
                ".tsx",
                ".js",
                ".jsx",
                ".mjs",
            }:
                frontend_files.append(path)
        declared = 0
        for file in frontend_files:
            text = read_text(file)
            declared += len(re.findall(r"(?m)(?<![A-Za-z0-9_])(?:it|test)\s*\(", text))
        rows.append(
            {
                "project": "webUI",
                "path": "webUI/package.json",
                "source_files": len(frontend_files),
                "files_with_test_declarations": len(frontend_files),
                "fact_attributes": 0,
                "theory_attributes": 0,
                "inline_data_attributes": 0,
                "member_data_attributes": 0,
                "class_data_attributes": 0,
                "property_attributes": 0,
                "skipped_declarations": 0,
                "categories": ["frontend", "browser"],
                "test_packages": [],
                "approximate_js_test_blocks": declared,
                "evidence_class": "static_declaration_inventory",
            }
        )
    summary = dict(totals)
    summary.update(
        {
            "dotnet_test_project_count": sum(1 for r in rows if r["project"] != "webUI"),
            "frontend_test_file_count": len(frontend_files),
            "frontend_approximate_test_block_count": next(
                (r.get("approximate_js_test_blocks", 0) for r in rows if r["project"] == "webUI"), 0
            ),
            "note": "Attribute and source declaration counts are not executed test-case counts.",
        }
    )
    return rows, summary


def combine_route(base: str, suffix: str) -> str:
    base = base.strip().strip('"').strip("/")
    suffix = suffix.strip().strip('"').strip("/")
    if base and suffix:
        return "/" + base + "/" + suffix
    if base:
        return "/" + base
    if suffix:
        return "/" + suffix
    return "/"


def auth_from_attrs(class_attrs: str, method_attrs: str) -> tuple[str, str, str]:
    combined = class_attrs + "\n" + method_attrs
    if "[AllowAnonymous" in method_attrs:
        return "anonymous", "", ""
    role_matches = re.findall(r"Authorize\s*\(\s*Roles\s*=\s*\"([^\"]+)\"", method_attrs)
    if not role_matches:
        role_matches = re.findall(r"Authorize\s*\(\s*Roles\s*=\s*\"([^\"]+)\"", class_attrs)
    policy_matches = re.findall(r"Authorize\s*\(\s*Policy\s*=\s*\"([^\"]+)\"", method_attrs)
    if not policy_matches:
        policy_matches = re.findall(r"Authorize\s*\(\s*Policy\s*=\s*\"([^\"]+)\"", class_attrs)
    if role_matches:
        return "roles", role_matches[-1], ""
    if policy_matches:
        return "policy", "", policy_matches[-1]
    if "[Authorize" in combined:
        return "authenticated", "", ""
    return "unspecified", "", ""


def collect_endpoints(repo: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    controllers_root = repo / "src" / "NatureProtector.Backoffice.Api" / "Controllers"
    if not controllers_root.exists():
        return rows
    for path in sorted(controllers_root.rglob("*.cs")):
        text = read_text(path)
        class_match = re.search(
            r"(?P<attrs>(?:\s*\[[^\]]+\]\s*)*)public\s+(?:sealed\s+)?class\s+(?P<name>\w+Controller)\b",
            text,
            flags=re.MULTILINE,
        )
        if not class_match:
            continue
        class_attrs = class_match.group("attrs") or ""
        route_match = re.search(r"\[Route\(\s*\"([^\"]+)\"\s*\)\]", class_attrs)
        base_route = route_match.group(1) if route_match else ""
        controller = class_match.group("name")
        pattern = re.compile(
            r"(?P<attrs>(?:\s*\[[^\]]+\]\s*)*)"
            r"(?P<signature>public\s+(?:async\s+)?[^\n\{;]+?\s+(?P<method>\w+)\s*\()",
            flags=re.MULTILINE,
        )
        for match in pattern.finditer(text, class_match.end()):
            attrs = match.group("attrs") or ""
            http = re.search(r"\[Http(Get|Post|Put|Delete|Patch|Head|Options)(?:\(\s*\"([^\"]*)\"\s*\))?\]", attrs)
            if not http:
                continue
            verb = http.group(1).upper()
            suffix = http.group(2) or ""
            auth, roles, policy = auth_from_attrs(class_attrs, attrs)
            rows.append(
                {
                    "http_method": verb,
                    "route": combine_route(base_route, suffix),
                    "controller": controller,
                    "action": match.group("method"),
                    "authorization": auth,
                    "roles": roles,
                    "policy": policy,
                    "source": rel(path, repo),
                    "line": text.count("\n", 0, match.start()) + 1,
                    "evidence_class": "static_route_declaration",
                }
            )
    return sorted(rows, key=lambda row: (row["route"], row["http_method"], row["controller"]))


def collect_events(repo: Path) -> list[dict[str, Any]]:
    event_types = repo / "src" / "NatureProtector.Shared" / "Messaging" / "EventTypes.cs"
    if not event_types.exists():
        return []
    text = read_text(event_types)
    declarations = []
    for match in re.finditer(r"public\s+const\s+string\s+(\w+)\s*=\s*\"([^\"]+)\"", text):
        declarations.append((match.group(1), match.group(2), text.count("\n", 0, match.start()) + 1))
    source_files = [p for p in iter_files(repo, ["src"]) if p.suffix.lower() == ".cs"]
    rows = []
    for symbol, event_type, line in declarations:
        refs = []
        producer_candidates = set()
        consumer_candidates = set()
        token = f"EventTypes.{symbol}"
        for path in source_files:
            content = read_text(path)
            count = content.count(token)
            if count:
                relative = rel(path, repo)
                refs.append({"path": relative, "count": count})
                lower = relative.lower()
                if any(key in lower for key in ("publishing", "publisher", "simulator")):
                    producer_candidates.add(relative)
                if any(key in lower for key in ("prevention", "consumer", "processing", "inbox")):
                    consumer_candidates.add(relative)
        rows.append(
            {
                "symbol": symbol,
                "event_type": event_type,
                "declared_in": rel(event_types, repo),
                "declared_line": line,
                "reference_count": sum(item["count"] for item in refs),
                "reference_files": [item["path"] for item in refs],
                "producer_candidate_files": sorted(producer_candidates),
                "consumer_candidate_files": sorted(consumer_candidates),
                "evidence_class": "static_contract_and_reference_inventory",
            }
        )
    return rows


def collect_telemetry(
    repo: Path,
) -> tuple[list[dict[str, Any]], list[dict[str, Any]], list[dict[str, Any]], dict[str, Any]]:
    metrics: list[dict[str, Any]] = []
    activities: list[dict[str, Any]] = []
    tag_usage: defaultdict[str, list[dict[str, Any]]] = defaultdict(list)
    meter_names: set[str] = set()
    activity_sources: set[str] = set()
    files = [p for p in iter_files(repo, ["src"]) if p.suffix.lower() == ".cs"]
    metric_pattern = re.compile(
        r"(?P<field>\w+)\s*=\s*Meter\.Create(?P<kind>Counter|Histogram|ObservableCounter|ObservableGauge|UpDownCounter|ObservableUpDownCounter)"
        r"(?:<[^>]+>)?\s*\(\s*\"(?P<name>[^\"]+)\"(?P<args>[^;]*)\);",
        flags=re.DOTALL,
    )
    for path in files:
        text = read_text(path)
        relative = rel(path, repo)
        for match in re.finditer(r"new\s+Meter\(\s*\"([^\"]+)\"", text):
            meter_names.add(match.group(1))
        for match in re.finditer(r"new\s+ActivitySource\(\s*\"([^\"]+)\"", text):
            activity_sources.add(match.group(1))
        for match in metric_pattern.finditer(text):
            args = match.group("args")
            unit_match = re.search(r"unit\s*:\s*\"([^\"]+)\"", args)
            desc_match = re.search(r"description\s*:\s*\"([^\"]+)\"", args)
            metrics.append(
                {
                    "name": match.group("name"),
                    "instrument_kind": match.group("kind"),
                    "field": match.group("field"),
                    "unit": unit_match.group(1) if unit_match else "",
                    "description": desc_match.group(1) if desc_match else "",
                    "source": relative,
                    "line": text.count("\n", 0, match.start()) + 1,
                    "evidence_class": "static_instrument_declaration",
                }
            )
        for match in re.finditer(r"\.StartActivity\(\s*(?:\$)?\"([^\"]+)\"", text):
            name = match.group(1)
            activities.append(
                {
                    "name_or_template": name,
                    "source": relative,
                    "line": text.count("\n", 0, match.start()) + 1,
                    "evidence_class": "static_activity_creation",
                }
            )
        for match in re.finditer(r"\.SetTag\(\s*TelemetryTags\.(\w+)", text):
            tag_usage[match.group(1)].append(
                {
                    "source": relative,
                    "line": text.count("\n", 0, match.start()) + 1,
                }
            )
    tag_constants: dict[str, str] = {}
    for path in files:
        if path.name != "TelemetryTags.cs":
            continue
        text = read_text(path)
        for match in re.finditer(r"public\s+const\s+string\s+(\w+)\s*=\s*\"([^\"]+)\"", text):
            tag_constants[match.group(1)] = match.group(2)
    tags = []
    for symbol in sorted(set(tag_constants) | set(tag_usage)):
        usages = tag_usage.get(symbol, [])
        tags.append(
            {
                "symbol": symbol,
                "tag_name": tag_constants.get(symbol, ""),
                "usage_count": len(usages),
                "usage_files": sorted(set(item["source"] for item in usages)),
                "evidence_class": "static_tag_declaration_and_usage",
            }
        )
    summary = {
        "meter_names": sorted(meter_names),
        "activity_source_names": sorted(activity_sources),
        "metric_instrument_count": len(metrics),
        "unique_metric_name_count": len({m["name"] for m in metrics}),
        "activity_creation_count": len(activities),
        "unique_activity_name_or_template_count": len({a["name_or_template"] for a in activities}),
        "telemetry_tag_count": len(tags),
    }
    return (
        sorted(metrics, key=lambda row: (row["name"], row["source"])),
        sorted(activities, key=lambda row: (row["name_or_template"], row["source"], row["line"])),
        tags,
        summary,
    )


def balanced_segment(text: str, start: int, open_char: str = "(", close_char: str = ")") -> tuple[str, int]:
    depth = 0
    in_string = False
    verbatim = False
    escaped = False
    for index in range(start, len(text)):
        char = text[index]
        if in_string:
            if verbatim:
                if char == '"':
                    if index + 1 < len(text) and text[index + 1] == '"':
                        continue
                    in_string = False
            else:
                if escaped:
                    escaped = False
                elif char == "\\":
                    escaped = True
                elif char == '"':
                    in_string = False
            continue
        if char == "@" and index + 1 < len(text) and text[index + 1] == '"':
            in_string = True
            verbatim = True
            continue
        if char == '"':
            in_string = True
            verbatim = False
            continue
        if char == open_char:
            depth += 1
        elif char == close_char:
            depth -= 1
            if depth == 0:
                return text[start : index + 1], index + 1
    return text[start:], len(text)


def extract_brace_block(text: str, opening_index: int) -> tuple[str, int]:
    """Return a C# brace block, including braces, while tolerating strings."""
    return balanced_segment(text, opening_index, "{", "}")


def collect_migrations_and_schema(
    repo: Path,
) -> tuple[
    list[dict[str, Any]],
    list[dict[str, Any]],
    list[dict[str, Any]],
    list[dict[str, Any]],
    list[dict[str, Any]],
    dict[str, Any],
]:
    mig_root = repo / "src" / "NatureProtector.Infrastructure.Postgres" / "Migrations"
    migrations: list[dict[str, Any]] = []
    tables: dict[tuple[str, str], dict[str, Any]] = {}
    columns: dict[tuple[str, str, str], dict[str, Any]] = {}
    indexes: dict[tuple[str, str, str], dict[str, Any]] = {}
    schemas: set[str] = set()
    if not mig_root.exists():
        return migrations, [], [], [], [], {}

    migration_files = sorted(
        p for p in mig_root.glob("*.cs") if re.match(r"^\d+_.+\.cs$", p.name) and not p.name.endswith(".Designer.cs")
    )
    for path in migration_files:
        text = read_text(path)
        migration_attr = re.search(r"\[Migration\(\"([^\"]+)\"\)\]", text)
        migration_id = migration_attr.group(1) if migration_attr else path.stem
        class_match = re.search(r"(?:public\s+)?(?:sealed\s+|partial\s+)?class\s+(\w+)\s*:\s*Migration", text)
        operations = Counter()
        for operation in (
            "CreateTable",
            "DropTable",
            "AddColumn",
            "DropColumn",
            "AlterColumn",
            "CreateIndex",
            "DropIndex",
            "AddForeignKey",
            "Sql",
        ):
            operations[operation] = len(re.findall(rf"migrationBuilder\.{operation}\s*(?:<[^>]+>)?\s*\(", text))
        migrations.append(
            {
                "migration_id": migration_id,
                "class_name": class_match.group(1) if class_match else "",
                "path": rel(path, repo),
                "create_table_operations": operations["CreateTable"],
                "add_column_operations": operations["AddColumn"],
                "alter_column_operations": operations["AlterColumn"],
                "create_index_operations": operations["CreateIndex"],
                "raw_sql_operations": operations["Sql"],
                "evidence_class": "static_migration_inventory",
            }
        )

    # Use the latest EF model snapshot as the best static representation of the
    # effective model. Raw SQL migrations are added afterwards because EF does
    # not include those objects in the model snapshot.
    snapshot = mig_root / "NatureProtectorControlDbContextModelSnapshot.cs"
    if snapshot.exists():
        text = read_text(snapshot)
        cursor = 0
        marker = 'modelBuilder.Entity("'
        while True:
            pos = text.find(marker, cursor)
            if pos < 0:
                break
            entity_start = pos + len(marker)
            entity_end = text.find('"', entity_start)
            entity_name = text[entity_start:entity_end]
            brace_start = text.find("{", entity_end)
            if brace_start < 0:
                break
            block, next_pos = extract_brace_block(text, brace_start)
            cursor = next_pos
            table_match = re.search(r'b\.ToTable\("([^"]+)",\s*"([^"]+)"\)', block)
            if not table_match:
                continue
            table, schema = table_match.groups()
            schemas.add(schema)
            tables[(schema, table)] = {
                "schema": schema,
                "table": table,
                "model_entity": entity_name,
                "introduced_by": "model_snapshot",
                "source": rel(snapshot, repo),
                "declaration_kind": "ef_model_snapshot",
                "evidence_class": "static_effective_model_snapshot",
            }
            property_matches = list(re.finditer(r'b\.Property<([^>]+)>\("([^"]+)"\)', block))
            for i, prop in enumerate(property_matches):
                prop_end = property_matches[i + 1].start() if i + 1 < len(property_matches) else len(block)
                prop_segment = block[prop.start() : prop_end]
                # Stop at key/index/table configuration when this is the final property.
                cut = re.search(r"\n\s*b\.(?:HasKey|HasIndex|ToTable)\b", prop_segment)
                if cut:
                    prop_segment = prop_segment[: cut.start()]
                clr_type, column = prop.groups()
                type_match = re.search(r'\.HasColumnType\("([^"]+)"\)', prop_segment)
                is_required = ".IsRequired()" in prop_segment
                nullable = "true" if clr_type.endswith("?") or (clr_type == "string" and not is_required) else "false"
                columns[(schema, table, column)] = {
                    "schema": schema,
                    "table": table,
                    "column": column,
                    "clr_type": clr_type,
                    "sql_type": type_match.group(1) if type_match else "",
                    "nullable": nullable,
                    "introduced_by": "model_snapshot",
                    "source": rel(snapshot, repo),
                    "declaration_kind": "ef_model_snapshot_property",
                }
            index_matches = list(re.finditer(r"b\.HasIndex\(([^)]*)\)", block))
            for index_no, idx in enumerate(index_matches, start=1):
                idx_end = index_matches[index_no].start() if index_no < len(index_matches) else len(block)
                idx_segment = block[idx.start() : idx_end]
                cut = re.search(r"\n\s*b\.(?:HasKey|ToTable)\b", idx_segment)
                if cut:
                    idx_segment = idx_segment[: cut.start()]
                cols = re.findall(r'"([^"]+)"', idx.group(1))
                if not cols:
                    continue
                database_name = re.search(r'\.HasDatabaseName\("([^"]+)"\)', idx_segment)
                idx_name = database_name.group(1) if database_name else f"model:{table}:{'+'.join(cols)}"
                indexes[(schema, table, idx_name)] = {
                    "schema": schema,
                    "table": table,
                    "index": idx_name,
                    "columns": cols,
                    "unique": "true" if ".IsUnique()" in idx_segment else "false",
                    "introduced_by": "model_snapshot",
                    "source": rel(snapshot, repo),
                    "declaration_kind": "ef_model_snapshot_index",
                }

    # Add objects created through raw SQL and absent from the model snapshot.
    for path in migration_files:
        text = read_text(path)
        migration_attr = re.search(r"\[Migration\(\"([^\"]+)\"\)\]", text)
        migration_id = migration_attr.group(1) if migration_attr else path.stem
        for raw_table in re.finditer(
            r"(?is)CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?:(\w+)\.)?(\w+)\s*\((.*?)\)\s*;", text
        ):
            schema = raw_table.group(1) or "public"
            table = raw_table.group(2)
            schemas.add(schema)
            tables[(schema, table)] = {
                "schema": schema,
                "table": table,
                "model_entity": "",
                "introduced_by": migration_id,
                "source": rel(path, repo),
                "declaration_kind": "raw_sql_create_table",
                "evidence_class": "static_migration_declaration",
            }
            body = raw_table.group(3)
            for raw_line in body.splitlines():
                clean = raw_line.strip().rstrip(",")
                if not clean or re.match(r"(?i)^(CONSTRAINT|PRIMARY|FOREIGN|UNIQUE|CHECK)\b", clean):
                    continue
                column_match = re.match(r"(?i)^([a-z_][a-z0-9_]*)\s+(.+)$", clean)
                if column_match:
                    col_name, spec = column_match.groups()
                    sql_type = re.split(
                        r"\s+(?:NOT\s+NULL|NULL|PRIMARY\s+KEY|UNIQUE|DEFAULT|REFERENCES)\b",
                        spec,
                        maxsplit=1,
                        flags=re.IGNORECASE,
                    )[0].strip()
                    columns[(schema, table, col_name)] = {
                        "schema": schema,
                        "table": table,
                        "column": col_name,
                        "clr_type": "",
                        "sql_type": sql_type,
                        "nullable": "false"
                        if re.search(r"(?i)\bNOT\s+NULL\b|\bPRIMARY\s+KEY\b", spec)
                        else "true_or_unspecified",
                        "introduced_by": migration_id,
                        "source": rel(path, repo),
                        "declaration_kind": "raw_sql_create_table_column",
                    }
        for raw_idx in re.finditer(
            r"(?is)CREATE\s+(UNIQUE\s+)?INDEX\s+(\w+)\s+ON\s+(?:(\w+)\.)?(\w+)\s*\(([^)]+)\)", text
        ):
            unique, idx_name, schema, table, cols_text = raw_idx.groups()
            schema = schema or "public"
            indexes[(schema, table, idx_name)] = {
                "schema": schema,
                "table": table,
                "index": idx_name,
                "columns": [c.strip().strip('"') for c in cols_text.split(",")],
                "unique": "true" if unique else "false",
                "introduced_by": migration_id,
                "source": rel(path, repo),
                "declaration_kind": "raw_sql_create_index",
            }

    table_rows = sorted(tables.values(), key=lambda row: (row["schema"], row["table"]))
    column_rows = sorted(columns.values(), key=lambda row: (row["schema"], row["table"], row["column"]))
    index_rows = sorted(indexes.values(), key=lambda row: (row["schema"], row["table"], row["index"]))
    schema_rows = [{"schema": name, "evidence_class": "static_effective_model_or_raw_sql"} for name in sorted(schemas)]
    summary = {
        "migration_count": len(migrations),
        "declared_schema_count": len(schema_rows),
        "declared_table_count": len(table_rows),
        "declared_column_count": len(column_rows),
        "declared_index_count": len(index_rows),
        "note": "Modelo estático reconstruído do snapshot EF atual e de migrações SQL em bruto; não é um inventário de uma instância PostgreSQL em execução.",
    }
    return migrations, schema_rows, table_rows, column_rows, index_rows, summary


def collect_workflows(repo: Path) -> list[dict[str, Any]]:
    root = repo / ".github" / "workflows"
    rows = []
    if not root.exists():
        return rows
    for path in sorted(list(root.glob("*.yml")) + list(root.glob("*.yaml"))):
        text = read_text(path)
        name_match = re.search(r"(?m)^name:\s*(.+?)\s*$", text)
        jobs = re.findall(r"(?m)^  ([A-Za-z0-9_-]+):\s*$", text[text.find("\njobs:") :] if "\njobs:" in text else "")
        triggers: list[str] = []
        lines = text.splitlines()
        for i, line in enumerate(lines):
            if re.match(r"^on:\s*\[", line):
                triggers.extend(re.findall(r"[A-Za-z_]+", line.split(":", 1)[1]))
                break
            if re.match(r"^on:\s*$", line):
                for following in lines[i + 1 :]:
                    if following and not following.startswith(" "):
                        break
                    match = re.match(r"^  ([A-Za-z_]+):", following)
                    if match:
                        triggers.append(match.group(1))
                break
        rows.append(
            {
                "path": rel(path, repo),
                "name": (name_match.group(1).strip("\"'") if name_match else path.stem),
                "triggers": sorted(set(triggers)),
                "job_count": len(set(jobs)),
                "jobs": sorted(set(jobs)),
                "uses_oidc_permission": bool(re.search(r"id-token:\s*write", text)),
                "contains_apply_token": bool(
                    re.search(r"(?i)terraform\s+apply|gcloud\s+.*deploy|kubectl\s+apply", text)
                ),
                "evidence_class": "static_workflow_inventory",
            }
        )
    return rows


def collect_compose_services(repo: Path) -> list[dict[str, Any]]:
    rows = []
    for path in sorted(repo.glob("docker-compose*.yml")) + sorted(repo.glob("docker-compose*.yaml")):
        lines = read_text(path).splitlines()
        in_services = False
        current: dict[str, Any] | None = None
        for line_no, line in enumerate(lines, start=1):
            if re.match(r"^services:\s*$", line):
                in_services = True
                continue
            if in_services and line and not line.startswith(" "):
                in_services = False
                current = None
            if not in_services:
                continue
            service_match = re.match(r"^  ([A-Za-z0-9_.-]+):\s*$", line)
            if service_match:
                current = {
                    "compose_file": rel(path, repo),
                    "service": service_match.group(1),
                    "image": "",
                    "build": "",
                    "profiles": [],
                    "declared_line": line_no,
                    "evidence_class": "static_compose_inventory",
                }
                rows.append(current)
                continue
            if current:
                image_match = re.match(r"^    image:\s*(.+?)\s*$", line)
                build_match = re.match(r"^    build:\s*(.+?)\s*$", line)
                if image_match:
                    current["image"] = image_match.group(1).strip("\"'")
                if build_match:
                    current["build"] = build_match.group(1).strip("\"'")
        # No YAML dependency: nested build contexts are intentionally not resolved.
    return sorted(rows, key=lambda row: (row["compose_file"], row["service"]))


def collect_frontend(repo: Path) -> dict[str, Any]:
    package_path = repo / "webUI" / "package.json"
    if not package_path.exists():
        return {}
    try:
        package = json.loads(read_text(package_path))
    except json.JSONDecodeError:
        return {"path": rel(package_path, repo), "parse_error": True}
    source_files = (
        [
            p
            for p in iter_files(repo / "webUI" / "src")
            if p.suffix.lower() in {".ts", ".tsx", ".js", ".jsx", ".css", ".scss"}
        ]
        if (repo / "webUI" / "src").exists()
        else []
    )
    routes = []
    for path in source_files:
        text = read_text(path)
        for match in re.finditer(r"\bpath\s*:\s*['\"]([^'\"]+)['\"]", text):
            routes.append(
                {"route": match.group(1), "source": rel(path, repo), "line": text.count("\n", 0, match.start()) + 1}
            )
    return {
        "path": rel(package_path, repo),
        "name": package.get("name", ""),
        "version": package.get("version", ""),
        "script_count": len(package.get("scripts", {})),
        "scripts": package.get("scripts", {}),
        "dependency_count": len(package.get("dependencies", {})),
        "dev_dependency_count": len(package.get("devDependencies", {})),
        "source_file_count": len(source_files),
        "declared_route_count": len(routes),
        "declared_routes": sorted(routes, key=lambda item: (item["route"], item["source"])),
        "evidence_class": "static_frontend_inventory",
    }


def collect_repo_identity(repo: Path) -> dict[str, Any]:
    result: dict[str, Any] = {
        "repository_name": repo.name,
        "repository_path": str(repo.resolve()),
        "git_available": False,
        "git_repository": False,
        "branch": "unknown",
        "commit": "unknown",
        "working_tree_state": "unknown",
    }
    try:
        subprocess.run(["git", "--version"], check=True, capture_output=True, text=True)
        result["git_available"] = True
        probe = subprocess.run(
            ["git", "-C", str(repo), "rev-parse", "--is-inside-work-tree"], capture_output=True, text=True
        )
        if probe.returncode == 0 and probe.stdout.strip() == "true":
            result["git_repository"] = True
            branch = subprocess.run(
                ["git", "-C", str(repo), "branch", "--show-current"], capture_output=True, text=True
            )
            commit = subprocess.run(["git", "-C", str(repo), "rev-parse", "HEAD"], capture_output=True, text=True)
            status = subprocess.run(["git", "-C", str(repo), "status", "--porcelain"], capture_output=True, text=True)
            result["branch"] = branch.stdout.strip() or "detached"
            result["commit"] = commit.stdout.strip() or "unknown"
            entries = [line for line in status.stdout.splitlines() if line.strip()]
            result["working_tree_state"] = "clean" if not entries else "dirty"
            result["dirty_entry_count"] = len(entries)
    except (OSError, subprocess.SubprocessError):
        pass
    return result


def markdown_table(rows: Sequence[Sequence[Any]], headers: Sequence[str]) -> list[str]:
    lines = ["| " + " | ".join(headers) + " |", "| " + " | ".join("---" for _ in headers) + " |"]
    for row in rows:
        lines.append("| " + " | ".join(str(value).replace("|", "\\|") for value in row) + " |")
    return lines


def render_summary(summary: dict[str, Any]) -> str:
    counts = summary["counts"]
    lines = [
        "# NatureProtector — Fase 1: inventário estático do repositório",
        "",
        f"- Gerado em UTC: `{summary['generated_at_utc']}`",
        f"- Versão do coletor: `{summary['collector_version']}`",
        f"- Baseline: `{summary['baseline_id']}`",
        "- Classe de evidência: **STATIC_REPOSITORY_INVENTORY**",
        "- Execução de runtime: **NÃO REALIZADA**",
        "",
        "## Contagens principais",
        "",
    ]
    lines += markdown_table(
        [
            ("Ficheiros do repositório (com exclusões)", counts["repository_files"]),
            ("Linhas físicas em ficheiros de texto reconhecidos", counts["recognized_text_lines"]),
            ("Projetos .NET", counts["dotnet_projects"]),
            ("Projetos .NET de produto/biblioteca", counts["dotnet_product_projects"]),
            ("Projetos .NET de testes", counts["dotnet_test_projects"]),
            ("Projetos de benchmark", counts["benchmark_projects"]),
            ("Atributos Fact", counts["fact_attributes"]),
            ("Atributos Theory", counts["theory_attributes"]),
            ("Atributos InlineData", counts["inline_data_attributes"]),
            ("Rotas API declaradas", counts["api_endpoints"]),
            ("Tipos de evento constantes", counts["event_types"]),
            ("Instrumentos de métricas", counts["telemetry_metrics"]),
            ("Criações de activities", counts["telemetry_activities"]),
            ("Migrações EF", counts["migrations"]),
            ("Schemas PostgreSQL declarados", counts["database_schemas"]),
            ("Tabelas PostgreSQL declaradas", counts["database_tables"]),
            ("Colunas PostgreSQL declaradas", counts["database_columns"]),
            ("Índices PostgreSQL declarados", counts["database_indexes"]),
            ("Workflows GitHub Actions", counts["workflows"]),
            ("Declarações de serviços Compose", counts["compose_services"]),
        ],
        ["Métrica", "Valor"],
    )
    lines += [
        "",
        "## Interpretação",
        "",
        "Os valores foram extraídos dos ficheiros de código, projetos, migrações, workflows e configurações do snapshot fornecido. Podem ser usados para descrever a estrutura implementada dessa versão.",
        "",
        "Não provam que os testes passam, que as migrações foram aplicadas, que os endpoints respondem, que a telemetria é emitida, que os serviços Docker arrancam, que os workflows terminam com sucesso ou que existem metas de desempenho cumpridas. Essas afirmações exigem fases posteriores de execução.",
        "",
        "## Datasets produzidos",
        "",
        "- `projects.csv` — projetos .NET, referências entre projetos e packages.",
        "- `source-inventory.csv` — ficheiros, bytes e linhas por área e extensão.",
        "- `test-inventory.csv` — declarações estáticas de testes por projeto.",
        "- `endpoints.csv` — rotas de controllers e metadados estáticos de autorização.",
        "- `event-catalog.csv` — tipos de evento e referências candidatas.",
        "- `telemetry-metrics.csv`, `telemetry-activities.csv` e `telemetry-tags.csv`.",
        "- `migrations.csv`, `database-schemas.csv`, `database-tables.csv`, `database-columns.csv` e `database-indexes.csv`.",
        "- `workflows.csv`, `compose-services.csv` e `frontend-inventory.json`.",
        "- `inventory.json` — inventário consolidado legível por máquina.",
        "- `SHA256SUMS.txt` — hashes de todos os ficheiros de evidência gerados.",
        "",
        "## Limitações conhecidas",
        "",
        "- O ZIP fornecido não contém o histórico `.git`; a identidade de branch e commit continua a ser a definida pela Fase 0.",
        "- As contagens de testes representam declarações no código e não casos efetivamente executados; teorias podem expandir-se em vários casos.",
        "- O modelo da base de dados é reconstruído do snapshot EF atual e das migrações SQL em bruto; não é o catálogo de uma instância PostgreSQL em execução.",
        "- A extração de endpoints cobre atributos de MVC controllers e não substitui um OpenAPI gerado por uma build em execução.",
        "- Workflows e serviços Compose são declarações estáticas, não prova de execução bem-sucedida.",
        "",
    ]
    return "\n".join(lines)


def export_pair(
    output: Path, stem: str, rows: Sequence[dict[str, Any]], fieldnames: Sequence[str] | None = None
) -> None:
    write_json(output / f"{stem}.json", list(rows))
    write_csv(output / f"{stem}.csv", rows, fieldnames)


def build_inventory(repo: Path, output: Path, baseline_id: str) -> dict[str, Any]:
    generated = utc_now()
    output.mkdir(parents=True, exist_ok=True)
    projects = collect_projects(repo)
    source_rows, source_summary = collect_source_inventory(repo)
    tests, test_summary = collect_tests(repo)
    endpoints = collect_endpoints(repo)
    events = collect_events(repo)
    metrics, activities, tags, telemetry_summary = collect_telemetry(repo)
    migrations, db_schemas, db_tables, db_columns, db_indexes, db_summary = collect_migrations_and_schema(repo)
    workflows = collect_workflows(repo)
    compose = collect_compose_services(repo)
    frontend = collect_frontend(repo)
    identity = collect_repo_identity(repo)

    consolidated = {
        "schema_version": "1.0",
        "collector_version": SCRIPT_VERSION,
        "generated_at_utc": generated,
        "baseline_id": baseline_id,
        "evidence_class": "STATIC_REPOSITORY_INVENTORY",
        "runtime_execution_performed": False,
        "repository_identity": identity,
        "source_summary": source_summary,
        "test_summary": test_summary,
        "telemetry_summary": telemetry_summary,
        "database_summary": db_summary,
        "projects": projects,
        "source_inventory": source_rows,
        "tests": tests,
        "endpoints": endpoints,
        "events": events,
        "telemetry_metrics": metrics,
        "telemetry_activities": activities,
        "telemetry_tags": tags,
        "migrations": migrations,
        "database_schemas": db_schemas,
        "database_tables": db_tables,
        "database_columns": db_columns,
        "database_indexes": db_indexes,
        "workflows": workflows,
        "compose_services": compose,
        "frontend": frontend,
    }
    counts = {
        "repository_files": source_summary["file_count"],
        "recognized_text_lines": source_summary["physical_lines_in_recognized_text_files"],
        "dotnet_projects": len(projects),
        "dotnet_product_projects": sum(1 for p in projects if p["group"] == "src"),
        "dotnet_test_projects": sum(1 for p in projects if p["is_test_project"]),
        "benchmark_projects": sum(1 for p in projects if p["is_benchmark_project"]),
        "fact_attributes": test_summary.get("fact_attributes", 0),
        "theory_attributes": test_summary.get("theory_attributes", 0),
        "inline_data_attributes": test_summary.get("inline_data_attributes", 0),
        "api_endpoints": len(endpoints),
        "event_types": len(events),
        "telemetry_metrics": len(metrics),
        "telemetry_activities": len(activities),
        "migrations": len(migrations),
        "database_schemas": len(db_schemas),
        "database_tables": len(db_tables),
        "database_columns": len(db_columns),
        "database_indexes": len(db_indexes),
        "workflows": len(workflows),
        "compose_services": len(compose),
    }
    summary = {
        "generated_at_utc": generated,
        "collector_version": SCRIPT_VERSION,
        "baseline_id": baseline_id,
        "counts": counts,
        "repository_identity": identity,
        "limitations": [
            "Static repository inspection only; no application, test, database, Docker, cloud or benchmark execution.",
            "Theories and data-driven tests are not equivalent to executed test case counts.",
            "Database catalogue is reconstructed from migration declarations, not from a live PostgreSQL instance.",
        ],
    }

    export_pair(output, "projects", projects)
    export_pair(output, "source-inventory", source_rows)
    export_pair(output, "test-inventory", tests)
    export_pair(output, "endpoints", endpoints)
    export_pair(output, "event-catalog", events)
    export_pair(output, "telemetry-metrics", metrics)
    export_pair(output, "telemetry-activities", activities)
    export_pair(output, "telemetry-tags", tags)
    export_pair(output, "migrations", migrations)
    export_pair(output, "database-schemas", db_schemas)
    export_pair(output, "database-tables", db_tables)
    export_pair(output, "database-columns", db_columns)
    export_pair(output, "database-indexes", db_indexes)
    export_pair(output, "workflows", workflows)
    export_pair(output, "compose-services", compose)
    write_json(output / "frontend-inventory.json", frontend)
    write_json(output / "inventory.json", consolidated)
    write_json(output / "inventory-summary.json", summary)
    (output / "inventory-summary.md").write_text(render_summary(summary), encoding="utf-8")

    hash_targets = sorted(p for p in output.iterdir() if p.is_file() and p.name != "SHA256SUMS.txt")
    hash_lines = [f"{sha256_file(path)}  {path.name}" for path in hash_targets]
    (output / "SHA256SUMS.txt").write_text("\n".join(hash_lines) + "\n", encoding="utf-8")
    return summary


def validate_repo(repo: Path) -> None:
    required = [repo / "NatureProtector.sln", repo / "src", repo / "tests", repo / "webUI"]
    missing = [str(path) for path in required if not path.exists()]
    if missing:
        raise SystemExit("Repository root validation failed; missing: " + ", ".join(missing))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path.cwd(), help="NatureProtector repository root")
    parser.add_argument("--output", type=Path, help="Output directory")
    parser.add_argument("--baseline-id", default="unknown", help="Phase 0 baseline/campaign identifier")
    parser.add_argument("--print-json", action="store_true", help="Print the summary JSON to stdout")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo = args.repo.resolve()
    validate_repo(repo)
    output = (args.output or (repo / "artifacts" / "report-evidence" / args.baseline_id / "01-inventory")).resolve()
    summary = build_inventory(repo, output, args.baseline_id)
    print("PHASE_1_STATUS=STATIC_INVENTORY_COLLECTED")
    print(f"BASELINE_ID={args.baseline_id}")
    print(f"OUTPUT={output}")
    for key, value in summary["counts"].items():
        print(f"{key.upper()}={value}")
    print(f"SHA256SUMS={output / 'SHA256SUMS.txt'}")
    if args.print_json:
        print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
