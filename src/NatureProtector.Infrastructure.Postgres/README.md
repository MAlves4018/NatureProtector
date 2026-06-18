# NatureProtector.Infrastructure.Postgres

Este projeto prepara a ligação entre o domínio e o `PostgreSQL`.

Nesta fase ele já não representa só intenção: fixa a base do plano de controlo, da inbox durável e da primeira superfície operacional persistida:

- entidades persistentes do schema `control`;
- entidades persistentes do schema `pipeline`;
- entidades persistentes do schema `projection`;
- `DbContext` para a primeira vaga de configuração;
- `DbContextFactory` para tooling e migrations;
- migrations incrementais dos schemas `control`, `pipeline` e `projection`;
- migrations incrementais dos logs operacionais duráveis e da projeção por célula;
- bootstrap de importação a partir dos datasets preparados;
- extensão de DI para expor `IDbContextFactory` aos hosts;
- convenções de nomes de schema e tabelas.

## Missão do módulo

- persistir configuração versionada;
- persistir áreas, contexto, grelha e deployments de sensores;
- persistir definições de cenário e metadados dos datasets usados;
- persistir o estado das `simulation_runs` do simulador;
- persistir o estado durável de entrada e tentativa do fluxo operacional;
- persistir novas tentativas agendadas e quarentena do fluxo operacional;
- persistir a primeira vaga das projeções operacionais.
- persistir logs duráveis de leituras aceites, assessments e snapshots.

## O que existe aqui hoje

- `Control/`
  - records persistentes do schema `control`
- `Pipeline/`
  - records persistentes do schema `pipeline`
- `Projection/`
  - records persistentes do schema `projection`, incluindo logs operacionais e projeções por célula
- `Bootstrap/`
  - importador de áreas, grid cells, perfis, rede de sensores, cenários e dataset artifacts
- `Configuration/`
  - resolução de ligação ao PostgreSQL a partir de variáveis de ambiente e `.env`
- `DependencyInjection/`
  - extensão para registar `IDbContextFactory<NatureProtectorControlDbContext>`
- `Schemas/`
  - nomes de schema usados pelo `DbContext`
- `Persistence/`
  - `NatureProtectorControlDbContext` e `NatureProtectorControlDbContextFactory`
- `Migrations/`
  - `InitialControlSchema`
  - `AddSimulationRunsToControlSchema`
  - `AddPipelineInboxSchema`
  - `AddPipelineRetriesAndQuarantine`
  - `AddProjectionSchema`
  - `AddProjectionDurableLogsAndCellState`

## O que ainda não está fechado

- replay assistido e operações de recuperação sobre o schema `pipeline`;
- histórico operacional rico para consultas agregadas mais avançadas;
- execução funcional do bootstrap contra uma instância PostgreSQL ativa.

## O que já ficou ligado

- o [../NatureProtector.Backoffice.Api/README.md](../NatureProtector.Backoffice.Api/README.md) já usa este módulo para expor os schemas `control` e `projection` por HTTP;
- a API já consegue consultar configurações, áreas, sensores, cenários, `simulation_runs`, estado operacional por área, estado operacional por célula e alertas ativos simples;
- o [../NatureProtector.Prevention.Host/README.md](../NatureProtector.Prevention.Host/README.md) já usa este módulo para inbox durável, retries, quarentena, persistência operacional e projeção operacional;
- a ativação mínima de `configuration_versions` já passa por esta persistência.

## Nota de desenho

O projeto referencia o `Core`, mas não arrasta o `Core` para detalhes de EF. A direção correta continua a ser:

- o domínio define conceitos;
- este projeto adapta esses conceitos à persistência.
