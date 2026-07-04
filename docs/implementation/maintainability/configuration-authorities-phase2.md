# Phase 2 — Configuration and dependency authorities

## Scope

This phase removes competing sources for deployment identity, required Google
Cloud APIs, NuGet versions and the shared Python `jsonschema` version. It does
not change domain behavior, cloud resources, Terraform state, application
contracts, scoring, roles, migrations or alert semantics.

## Authority map

| Concern | Authority | Consumers |
|---|---|---|
| Project, region, artifact repository and common deployment paths | `deploy/environments/common.json` | `scripts/np.ps1`, cloud validators and environment overlays |
| Staging-specific values | `deploy/environments/staging.json` | standard CD staging operations |
| Production lock and production-specific values | `deploy/environments/production.json` | production policy checks |
| Required Google Cloud APIs | `config/cloud/required-apis.txt` | setup and preflight scripts |
| NuGet package versions | `Directory.Packages.props` | all `.csproj` files |
| .NET dependency policy and reviewed exceptions | `config/dependencies/dotnet-package-policy.json` | configuration authority validator and reviewers |
| Shared Python JSON Schema version | `scripts/cloud/requirements-jsonschema.txt` | G8.2 and general cloud validation requirements |
| Machine-readable registry | `config/configuration-authorities.json` | `tools/config-audit/validate.py` |

## Overlay rule

`staging.json` and `production.json` are overlays. They must not redefine:

- `project_id`;
- `region`;
- `artifact_repository`;
- `terraform`;
- `kustomize`;
- `release`;
- `evidence`.

The effective configuration is the shallow merge of `common.json` followed by
the selected environment overlay. The current nested common objects are not
overridden by environment files.

## Intentional fixed guards

Some exact values remain in Terraform validation blocks and released contract
schemas. These are trust-boundary guards, not alternative configuration
sources. Changing them is an infrastructure authorization change and is outside
this maintainability phase.

## Dependency decisions

Central Package Management preserves the exact package graph that existed
before Phase 2. In particular, the existing `Microsoft.Extensions.*` 10.0.5
references on `net9.0` and the mixed OpenTelemetry release tracks are recorded
as reviewed preservation decisions. They are not silently upgraded or
 downgraded. A later version change requires restore, build and test evidence.

## Validation

```bash
python tools/config-audit/validate.py --repo .
python -m unittest discover -s tools/config-audit/tests -p "test_*.py"
python scripts/cloud/Test-StandardStagingConfiguration.py
```

The validator is standard-library-only and performs no cloud or repository
mutation.
