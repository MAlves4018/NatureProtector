#!/usr/bin/env python3
"""Validate the Phase 5 control-plane decomposition contract."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path
from typing import Any

METHOD_RE = re.compile(
    r"^\s*(?P<public>public\s+)?(?:async\s+)?(?:[A-Za-z_][\w.<>,?\[\]]*\s+)+(?P<name>[A-Za-z_]\w*)\s*\(",
    re.MULTILINE,
)
PROPERTY_RE = re.compile(
    r"^\s*(?:public\s+)?[A-Za-z_][\w.<>,?\[\]]*\s+(?P<name>[A-Za-z_]\w*)\s*(?:\{|=>)",
    re.MULTILINE,
)


def sha256_text(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def extract_slice(text: str, slice_id: str) -> str | None:
    start_marker = f'    // <phase5-slice id="{slice_id}">\n'
    end_marker = "    // </phase5-slice>\n"
    start = text.find(start_marker)
    if start < 0:
        return None
    start += len(start_marker)
    end = text.find(end_marker, start)
    if end < 0:
        return None
    return text[start:end]


def balanced_csharp(text: str) -> bool:
    depth = 0
    i = 0
    state = "code"
    while i < len(text):
        ch = text[i]
        nxt = text[i + 1] if i + 1 < len(text) else ""
        if state == "line_comment":
            if ch == "\n":
                state = "code"
        elif state == "block_comment":
            if ch == "*" and nxt == "/":
                state = "code"
                i += 1
        elif state == "string":
            if ch == "\\":
                i += 1
            elif ch == '"':
                state = "code"
        elif state == "char":
            if ch == "\\":
                i += 1
            elif ch == "'":
                state = "code"
        else:
            if ch == "/" and nxt == "/":
                state = "line_comment"
                i += 1
            elif ch == "/" and nxt == "*":
                state = "block_comment"
                i += 1
            elif ch == '"':
                state = "string"
            elif ch == "'":
                state = "char"
            elif ch == "{":
                depth += 1
            elif ch == "}":
                depth -= 1
                if depth < 0:
                    return False
        i += 1
    return depth == 0 and state not in {"block_comment", "string", "char"}


def method_names(text: str, *, require_public: bool) -> set[str]:
    return {
        match.group("name")
        for match in METHOD_RE.finditer(text)
        if not require_public or match.group("public") is not None
    }


def public_property_names(text: str) -> set[str]:
    names: set[str] = set()
    for line in text.splitlines():
        if not line.lstrip().startswith("public "):
            continue
        match = PROPERTY_RE.match(line)
        if match and "(" not in line:
            names.add(match.group("name"))
    return names


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=".")
    parser.add_argument("--output")
    args = parser.parse_args()

    repo = Path(args.repo).resolve()
    contract_path = repo / "config/quality/control-plane-decomposition.json"
    interface_path = repo / "src/NatureProtector.Backoffice.Api/ControlPlane/Services/IControlPlaneService.cs"
    checks: list[dict[str, Any]] = []
    failures: list[str] = []

    def check(name: str, condition: bool, detail: str) -> None:
        checks.append({"name": name, "status": "PASS" if condition else "FAIL", "detail": detail})
        if not condition:
            failures.append(f"{name}: {detail}")

    check("contract-exists", contract_path.is_file(), str(contract_path))
    check("interface-exists", interface_path.is_file(), str(interface_path))
    if not contract_path.is_file() or not interface_path.is_file():
        return 1

    contract = json.loads(contract_path.read_text(encoding="utf-8"))
    check("schema-version", contract.get("schema_version") == 1, str(contract.get("schema_version")))
    slices = contract.get("slices", [])
    slice_ids = [item.get("id") for item in slices]
    check("slice-ids-unique", len(slice_ids) == len(set(slice_ids)), json.dumps(slice_ids))

    expected_files = sorted({item["file"] for item in slices})
    actual_files = sorted(
        path.relative_to(repo).as_posix()
        for path in (repo / "src/NatureProtector.Backoffice.Api/ControlPlane/Services").glob(
            "PostgresControlPlaneService*.cs"
        )
    )
    check("feature-file-set", actual_files == expected_files, json.dumps(actual_files))

    combined_text = ""
    for relative in expected_files:
        path = repo / relative
        check(f"file-exists:{relative}", path.is_file(), relative)
        if not path.is_file():
            continue
        text = path.read_text(encoding="utf-8")
        combined_text += "\n" + text
        check(f"partial-class:{relative}", "public sealed partial class PostgresControlPlaneService" in text, relative)
        check(f"balanced-csharp:{relative}", balanced_csharp(text), relative)
        line_count = len(text.splitlines())
        limit = (
            contract["limits"]["max_core_file_lines"]
            if relative.endswith("/PostgresControlPlaneService.cs")
            else contract["limits"]["max_feature_file_lines"]
        )
        check(f"line-limit:{relative}", line_count <= limit, f"{line_count} <= {limit}")

    for item in slices:
        relative = item["file"]
        path = repo / relative
        if not path.is_file():
            continue
        raw = extract_slice(path.read_text(encoding="utf-8"), item["id"])
        check(f"slice-present:{item['id']}", raw is not None, relative)
        if raw is not None:
            check(f"slice-hash:{item['id']}", sha256_text(raw) == item["sha256"], sha256_text(raw))
            check(f"slice-lines:{item['id']}", raw.count("\n") == item["line_count"], str(raw.count("\n")))

    interface_text = interface_path.read_text(encoding="utf-8")
    expected_methods = method_names(interface_text, require_public=False)
    actual_methods = method_names(combined_text, require_public=True)
    check(
        "public-method-contract",
        actual_methods == expected_methods,
        json.dumps({"expected": sorted(expected_methods), "actual": sorted(actual_methods)}),
    )
    expected_properties = {"IsAvailable", "AvailabilityMessage"}
    actual_properties = public_property_names(combined_text)
    check("public-property-contract", actual_properties == expected_properties, json.dumps(sorted(actual_properties)))
    check(
        "constructor-count",
        combined_text.count("public PostgresControlPlaneService(") == 1,
        str(combined_text.count("public PostgresControlPlaneService(")),
    )
    check(
        "no-nonpartial-declaration",
        "public sealed class PostgresControlPlaneService" not in combined_text,
        "all declarations must remain partial",
    )

    payload = {
        "schema_version": 1,
        "status": "PASS" if not failures else "FAIL",
        "summary": {
            "checks": len(checks),
            "failures": len(failures),
            "feature_files": len(expected_files),
            "slices": len(slices),
            "public_methods": len(actual_methods),
        },
        "checks": checks,
        "failures": failures,
    }
    rendered = json.dumps(payload, indent=2, sort_keys=True) + "\n"
    if args.output:
        Path(args.output).write_text(rendered, encoding="utf-8")
    print(rendered, end="")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
