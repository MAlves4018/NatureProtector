# Testes

Esta pasta contém os projetos de teste da solução. O objetivo da suite atual é proteger a lógica de domínio mais estável, cobrir os caminhos críticos da baseline de prevenção já operacionais em runtime e manter rastreáveis as decisões técnicas da V1.

## Projetos existentes

- [NatureProtector.Core.Tests](NatureProtector.Core.Tests)
  - domínio base: áreas, primitivas, leituras, risco, cenários, sensores e meteorologia;
- [NatureProtector.Prevention.Tests](NatureProtector.Prevention.Tests)
  - scoring, snapshots, `NormalizedReading`, `OperationalEvent`, `ClassifierResult`, elegibilidade, `RiskInput`, `DailyCellState` e persistência in-memory do módulo de prevenção;
- [NatureProtector.Shared.Tests](NatureProtector.Shared.Tests)
  - serialização, contratos, fixtures JSON versionadas do envelope `SensorReadingProduced`, topologia de messaging, guardrails de dependências e smoke de wiring OpenTelemetry;
- [NatureProtector.Simulator.Host.Tests](NatureProtector.Simulator.Host.Tests)
  - contexto, geração de leituras, publishers, runtime do simulador, `RunOverrides`, seleção determinística de sensores e suporte ao orquestrador de cenários;
- [NatureProtector.Prevention.Host.Tests](NatureProtector.Prevention.Host.Tests)
  - pipeline ativa, validação pré-inbox do contrato JSON, inbox, retries, quarentena, classificadores de falha, política de alertas V1, projeções operacionais e adaptadores PostgreSQL do host de prevenção;
- [NatureProtector.Infrastructure.Influx.Tests](NatureProtector.Infrastructure.Influx.Tests)
  - configuração, DI e write service de InfluxDB;
- [NatureProtector.Backoffice.Api.Tests](NatureProtector.Backoffice.Api.Tests)
  - arranque da API, endpoints do control plane, respostas de indisponibilidade, áreas, grelha, configurações, simulation runs, projeções operacionais e matriz de autorização inventariada por endpoint;
- [NatureProtector.IntegrationTests](NatureProtector.IntegrationTests)
  - compatibilidade entre simulador e prevenção, integrações Docker com PostgreSQL/RabbitMQ/InfluxDB reais e smoke de processos publicados.

## Taxonomia de qualidade

A taxonomia usada no inventario automatico distingue: `Unit`, `Component`, `Domain`, `API`, `Contract`, `Architecture`, `PropertyBased`, `AdapterIntegration`, `DistributedIntegration`, `ProcessLevelIntegration`, `BrowserIntegration`, `FullStackE2E`, `Accessibility`, `Security`, `Mutation`, `Microbenchmark` e `SystemPerformance`.

O inventario e gerado a partir do workspace, nao apenas desta documentacao. Ele classifica projetos, categorias explicitas, dependencias externas, duracao estimada e frequencia recomendada:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\export-test-inventory.ps1
```

O run de `18/06/2026` gerou `artifacts/validation/test-inventory/20260618-045752/` com `8` projetos backend de teste, `985` atributos `[Fact]`/`[Theory]`/`[Property]` detetados, `8` propriedades FsCheck e `9` TRX existentes para enriquecimento futuro de duracao. Estes numeros sao inventario estatico; as contagens reais de casos xUnit continuam a vir de `dotnet test`.

Cobertura de alto risco reforcada neste bloco: `NatureProtector.Simulator.Host.Tests` cobre `RabbitMqPublishGuarantees` para publisher confirms e mandatory returns sem broker real; `NatureProtector.Backoffice.Api.Tests` cobre fallbacks de user-plane/observability indisponiveis e a recolha de metricas RabbitMQ via `RuntimeObservabilityService` com HTTP fake.

Estado atual importante: Playwright/webUI fica classificado como `BrowserIntegration` e `Accessibility`, nao como `FullStackE2E`, porque os testes atuais usam fixture HTTP limitada e nao provam um fluxo live completo com API, runtime e infraestrutura reais.

## Como executar

Para compilar a solution antes da suite:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet build .\NatureProtector.sln --nologo -v minimal --configfile NuGet.Config
```

Para correr todos os testes disponíveis:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet test .\NatureProtector.sln --nologo -v minimal -m:1
````

Para validar os wrappers locais de workspace sem Docker, sem Git e sem operacoes destrutivas:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\test-workspace-script.ps1
```

Para focar apenas a `Prevention.Host`:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet test .\tests\NatureProtector.Prevention.Host.Tests\NatureProtector.Prevention.Host.Tests.csproj --nologo -v minimal
```

Para correr as integrações Docker locais:

```powershell
$env:NP_TEST_RABBITMQ_PORT='5673'
$env:NP_TEST_RABBITMQ_CONTAINER='np-rabbitmq-it'
dotnet test .\tests\NatureProtector.IntegrationTests\NatureProtector.IntegrationTests.csproj `
  -c Release --no-restore --nologo -v minimal -m:1 `
  --filter "Category=DockerIntegration"
```

Esta suite espera PostgreSQL, RabbitMQ e InfluxDB locais acessíveis. Nos serviços usados pela baseline local, os defaults são `np-postgres` em `5433`, `np-rabbitmq-it` em `5673` e `np-influxdb` em `8181`. Os testes criam recursos temporários isolados: bases PostgreSQL `np_it_*`, vhosts RabbitMQ `np-it-*` e databases InfluxDB `np_it_*`. O cleanup é parte do contrato da suite.

