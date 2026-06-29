# Phase 1 — repository hygiene

## Status

`PHASE_1_REPOSITORY_HYGIENE_IMPLEMENTED`

Phase 1 removes only repository-local, generated, superseded, duplicated, or runtime-state artefacts whose lack of a canonical consumer was demonstrated. It does not change application code, tests, workflows, cloud configuration, domain semantics, contracts, migrations, scoring, alerts, roles, `.env`, or `.env.example`.

## Decisions

### Removed local IDE and user state

The committed `.idea/` tree and `NatureProtector.sln.DotSettings.user` were local editor state. No repository source, workflow, test, or documentation consumed them. `.gitignore` now prevents their reintroduction.

### Removed `infra/grafana/grafana.db`

The checked-in SQLite database was not mounted by `docker-compose.yml`. Grafana uses the named volume `grafana_data` and declarative provisioning from `infra/grafana/provisioning/` and `infra/grafana/dashboards/`.

A read-only inspection of the removed database established:

- SQLite integrity check: `ok`;
- dashboards: `0`;
- dashboard provisioning rows: `0`;
- data sources: `2`, both represented by declarative provisioning files, with historical values in the database;
- the file contained encrypted secure-data state and therefore did not belong in source control.

The dashboard JSON files and provisioning YAML files remain unchanged.

### Removed superseded test archive

`tests/NatureProtector.Prevention.Tests/NatureProtector.Prevention.Tests.zip` contained older copies of 20 test source files. The corresponding live `.cs` files remain in the test project, and the ZIP was not referenced or compiled. Removing the archive does not reduce the test-source inventory.

### Normalised scenario examples

- removed unreferenced `scenario-b-orchestrator-smoke.json`, which was byte-for-byte identical to the referenced canonical `scenario-b-default.json`;
- renamed `scenario-c-5cycles-6sensors-smoke copy.json` to `scenario-c-5cycles-6sensors-smoke.json`, preserving its unique Scenario C content while removing the accidental copy suffix.

### Removed LaTeX transient outputs

Auxiliary LaTeX products and `References.bib.bak` were removed. The committed `Report.pdf`, all `.tex`, `.bib`, style, class, image, and bibliography-style source files remain.

A clean build from source, after deleting all transient products, completed with `pdflatex`, `bibtex8`, and two further `pdflatex` passes and produced a 40-page PDF, matching the checked-in PDF page count. This proves that the removed auxiliaries are reproducible rather than source inputs.

## Guardrails

`.gitignore` now covers the exact local/runtime/generated artefacts removed by this phase. A new `.gitattributes` classifies common binary formats without imposing repository-wide line-ending changes.

## Rollback

The Phase 1 review package includes `phase1-removed-files.zip` and a SHA-256 removal manifest. Rollback consists of restoring those paths, reversing the Scenario C rename, and reverting `.gitignore`, `.gitattributes`, and this document.
