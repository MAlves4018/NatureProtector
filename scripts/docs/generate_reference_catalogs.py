#!/usr/bin/env python3
"""Generate documentation reference tables from the current C# authorities.

This script deliberately extracts only stable facts needed by documentation:
roles/capabilities and the closed engineering operation catalog. It does not
attempt to be a complete C# parser.
"""
from __future__ import annotations

import argparse
import csv
import re
from pathlib import Path


def extract_balanced_call(source: str, start: int) -> str:
    open_pos = source.find("(", start)
    if open_pos < 0:
        raise ValueError("Invocation has no opening parenthesis")
    depth = 0
    in_string = False
    escaped = False
    for i in range(open_pos, len(source)):
        ch = source[i]
        if in_string:
            if escaped:
                escaped = False
            elif ch == "\\":
                escaped = True
            elif ch == '"':
                in_string = False
            continue
        if ch == '"':
            in_string = True
        elif ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
            if depth == 0:
                return source[start : i + 1]
    raise ValueError("Unbalanced invocation")


def generate(repo: Path) -> tuple[int, int]:
    output = repo / "docs/reference/generated"
    output.mkdir(parents=True, exist_ok=True)

    cap_path = repo / "src/NatureProtector.Backoffice.Api/Operations/Authorization/OperationCapabilities.cs"
    cap_source = cap_path.read_text(encoding="utf-8")
    constants = dict(re.findall(r'public const string (\w+) = "([^"]+)";', cap_source))
    roles: dict[str, list[str]] = {}
    for role, body in re.findall(r'\["([^"]+)"\]\s*=\s*\[(.*?)\]\s*,?', cap_source, flags=re.S):
        roles[role] = re.findall(r"OperationCapabilities\.([A-Za-z0-9_]+)", body)

    with (output / "role-capability-matrix.csv").open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(["role", "capability_constant", "capability_value"])
        for role in sorted(roles):
            for constant in roles[role]:
                writer.writerow([role, constant, constants.get(constant, "")])

    op_path = repo / "src/NatureProtector.Backoffice.Api/Operations/Services/OperationCatalog.cs"
    op_source = op_path.read_text(encoding="utf-8")
    operations: list[list[str]] = []
    for match in re.finditer(r"\b(Quality|Evidence|Deployment|Cloud)\s*\(", op_source):
        call = extract_balanced_call(op_source, match.start())
        literals = re.findall(r'"((?:\\.|[^"])*)"', call)
        if len(literals) < 2:
            continue
        category = match.group(1).lower()
        operation_id, display_name = literals[0], literals[1]
        blocked = next((value for value in literals if value.startswith("blocked-")), None)
        availability_match = re.search(r'availability\s*:\s*"([^"]+)"', call)
        availability = availability_match.group(1) if availability_match else blocked or "implemented"
        evidence_match = re.search(r'evidenceLevel\s*:\s*"([^"]+)"', call)
        evidence = evidence_match.group(1) if evidence_match else ("NOT_PROVED" if availability.startswith("blocked-") else "IMPLEMENTED_NOT_PROVED")
        operations.append([operation_id, category, display_name, availability, evidence])

    with (output / "operation-catalog.csv").open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(["operation_id", "category", "display_name", "availability", "evidence_level"])
        writer.writerows(operations)

    return sum(len(v) for v in roles.values()), len(operations)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    capabilities, operations = generate(args.repo.resolve())
    print(f"role-capability rows: {capabilities}")
    print(f"operations: {operations}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