Os testes RabbitMQ em Docker validam também garantias de transporte do publisher: delivery persistente, `contentType`/`contentEncoding`, `messageId`, `correlationId`, publisher confirms, publicação `mandatory`, falha em mensagem unroutable e recriação de canal/conexão fechados. Esses testes usam vhost temporário e não tocam nas filas canónicas.

Os testes DockerIntegration do consumidor Prevention cobrem backlog antes do arranque do consumidor, duplicados com efeitos únicos, mismatch de payload duplicado, falhas transitórias/permanentes depois da materialização no inbox, recovery de leases `Processing` expirados e outages reais isolados. `PreventionWorker_PostgresOutageBeforeInboxCommit_RequeuesWithoutAcking_OnIsolatedDatabase` remove a base PostgreSQL temporária `np_it_*` antes do commit no inbox e valida que a mensagem RabbitMQ volta para ready sem ack. `PreventionWorker_IsolatedRabbitMqVhostDeletion_StopsWithoutTouchingPostgres` remove apenas o vhost RabbitMQ temporário `np-it-*` e valida que a base PostgreSQL temporária continua disponível. `PreventionWorker_RediveredStoredInboxEvent_AcksAndRecoversViaRetryWorker_WithoutDuplicateEffects` cobre a janela operacional equivalente a "inbox commit antes de broker ACK": o evento é materializado no inbox, a redelivery RabbitMQ do mesmo envelope é observada como duplicado sem efeitos de projeção, e o `InboxRetryWorker` conclui o processamento depois da expiração controlada do lease.

## Backoffice API authorization inventory

`NatureProtector.Backoffice.Api.Tests` inclui uma matriz de autorização derivada do runtime ASP.NET Core. A suite lê `EndpointDataSource`, cruza método HTTP e route pattern com uma classificação explícita e falha se nascer um endpoint sem política testada.

A matriz cobre endpoints anónimos (`/health`, OpenAPI em Development e rotas públicas), endpoints autenticados sem role específica, endpoints `Admin`, `Sim`, `Pipeline` e combinações reais como `Sim/Admin`. Asserções permitidas verificam a fronteira de autorização; alguns endpoints de user plane podem devolver indisponibilidade funcional quando o control plane está desligado nos testes, mas não podem devolver `401`/`403` para perfis autorizados. Perfis não autorizados devem devolver `401` ou `403`.

Os smoke tests de `Program` também desligam explicitamente `BackofficeApi:ControlPlaneEnabled` para validar arranque, `/health`, rota desconhecida e OpenAPI sem depender do estado PostgreSQL local.

A mesma suite inclui testes JWT reais com o middleware `JwtBearer`: token válido, token expirado, assinatura inválida, issuer inválido, audience inválida, token sem role, role diferente, múltiplas roles com uma role válida, utilizadores distintos no endpoint autenticado `me` e acesso direto a endpoint protegido sem bearer token.

`NatureProtector.IntegrationTests.UserPlane.PostgresUserRolePlaneServiceTests` cobre o user-plane real em PostgreSQL temporário `np_it_*`: criação e consulta de utilizadores, rejeição de duplicados/input inválido/roles inexistentes sem rows parciais, hashing de password, login válido/inválido, atribuição/remoção idempotente de roles, múltiplas roles, users by role, rollback em update inválido, cleanup da database temporária e o fluxo HTTP `PostgreSQL -> login -> JWT -> endpoint protegido` via `WebApplicationFactory`.

Os endpoints de evidence HTTP têm corpus de segurança para `../`, `..\`, traversal encoded e double-encoded, paths absolutos, null byte, nomes reservados, acesso anónimo, role não autorizada e containment real contra reparse points/symlinks na enumeração do catálogo.

Os testes OpenAPI da Backoffice API validam semanticamente o documento runtime gerado: `securitySchemes` JWT bearer, `security` por operação protegida, response codes `401`/`403`/`503`, schemas runtime/observability, fields required, nullability crítica e content types de request/response. A webUI mantém um snapshot reduzido derivado desse OpenAPI para alinhar interfaces TypeScript e rotas runtime usadas pelo client API.

## Architecture guardrails

Os guardrails de arquitetura combinam scans textuais com verificações semânticas locais. `NatureProtector.Core.Tests` valida as fronteiras por `ProjectReference` e falha se os projetos em `src/` formarem ciclos. Também falha se `NatureProtector.Shared` voltar a referenciar pacotes `OpenTelemetry*`, porque exporters e instrumentation pertencem a `NatureProtector.Shared.Observability`. `NatureProtector.Shared.Tests` e `NatureProtector.Prevention.Tests` inspecionam referências reais de assemblies para impedir dependências de feature, API, infrastructure, EF ou Npgsql onde essas camadas não devem entrar. `NatureProtector.Backoffice.Api.Tests` usa reflection nos tipos públicos em namespaces `Contracts` para impedir que contratos HTTP exponham tipos de persistence, EF, Npgsql, `DbContext` ou infrastructure.

No frontend, `webUI/src/app/routingImports.test.ts` protege a app browser contra imports diretos de `react-router`, uso de `process.env`, acesso genérico ou não público a `import.meta.env` e imports Node-only fora de testes. Variáveis expostas ao bundle devem continuar no formato público do Vite (`VITE_*`) e não podem transportar secrets de servidor.

The frontend guardrail also rejects non-test browser `console.*` messages that mention sensitive user, session, role, token, bearer, authorization, password or credential terms. Browser diagnostics that remain must stay generic and must not log response bodies or session/user objects.

## Smoke B/C runtime

O smoke B/C executa a prova operacional reprodutivel da V1 quando a API e a infraestrutura local estao disponiveis. Ele nao substitui os testes unitarios nem recalcula risco; apenas orquestra runs e recolhe evidencia persistida.

Validacao sem executar HTTP/runtime:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\run-v1-bc-smoke.ps1 -DryRun
```

