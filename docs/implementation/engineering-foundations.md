# Fundações de engenharia

Esta página é o contrato operacional M02 para build, testes, coverage, CI, infraestrutura local, triagem de segurança frontend e checks de observabilidade.

## Gates locais obrigatórias

Executar a partir da raiz do repositório, salvo indicação em contrário.

O .NET SDK está fixado por `global.json` na feature band `9.0.306` com `rollForward: latestPatch`. Suportar outra feature band deve ser feito por matriz CI explícita, não alargando o roll-forward local.

| Área | Comando | Notas |
| --- | --- | --- |
| Setup do workspace | `.\scripts\workspace.ps1 setup` | Entrada de compatibilidade para setup de engenharia. Para clone-to-run local, a entrada canónica é `.\scripts\np.ps1` conforme `docs/setup/local-baseline-setup.md`. Não executa comandos Git e não cria nem edita `.env`. |
| Validação rápida do workspace | `.\scripts\workspace.ps1 validate -Profile Quick` | Executa a gate local estreita de engenharia: build Release sem restore e check de toolchain frontend. |
| Validação de segurança do workspace | `.\scripts\workspace.ps1 validate -Profile Security` | Executa auditoria NuGet com artifact, política npm audit, canary scan sem Git e testes focados JWT/autorização/evidence traversal. Escreve em `artifacts/validation/workspace-profiles/security/`. |
| Smoke de performance do workspace | `.\scripts\workspace.ps1 validate -Profile PerformanceSmoke` | Executa o wrapper bounded de BenchmarkDotNet em B0 para `SerializationBenchmarks.SerializeEnvelopeBatch`, com timeout e summaries em `artifacts/validation/workspace-profiles/performance-smoke/`. Não é benchmark sistémico nem SLO. |
| Regressões dos scripts de workspace | `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\test-workspace-script.ps1` | Checks estáticos e `PlanOnly` para scripts workspace/setup/infra/mutation/docs, imutabilidade de `.env` e wiring CI de validation artifacts. |
| Build backend | `dotnet build .\NatureProtector.sln --no-restore --nologo -v minimal` | Exige restore prévio numa máquina limpa. |
| Testes backend | `dotnet test .\NatureProtector.sln --no-restore --nologo -v minimal -m:1` | A run local M02 passou com serviços Docker disponíveis. |
| Coverage backend | `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\generate-coverage-report.ps1` | Gera `artifacts/coverage/backend-integral/` e `artifacts/coverage/backend-focused/`. |
| Inventário de testes | `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\export-test-inventory.ps1` | Exporta inventário JSON, CSV e Markdown em `artifacts/validation/test-inventory/`. |
| Gaps de coverage | `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\export-coverage-gaps.ps1` | Exporta findings de zero/low coverage em `artifacts/validation/coverage-gaps/` a partir dos summaries ReportGenerator mais recentes. |
| Qualidade documental | `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\docs\export-documentation-quality.ps1` | Exporta inventário documental JSON, CSV e Markdown em `artifacts/validation/documentation-quality/`; classifica documents canónicos, históricos, evidence e termos técnicos sem transformar tudo em erro automático. |
| Higiene de artifacts | `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\validation\export-artifact-inventory.ps1` | Exporta inventário read-only de outputs gerados em `artifacts/validation/artifact-hygiene/`; classifica evidence, outputs históricos, caches de ferramentas e diretórios grandes sem apagar ficheiros. |
| Auditoria de vulnerabilidades NuGet | `dotnet list .\NatureProtector.sln package --vulnerable --include-transitive` | A run E2 atual reporta zero pacotes vulneráveis nos projetos da solution. |
| Instalação frontend | `npm ci` em `webUI/` | Usa `package-lock.json`. |
| Typecheck frontend | `npm run typecheck` em `webUI/` | Executa `tsc --noEmit`. |
| Lint frontend | `npm run lint` em `webUI/` | Executa Biome no scope frontend ratcheted: UI atual, services, e2e, scripts e config. Rewrites estilísticos/a11y legacy não estão ativos intencionalmente. |
| Check de formato frontend | `npm run format:check` em `webUI/` | Executa o formatter Biome em `biome.jsonc`, `package.json` e `tsconfig.json`. É uma gate incremental de formatter, não um reformat global do frontend. |
| Testes frontend | `npm test` em `webUI/` | Executa Vitest com jsdom e output JUnit. |
| Coverage frontend | `npm run test:coverage` em `webUI/` | Gera `webUI/coverage/`; o output é ignorado por git. |
| Build frontend | `npm run build` em `webUI/` | Chunks por rota mantêm a maior entrada abaixo do threshold de warning Vite na run M02. |
| Browser integration frontend | `npm run test:e2e` em `webUI/` | Playwright constrói a app Vite, serve `dist/` via `vite preview` e executa browser checks contra o artifact construído. Chromium é a gate por defeito; definir `NP_PLAYWRIGHT_BROWSER_MATRIX=all` para Chromium, Firefox e WebKit. A spec autenticada da UI atual usa uma fixture HTTP bounded para exercitar jornadas Anonymous/Admin/Sim/Pipeline, estados de falha e download de evidence sem depender de uma base local live de identidades. |
| Auditoria frontend | `npm audit --json` em `webUI/` | A M02 deixou o advisory Vite/esbuild como risco residual; ver abaixo. |
| Secret scan | `.\scripts\ci\run-secret-scan.ps1 -RepositoryRoot . -IncludeUntracked` | Descarrega Gitleaks `8.28.0` fixo quando ausente, faz scan de histórico Git, staged changes e snapshot do working tree, depois escreve relatórios redigidos em `artifacts/secret-scan/`. Usar `-SkipGitBackedScans -SkipInstall` apenas para validação local bounded quando comandos Git estão proibidos; esse modo salta history/staged e não copia `.env`. |
| Release candidate package | `.\scripts\release\build-release-candidate.ps1 -Version <label> -SkipRestore -SkipFrontendInstall` | Constrói um package local académico/demo em `artifacts/release/`. É apenas packaging; não faz deploy, signing ou attestation. |
| Validação clean install estrutural | `.\scripts\release\test-clean-install.ps1 -ArchivePath <zip>` | Expande o package, verifica checksum externo do archive, paths obrigatórios e `checksums.sha256` interno. |
| Smoke funcional de package | `.\scripts\release\test-functional-package-smoke.ps1 -ArchivePath <zip>` | Expande o package fora da source tree, valida checksums, arranca `Backoffice.Api` publicado com control plane desligado, consulta `/health`, valida assets estáticos da webUI e executa o bootstrap PostgreSQL publicado duas vezes contra uma base temporária `np_pkg_smoke_*`. Não prova workload completo Simulator -> RabbitMQ -> Prevention a partir do package. |
| Validação de tamper detection | `.\scripts\release\test-package-tamper-detection.ps1 -ArchivePath <zip>` | Muta um ficheiro do package e verifica que a validação clean-install rejeita o archive adulterado. |
| Backup/restore PostgreSQL com dados reais locais | `.\scripts\release\test-postgres-real-data-backup-restore.ps1` | Faz dump da base local `natureprotector`, restaura para uma base temporária, compara contagens de tabelas canónicas de `control.*`, guarda artifact e remove a base restaurada. Não troca a base live nem prova continuação da aplicação após restore. |
| Smoke de microbenchmarks | `.\scripts\performance\run-benchmarks.ps1 -Profile B0 -Filter "*" -TimeoutSeconds <segundos>` | Executa smoke BenchmarkDotNet para scoring candidato, classificação temporal, mappings territoriais e serialização de eventos, com summaries `summary.json/md`. `-SummarizeOnlyDirectory <artifact>` regenera summaries a partir de reports existentes sem relançar BenchmarkDotNet. B0 é smoke de engenharia bounded; B1/B2 devem ser filtrados e temporizados quando usados localmente. Não é baseline estatística de performance nem validação científica. |
| Workload de capacidade sistémica | `.\scripts\performance\run-system-capacity-workload.ps1 -Profile <Calibration|B0|B1|B2> -UseDevelopmentAdminDefault -CalibrationRunDirectory <path>` | Executa workloads locais bounded por API -> Simulator -> RabbitMQ -> Prevention -> PostgreSQL/InfluxDB e escreve `artifacts/performance/system-*/`. É uma baseline local reprodutível de capacidade, não readiness de produção, stress testing ou calibração científica. |
| Catálogo de telemetria | `.\scripts\observability\export-telemetry-catalog.ps1` | Exporta serviços, métricas e tags definidos em `HostTelemetry.cs`, com classificação de cardinalidade para apoio a dashboards e revisão. É catálogo estático, não prova delivery para collector remoto nem correlação cross-service. |
| Smoke OTLP Collector local | `.\scripts\observability\test-otlp-collector-smoke.ps1 -ApiPublishDirectory <publish/backoffice-api>` | Arranca um OpenTelemetry Collector real temporário com OTLP gRPC e exporters para ficheiro, arranca `Backoffice.Api`, consulta `/health` e valida receção de trace e métricas com `service.name=NatureProtector.Backoffice.Api`. Não prova collector remoto, dashboards ou correlação cross-service completa. |

