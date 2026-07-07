#!/usr/bin/env python3
"""Validate shared PowerShell tooling outside the canonical deployment boundary."""

from __future__ import annotations
import argparse
import hashlib
import json
import re
from collections import Counter, defaultdict
from pathlib import Path

FUNCTION_RE = re.compile(r"(?im)^\s*function\s+([A-Za-z0-9_-]+)\s*\{")
IMPORT_RE = re.compile(r"Import-Module\s+\(Join-Path\s+\$PSScriptRoot\s+['\"]([^'\"]+)['\"]\)", re.I)
LOCAL_SCAN_EXCLUDED_PREFIXES = (
    ".nuget/",
    ".testbin/",
    "artifacts/",
    "BenchmarkDotNet.Artifacts/",
    "coveragereport_backend/",
    "coveragereport_core/",
    "docs/RepositorioDocumental/",
    "graphify-out/",
    "TestResults/",
)
LOCAL_SCAN_EXCLUDED_PARTS = {
    ".terraform",
    "bin",
    "node_modules",
    "obj",
}


def function_names(text: str) -> list[str]:
    return FUNCTION_RE.findall(text)


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def strip_ps_comments_and_strings(text: str) -> str:
    out = []
    i = 0
    single = double = block = False
    while i < len(text):
        if block:
            if text.startswith("#>", i):
                block = False
                out.extend("  ")
                i += 2
            else:
                out.append("\n" if text[i] == "\n" else " ")
                i += 1
            continue
        if not single and not double and text.startswith("<#", i):
            block = True
            out.extend("  ")
            i += 2
            continue
        ch = text[i]
        if not single and not double and ch == "#":
            while i < len(text) and text[i] != "\n":
                out.append(" ")
                i += 1
            continue
        if single:
            if ch == "'":
                if i + 1 < len(text) and text[i + 1] == "'":
                    out.extend("  ")
                    i += 2
                    continue
                single = False
            out.append("\n" if ch == "\n" else " ")
            i += 1
            continue
        if double:
            if ch == "`" and i + 1 < len(text):
                out.extend("  ")
                i += 2
                continue
            if ch == '"':
                double = False
            out.append("\n" if ch == "\n" else " ")
            i += 1
            continue
        if ch == "'":
            single = True
            out.append(" ")
        elif ch == '"':
            double = True
            out.append(" ")
        else:
            out.append(ch)
        i += 1
    return "".join(out)


def delimiter_errors(path: Path, text: str) -> list[str]:
    skeleton = strip_ps_comments_and_strings(text)
    pairs = {"}": "{", ")": "(", "]": "["}
    stack = []
    errors = []
    for index, ch in enumerate(skeleton):
        if ch in "{([":
            stack.append((ch, index))
        elif ch in "})]":
            if not stack or stack[-1][0] != pairs[ch]:
                errors.append(f"{path}: unmatched {ch} at offset {index}")
                break
            stack.pop()
    if stack:
        ch, index = stack[-1]
        errors.append(f"{path}: unmatched {ch} at offset {index}")
    return errors


def resolve_import(script_path: Path, relative: str) -> Path:
    return (script_path.parent / relative.replace("\\", "/")).resolve()