Execucao real, com `Backoffice.Api` em Development e PostgreSQL/RabbitMQ/Prevention/Simulator acessiveis:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\run-v1-bc-smoke.ps1 `
  -ApiBaseUrl http://localhost:5254 `
  -AreaCode proenca-a-nova
```

O script gera uma pasta `docs/evidence/runs/v1-bc-smoke-<timestamp>/` com:

* `summary.md`;
* `run-spec.resolved.json`;
* `run-b.json` e `run-c.json`;
* `audit-b.json` e `audit-c.json`;
* `runtime-summary.json`;
* `np-vs-fwi-kbdi.json`;
* `portuguese-context-proxy.json`;
* `kbdi-series-context.json`;
* `components.json`;
* `daily-cell-state.json`;
* `degradation-effects.json`;
* `b-vs-c.json`;
* `compare-b-vs-c.json`;
* diagnostics de qualidade, contexto diario, classes NP/FWI/KBDI, proxy portugues candidato e coverage/freshness.

O smoke valida que FWI/KBDI aparecem como calculados ou como Missing/Partial com limitation explicita. O `PortugueseContextRiskProxy` e candidato e nao deve ser apresentado como RCM/PIR/IPMA oficial. O KBDI e diario/acumulativo; quando falta historico antecedente, espera-se status/limitation de historico limitado em vez de leitura como calibrada.

Por defeito, a smoke recolhe evidencia via API e nao ativa `collectEvidence` no endpoint de arranque do `Simulator.Host`. Isto evita bloqueios em stdout/stderr de processos long-running. Se for necessario recolher tambem logs/evidencia do processo filho, usar `-CollectRuntimeProcessEvidence`.

Se a API ou Docker/PostgreSQL/RabbitMQ nao estiverem disponiveis, o script escreve `limitations.md` com uma mensagem objetiva. A execucao real continua a ser opcional/manual para nao tornar a suite `dotnet test` dependente de broker, base de dados ou processos long-running.

## Coverage

O repositório usa `coverlet.collector` e agrega os resultados com `reportgenerator`.

Para gerar o relatório consolidado:

```powershell
.\scripts\tests\generate-coverage-report.ps1
```

Este comando:

* corre `dotnet test` em `Release` com `coverage.runsettings`;
* escreve TRX e Cobertura apenas em `artifacts/coverage/test-results`;
* exclui `DockerIntegration` por defeito; usar `-IncludeDockerIntegration` para incluir esses testes;
* agrega os `coverage.cobertura.xml` gerados nesse diretório controlado;
* gera dois relatórios HTML/TextSummary: `artifacts/coverage/backend-integral` e `artifacts/coverage/backend-focused`.

O relatório `backend-integral` é a leitura ampla do backend/runtime e mantém composition roots, `Program.cs`, workers, hosted/background services, o assembly de bootstrap PostgreSQL e `NatureProtector.Shared.Observability` no escopo. O relatório `backend-focused` cobre risco, classificadores, eligibility, mappings e contratos críticos; não deve ser apresentado como coverage global. A geração falha se um assembly esperado desaparecer do resumo, para evitar scope drift silencioso.

Para exportar classes a `0%` e classes abaixo do limiar conservador sem regenerar a coverage:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\export-coverage-gaps.ps1
```

O run de `18/06/2026` gerou `artifacts/validation/coverage-gaps/20260618-051212/`: `38` classes a `0%` no `backend-integral` e `5` classes com coverage nao nula abaixo de `50%`. O relatorio mantem `backend-integral` e `backend-focused` separados; migrations, DTOs e composition roots devem ser classificados antes de criar testes apenas para inflar coverage.

O run final da remediacao owner audit de `19/06/2026` gerou `artifacts/validation/coverage-gaps/20260619-004318/`: `35` classes a `0%` no `backend-integral` e `4` classes com coverage nao nula abaixo de `50%`. `RuntimeObservabilityService` passou a `73.6%` e `ControlledValidationRunner` a `100%` no `backend-integral` depois de testes operacionais focados. `PostgresUserRolePlaneService` continua a aparecer a `0%` na coverage non-Docker porque a prova real usa PostgreSQL temporario em `DockerIntegration`; essa cobertura deve ser lida pela evidence TRX, nao como ausencia de testes.

## Mutation testing