## Gate de infraestrutura local

A validação com Docker é local/manual por desenho e não é exigida pelo workflow CI por defeito.

```powershell
.\scripts\np.ps1 up
.\scripts\workspace.ps1 validate -Profile Infrastructure
```

`np.ps1 up` é o caminho recomendado para subir a infraestrutura local no fluxo clone-to-run. `workspace.ps1 up` permanece disponível como compatibilidade de engenharia para fluxos antigos que juntavam setup local, Docker Compose, bootstrap do control plane e validação de infraestrutura. Exige `.env` existente; o script não cria, copia nem edita `.env` ou `.env.example`.

## Âmbito CI

`.github/workflows/engineering-foundations.yml` executa:

- backend restore, regressões dos scripts de workspace, auditoria NuGet, build, backend coverage, export de test inventory e export de coverage gaps em Windows com `BackofficeApi__ControlPlaneEnabled=false`;
- testes Docker integration do backend num job Ubuntu separado com containers service/runtime PostgreSQL, RabbitMQ e InfluxDB;
- frontend `npm ci`, typecheck, Biome lint/format checks, Vitest, coverage, build de produção e browser checks Playwright contra `vite preview` em Ubuntu;
- artifact de auditoria frontend não bloqueante.

Pull requests e pushes executam a gate crítica Chromium. O workflow agendado e runs manuais com `browser_matrix=all` instalam e executam Chromium, Firefox e WebKit. Playwright mantém traces e screenshots em falha, e retém vídeo apenas em falhas CI.

