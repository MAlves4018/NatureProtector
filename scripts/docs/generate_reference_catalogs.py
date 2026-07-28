#!/usr/bin/env python3
"""Generate documentation reference tables from current code authorities.

The extractor is intentionally narrow. It reads the stable declaration shapes used
by NatureProtector for roles/capabilities, the closed engineering operation catalog,
API controller routes and the frontend page registry. It is not a general C# or
TypeScript parser.
"""
from __future__ import annotations

import argparse
import csv
import re
import tempfile
from pathlib import Path


def extract_balanced(source: str, start: int, opening: str = "(", closing: str = ")") -> str:
    open_pos = source.find(opening, start)
    if open_pos < 0:
        raise ValueError(f"Expression has no opening {opening!r}")
    depth = 0
    in_string = False
    escaped = False
    for index in range(open_pos, len(source)):
        char = source[index]
        if in_string:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
            continue
        if char == '"':
            in_string = True
        elif char == opening:
            depth += 1
        elif char == closing:
            depth -= 1
            if depth == 0:
                return source[start : index + 1]
    raise ValueError(f"Unbalanced expression starting at {start}")


def write_csv(path: Path, header: list[str], rows: list[list[object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(header)
        writer.writerows(rows)


def extract_capabilities(repo: Path) -> tuple[dict[str, str], dict[str, list[str]]]:
    path = repo / "src/NatureProtector.Backoffice.Api/Operations/Authorization/OperationCapabilities.cs"
    source = path.read_text(encoding="utf-8")
    constants = dict(re.findall(r'public const string (\w+) = "([^"]+)";', source))
    roles: dict[str, list[str]] = {}
    for role, body in re.findall(r'\["([^"]+)"\]\s*=\s*\[(.*?)\]\s*,?', source, flags=re.S):
        roles[role] = re.findall(r"OperationCapabilities\.([A-Za-z0-9_]+)", body)
    return constants, roles


def generate_role_capabilities(output: Path, constants: dict[str, str], roles: dict[str, list[str]]) -> int:
    rows: list[list[object]] = []
    for role in sorted(roles):
        for constant in roles[role]:
            rows.append([role, constant, constants.get(constant, "")])
    write_csv(output / "role-capability-matrix.csv", ["role", "capability_constant", "capability_value"], rows)
    return len(rows)


def generate_operations(repo: Path, output: Path) -> int:
    path = repo / "src/NatureProtector.Backoffice.Api/Operations/Services/OperationCatalog.cs"
    source = path.read_text(encoding="utf-8")
    initializer_start = source.find("All { get; } =")
    initializer_end = source.find("\n    ];", initializer_start)
    catalog_source = source[initializer_start:initializer_end] if initializer_start >= 0 and initializer_end >= 0 else source
    rows: list[list[object]] = []
    for match in re.finditer(r"\b(Quality|Evidence|Deployment|Cloud)\s*\(", catalog_source):
        call = extract_balanced(catalog_source, match.start())
        literals = re.findall(r'"((?:\\.|[^"])*)"', call)
        if len(literals) < 2:
            continue
        category = match.group(1).lower()
        operation_id, display_name = literals[0], literals[1]
        blocked = next((value for value in literals if value.startswith("blocked-")), None)
        availability_match = re.search(r'availability\s*:\s*"([^"]+)"', call)
        availability = availability_match.group(1) if availability_match else blocked or "implemented"
        evidence_match = re.search(r'evidenceLevel\s*:\s*"([^"]+)"', call)
        evidence = evidence_match.group(1) if evidence_match else (
            "NOT_PROVED" if availability.startswith("blocked-") else "IMPLEMENTED_NOT_PROVED"
        )
        rows.append([operation_id, category, display_name, availability, evidence])
    write_csv(
        output / "operation-catalog.csv",
        ["operation_id", "category", "display_name", "availability", "evidence_level"],
        rows,
    )
    return len(rows)


def resolve_authorization(attributes: str, default: str, constants: dict[str, str]) -> str:
    if re.search(r"\[AllowAnonymous\]", attributes):
        return "anonymous"
    role = re.findall(r'\[Authorize\(Roles\s*=\s*"([^"]+)"\)\]', attributes)
    if role:
        return f"role:{role[-1]}"
    policy = re.findall(
        r"\[Authorize\(Policy\s*=\s*OperationCapabilities\.([A-Za-z0-9_]+)\)\]", attributes
    )
    if policy:
        return f"capability:{constants.get(policy[-1], policy[-1])}"
    if re.search(r"\[Authorize(?:\(\))?\]", attributes):
        return "authenticated"
    return default


def generate_api_endpoints(repo: Path, output: Path, constants: dict[str, str]) -> int:
    rows: list[list[object]] = []
    controllers = repo / "src/NatureProtector.Backoffice.Api/Controllers"
    action_pattern = re.compile(
        r"(?P<attrs>(?:\s*\[[^\]]+\]\s*)+)\s*public\s+(?:async\s+)?[^\n{]+?\s+"
        r"(?P<name>[A-Za-z0-9_]+)\s*\(",
        re.M,
    )
    for path in sorted(controllers.glob("*Controller.cs")):
        source = path.read_text(encoding="utf-8")
        class_pos = source.find("class ")
        if class_pos < 0:
            continue
        class_attributes = source[:class_pos]
        routes = re.findall(r'\[Route\("([^"]+)"\)\]', class_attributes)
        if not routes:
            continue
        base_route = routes[-1].strip("/")
        class_access = resolve_authorization(class_attributes, "public-unspecified", constants)
        for match in action_pattern.finditer(source[class_pos:]):
            attributes = match.group("attrs")
            http = re.search(r'\[Http(Get|Post|Put|Delete|Patch)(?:\("([^"]*)"\))?\]', attributes)
            if http is None:
                continue
            method = http.group(1).upper()
            suffix = (http.group(2) or "").strip("/")
            route = f"/{base_route}"
            if suffix:
                route += f"/{suffix}"
            access = resolve_authorization(attributes, class_access, constants)
            rows.append(
                [
                    method,
                    route,
                    access,
                    path.stem,
                    match.group("name"),
                    path.relative_to(repo).as_posix(),
                ]
            )
    rows.sort(key=lambda row: (str(row[1]), str(row[0])))
    write_csv(
        output / "api-endpoint-catalog.csv",
        ["method", "route", "access", "controller", "action", "source"],
        rows,
    )
    return len(rows)


def generate_ui_routes(repo: Path, output: Path) -> int:
    registry_path = repo / "webUI/src/app/navigation/pageRegistry.ts"
    source = registry_path.read_text(encoding="utf-8")
    registry: dict[str, list[object]] = {}
    for match in re.finditer(r"\{\s*id:\s*'([^']+)'", source):
        block = extract_balanced(source, match.start(), "{", "}")
        page_id = match.group(1)
        capabilities_match = re.search(r"requiredCapabilities:\s*\[([^\]]*)\]", block, re.S)
        capabilities = re.findall(r"'([^']+)'", capabilities_match.group(1)) if capabilities_match else []
        audience_match = re.search(r"audience:\s*'([^']+)'", block)
        group_match = re.search(r"group:\s*'([^']+)'", block)
        order_match = re.search(r"order:\s*(\d+)", block)
        registry[page_id] = [
            ";".join(capabilities),
            audience_match.group(1) if audience_match else "",
            group_match.group(1) if group_match else "",
            int(order_match.group(1)) if order_match else "",
        ]

    app_path = repo / "webUI/src/app/App.tsx"
    app_source = app_path.read_text(encoding="utf-8")
    mounted: dict[str, str] = {}
    for match in re.finditer(r"\{\s*path:\s*'([^']+)'\s*,\s*element:\s*([^\n]+)", app_source):
        path_value, element = match.groups()
        if path_value in {"/", "*"}:
            continue
        route = path_value if path_value.startswith("/") else f"/{path_value}"
        if "Navigate" in element:
            status = "redirect"
        elif "UiRetiredOperationalSurface" in element:
            status = "retired"
        else:
            status = "page"
        mounted[route] = status

    rows: list[list[object]] = []
    for page_id, values in registry.items():
        route = f"/{page_id}"
        status = mounted.get(route, "registry-unmounted")
        rows.append([route, page_id, *values, status, registry_path.relative_to(repo).as_posix()])

    for route, status in mounted.items():
        page_id = route.strip("/")
        if page_id in registry:
            continue
        if route == "/login":
            rows.append([route, "login", "anonymous", "public", "public", 0, status, app_path.relative_to(repo).as_posix()])
        elif route == "/db-queries":
            rows.append([route, "queries", "simulation.execute", "sim", "simulate", "", status, app_path.relative_to(repo).as_posix()])
        elif route == "/qa-tests":
            rows.append([route, "retired", "authenticated", "qa", "technical", "", status, app_path.relative_to(repo).as_posix()])
        else:
            rows.append([route, page_id, "", "", "", "", "mounted-unregistered", app_path.relative_to(repo).as_posix()])

    rows.sort(key=lambda row: (9999 if row[5] == "" else int(row[5]), str(row[0])))
    write_csv(
        output / "ui-route-capability-matrix.csv",
        ["route", "page_id", "required_capabilities", "audience", "group", "order", "status", "source"],
        rows,
    )
    return len(rows)


def generate_diagnostics(repo: Path, output: Path) -> int:
    path = repo / "src/NatureProtector.Backoffice.Api/ControlPlane/Services/PostgresControlPlaneService.cs"
    source = path.read_text(encoding="utf-8")
    start = source.find("RuntimeDiagnostics =")
    end = source.find("];", start)
    block = source[start:end] if start >= 0 and end >= 0 else ""
    rows = [list(values) for values in re.findall(r'new\("([^"]+)",\s*"([^"]+)",\s*"([^"]+)"\)', block)]
    write_csv(output / "runtime-diagnostic-catalog.csv", ["diagnostic_id", "title", "description"], rows)
    return len(rows)


def generate(repo: Path, output: Path | None = None) -> dict[str, int]:
    output = output or (repo / "docs/reference/generated")
    output.mkdir(parents=True, exist_ok=True)
    constants, roles = extract_capabilities(repo)
    return {
        "role_capability_rows": generate_role_capabilities(output, constants, roles),
        "operations": generate_operations(repo, output),
        "api_endpoints": generate_api_endpoints(repo, output, constants),
        "ui_routes": generate_ui_routes(repo, output),
        "runtime_diagnostics": generate_diagnostics(repo, output),
    }


def compare_generated(repo: Path) -> tuple[dict[str, int], list[str]]:
    expected = repo / "docs/reference/generated"
    with tempfile.TemporaryDirectory(prefix="np-reference-catalogs-") as temp_dir:
        candidate = Path(temp_dir)
        counts = generate(repo, candidate)
        drift: list[str] = []
        names = sorted(
            {path.name for path in candidate.glob("*.csv")}
            | {path.name for path in expected.glob("*.csv")}
        )
        for name in names:
            actual_path = expected / name
            candidate_path = candidate / name
            if not actual_path.exists():
                drift.append(f"missing:{name}")
            elif not candidate_path.exists():
                drift.append(f"unexpected:{name}")
            elif actual_path.read_bytes() != candidate_path.read_bytes():
                drift.append(f"changed:{name}")
        return counts, drift


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument(
        "--check",
        action="store_true",
        help="Compare generated catalogues without modifying the repository.",
    )
    args = parser.parse_args()
    repo = args.repo.resolve()
    if args.check:
        counts, drift = compare_generated(repo)
    else:
        counts = generate(repo)
        drift = []
    for name, count in counts.items():
        print(f"{name}: {count}")
    if drift:
        for item in drift:
            print(f"catalogue_drift: {item}")
        return 1
    if args.check:
        print("catalogue_status: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