O wrapper local para Stryker.NET e:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\run-mutation.ps1 -Profile Smoke
```

O wrapper escreve manifest, stdout/stderr, snapshot de configuracao e outputs Stryker em `artifacts/mutation/<run-id>/`, aplica timeout e mantem `break-at 0` ate existir uma baseline fiavel.

Estado atual em `18/06/2026`: as primeiras execucoes em `artifacts/mutation/20260618-035944/` e `artifacts/mutation/20260618-042037/` ficaram `DEFECT` porque Stryker continuava a expandir a solucao real ou expirava sem reports fiaveis. O wrapper foi ajustado para o perfil `Smoke` gerar uma solucao temporaria isolada em `artifacts/mutation/<run-id>/isolated-solution/`, contendo apenas `NatureProtector.Prevention` e `NatureProtector.Prevention.Tests`, e para aceitar `-Reporters` como lista CSV validada.

A melhor evidencia atual e `artifacts/mutation/20260618-044201/`, classificada como `BLOCKED_AFTER_REMEDIATION_ATTEMPT`. Essa run usou a solucao temporaria, `Release`, mutation-level `Basic`, concurrency `2` e reporters `Progress,ClearText,Json`; Stryker analisou 1 test project, descobriu 256 testes, criou 1020 mutants, saltou 1016 por filtro/compile errors, executou 4 mutants, matou 3, deixou 1 survivor e reportou 0 timeouts. Contudo, o processo nao fechou nem escreveu JSON antes do timeout do wrapper. O score textual de 75% e diagnostico apenas; nao e baseline aceitavel e nao deve ser usado para subir `break`.

Nao repetir esta run apenas com timeout global maior. A proxima tentativa aceitavel deve atacar diretamente o blocker pos-score/report/exit, por exemplo diagnostic mode do Stryker, harness de mutation ainda mais pequeno ou investigacao de tooling. Ate existir JSON/HTML fiavel, mutation fica fora de ratchet automatico.

## Frontend test gate

A webUI tem uma gate propria de engenharia em `webUI/package.json`:

```powershell
cd .\webUI
npm ci
npm run typecheck
npm run lint
npm run format:check
npm test
npm run test:coverage
npm run build
npm run test:e2e
```

O ambiente de testes usa Vitest, jsdom, Testing Library e `axe-core` para uma primeira baseline de componente/acessibilidade. O comando `npm run test:e2e` usa Playwright sobre `npm run build` + `vite preview`, validando o artefacto em `dist/` em vez do Vite dev server. Os artefactos `webUI/coverage/`, `webUI/test-results/` e `webUI/playwright-report/` sao gerados localmente e ignorados por git.

`webUI/e2e/ui-v2-authenticated.spec.ts` cobre a matriz browser UI v2 com fixture HTTP isolada: Anonymous sem operacoes protegidas, Admin com Pipeline/health/evidence/admin, Sim com simulacao e perfil degradado, Pipeline sem acesso a simulacao, roles desconhecidas como superficie publica, expiracao de sessao, login 401, write 403, runtime summary 500/network/timeout/null/unknown/stale/blocked/partial e download de evidence via cliente autenticado. Esta suite valida o bundle final e a experiencia browser/capability; nao substitui testes backend de JWT, autorizacao real por endpoint ou uma execucao contra identidades persistidas locais.

Remediação owner audit em 2026-06-18: a suite autenticada UI v2 também cobre `Admin`, `Sim` e `Pipeline` depois de browser reload, e token guardado inválido/expirado a remover a sessão antes de expor superfícies internas. `ContextualHelp.test.tsx` e `EmptyState.test.tsx` cobrem foco/fecho e estados vazios úteis dos componentes UI v2 sem transformar essa cobertura em claim FullStackE2E.

`webUI/e2e/ui-v2-public.spec.ts` cobre tambem a superficie publica com `axe-core`, skip link funcional por teclado, ciclo de foco do dialogo de ajuda F1, Escape/focus restore, dark mode, viewport mobile e `prefers-reduced-motion: reduce`. Isto e uma gate de regressao de acessibilidade, nao uma declaracao de conformidade WCAG.

O frontend nao deve fazer logging de payloads de utilizador, sessao, roles, token, bearer, autorizacao, password ou credenciais. Mensagens diagnosticas que permanecem no browser devem ser genericas e sem corpos de resposta ou objetos de sessao.

## Secret scanning gate

`scripts\ci\run-secret-scan.ps1` executa Gitleaks `8.28.0` com `.gitleaks.toml`, `--redact=100` e reports JSON em `artifacts/secret-scan/`. A gate CI cobre historico Git relevante, staged changes e snapshot do working tree; em CI usa `-IncludeUntracked`. O snapshot exclui ambientes virtuais, `node_modules`, `bin`, `obj`, `dist`, coverage, `TestResults`, `artifacts` e `graphify-out`: estes outputs gerados sao cobertos pela cadeia de dependencias e nao podem produzir falsos positivos como se fossem source do projeto. Quando a operacao local nao pode executar comandos Git, o wrapper tambem suporta `-SkipGitBackedScans`, que salta history/staged de forma explicita e faz apenas um snapshot bounded por filesystem, sem copiar `.env`. O script tambem corre `scripts\ci\check-secret-canaries.ps1` como scan regex complementar; esse canary tem o mesmo modo `-NoGit`.

Validacao Bloco E de `18/06/2026`: `run-secret-scan.ps1 -RepositoryRoot . -SkipGitBackedScans -SkipInstall -IncludeUntracked` passou com Gitleaks `8.28.0`; `history` e `staged` ficaram `skipped`, `working-tree` passou com `9328` ficheiros no snapshot bounded e `canaryScan` passou. Esta evidence nao substitui a gate CI Git-backed.

`.gitleaksignore` contem apenas fingerprints historicos de fixtures local/dev ou falso positivo CSS. Nao adicionar valores secretos a esse ficheiro. `.env` permanece ignorado e `.env.example` nao foi alterado.

## Toolchain pinning

`global.json` fixa o SDK .NET em `9.0.306` com `rollForward: latestPatch`. Feature bands adicionais devem entrar por matriz explicita de CI, nao por `latestFeature` local.

## Observability compatibility smoke

`NatureProtector.Shared.Observability` concentra o wiring OpenTelemetry, exporters e instrumentation runtime. `NatureProtector.Shared` permanece a fronteira de contratos/messaging e nao deve precisar de exporters para compilar ou ser consumido.

O pacote beta `OpenTelemetry.Instrumentation.Process` continua aceite nesta fase, mas esta isolado no assembly de observabilidade runtime. A decisao atual e manter a instrumentacao de processo sem a propagar para contratos puros.

`NatureProtector.Shared.Tests` cobre:

* nomes estaveis de `ActivitySource`/`Meter`;
* configuracao de `ActivityTrackingOptions`;
* registo de OpenTelemetry sem infraestrutura externa;
* arranque do hosted service OpenTelemetry com endpoint OTLP configurado, sem exigir um collector real.

Por defeito, Playwright corre em Chromium. Para validar a matriz completa localmente:

```powershell
cd .\webUI
$env:NP_PLAYWRIGHT_BROWSER_MATRIX='all'
npm run test:e2e
Remove-Item Env:\NP_PLAYWRIGHT_BROWSER_MATRIX
```

Na CI, PR/push mantem Chromium como gate critico. A execucao agendada e `workflow_dispatch` com `browser_matrix=all` correm Chromium, Firefox e WebKit. Traces e screenshots sao retidos em falha; video fica limitado a falhas em CI.

Ver tambem [docs/implementation/engineering-foundations.md](../docs/implementation/engineering-foundations.md) para a gate M02 completa, incluindo CI, auditoria npm e infraestrutura local.

A fatia UI v2 da M03 acrescentou testes dedicados em `webUI/src/app/ui-v2/` para capacidades read-only, adapter de contexto, estados degradados, ajuda/F1, troca PT/EN e uma smoke de acessibilidade com `axe-core`. A M04 expandiu essa suite para selecao dinamica de area, runs/cenarios, ajuda integrada, simulacao read-only/autorizada e URLs novas do client API. A M05 expandiu novamente a suite para superficies tecnicas: Pipeline/Observability, QA, Evidence/Limitations, Administracao proporcional, P3 experimental, readiness, claims seguros e estados de ausencia explicita.

Validacao M06 em `14/06/2026`:

* `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-local-readiness-workload.ps1 -DryRun`: passou;
* `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-local-readiness-workload.ps1 -ApiBaseUrl http://127.0.0.1:5254 -WebBaseUrl http://127.0.0.1:5173 -Repetitions 5`: passou com 55/55 statuses esperados;
* `dotnet test .\NatureProtector.sln --nologo -v minimal -m:1`: passou com `1182` testes e `NU1902` conhecido;
* `npm run typecheck`: passou;
* `npm test`: passou com `30` testes frontend;
* `npm run test:coverage`: passou, com `31.71%` de line coverage global da webUI e `84.28%` de line coverage em `app/ui-v2`;
* `npm run build`: passou.

