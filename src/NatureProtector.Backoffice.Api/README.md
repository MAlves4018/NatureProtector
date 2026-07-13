# NatureProtector.Backoffice.Api

Este projeto deixou de ser apenas um esqueleto ASP.NET Core e passou a expor a primeira superfície HTTP do plano de controlo.

Já inclui autenticação JWT e autorização por roles nos endpoints atuais. A suite de API cobre token válido, token expirado, assinatura inválida, issuer/audience inválidos, roles e utilizadores distintos. A API consegue servir configurações, áreas, sensores, cenários, `simulation_runs` e a primeira leitura do estado operacional por área e por célula a partir dos schemas `control` e `projection`.

## O que existe hoje

- `Program.cs`
  - arranque da API e ligação opcional ao `PostgreSQL`
- `Configuration/BackofficeApiOptions.cs`
- ativação da frente do plano de controlo via `BackofficeApi:ControlPlaneEnabled`
- `OpenApi/`
  - transformers para declarar JWT bearer e security por operação no OpenAPI runtime
- `ControlPlane/Contracts/`
  - contratos HTTP desta fase
- `ControlPlane/Services/IControlPlaneService.cs`
  - fronteira interna de leitura e ativação
- `ControlPlane/Services/PostgresControlPlaneService*.cs`
  - implementação real sobre `NatureProtectorControlDbContext`, decomposta por catálogo, timings, resumo, diagnósticos, operações e helpers partilhados;
  - a interface pública continua concentrada em `IControlPlaneService`.
- `Controllers/`
  - endpoints de configuração, áreas, estado operacional e runs
- `NatureProtector.Backoffice.Api.http`
  - exemplos para explorar a API manualmente

## Endpoints desta fase

### Health tecnico

- `GET /health`
- `GET /health/live`
- `GET /health/ready`

Estes endpoints usam ASP.NET health checks e não substituem o health operacional detalhado em `GET /api/control/runtime/observability/health`.

Semântica atual:

- `/health/live` prova apenas que o processo HTTP está vivo; não consulta dependências externas;
- `/health/ready` exige PostgreSQL quando `BackofficeApi:ControlPlaneEnabled=true`;
- `/health` agrega os checks registados e, com o control plane ativo, também devolve indisponibilidade quando PostgreSQL não responde;
- quando `BackofficeApi:ControlPlaneEnabled=false`, não existe dependência PostgreSQL obrigatória e os três endpoints podem responder `200`.

### Configuração

- `GET /api/control/configurations`
- `GET /api/control/configurations/active`
- `POST /api/control/configurations/{versionNumber}/activate`

### Áreas e topologia

- `GET /api/control/areas`
- `GET /api/control/areas/{areaCode}`
- `GET /api/control/areas/{areaCode}/grid-cells`
- `GET /api/control/areas/{areaCode}/sensor-nodes`
- `GET /api/control/areas/{areaCode}/scenarios`
- `GET /api/control/areas/{areaCode}/operational-state`
- `GET /api/control/areas/{areaCode}/cells/operational-state`
- `GET /api/control/areas/{areaCode}/alerts/active`

### Execução

- `GET /api/control/simulation-runs`
- `GET /api/control/simulation-runs/{runId}`

### Runtime Monitor

- `GET /api/control/runtime/summary`
  - query params: `areaCode` opcional, `recentMinutes` com janela recente por defeito de 30 minutos;
  - agrega `simulation_runs`, inbox, attempts, rejected/quarantine, risk assessments recentes, estado operacional, alertas ativos e limitacoes de observabilidade;
  - endpoint read-only: nao recalcula risco nem alertas.
- `GET /api/control/runtime/observability/health`
- `GET /api/control/runtime/observability/rabbitmq`
- `GET /api/control/runtime/observability/evidence`
- `GET /api/control/runtime/observability/evidence/{evidenceId}`
  - `evidenceId` e um identificador gerado pelo catalogo, nao um caminho de filesystem.

### Developer Runtime Control

- `GET /api/control/runtime/diagnostics`
- `POST /api/control/runtime/diagnostics/{diagnosticId}`
  - executa apenas diagnosticos fixos e parametrizados; nao aceita SQL livre vindo do frontend.
  - inclui diagnosticos para detalhes de `scenario_definitions` e comparacao da ultima run `scenario_b` vs `scenario_c`.
- `POST /api/control/runtime/runs`
  - Development-only; inicia `Simulator.Host` com `RunOverrides`; bloqueia runs paralelas por defeito.
  - com `collectEvidence=true`, escreve evidencia local em `docs/evidence/dev-runtime/...` com request/response, summaries, diagnosticos e relatorios markdown.
- `GET /api/control/runtime/runs/latest`
- `GET /api/control/runtime/runs/{runId}`
- `POST /api/control/runtime/reset`
  - Development-only; limpa apenas estado runtime, exige confirmacao `RESET_RUNTIME_STATE`, suporta dry run e bloqueia se houver run ativa.

## Como funciona

Quando `BackofficeApi:ControlPlaneEnabled = true`, a API:

- resolve a ligação ao `PostgreSQL` a partir do `.env`;
- regista `IDbContextFactory<NatureProtectorControlDbContext>`;
- usa `PostgresControlPlaneService` para consultar os schemas `control` e `projection`;
- quando existe password de bootstrap (`NP_BOOTSTRAP_ADMIN_PASSWORD`, ou `admin123` em Development), garante que o role fixo `Admin` existe antes de atribuir esse role ao utilizador local de desenvolvimento.

Quando `BackofficeApi:ControlPlaneEnabled = false`, a API continua a arrancar, mas devolve indisponibilidade controlada para estes endpoints.

## O que este módulo já fecha

- consulta da configuração ativa;
- listagem de áreas da configuração ativa ou de uma versão pedida;
- consulta da grelha, sensores e cenários por área;
- consulta do estado operacional por área;
- consulta do estado operacional por célula;
- consulta de alertas ativos simples por área;
- consulta das `simulation_runs`;
- resumo agregado read-only para a vista tecnica Runtime Monitor;
- observabilidade interna de runtime, incluindo health operacional, RabbitMQ e evidence HTTP allowlisted;
- contrato OpenAPI runtime com security JWT e schemas das respostas runtime/observability consumidas pela UI;
- ativação mínima de `configuration_versions`.

## O que ainda não fecha

- cenário ativo por área;
- alertas ricos com ciclo de vida completo;
- inbox, novas tentativas e estado durável do fluxo operacional por HTTP;
- refresh token, sessão persistente por cookie e gestão produtiva de rotação/revogação de tokens.
