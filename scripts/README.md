# Repository scripts

Scripts are grouped by operational responsibility. A script that is not called by a workflow may still be an intentional manual entrypoint; manual tools must be listed here or in a dedicated runbook so static audits can distinguish them from abandoned code.

## Canonical entrypoints

- `scripts/np.ps1`: standard validation, release, deployment and qualification entrypoint.
- `scripts/workspace.ps1`: local workspace lifecycle and developer runtime entrypoint.
- `scripts/dotnet/Use-RepoDotnetEnvironment.ps1`: configure the repository-local .NET environment in the current PowerShell session.
- `scripts/dotnet/Invoke-RepoDotnet.ps1`: manual wrapper that configures the repository-local .NET environment and invokes `dotnet`, adding single-process execution for restore, build, test and publish unless the caller supplied an override.
- `scripts/docker/Export-DockerDiagnostics.ps1`: manual local-runtime diagnostics exporter. It writes Docker and Compose snapshots under `artifacts/local-runtime/docker-diagnostics/` or an explicit output directory and does not prune, remove or mutate Docker resources.
- `scripts/evidence/export_db_evidence.py`: manual PostgreSQL evidence exporter. It requires explicit database connectivity and writes a Markdown inventory; it is not part of normal CI or deployment.

## Dynamic G8.2 probes

`scripts/cloud/Invoke-G82RuntimeProbe.ps1` calls the single dispatcher `scripts/cloud/probes/Invoke-G82ProbeAdapter.ps1` with a validated action. The dispatcher resolves the reviewed implementation at `scripts/cloud/probes/sources/<action>.ps1`. Per-action forwarding wrappers are intentionally absent.

## Retention rule

New manual entrypoints must be documented. Helpers imported by other scripts should use normal module imports so `tools/repo-audit/audit.py` can record the relationship. A script with no workflow, automation, documentation, test or source reference remains a review candidate rather than being removed automatically.

## Package structure and operations scripts

- `scripts/__init__.py`: package marker; makes `scripts` a proper Python package for import-based discovery.
- `scripts/operations/__init__.py`: package marker for the `scripts.operations` subpackage.
- `scripts/operations/consolidate_test_results.py`: consolidates JUnit and TRX test results into a deterministic JSON summary. Called by CI workflows and locally for test result aggregation.

## Tooling de evidência do relatório

- `scripts/evidence/run-report-evidence-campaign.sh`: entrypoint manual suportado para Git Bash/Linux; delega na campanha Python e exige `--execute` para ações selecionadas.
- `scripts/evidence/collect-reliability-evidence.sh`: entrypoint manual suportado para recolha de fiabilidade; não executa P3 sem confirmação explícita.
- `scripts/evidence/collect-evidence-gap-closure.sh`: wrapper manual para regenerar e verificar a matriz de fecho de gaps de evidência.
- `scripts/evidence/collect-evidence-intelligence.sh`: wrapper manual para recolher o inventário de claims e artefactos do relatório.
- `scripts/evidence/Generate-DeepEngineeringExploration20260701.ps1`: entrypoint manual histórico para regenerar o pacote documental de exploração de engenharia de 2026-07-01. Mantém-se documentado como ferramenta de reprodução de evidência, não como fluxo CI ou deployment.
- `scripts/evidence/run-final-evidence-campaign.py`: entrypoint manual para agregar a campanha final de evidência local; não substitui provas runtime/live ausentes.
- `scripts/evidence/verify-final-evidence-campaign.py`: verificador manual do pacote de campanha final; falha evidência vazia ou sem identidade explícita.
- `scripts/runtime/verify-long-run-proof.py`: verificador manual de matrizes de long-run proof; classifica resultados e não executa produção.
- `scripts/autoscaling/analyze-capacity.py`: analisador manual de capacidade/autoscaling a partir de amostras locais ou recolhidas.
- `scripts/observability/generate-grafana-dashboards.py`: gerador manual dos dashboards Grafana versionados em `infra/grafana/dashboards/`.
- `scripts/audit/Invoke-RabbitMqHealthPhase3EValidation.ps1`: entrypoint manual para validação local de saúde RabbitMQ fase 3E.
- `scripts/audit/Test-RabbitMqHealthPhase1Package.py`: teste manual/estático do pacote RabbitMQ health fase 1.
- `scripts/audit/Test-RabbitMqHealthPhase2Package.py`: teste manual/estático do pacote RabbitMQ health fase 2.

Os resultados são escritos em `artifacts/report-evidence/` e não são versionados.

## Documentação e referências geradas

- `scripts/docs/generate_reference_catalogs.py`: regenera as matrizes de roles/capabilities e o catálogo fechado de operações a partir das autoridades C# atuais. É um entrypoint manual e pode ser usado antes de validar ou publicar documentação.
- `scripts/docs/build_offline_portal.py`: gera o portal HTML pesquisável a partir da camada documental canónica. Requer as dependências opcionais `mistune` e `jinja2`; não altera o repositório nem executa operações cloud.
- `scripts/docs/validate_documentation.py`: gate estático de links, autoridade e integridade source/render/sidecar, chamado pelo workflow de documentação.

## Runtime validation matrices

The following PowerShell entrypoints are maintained manual harnesses for local
runtime validation. They use real local services and write evidence outside the
versioned repository tree.

- `scripts/testing/Invoke-MultiReplicaTemporalMatrix.ps1` validates temporal
  correctness with 1, 2, and 3 separate Prevention processes.
- `scripts/testing/Invoke-SystemResetRecoveryMatrix.ps1` validates reset and
  recovery across PostgreSQL, RabbitMQ, and InfluxDB.
- `scripts/testing/Invoke-AutoscalingExperimentMatrix.ps1` validates the local
  S1-S8 autoscaling experiments, backlog drainage, latency, and replica changes.

Run from the repository root:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Invoke-MultiReplicaTemporalMatrix.ps1 -SkipBuild
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Invoke-SystemResetRecoveryMatrix.ps1 -SkipBuild
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Invoke-AutoscalingExperimentMatrix.ps1 -SkipBuild
```

These scripts are not production or cloud deployment entrypoints.