O workload M06 e uma medicao local de HTTP/status/tempo. Nao mede throughput maximo, stress, broker backlog, publisher timestamps ou latencia end-to-end por evento.

Validacao de microbenchmarks em `18/06/2026`:

* `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-benchmarks.ps1 -Profile B0 -Filter "*"`: passou;
* o run criou `artifacts/performance/benchmarks-B0-20260618-053825/` com `12` benchmarks BenchmarkDotNet para scoring candidato, classificacao temporal, mappings territoriais e serializacao de envelopes;
* `B0` usa uma launch e uma iteracao medida; por isso os campos `Error=NA` e avisos de iteracao minima sao esperados e nao devem ser usados como baseline estatistica, SLO, stress test ou validacao cientifica.

Validacao de capacidade sistemica local em `18/06/2026`:

* `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-system-capacity-workload.ps1 -Profile Calibration -UseDevelopmentAdminDefault`: passou; artifact final `artifacts/performance/system-Calibration-20260618-133911-493/`;
* `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-system-capacity-workload.ps1 -Profile B0 -UseDevelopmentAdminDefault -CalibrationRunDirectory .\artifacts\performance\system-Calibration-20260618-133911-493`: passou; artifact final `artifacts/performance/system-B0-20260618-134327-718/`;
* `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-system-capacity-workload.ps1 -Profile B1 -UseDevelopmentAdminDefault -CalibrationRunDirectory .\artifacts\performance\system-Calibration-20260618-133911-493`: passou; artifact final `artifacts/performance/system-B1-20260618-134408-654/`;
* `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-system-capacity-workload.ps1 -Profile B2 -UseDevelopmentAdminDefault -CalibrationRunDirectory .\artifacts\performance\system-Calibration-20260618-133911-493`: passou apos aumentar a janela de observacao por perfil; artifact final `artifacts/performance/system-B2-20260618-134841-918/`;
* B0 final mediu 8/8 accepted/risk, B1 final 60/60 e B2 final 60/60, sempre com 0 rejected, 0 quarantined, 0 lost events e fila final `np.ingestion.readings` a 0;
* esta e uma baseline local reproduzivel de capacidade do sistema, nao readiness de producao, stress test, SLO ou validacao cientifica.

Validacao PRE-EXTERNAL-VERIFICATION-READINESS em `14/06/2026`:

