# Phase 5 — controlled decomposition of `PostgresControlPlaneService`

## Status

`PHASE_5_CONTROL_PLANE_DECOMPOSITION_IMPLEMENTED_STATICALLY_PROVED`

The 4,962-line implementation was split into seven partial feature files without changing its interface, constructor, controllers, dependency-injection registration, queries, runtime commands, evidence semantics, or response contracts.

## Feature slices

| File | Responsibility | Lines after decomposition |
|---|---|---:|
| `PostgresControlPlaneService.cs` | fields, constructor, availability metadata | 110 |
| `.Catalog.cs` | configuration, topology, runs and operational-state queries | 693 |
| `.RunTimings.cs` | persisted run timing projection | 168 |
| `.RuntimeSummary.cs` | runtime summary and risk/index aggregation | 906 |
| `.RuntimeDiagnostics.cs` | diagnostic catalogue, dispatcher and fixed diagnostic queries | 1,838 |
| `.RuntimeOperations.cs` | run start/reset, controlled validation and evidence writing | 910 |
| `.Shared.cs` | mapping, parsing, normalization and shared helpers | 481 |

No feature file may exceed 2,000 lines and the core file may not exceed 180 lines. These limits are enforced by `tools/control-plane-audit/validate.py`.

## Behaviour-preservation proof

`config/quality/control-plane-decomposition.json` records eleven source slices from the Phase 4 monolith. Each slice contains:

- its original line interval;
- its destination feature file;
- its exact SHA-256;
- its exact line count.

The validator extracts every marked slice from the decomposed files and requires the content to remain byte-identical. It also verifies:

- the exact seven-file feature set;
- balanced C# delimiters outside comments and strings;
- one partial class declaration in each file;
- the exact 22 public methods declared by `IControlPlaneService`;
- the two availability properties;
- a single unchanged public constructor;
- absence of the old non-partial declaration.

The runtime characterization test `PostgresControlPlaneServiceContractTests.cs` independently compares the compiled public method and property signatures with `IControlPlaneService` and checks the constructor signature and defaults.

## Deliberate boundary

This phase does not introduce new dependency-injection services. It first creates stable feature boundaries while preserving every existing member body byte for byte. Moving the slices into separately injected collaborators can now be considered later with smaller diffs and focused runtime tests, rather than combining structural movement with semantic changes.

## Validation commands

```bash
python tools/control-plane-audit/validate.py --repo .
python -m unittest discover -s tools/control-plane-audit/tests -p 'test_*.py'
```

With the .NET 9 SDK available:

```bash
dotnet test tests/NatureProtector.Backoffice.Api.Tests/NatureProtector.Backoffice.Api.Tests.csproj -c Release
```

## Non-goals

This phase does not change:

- `IControlPlaneService`;
- controllers or endpoint routes;
- `Program.cs` service registration;
- database schemas or migrations;
- scoring, alerts, roles or domain semantics;
- process-launch policy;
- evidence paths or formats;
- cloud, Terraform or Kubernetes configuration.
