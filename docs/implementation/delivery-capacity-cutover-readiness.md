# M06 - Capacidade de entrega, simulação e readiness de cutover

> **Evidence histórica:** este documento regista uma passagem M06 de 2026-06-14. Os paths, métricas e resultados não devem ser usados como afirmação do snapshot atual sem nova execução.


Data: 2026-06-14

Este documento regista a passagem local de readiness técnica executada na M06. É um handoff de entrega para o protótipo atual do NatureProtector, não uma decisão de cutover de produção e não validação científica de previsão de incêndio.

## Âmbito

A M06 mediu e documentou:

- readiness local clone-to-run usando scripts existentes de setup/runtime;
- disponibilidade API/web local e probes de tempo de resposta;
- workloads curtos do simulador para perfis nominal, missing-readings e value-degradation;
- jornadas browser para estados logged-out e Admin local em Development;
- findings de dependências/segurança;
- comandos de validação para gates backend e frontend.

A M06 não:

- removeu ou substituiu a UI beta;
- fez deployment para produção nem executou cutover;
- contactou stakeholders nem recolheu consentimento;
- integrou P3 em scoring/runtime;
- alterou contratos RabbitMQ, nomes de eventos, projeções API, schema de base de dados, migrations, scoring, semântica de alertas, roles ou JWT claims;
- executou teste de carga/stress externo nem calibração científica.

## Pacote de evidence

A evidence principal da missão está em:

```text
NatureProtector.brain/control/M06-DELIVERY-CAPACITY-SIMULATION-AND-CUTOVER-READINESS/
```

A evidence acrescentada no repositório pela M06 está em:

```text
docs/evidence/m06-readiness/specs/
docs/evidence/runs/20260614-131245-scenario_b-m06-nominal-scenario-b/
docs/evidence/runs/20260614-131314-scenario_c-m06-missing-readings-scenario-c/
docs/evidence/runs/20260614-131340-scenario_b-m06-noise-scenario-b/
```

Observações locais principais:

| Área | Evidence | Resultado | Classificação |
| --- | --- | --- | --- |
| Pré-requisitos | `Test-LocalPrerequisites.ps1` | 0 falhas, 0 warnings | Medido localmente |
| Baseline de infraestrutura | `Test-LocalBaseline.ps1 -InfrastructureOnly` | PostgreSQL, RabbitMQ, InfluxDB, Grafana OK | Medido localmente |
| Baseline completa | `Test-LocalBaseline.ps1 -Full` | Um `401` conhecido num endpoint autenticado; webUI OK | Limitação local observada |
| Probes API/web | `run-local-readiness-workload.ps1` | 55/55 status HTTP esperados | Medido localmente |
| Simulações | três run specs M06 | 3 runs concluídas | Medido localmente |
| Browser | in-app browser | workspace logged-out e workspace/UI v2 Admin observados | Observado localmente |
| Testes backend | `dotnet test .\NatureProtector.sln --nologo -v minimal -m:1` | 1182/1182 passaram | Medido localmente |
| Testes frontend | `npm test` | 30/30 passaram | Medido localmente |
| Coverage frontend | `npm run test:coverage` | `app/ui-v2` line coverage 84.28%; webUI global 31.71% | Medido localmente |
| Build frontend | `npm run build` | passou | Medido localmente |
| Auditoria de dependências | `npm audit --audit-level=high --json` | 3 findings high na cadeia Vite/esbuild | Medido localmente |
| Auditoria NuGet | `dotnet list package --vulnerable --include-transitive` | A M06 mediu um advisory do exporter OpenTelemetry; a E2 de 2026-06-16 reporta zero pacotes NuGet vulneráveis | Medido localmente |

## Workload local de readiness

A M06 acrescentou:

```powershell
.\scripts\performance\run-local-readiness-workload.ps1
```

O script mede status HTTP local e tempo decorrido em probes API/web bounded. Escreve `manifest.json`, `probes.json`, `measurements.csv/json`, `summary.csv/json` e `summary.md`.

Exemplo de execução:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-local-readiness-workload.ps1 `
  -ApiBaseUrl http://127.0.0.1:5254 `
  -WebBaseUrl http://127.0.0.1:5173 `
  -Repetitions 5 `
  -TimeoutSeconds 15
```