O job backend Windows por defeito continua a não executar a infraestrutura local Docker Compose. Smoke dependente de infraestrutura fica dividido entre o job CI Docker integration dedicado e a gate local `workspace validate -Profile Infrastructure`.

As gates dedicadas de segurança e release-candidate executam `scripts/ci/run-secret-scan.ps1`. O wrapper usa `.gitleaks.toml`, redige findings, faz upload de `artifacts/secret-scan/`, cobre histórico Git relevante com `--all`, staged changes e ficheiros tracked mais untracked não ignorados no working tree. Para evidence local sob restrição no-Git, `-SkipGitBackedScans` regista history/staged como skipped e faz apenas um snapshot bounded por filesystem; isto é smoke, não substituto da gate CI. `.gitleaksignore` baselina apenas fingerprints históricas local/dev conhecidas e um falso positivo CSS; não contém valores secretos. `.env` e `.env.example` não são modificados pelo scanner, e `.env` é excluído do snapshot filesystem no-Git.

## Snapshot de validação M02

Run local em 2026-06-13:

- `dotnet build .\NatureProtector.sln --no-restore --nologo -v minimal`: passou; na altura reportava um warning `NU1902` conhecido para `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.10.0`, que deixou de existir depois da atualização do package OpenTelemetry.
- `dotnet test .\NatureProtector.sln --no-restore --nologo -v minimal -m:1`: passou, 1179 testes.
- `scripts/tests/generate-coverage-report.ps1`: passou; line coverage `82%`, branch coverage `68.1%`, method coverage `89.5%`.
- `npm run typecheck`: passou.
- `npm test`: passou, 5 testes.
- `npm run test:coverage`: passou; frontend baseline line coverage `5.02%`.
- `npm run build`: passou; maior chunk JS `497.24 kB` minificado, sem warning de large chunk.
- `npm audit --json`: 3 findings high permanecem através de Vite -> esbuild.

