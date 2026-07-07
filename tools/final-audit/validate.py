#!/usr/bin/env python3
"""Validate cleanup while preserving deployment files from the canonical repository."""

from __future__ import annotations
import argparse
import importlib.util
import json
import sys
from pathlib import Path

sys.dont_write_bytecode = True


def load_module(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=".")
    parser.add_argument("--output")
    args = parser.parse_args()
    repo = Path(args.repo).resolve()
    contract_path = repo / "config/quality/repository-cleanup.json"
    failures = []
    checks = []

    def check(name: str, condition: bool, detail: str) -> None:
        checks.append({"name": name, "status": "PASS" if condition else "FAIL", "detail": detail})
        if not condition:
            failures.append(f"{name}: {detail}")

    check("contract-exists", contract_path.is_file(), str(contract_path))
    if not contract_path.is_file():
        return 1
    contract = json.loads(contract_path.read_text(encoding="utf-8"))
    check("schema-version", contract.get("schema_version") == 2, str(contract.get("schema_version")))
    check("phase", contract.get("phase") == 8, str(contract.get("phase")))
    scripts = contract["script_candidates"]
    wrappers = scripts["deployment_authority_wrappers"]
    for relative in wrappers:
        check(f"deployment-wrapper-preserved:{relative}", (repo / relative).is_file(), relative)
    dispatcher = repo / scripts["parameterized_dispatcher"]
    runtime = repo / scripts["runtime_caller"]
    source_dir = repo / scripts["source_adapter_directory"]
    check("dispatcher-exists", dispatcher.is_file(), str(dispatcher))
    check("runtime-caller-exists", runtime.is_file(), str(runtime))
    check("source-adapter-directory-exists", source_dir.is_dir(), str(source_dir))
    check("source-adapter-readme-exists", (source_dir / "README.md").is_file(), str(source_dir / "README.md"))
    for relative in scripts["python_imported_helpers"] + scripts["retained_manual_entrypoints"]:
        check(f"retained-script-exists:{relative}", (repo / relative).is_file(), relative)
    scripts_readme = (repo / "scripts/README.md").read_text(encoding="utf-8")
    for relative in scripts["retained_manual_entrypoints"]:
        check(f"manual-entrypoint-documented:{relative}", relative in scripts_readme, relative)
    package_json = json.loads((repo / "webUI/package.json").read_text(encoding="utf-8"))
    package_lock = json.loads((repo / "webUI/package-lock.json").read_text(encoding="utf-8"))
    declared = set(package_json.get("dependencies", {})) | set(package_json.get("devDependencies", {}))
    root_lock = package_lock.get("packages", {}).get("", {})
    locked = set(root_lock.get("dependencies", {})) | set(root_lock.get("devDependencies", {}))
    for package in contract["removed_dependencies"]["npm"]:
        check(f"npm-package-not-declared:{package}", package not in declared, package)
        check(f"npm-package-not-root-locked:{package}", package not in locked, package)
        check(
            f"npm-package-not-installed-in-lock:{package}",
            f"node_modules/{package}" not in package_lock.get("packages", {}),
            package,
        )
    audit = load_module(repo / "tools/repo-audit/audit.py", "phase8_repo_audit")
    config = repo / "tools/repo-audit/audit-config.json"
    audit_config = audit.read_json(config)
    repository_files = list(audit.iter_repository_files(repo, audit_config))

    dotnet = (repo / "Directory.Packages.props").read_text(encoding="utf-8-sig")
    csproj = "\n".join(
        path.read_text(encoding="utf-8-sig") for relative, path in repository_files if relative.endswith(".csproj")
    )
    csharp = "\n".join(
        path.read_text(encoding="utf-8-sig", errors="ignore")
        for relative, path in repository_files
        if relative.endswith(".cs")
    )
    for package in contract["removed_dependencies"]["nuget"]:
        check(f"nuget-package-not-catalogued:{package}", package not in dotnet, package)
        check(f"nuget-package-not-referenced:{package}", package not in csproj, package)
        check(f"nuget-namespace-not-used:{package}", package not in csharp, package)
    model = audit.build_model(repo, audit_config, audit.sha256_file(config))
    unresolved = [item["path"] for item in model["script_inventory"] if item["status"] == "NO_STATIC_REFERENCE_FOUND"]
    check("zero-unresolved-script-candidates", unresolved == [], json.dumps(unresolved))
    for relative in contract["documentation"]:
        check(f"documentation-exists:{relative}", (repo / relative).is_file(), relative)
    payload = {
        "schema_version": 2,
        "status": "PASS" if not failures else "FAIL",
        "summary": {
            "checks": len(checks),
            "failures": len(failures),
            "deployment_wrappers_preserved": len(wrappers),
            "unresolved_script_candidates": len(unresolved),
        },
        "checks": checks,
        "failures": failures,
        "unresolved_script_candidates": unresolved,
    }
    rendered = json.dumps(payload, indent=2, sort_keys=True) + "\n"
    if args.output:
        Path(args.output).write_text(rendered, encoding="utf-8")
    print(rendered, end="")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