def is_local_scan_excluded(path: Path, repo: Path) -> bool:
    rel = path.relative_to(repo).as_posix()
    return rel.startswith(LOCAL_SCAN_EXCLUDED_PREFIXES) or bool(LOCAL_SCAN_EXCLUDED_PARTS.intersection(path.parts))


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--repo", default=".")
    p.add_argument("--output")
    a = p.parse_args()
    repo = Path(a.repo).resolve()
    contract = json.loads((repo / "tools/script-audit/migration-contract.json").read_text(encoding="utf-8"))
    failures = []
    checks = []

    def check(name, ok, detail):
        checks.append({"name": name, "status": "PASS" if ok else "FAIL", "detail": detail})
        if not ok:
            failures.append(f"{name}: {detail}")

    manifest = repo / contract["module_manifest"]
    implementation = repo / contract["module_implementation"]
    runtime = repo / contract["runtime_contract_test"]
    for name, path in [
        ("module-manifest-exists", manifest),
        ("module-implementation-exists", implementation),
        ("runtime-contract-test-exists", runtime),
    ]:
        check(name, path.is_file(), str(path))
    exported = set(contract["exported_functions"])
    impl_text = implementation.read_text(encoding="utf-8") if implementation.is_file() else ""
    manifest_text = manifest.read_text(encoding="utf-8") if manifest.is_file() else ""
    implemented = set(function_names(impl_text))
    check("module-functions-exact", implemented == exported, f"implemented={sorted(implemented)}")
    for name in sorted(exported):
        check(f"manifest-exports:{name}", bool(re.search(rf"['\"]{re.escape(name)}['\"]", manifest_text)), name)
        check(
            f"implementation-exports:{name}",
            bool(re.search(rf"['\"]{re.escape(name)}['\"]", impl_text.split("Export-ModuleMember", 1)[-1])),
            name,
        )
    changed = {implementation, manifest, runtime}
    managed = contract.get("managed_consumers", {})
    for relative, removed in managed.items():
        script = repo / relative
        changed.add(script)
        check(f"consumer-exists:{relative}", script.is_file(), relative)
        if not script.is_file():
            continue
        text = script.read_text(encoding="utf-8-sig")
        definitions = set(function_names(text))
        for old in removed:
            check(f"legacy-definition-removed:{relative}:{old}", old not in definitions, old)
        imports = IMPORT_RE.findall(text)
        check(f"module-import-count:{relative}", len(imports) == 1, f"imports={imports}")
        if len(imports) == 1:
            resolved_import = resolve_import(script, imports[0])
            check(
                f"module-import-resolves:{relative}",
                resolved_import in {manifest.resolve(), implementation.resolve()},
                str(resolved_import),
            )
    for relative, names in contract.get("intentional_local_exceptions", {}).items():
        path = repo / relative
        if not path.is_file():
            continue
        definitions = set(function_names(path.read_text(encoding="utf-8-sig")))
        for name in names:
            check(f"intentional-local-exception:{relative}:{name}", name in definitions, name)
    np_call_re = re.compile(r"\b([A-Z][A-Za-z]+-Np[A-Za-z0-9]+)\b")
    unresolved = {}
    for script in sorted(changed):
        if not script.is_file():
            continue
        text = script.read_text(encoding="utf-8-sig")
        missing = sorted(set(np_call_re.findall(text)) - exported)
        if missing:
            unresolved[script.relative_to(repo).as_posix()] = missing
        failures.extend(delimiter_errors(script.relative_to(repo), text))
    check("np-calls-resolve", not unresolved, json.dumps(unresolved, sort_keys=True))
    authority_rel = contract["canonical_deployment_exclusions"]["authority_manifest"]
    authority = repo / authority_rel
    check("deployment-authority-manifest-exists", authority.is_file(), authority_rel)
    protected = {}
    if authority.is_file():
        protected = json.loads(authority.read_text(encoding="utf-8")).get("protected_hashes", {})
    deployment_snapshot_drift = []
    for rel, expected in protected.items():
        path = repo / rel
        check(f"deployment-authority-exists:{rel}", path.is_file(), rel)
        if path.is_file() and sha256(path) != expected:
            deployment_snapshot_drift.append({"path": rel, "expected": expected, "actual": sha256(path)})
    prefixes = tuple(contract["canonical_deployment_exclusions"]["prefixes"])
    exact = set(contract["canonical_deployment_exclusions"]["exact"])
    counts = Counter()
    locations = defaultdict(list)
    for script in list(repo.rglob("*.ps1")) + list(repo.rglob("*.psm1")):
        if is_local_scan_excluded(script, repo):
            continue
        rel = script.relative_to(repo).as_posix()
        if rel in exact or rel.startswith(prefixes):
            continue
        for name in function_names(script.read_text(encoding="utf-8-sig", errors="ignore")):
            counts[name] += 1
            locations[name].append(rel)
    duplicates = {n: locations[n] for n, c in sorted(counts.items(), key=lambda x: (-x[1], x[0].lower())) if c > 1}
    result = {
        "schema_version": 2,
        "status": "PASS" if not failures else "FAIL",
        "checks": checks,
        "summary": {
            "checks_total": len(checks),
            "checks_failed": sum(c["status"] == "FAIL" for c in checks),
            "managed_consumers": len(managed),
            "exported_functions": len(exported),
            "historical_deployment_snapshot_files": len(protected),
            "deployment_snapshot_drift": len(deployment_snapshot_drift),
            "remaining_non_deployment_duplicate_function_names": len(duplicates),
        },
        "remaining_non_deployment_duplicate_functions": duplicates,
        "deployment_snapshot_observations": deployment_snapshot_drift,
        "failures": failures,
    }
    rendered = json.dumps(result, indent=2, sort_keys=True) + "\n"
    if a.output:
        Path(a.output).write_text(rendered, encoding="utf-8")
    print(rendered, end="")
    return 0 if result["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
