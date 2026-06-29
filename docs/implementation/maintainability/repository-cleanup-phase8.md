# Phase 8 — final repository cleanup and classification

Phase 8 closes the maintainability programme started by the repository inventory. It removes only wrappers and dependencies for which the repository provides positive evidence of non-use, while recording why the remaining candidates and exact duplicates are retained.

## Script candidate classification

The Phase 7 inventory reported 18 scripts without a literal path reference.

| Classification | Count | Decision |
|---|---:|---|
| G8.2 per-action forwarding wrappers | 14 | Removed. They only called the same dispatcher with a constant action. `Invoke-G82RuntimeProbe.ps1` now calls the parameterized dispatcher directly and records both dispatcher and source-adapter hashes. |
| Python helpers imported by sibling scripts | 2 | Retained. `Test_G102_import_helper.py` and `g8_state_evidence.py` are real module dependencies; the repository auditor now recognizes Python imports. |
| Manual operator entrypoints | 2 | Retained and documented in `scripts/README.md`: `scripts/dotnet/Invoke-RepoDotnet.ps1` and `scripts/evidence/export_db_evidence.py`. |

The expected final count of scripts with `NO_STATIC_REFERENCE_FOUND` is zero. This is an audit property, not a rule that every script must be called by CI.

## Dependency removals

- `react-leaflet` was removed from `webUI/package.json` and `package-lock.json`. No source, test, configuration or build file imports it; the map implementation uses `leaflet` directly.
- `InfluxDB.Client.Linq` was removed from the Influx infrastructure project and central package catalogue. No C# file imports or references the LINQ client namespace; the implementation uses `InfluxDB.Client` directly.

No other dependency was removed without runtime restore/build evidence. Compiler packages, type packages, test environments, coverage providers and framework adapters may be consumed implicitly by tool configuration and are therefore retained.

## Exact duplicate classification

The remaining byte-identical groups are intentional or structurally independent:

- historical evidence snapshots are immutable records;
- Terraform lock files belong to separate roots even when provider selections match;
- minimal `.csproj` files identify different assemblies;
- host `appsettings.Development.json` files are independent deployment defaults;
- `.nvmrc` and `.node-version` support different version managers;
- `.gitkeep` files preserve separate required directories.

No historical evidence, lock file, project identity, host setting or placeholder was removed merely to reduce duplicate-byte metrics.

## Final guardrail

`tools/final-audit/validate.py` enforces the classification, dependency absences, G8.2 dispatcher topology, documentation of manual tools and zero unresolved script candidates. It is part of `scripts/np.ps1 validate` and the enforced quality-gate matrix.
