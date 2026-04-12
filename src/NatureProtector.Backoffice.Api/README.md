# NatureProtector.Backoffice.Api

Este projeto deixou de ser apenas um esqueleto ASP.NET Core e passou a expor a primeira superfície HTTP do plano de controlo.

Ainda não fecha autenticação nem comandos ricos de backoffice, mas já consegue servir configurações, áreas, sensores, cenários, `simulation_runs` e a primeira leitura do estado operacional por área e por célula a partir dos schemas `control` e `projection`.

## O que existe hoje

- `Program.cs`
  - arranque da API e ligação opcional ao `PostgreSQL`
- `Configuration/BackofficeApiOptions.cs`
- ativação da frente do plano de controlo via `BackofficeApi:ControlPlaneEnabled`
- `ControlPlane/Contracts/`
  - contratos HTTP desta fase
- `ControlPlane/Services/IControlPlaneService.cs`
  - fronteira interna de leitura e ativação
- `ControlPlane/Services/PostgresControlPlaneService.cs`
  - implementação real sobre `NatureProtectorControlDbContext`
- `Controllers/`
  - endpoints de configuração, áreas, estado operacional e runs
- `NatureProtector.Backoffice.Api.http`
  - exemplos para explorar a API manualmente

## Endpoints desta fase

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

## Como funciona

Quando `BackofficeApi:ControlPlaneEnabled = true`, a API:

- resolve a ligação ao `PostgreSQL` a partir do `.env`;
- regista `IDbContextFactory<NatureProtectorControlDbContext>`;
- usa `PostgresControlPlaneService` para consultar os schemas `control` e `projection`.

Quando `BackofficeApi:ControlPlaneEnabled = false`, a API continua a arrancar, mas devolve indisponibilidade controlada para estes endpoints.

## O que este módulo já fecha

- consulta da configuração ativa;
- listagem de áreas da configuração ativa ou de uma versão pedida;
- consulta da grelha, sensores e cenários por área;
- consulta do estado operacional por área;
- consulta do estado operacional por célula;
- consulta de alertas ativos simples por área;
- consulta das `simulation_runs`;
- ativação mínima de `configuration_versions`.

## O que ainda não fecha

- cenário ativo por área;
- alertas ricos com ciclo de vida completo;
- inbox, novas tentativas e estado durável do fluxo operacional por HTTP;
- autenticação e autorização de backoffice.