* `scripts\tests\generate-coverage-report.ps1`: passou com `82%` line coverage backend, `68.1%` branch coverage e `89.5%` method coverage; `Backoffice.Api.Tests` passou com `92` testes, incluindo auth guard de `configurations/active`;
* `npm run test:coverage`: passou inicialmente com `31.66%` line coverage global da webUI e `84.12%` em `app/ui-v2`, abaixo do piso M06 de `84.28%`;
* foi adicionado um teste para roles desconhecidas em `webUI/src/app/ui-v2/capabilities.test.ts`, cobrindo o fallback publico demo/help sem inventar roles;
* `npm run test:coverage`: passou de novo com `31` testes frontend, `31.76%` line coverage global da webUI e `84.45%` em `app/ui-v2`.

Os gaps restantes continuam concentrados na UI legacy/beta, user-plane API sem cobertura agregada relevante, integration glue e caminhos dependentes de infraestrutura externa. Nao foram adicionados testes artificiais apenas para inflar percentagens.

Validacao M05 em `14/06/2026`:

* `npm run typecheck`: passou;
* `npm test -- src/app/ui-v2 src/app/services/api.test.ts`: passou com `27` testes;
* `npm run test:coverage -- src/app/ui-v2 src/app/services/api.test.ts`: passou, com `30.72%` de line coverage global da webUI no run focado e `84.12%` de line coverage em `app/ui-v2`;
* `npm run build`: passou;
* `npm test`: passou com `30` testes frontend;
* `dotnet test tests\NatureProtector.Backoffice.Api.Tests\NatureProtector.Backoffice.Api.Tests.csproj --no-restore --nologo -v minimal -m:1`: passou com `91` testes;
* `dotnet test NatureProtector.sln --no-restore --nologo -v minimal -m:1`: passou com `1182` testes.

O ratchet local da UI v2 fica, apos M05, em `84.12%` de line coverage para `app/ui-v2`, acima do baseline M04 de `81.28%`. Isto nao altera a leitura da coverage global da beta/webUI legacy, que continua baixa e fora do escopo destas missoes UI v2.

## Warnings conhecidos

Nao existe, na validacao E2 de `16/06/2026`, warning `NU1902` atual para OpenTelemetry. `dotnet list .\NatureProtector.sln package --vulnerable --include-transitive` reportou zero pacotes vulneraveis em todos os projetos da solution.

## Resultados atuais

Atualizacao Bloco C de `18/06/2026`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\generate-coverage-report.ps1 -NoRestore` passou com `1553` testes backend nao-Docker. O relatorio `backend-integral` em `artifacts/coverage/backend-integral/Summary.txt` reporta `64.5%` line coverage, `65.3%` branch coverage, `87%` method coverage e `81.6%` full method coverage em `10` assemblies, incluindo `NatureProtector.Shared.Observability`. O relatorio `backend-focused` reporta `97.1%` line coverage, `87.8%` branch coverage, `97%` method coverage e `95.4%` full method coverage. A leitura integral e ampla e inclui runtime/API/glue operacional; nao deve ser comparada diretamente com medicoes antigas de escopo menor nem lida como validacao cientifica do modelo. A mesma vaga reforcou a protecao de recovery dos inboxes: finalizacoes tardias de leases expiradas ja recuperadas nao podem sobrescrever a tentativa corrente.

Atualizacao owner audit de `19/06/2026`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\generate-coverage-report.ps1 -NoRestore -NoBuild` passou com `1559` testes backend nao-Docker. O `backend-integral` reporta `65.3%` line coverage, `66.1%` branch coverage, `87.8%` method coverage e `82.1%` full method coverage; o `backend-focused` manteve `97.1%` line coverage e `87.8%` branch coverage. A subida veio de testes focados em `RuntimeObservabilityService.GetOperationalHealthAsync` e `ControlledValidationRunner`, nao de testes triviais para DTOs/migrations.

Atualizacao PRE-EXTERNAL-VERIFICATION-READINESS de `14/06/2026`: a medicao consolidada mais recente em `coveragereport_core/Summary.txt` reporta `82%` de line coverage, `68.1%` de branch coverage, `89.5%` de method coverage e `83.5%` de full method coverage, com `1183` testes backend passados nos projetos cobertos. A descida face ao valor historico acompanha a inclusao de API/runtime/control-plane/user-plane e glue operacional no escopo agregado; nao deve ser lida como validacao cientifica do modelo.

Atualizacao de `28/05/2026`: a medicao consolidada anterior em `coveragereport_core/Summary.txt` reportava `87.6%` de line coverage, `76.8%` de branch coverage, `91.9%` de method coverage e `86.1%` de full method coverage. A queda face a medicoes anteriores e conhecida e vem sobretudo de DTOs, diagnostics, glue runtime/API, migrations e scripts de evidencia adicionados na frente V1. A prioridade imediata e estabilidade funcional dos indices NP/FWI/KBDI; testes adicionais devem focar `ControlRuntimeController`, diagnostics vazios/preenchidos, projection status e smoke B/C, sem criar testes artificiais apenas para inflar coverage.

Medição histórica de `16/05/2026`, antes da frente V1 de diagnostics/API:

* `97.6%` de line coverage (`6677/6837`);
* `90.1%` de branch coverage (`1549/1719`);
* `97.1%` de method coverage (`930/957`);
* `92.9%` de full method coverage (`890/957`).

O relatório agregado cobre `7` assemblies, `116` classes e `89` ficheiros relevantes para a lógica aplicacional. O detalhe navegável fica em `coveragereport_core/index.html` e o resumo textual em `coveragereport_core/Summary.txt`.