Validação backend atual em 2026-06-18:

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\test-workspace-script.ps1`: passou, incluindo assertions de workflow para regressões de workspace, test inventory, coverage gaps e upload de `artifacts/validation/`.
- Remediação owner audit em 2026-06-18: `Security` deixou de ser um falso verde de pré-requisitos e passou a planear/executar `check-dotnet-audit.ps1`, `npm run audit:ci`, `check-secret-canaries.ps1 -NoGit` e testes focados de JWT/autorização/evidence traversal com artifacts. `PerformanceSmoke` passou a usar `run-benchmarks.ps1` com filtro, timeout e summary artifacts em vez de invocar BenchmarkDotNet diretamente sem evidence estruturada.
- Fecho owner audit em 2026-06-19: a vulnerabilidade transitiva `SQLitePCLRaw.lib.e_sqlite3 2.1.10` nos projetos de teste SQLite foi removida com `SQLitePCLRaw.bundle_e_sqlite3 3.0.3`, que resolve `SourceGear.sqlite3 3.50.4.5`. `workspace validate -Profile Security -NonInteractive` passou depois da remediação, e `dotnet list package --vulnerable --include-transitive` ficou limpo nos três projetos afetados.
- Fecho owner audit em 2026-06-19: `dotnet build .\NatureProtector.sln -c Release --no-restore --nologo -v minimal -m:1 -p:UseSharedCompilation=false` passou; a suite backend Release sem Docker final passou com `1559` testes; `DockerIntegration` Release final passou com `34` testes; coverage final `backend-integral` reportou `65.3%` line e `66.1%` branch coverage, com `RuntimeObservabilityService` a `73.6%` e `ControlledValidationRunner` a `100%`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\workspace.ps1 validate -Profile Quick -NonInteractive`: passou.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\generate-coverage-report.ps1 -NoRestore`: passou, 1553 testes backend não-Docker; `backend-integral` line coverage `64.5%`, branch coverage `65.3%`; `backend-focused` line coverage `97.1%`, branch coverage `87.8%`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\export-coverage-gaps.ps1`: passou, exportando `artifacts/validation/coverage-gaps/20260618-051212/` com 38 classes a zero coverage e 5 classes com coverage não nula baixa.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ci\run-secret-scan.ps1 -RepositoryRoot . -SkipGitBackedScans -SkipInstall -IncludeUntracked`: passou com Gitleaks `8.28.0`; history/staged foram explicitamente skipped e o snapshot bounded do working-tree mais canary scan passaram.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ci\check-dotnet-audit.ps1 -OutputPath .\artifacts\dotnet-audit.txt`: passou, sem pacotes NuGet vulneráveis reportados.
- `npm run test:audit-script` e `npm run audit:ci` em `webUI/`: passaram; a política npm audit reportou 1 advisory low e nenhum blocker high/critical.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\release\build-release-candidate.ps1 -DryRun -OutputRoot artifacts\release-smoke`: passou e produziu manifest `nogit` sem executar Git.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\release\build-release-candidate.ps1 -Version validation-20260618-0534 -SkipRestore -SkipFrontendInstall`: passou, produzindo `artifacts/release/natureprotector-validation-20260618-0534.zip`; `-SkipRestore` agora propaga para `dotnet publish`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\release\test-clean-install.ps1 -ArchivePath .\artifacts\release\natureprotector-validation-20260618-0534.zip`: passou.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\release\test-package-tamper-detection.ps1 -ArchivePath .\artifacts\release\natureprotector-validation-20260618-0534.zip`: passou.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\release\build-release-candidate.ps1 -Version validation-20260618-block-f2 -SkipRestore -SkipFrontendInstall`: passou, produzindo `artifacts/release/natureprotector-validation-20260618-block-f2.zip` com `data/` e `evidence/sbom.json`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\release\test-clean-install.ps1 -ArchivePath .\artifacts\release\natureprotector-validation-20260618-block-f2.zip`: passou, exigindo `data/` e `evidence/sbom.json`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\release\test-functional-package-smoke.ps1 -ArchivePath .\artifacts\release\natureprotector-validation-20260618-block-f2.zip -StartupTimeoutSeconds 45`: passou; artifact `artifacts/release/functional-package-smoke/20260618-183058/` validou `/health`, assets webUI e dois runs idempotentes do bootstrap publicado contra `np_pkg_smoke_20260618183058`.
- `dotnet test .\tests\NatureProtector.IntegrationTests\NatureProtector.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~DockerPublishedRuntimeProcessTests.PublishedRuntimeProcesses_RunSimulatorPreventionApiPath_AndCleanUp"` com `OTEL_EXPORTER_OTLP_ENDPOINT` apontado para um Collector real local e `OTEL_BSP_SCHEDULE_DELAY=200`: passou; artifact `artifacts/observability/otlp-published-runtime-smoke/20260618-183424/` recebeu traces e métricas com `NatureProtector.Backoffice.Api`, `NatureProtector.Prevention.Host` e `NatureProtector.Simulator.Host`, incluindo texto de correlação. Isto prova delivery OTLP cross-service local, não collector remoto nem dashboards.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-benchmarks.ps1 -Profile B0 -Filter "*"`: passou, 12 benchmarks smoke BenchmarkDotNet executados; artifacts exportados em `artifacts/performance/benchmarks-B0-20260618-053825/`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-benchmarks.ps1 -SummarizeOnlyDirectory .\artifacts\performance\benchmarks-B1-20260618-145929`: passou, regenerando `summary.json/md` sem relançar BenchmarkDotNet e classificando a run B1 de serialização como `ready` a partir dos reports válidos.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-benchmarks.ps1 -Profile B1 -Filter "*ScoringBenchmarks.CreateAssessments*" -TimeoutSeconds 180`: passou, produzindo `artifacts/performance/benchmarks-B1-20260618-181921/` com 3 benchmarks B1 de scoring e summaries `ready`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-system-capacity-workload.ps1 -Profile Calibration -UseDevelopmentAdminDefault`: passou depois dos fixes de runtime startup; artifact final `artifacts/performance/system-Calibration-20260618-133911-493/` mediu 1/1 accepted reading, 1/1 risk assessment, 0 eventos rejected/quarantined/lost e fila final de ingestão a 0.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-system-capacity-workload.ps1 -Profile B0 -UseDevelopmentAdminDefault -CalibrationRunDirectory .\artifacts\performance\system-Calibration-20260618-133911-493`: passou; artifact final `artifacts/performance/system-B0-20260618-134327-718/` mediu 8/8 accepted readings, 8/8 risk assessments, 0 eventos rejected/quarantined/lost, duração p95 de request 7291.44 ms, backlog drain p95 30.62 ms e fila final de ingestão a 0.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-system-capacity-workload.ps1 -Profile B1 -UseDevelopmentAdminDefault -CalibrationRunDirectory .\artifacts\performance\system-Calibration-20260618-133911-493`: passou; artifact final `artifacts/performance/system-B1-20260618-134408-654/` mediu 60/60 accepted readings, 60/60 risk assessments, 0 eventos rejected/quarantined/lost, duração p95 de request 39942.78 ms, backlog drain p95 7141.64 ms e fila final de ingestão a 0.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-system-capacity-workload.ps1 -Profile B2 -UseDevelopmentAdminDefault -CalibrationRunDirectory .\artifacts\performance\system-Calibration-20260618-133911-493`: passou depois da janela de observação passar a ser específica por perfil; artifact final `artifacts/performance/system-B2-20260618-134841-918/` mediu 60/60 accepted readings, 60/60 risk assessments, 0 eventos rejected/quarantined/lost, duração p95 de request 67332.61 ms, backlog drain p95 5098.15 ms e fila final de ingestão a 0.

