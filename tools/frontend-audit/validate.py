#!/usr/bin/env python3
"""Validate sustainable frontend architecture invariants, not immutable source hashes."""

from __future__ import annotations
import argparse
import json
import re
from pathlib import Path
from typing import Any

EXPORT_RE = re.compile(r"^export\s+(?:interface|type|const|function|class|enum)\s+([A-Za-z_$][\w$]*)", re.MULTILINE)
BARREL_RE = re.compile(r'^export \* from ["\']([^"\']+)["\'];$', re.MULTILINE)
IMPORT_RE = re.compile(r'(?:from\s+|import\s*)["\']([^"\']+)["\']')


def _resolve_relative(source: Path, target: str, candidates: set[Path]) -> Path | None:
    if not target.startswith("."):
        return None
    base = source.parent / target
    variants = [base, base.with_suffix(".ts"), base.with_suffix(".tsx"), base / "index.ts", base / "index.tsx"]
    resolved = {path.resolve(): path for path in candidates}
    for variant in variants:
        if variant.resolve() in resolved:
            return resolved[variant.resolve()]
    return None


def _has_cycle(graph: dict[str, set[str]]) -> bool:
    visiting = set()
    visited = set()

    def visit(node: str) -> bool:
        if node in visiting:
            return True
        if node in visited:
            return False
        visiting.add(node)
        for child in graph.get(node, set()):
            if visit(child):
                return True
        visiting.remove(node)
        visited.add(node)
        return False

    return any(visit(node) for node in graph)


def validate(repo: Path) -> dict[str, Any]:
    repo = repo.resolve()
    failures = []
    checks = []

    def check(name: str, condition: bool, detail: str) -> None:
        checks.append({"name": name, "status": "PASS" if condition else "FAIL", "detail": detail})
        if not condition:
            failures.append(f"{name}: {detail}")

    contract_path = repo / "config/quality/frontend-decomposition.json"
    check("contract-exists", contract_path.is_file(), str(contract_path))
    if not contract_path.is_file():
        return {"schema_version": 2, "status": "FAIL", "checks": checks, "failures": failures}
    contract = json.loads(contract_path.read_text(encoding="utf-8"))
    check("schema-version", contract.get("schema_version") == 2, str(contract.get("schema_version")))
    proof = repo / contract["migration_proof"]
    check("migration-proof-exists", proof.is_file(), str(proof))
    module_paths = []
    for relative, spec in contract.get("workspace_modules", {}).items():
        path = repo / relative
        module_paths.append(path)
        check(f"workspace-file:{relative}", path.is_file(), relative)
        if path.is_file():
            text = path.read_text(encoding="utf-8")
            normalized_chars = len(re.sub(r"\s+", "", text))
            check(
                f"workspace-normalized-size:{relative}",
                normalized_chars <= int(spec["max_normalized_chars"]),
                f"{normalized_chars} <= {spec['max_normalized_chars']}",
            )
            check(f"workspace-marker:{relative}", spec["required_marker"] in text, spec["required_marker"])
    workspace = repo / contract.get("workspace_entrypoint", "")
    text = workspace.read_text(encoding="utf-8") if workspace.is_file() else ""
    workspace_export = contract.get("workspace_export", "Workspace")
    check(
        "workspace-public-export",
        bool(re.search(rf"^export function {re.escape(workspace_export)}\(", text, re.MULTILINE)),
        f"export function {workspace_export}",
    )
    app = repo / "webUI/src/app/App.tsx"
    lazy_import = contract.get("workspace_lazy_import", "import('./components/views/Workspace')")
    check(
        "workspace-lazy-import-preserved",
        app.is_file() and lazy_import in app.read_text(encoding="utf-8"),
        "App.tsx lazy import",
    )
    barrel = repo / contract["types_barrel"]
    check("types-barrel-exists", barrel.is_file(), str(barrel))
    actual_exports = []
    if barrel.is_file():
        barrel_text = barrel.read_text(encoding="utf-8")
        check(
            "types-barrel-exports",
            BARREL_RE.findall(barrel_text) == contract["expected_barrel_exports"],
            json.dumps(BARREL_RE.findall(barrel_text)),
        )
        check("types-barrel-no-definitions", not EXPORT_RE.findall(barrel_text), "barrel contains re-exports only")
    for relative, spec in contract.get("type_modules", {}).items():
        path = repo / relative
        module_paths.append(path)
        check(f"type-file:{relative}", path.is_file(), relative)
        if path.is_file():
            module_text = path.read_text(encoding="utf-8")
            lines = len(module_text.splitlines())
            check(f"type-line-limit:{relative}", lines <= int(spec["max_lines"]), f"{lines} <= {spec['max_lines']}")
            actual_exports.extend(EXPORT_RE.findall(module_text))
    expected = contract.get("expected_type_exports", [])
    check(
        "type-export-set",
        sorted(actual_exports) == sorted(expected),
        f"expected={len(expected)} actual={len(actual_exports)}",
    )
    check(
        "type-exports-unique",
        len(actual_exports) == len(set(actual_exports)),
        f"exports={len(actual_exports)} unique={len(set(actual_exports))}",
    )
    candidates = {path for path in module_paths if path.is_file()}
    graph = {str(path.relative_to(repo)): set() for path in candidates}
    for path in candidates:
        for target in IMPORT_RE.findall(path.read_text(encoding="utf-8")):
            resolved = _resolve_relative(path, target, candidates)
            if resolved:
                graph[str(path.relative_to(repo))].add(str(resolved.relative_to(repo)))
    check("decomposed-modules-acyclic", not _has_cycle(graph), json.dumps({k: sorted(v) for k, v in graph.items()}))
    payload = {
        "schema_version": 2,
        "status": "PASS" if not failures else "FAIL",
        "summary": {
            "checks": len(checks),
            "failures": len(failures),
            "workspace_modules": len(contract.get("workspace_modules", {})),
            "type_exports": len(actual_exports),
        },
        "checks": checks,
        "failures": failures,
    }
    return payload


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=".")
    parser.add_argument("--output")
    args = parser.parse_args()
    payload = validate(Path(args.repo))
    rendered = json.dumps(payload, indent=2, sort_keys=True) + "\n"
    if args.output:
        Path(args.output).write_text(rendered, encoding="utf-8")
    print(rendered, end="")
    return 0 if payload["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