Por assembly, nessa medição histórica:

* `NatureProtector.Backoffice.Api`: `99.0%`
* `NatureProtector.Core`: `99.2%`
* `NatureProtector.Infrastructure.Influx`: `95.0%`
* `NatureProtector.Prevention`: `98.1%`
* `NatureProtector.Prevention.Host`: `96.7%`
* `NatureProtector.Shared`: `90.7%`
* `NatureProtector.Simulator.Host`: `97.3%`

A melhoria mais expressiva desta iteração ocorreu pela expansão sistemática de testes de domínio, validação, policy, pipeline, API, Influx configurável e orquestração do simulador. A cobertura global passou de `91.3%` para `97.6%` em line coverage, de `80.9%` para `90.1%` em branch coverage e de `93.4%` para `97.1%` em method coverage.

Os componentes prioritários ficaram assim:

* `NatureProtector.Backoffice.Api.Controllers.ControlAreasController`: `100%`
* `NatureProtector.Backoffice.Api.Controllers.ControlConfigurationsController`: `100%`
* `NatureProtector.Backoffice.Api.Controllers.ControlSimulationRunsController`: `100%`
* `NatureProtector.Backoffice.Api.ControlPlane.Controllers.ControlPlaneControllerBase`: `100%`
* `NatureProtector.Core.Risk.RiskAssessment`: `100%`
* `NatureProtector.Core.Risk.RiskCell`: `100%`
* `NatureProtector.Core.Areas.GridCell`: `100%`
* `NatureProtector.Core.Sensors.SensorDeployment`: `100%`
* `NatureProtector.Prevention.Risk.ClassifierResult`: `100%`
* `NatureProtector.Prevention.Risk.RiskEligibilityResult`: `100%`
* `NatureProtector.Prevention.Risk.RiskInput`: `100%`
* `NatureProtector.Prevention.Host.Processing.DefaultProcessingFailureClassifier`: `100%`
* `NatureProtector.Prevention.Host.Projection.V1AlertPolicy`: `100%`
* `NatureProtector.Prevention.Host.Configuration.PreventionHostOptionsValidator`: `100%`
* `NatureProtector.Infrastructure.Influx.Services.SafeInfluxWriteService`: `100%`
* `NatureProtector.Simulator.Host.Configuration.SimulatorOptionsValidator`: `100%`
* `NatureProtector.Simulator.Host.Services.SimulationRunner`: `100%`

Os hotspots que ainda justificam trabalho adicional são sobretudo caminhos de integração externa, observabilidade e branches técnicos:

* `NatureProtector.Shared.Observability.PostgresBootstrapTelemetry`: `0%`
* `NatureProtector.Shared.Observability.NatureProtectorObservabilityExtensions`: `89.6%`
* `NatureProtector.Simulator.Host.Publishing.RabbitMqReadingPublisher`: `85.5%`
* `NatureProtector.Infrastructure.Influx.Services.InfluxWriteService`: `86.2%`
* `NatureProtector.Prevention.Host.Processing.ReadingEventProcessingService`: `89.8%`
* `NatureProtector.Prevention.Host.Projection.InMemoryAreaOperationalProjectionStore`: `88.7%`
* `NatureProtector.Prevention.Host.Projection.PostgresAreaOperationalProjectionStore`: `93.6%`
* `NatureProtector.Prevention.Risk.SimpleRiskScoringService`: `93.2%`

Estes valores não significam ausência de testes funcionais. Em vários casos, o que falta cobrir são branches de telemetry, `ActivitySource`, integração real com RabbitMQ/InfluxDB, fallback defensivo ou caminhos que exigiriam infraestrutura externa ou refactor específico para serem testados sem fragilidade.

## Nota sobre provider de teste

Os testes de persistência da `Prevention.Host` usam `SQLite` in-memory porque permitem validar comportamento relacional sem depender de serviços externos. Há, no entanto, uma limitação importante: `SQLite` não traduz `ORDER BY` sobre `DateTimeOffset` da mesma forma que `PostgreSQL`.

Por isso, o comportamento de ordenação crítica nos adaptadores PostgreSQL ficou protegido por testes e por uma abordagem segura no código que mantém a semântica em runtime sem esconder a diferença entre providers.

## Nota sobre InfluxDB em testes

Os testes unitários da pipeline não dependem de um servidor InfluxDB real: continuam a usar fakes ou `NoOpInfluxWriteService` para validar comportamento funcional sem infraestrutura externa.

A suite `DockerIntegration` acrescenta uma validação real e isolada de InfluxDB. Cada execução cria uma database temporária `np_it_*`, escreve nas medições `accepted_readings`, `risk_assessments` e `area_risk_snapshots`, valida tags/campos/timestamps, `event_id`, `simulation_run_id`, ausência de linhas extra na database temporária e remove a database no final.

O smoke de processos publicados (`Backoffice.Api` + `Prevention.Host` + `Simulator.Host`) desativa InfluxDB nesse teste com `InfluxDb__Enabled=false`. Esse smoke prova arranque, readiness, publicação RabbitMQ, consumo Prevention, PostgreSQL durável, consulta API, logs/exit code e ausência de processos órfãos; as asserções de InfluxDB pertencem aos testes Docker dedicados de Influx.

Nesta fase, a cobertura de `NatureProtector.Infrastructure.Influx`, da `Prevention.Host` e da suite Docker valida:

* modo `NoOp` quando `InfluxDb:Enabled=false`;
* tolerância a falhas quando `InfluxDb:FailPipelineOnWriteError=false`;
* comportamento estrito quando `InfluxDb:FailPipelineOnWriteError=true`;
* ativação ou desativação por measurement para `accepted_readings`, `risk_assessments` e `area_risk_snapshots`;
* escrita real em InfluxDB 3 Core com isolamento por database temporária.

Isto reforça a decisão arquitetural atual: `PostgreSQL` permanece o estado durável da pipeline e `InfluxDB` é observabilidade temporal configurável.

## Vagas recentes de testes V1

Durante a consolidação da V1 foram adicionadas várias vagas de testes focadas em comportamento útil, não apenas em percentagem de coverage.

A primeira vaga reforçou componentes de domínio e policy:

* validações de `Area`, `GridCell` e `SensorDeployment`;
* invariantes de `ClassifierResult` e `RiskEligibilityResult`;
* semântica de `Blocked`, `PartialButUsable` e `CompleteEligible`;
* `V1AlertPolicy`, incluindo thresholds, Warning, Alarm e histerese;
* `DefaultProcessingFailureClassifier`;
* `ExpectedUniqueViolationDetector`;
* `SimulatorOptionsValidator`.

A segunda vaga reforçou API, infraestrutura leve e orquestração:

* endpoints do Backoffice para áreas, grelha, configurações e simulation runs;
* respostas `503 ProblemDetails` quando o control plane está indisponível;
* `SafeInfluxWriteService` com fakes, sem servidor InfluxDB real;
* `PostgresSimulationContextSource` com SQLite/in-memory;
* `SimulationRunner`, incluindo falha de publisher e transições de run;
* `SimulationRun` e transições de ciclo de vida;
* `ExpectedUniqueConstraint`.

A vaga final reforçou branches ainda úteis:

* validação de `PreventionHostOptionsValidator`;
* overloads e limites de `RiskAssessment`;
* tendência em `RiskCell`;
* invariantes de `DailyCellState`;
* normalização/fallback em `RiskInput`;
* duplicados e retry no `InMemoryReadingEventInbox`;
* parsing isolado de `InfluxDbSettingsLoader`;
* paths indisponíveis dos controllers Backoffice.

A suite passou a proteger de forma mais explícita a cadeia V1 entre leitura operacional, normalização, elegibilidade, input de risco, assessment, alertas, projeções, API e orquestração de runs.

## Filosofia de coverage

* o foco principal do relatório consolidado é a lógica aplicacional e o comportamento observável;
* o relatório `backend-integral` inclui `Program.cs`, workers, hosted/background services, bootstrap PostgreSQL, `NatureProtector.Shared.Observability` e composition roots quando esses assemblies aparecem nos dados Cobertura;
* código gerado, `bin` e `obj` continuam excluídos;
* o relatório `backend-focused` existe para risco, classificadores, elegibilidade, mappings e contratos críticos, e não substitui a leitura integral;
* o objetivo não é perseguir `100%` artificial, mas cobrir caminhos críticos, regressões relevantes e decisões de domínio;
* line coverage e method coverage elevados são úteis, mas branch coverage é tratado com mais cautela, porque muitos branches restantes pertencem a telemetry, integração externa, fallbacks defensivos ou wrappers de bibliotecas;
* não se pretende cobrir branches de `ActivitySource`, `Meter`, exporters OpenTelemetry, RabbitMQ real ou InfluxDB real com testes frágeis apenas para subir percentagem;
* código de observabilidade sem decisão funcional pode ser considerado limite conhecido ou candidato futuro a exclusão explícita, desde que justificado;
* código de domínio, pipeline, scoring, alert policy, contratos, normalização, elegibilidade e persistência com lógica própria não deve ser excluído por conveniência.

## Relação com o roadmap

O roadmap em [../docs/planning/project-completion-roadmap.md](../docs/planning/project-completion-roadmap.md) continua a orientar as próximas vagas de testes.

As vagas recentes reduziram várias lacunas anteriores:

* semântica de `ClassifierResult`, quality flags e elegibilidade;
* separação entre `Blocked`, `PartialButUsable` e `CompleteEligible`;
* `RiskInput` como fronteira pré-scoring;
* `RiskAssessment` com `BaseRisk`, `AdjustedScore` e compatibilidade `RiskScore`;
* política interna de alertas V1 com `None`, `Warning`, `Alarm` e histerese;
* exposição de `alertState` e projeções pela `Backoffice.Api`;
* runtime do simulador com `RunOverrides` e orquestração por `run-spec.json`;
* recolha de evidência por run;
* modos configuráveis e tolerantes de escrita para InfluxDB;
* integrações reais e isoladas com RabbitMQ, PostgreSQL e InfluxDB em ambiente Docker local;
* recovery durável de redelivery duplicada após materialização no inbox e de leases `Processing` expirados;
* smoke local de processos publicados para API, Prevention e Simulator.

Continuam como trabalho futuro:

* testes integrados mais completos com cenários canónicos;
* expansão de cenários de restart/recovery real para processo Prevention, PostgreSQL e InfluxDB;
* testes da futura API/site para lançar e acompanhar runs;
* hardening de cancelamento, timeout e limpeza de runs;
* testes adicionais de projeções e processamento quando houver políticas finais de alertas/cooldown/persistência;
* validação externa e científica do modelo, que continua fora do âmbito dos testes unitários atuais.