O smoke de microbenchmark B0 usa uma launch e uma iteração medida. O output `Error=NA` e os warnings BenchmarkDotNet de iteração mínima são esperados neste perfil; portanto a run prova apenas saúde de build/execução/export dos benchmarks. Usar B1/B2 para comparações de engenharia; não usar valores B0 como SLOs de produção, evidence de carga ou calibração científica.

O workload de capacidade sistémica é separado do BenchmarkDotNet. Usa endpoints persistidos de runtime audit/timing e métricas RabbitMQ management. `PublishedAt` não é persistido no envelope RabbitMQ atual, por isso o workload não afirma latência integral publish-to-UI. p50/p95/p99 de request são calculados sobre requests de run concluídos, e backlog drain time é medido contra `np.ingestion.readings`; snapshots RabbitMQ completos, incluindo filas auxiliares como `np.observability.raw`, ficam no diretório `metrics/` da run.

Nota de runtime startup desta passagem: `scripts/dev/start-local-runtime.ps1` compila Backoffice API e Prevention Host sequencialmente em Release, depois inicia ambos com `dotnet run -c Release --no-build --no-restore`. Isto evita escritas concorrentes em `obj/` Debug durante startup local. O startup da Backoffice API também garante que a linha fixa da role Admin existe antes de atribuir o utilizador Admin local de Development, prevenindo falha de foreign key em `user_roles.RoleId` numa base local stale.