A M06 mediu 55 tentativas com 55 status esperados. Valores p95 selecionados:

| Probe | Status | P95 ms | Classificação |
| --- | ---: | ---: | --- |
| API health | 200 | 44.83 | Medido localmente |
| Areas list | 200 | 15.87 | Medido localmente |
| Area detail | 200 | 33.53 | Medido localmente |
| Grid cells, `take=25` | 200 | 74.64 | Medido localmente |
| Sensor nodes | 200 | 27.72 | Medido localmente |
| Active alerts | 200 | 24.18 | Medido localmente |
| Scenario auth guard | 401 | 4.73 | Controlo de acesso observado |
| Operational-state auth guard | 401 | 2.67 | Controlo de acesso observado |
| Runtime summary auth guard | 401 | 2.60 | Controlo de acesso observado |
| web root | 200 | 3.56 | Medido localmente |
| `/ui-v2` | 200 | 4.80 | Medido localmente |

Estas são medições de uma workstation local. Não são teste de carga e não definem SLOs de produção.

## Evidence de simulação

A M06 executou três simulações curtas sem reset nem cleanup:

| Run label | Cenário | Perfil | Run id | Estado | Risk assessments | Notas |
| --- | --- | --- | --- | --- | ---: | --- |
| `m06-nominal-scenario-b` | `scenario_b` | `none` | `ceb20860-ed0a-4554-ac43-70d3a6596f70` | Completed | 18 | Run curta nominal |
| `m06-missing-readings-scenario-c` | `scenario_c` | `missing-readings` | `93a397e9-87b3-4730-9e58-a44554c70072` | Completed | 14 | Gap de observation coverage; não é pipeline failure |
| `m06-noise-scenario-b` | `scenario_b` | `noise` | `467ddba8-80f9-4874-9949-1ac5c376d94e` | Completed | 18 | Perfil value-degradation |

Os perfis resolvidos foram confirmados a partir de `control.simulation_runs.MetadataJson`. A run `missing-readings` produziu menos risk assessments do que a run nominal. Isto é um efeito de observation gap e não deve ser descrito como falha de processamento rejected/quarantined.

## Interpretação de capacidade

Medido:

- os probes API/web locais terminaram dentro dos valores p95 registados acima;
- três runs curtas do simulador terminaram com 6 sensores, 3 ciclos e intervalos de 1 segundo;
- gates automatizadas backend e frontend passaram na máquina local.

Estimado:

- a baseline local é adequada para uma demo técnica controlada com runs pequenas semelhantes às specs M06 ou aos perfis smoke existentes de 6 sensores;
- a máquina local consegue apresentar API/webUI e processar runs curtas do simulador para demonstração.

Sem instrumentação:

- profundidade da fila do broker como métrica API/UI;
- timestamp de publicação por evento;
- latência integral por evento desde a publicação do simulador até à projeção na UI;
- throughput sustentado, ponto de saturação, teto de concorrência e SLOs de produção.

Não validado:

- deployment de produção;
- utilizadores/stakeholders externos;
- calibração científica;
- uso por proteção civil ou alertas oficiais.

## Jornadas UI e perfis

Jornadas browser observadas:

- utilizador logged-out consegue abrir a app, selecionar `proenca-a-nova`, entrar no workspace e ver dados públicos com estados explícitos `Not available` para dados runtime protegidos;
- Development Admin consegue iniciar sessão localmente, abrir o workspace, ver a última run M06 e runtime summary, e abrir `/ui-v2`;
- `/ui-v2` mostra a fronteira académica/não operacional, pipeline técnica, QA, evidence, Admin e superfícies experimentais P3.

Não disponível:

- jornadas browser reais para identidades separadas Pipeline e Sim. A base de dados local continha apenas o utilizador Admin existente durante a M06. Os testes backend de autorização continuam a cobrir comportamento read/write por role, mas isso não é igual a uma matriz browser real multi-login.

## Readiness de dependências e segurança

Findings abertos:

