# Repository scripts

Scripts are grouped by operational responsibility. A script that is not called by a workflow may still be an intentional manual entrypoint; manual tools must be listed here or in a dedicated runbook so static audits can distinguish them from abandoned code.

## Canonical entrypoints

- `scripts/np.ps1`: standard validation, release, deployment and qualification entrypoint.
- `scripts/workspace.ps1`: local workspace lifecycle and developer runtime entrypoint.
- `scripts/dotnet/Use-RepoDotnetEnvironment.ps1`: configure the repository-local .NET environment in the current PowerShell session.
- `scripts/dotnet/Invoke-RepoDotnet.ps1`: manual wrapper that configures the repository-local .NET environment and invokes `dotnet`, adding single-process execution for restore, build, test and publish unless the caller supplied an override.
- `scripts/evidence/export_db_evidence.py`: manual PostgreSQL evidence exporter. It requires explicit database connectivity and writes a Markdown inventory; it is not part of normal CI or deployment.

## Dynamic G8.2 probes

`scripts/cloud/Invoke-G82RuntimeProbe.ps1` calls the single dispatcher `scripts/cloud/probes/Invoke-G82ProbeAdapter.ps1` with a validated action. The dispatcher resolves the reviewed implementation at `scripts/cloud/probes/sources/<action>.ps1`. Per-action forwarding wrappers are intentionally absent.

## Retention rule

New manual entrypoints must be documented. Helpers imported by other scripts should use normal module imports so `tools/repo-audit/audit.py` can record the relationship. A script with no workflow, automation, documentation, test or source reference remains a review candidate rather than being removed automatically.

## Tooling de evidência do relatório

- `scripts/evidence/run-report-evidence-campaign.sh`: entrypoint manual suportado para Git Bash/Linux; delega na campanha Python e exige `--execute` para ações selecionadas.
- `scripts/evidence/collect-reliability-evidence.sh`: entrypoint manual suportado para recolha de fiabilidade; não executa P3 sem confirmação explícita.

Os resultados são escritos em `artifacts/report-evidence/` e não são versionados.

## Documentação e referências geradas

- `scripts/docs/generate_reference_catalogs.py`: regenera as matrizes de roles/capabilities e o catálogo fechado de operações a partir das autoridades C# atuais. É um entrypoint manual e pode ser usado antes de validar ou publicar documentação.
- `scripts/docs/build_offline_portal.py`: gera o portal HTML pesquisável a partir da camada documental canónica. Requer as dependências opcionais `mistune` e `jinja2`; não altera o repositório nem executa operações cloud.
- `scripts/docs/validate_documentation.py`: gate estático de links, autoridade e integridade source/render/sidecar, chamado pelo workflow de documentação.