## Triagem de segurança frontend

A M02 atualizou packages compatíveis por semver para remover advisories React Router e Tailwind/Vite plugin:

- `react-router` e `react-router-dom` para `7.17.0`;
- `@tailwindcss/vite` e `tailwindcss` para `4.3.1`;
- `vite` para `6.4.3`.

Finding residual:

- `vite@6.4.3` depende de `esbuild@0.25.12`; `npm audit` sinaliza `esbuild <0.28.1`.
- Vite 6 declara `esbuild` como `^0.25.0`, por isso forçar `0.28.1` ultrapassaria o intervalo declarado pelo Vite.
- `npm audit fix --force` propõe um caminho major/downgrade e não foi aplicado.

Próximo passo recomendado: avaliar um upgrade major controlado do Vite quando o runtime local Node for atualizado para uma versão aceite por essa linha Vite, depois repetir typecheck, testes, coverage e build frontend.

## Observabilidade

A Backoffice API expõe um endpoint mínimo `GET /health` através de ASP.NET health checks. O endpoint é técnico por desenho e não altera contratos RabbitMQ, domain events, semântica de scoring, schemas ou política de alertas.

O wiring runtime OpenTelemetry vive agora em `NatureProtector.Shared.Observability`. `NatureProtector.Shared` continua a fronteira de contratos/messaging e não deve referenciar packages `OpenTelemetry*`. O package beta `OpenTelemetry.Instrumentation.Process` fica intencionalmente isolado apenas no assembly de observabilidade runtime; contratos puros não devem depender de exporters ou instrumentation packages.

O smoke focado de observabilidade em `NatureProtector.Shared.Tests` inicia o hosted service OpenTelemetry com console export desligado e endpoint OTLP configurado. É smoke de compatibilidade/startup, não prova de collector live nem de entrega de telemetry.