- `npm audit --audit-level=high --json`: 3 findings high através de `@vitejs/plugin-react`, `vite` e `esbuild`. O caminho de fix npm disponível reporta mudanças semver-major; a M06 não aplicou `npm audit fix --force`.
- `dotnet list package --vulnerable --include-transitive`: zero pacotes NuGet vulneráveis na validação E2 de 2026-06-16. A M06 tinha registado antes um advisory moderate em `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.10.0`, antes da atualização do package.
- `.env` e `.env.example` contêm valores não vazios de development secrets. A M06 registou apenas classificações redigidas e não imprimiu valores.

Mínimo antes de qualquer partilha externa ou claim de entrega mais forte:

- rodar/remover dev secrets tracked com aparência real ou substituí-los por placeholders documentados;
- concluir uma passagem controlada de hardening de dependências;
- decidir se é necessária uma gate CI remota/service-container para infraestrutura.

## Readiness de cutover

Readiness local para demo técnica: go condicional.

Condições:

- usar apenas a baseline local Development;
- declarar explicitamente o estado académico/protótipo não operacional;
- usar runs M06 ou smoke conhecidas;
- evitar claims de produção/proteção civil;
- preservar a beta até existir uma decisão humana separada de cutover.

Readiness de produção ou cutover externo: no-go.

Razões:

- não existe validação de deployment de produção;
- findings de dependências continuam abertos;
- backlog de broker e latência integral por evento não estão instrumentados;
- identidades browser Pipeline/Sim não estavam disponíveis para validação real de jornada;
- consentimento/feedback de stakeholders não foi validado;
- calibração científica continua fora de âmbito.

## Rollback e preservação

A M06 não executou branch, commit, tag, reset, restore, clean, pull, push ou checkout Git.

Decisões de preservação de dados:

- nenhum volume foi apagado;
- nenhum reset de base de dados foi executado;
- a run smoke M04 foi preservada;
- a M06 acrescentou nova evidence de run e linhas de base de dados para as três simulações curtas.

Se for necessária uma demo limpa mais tarde, usar um caminho de reset/rebaseline explicitamente autorizado. Não apagar silenciosamente volumes ou runtime evidence.

## Suplemento pre-external verification readiness

Em 2026-06-14, uma passagem focada de pre-external-readiness fechou os blockers técnicos mínimos que bloqueavam diretamente a preparação de reprodução local independente. Este suplemento não reabre a M06 e não afirma que a verificação externa tenha sido concluída.

Mudanças validadas localmente:

- `Test-LocalBaseline.ps1 -Full` passou a usar `/health` público para readiness da Backoffice API e a tratar `GET /api/control/configurations/active` não autenticado com `401` como authentication guard esperado. O endpoint protegido continua protegido por `Sim`, `Pipeline` ou `Admin`.
- `scripts/setup/Ensure-LocalDemoIdentities.ps1` prepara identidades locais `Pipeline` e `Sim` através da Admin user-plane API existente e roles existentes. Exige passwords por parâmetros ou variáveis de ambiente e não grava secrets no repositório.
- Jornadas API diretas foram validadas: `Pipeline` consegue fazer login e ler runtime summary mas recebe `403` ao iniciar runtime; `Sim` consegue fazer login, ler cenários e iniciar uma run local mínima.
- A orientação de reset/rebaseline agora prefere dry-run autenticado de runtime reset e reset runtime confirmado explicitamente em vez de eliminação de volume Docker. Nenhum reset confirmado foi executado nesta passagem.
- Coverage foi regenerada. O agregado backend manteve `82%` line / `68.1%` branch. A UI v2 voltou a ficar acima do ratchet M06 com `84.45%` line coverage.

Efeitos laterais conhecidos:

- A validação da jornada `Sim` criou a run local mínima `53f5ab53-c39f-4274-9665-934140a98291`.
- Utilizadores locais `pipeline.local` e `sim.local` foram criados na base de dados PostgreSQL atual para preparação de reprodução.
- Logs app/runtime e validation evidence foram gerados localmente.

Fronteiras residuais:

- Isto continua a ser readiness técnica local, não cutover de produção, não validação de stakeholders e não calibração científica.
- Findings de dependências/segurança da M06 continuam abertos.
- A higiene de secrets em `.env`/`.env.example` continua a ser um blocker separado para partilha externa, salvo resolução explícita antes de publicação.
