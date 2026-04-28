@page control_plane_and_bootstrap Plano de controlo e bootstrap

# Plano de controlo e bootstrap

## Objetivo da página

Explicar como a baseline do projeto passa de ficheiros e manifestos do repositório para estado relacional consultável e como esse estado é exposto pela API de backoffice.

## Âmbito

Esta página cobre o caminho implementado que começa em `NatureProtector.Postgres.Bootstrap`, grava dados nos schemas PostgreSQL e termina nos endpoints de leitura de `NatureProtector.Backoffice.Api`.

Não cobre a curadoria completa dos datasets em `scripts/data/`, nem descreve a API como ferramenta geral de administração. No estado atual, a API lê sobretudo dados já materializados e apenas confirma uma escrita operacional limitada: a ativação de uma versão de configuração.

## Componentes principais

O bootstrap é iniciado por `src/NatureProtector.Postgres.Bootstrap/Program.cs`. Esse programa resolve a raiz do repositório, carrega a configuração de ligação PostgreSQL a partir do ambiente ou de `.env`, cria @ref NatureProtector.Infrastructure.Postgres.Persistence.NatureProtectorControlDbContext e delega a importação para @ref NatureProtector.Infrastructure.Postgres.Bootstrap.ControlPlaneBootstrapper.

O modelo relacional fica concentrado em @ref NatureProtector.Infrastructure.Postgres.Persistence.NatureProtectorControlDbContext. A configuração partilhada de acesso PostgreSQL é registada pelos hosts através do método `AddNatureProtectorControlPlanePostgres`, definido em `src/NatureProtector.Infrastructure.Postgres/DependencyInjection/ServiceCollectionExtensions.cs`.

Na API, os controladores dependem de @ref NatureProtector.Backoffice.Api.ControlPlane.Services.IControlPlaneService. Quando `BackofficeApi:ControlPlaneEnabled=true`, a implementação ativa é @ref NatureProtector.Backoffice.Api.ControlPlane.Services.PostgresControlPlaneService. Quando a opção está desligada, @ref NatureProtector.Backoffice.Api.ControlPlane.Services.UnavailableControlPlaneService mantém os endpoints acessíveis com uma resposta explícita de indisponibilidade.

## Fluxo de bootstrap

\startuml
title Bootstrap do plano de controlo
autonumber
actor "Operador local" as Operator
participant "Postgres.Bootstrap Program" as Program
participant "ControlPlaneBootstrapper" as Bootstrapper
database "NatureProtectorControlDbContext" as Db
collections "data/ e manifestos" as RepoData

Operator -> Program : executar bootstrap
Program -> Program : resolver raiz e ligação PostgreSQL
Program -> Db : criar DbContext
Program -> Bootstrapper : BootstrapPilotAreaAsync()
Bootstrapper -> Db : garantir schema e migrations aplicáveis
Bootstrapper -> RepoData : ler baseline, manifestos e cenários
Bootstrapper -> Db : upsert configuration version
Bootstrapper -> Db : upsert datasets, área, grelha e sensores
Bootstrapper -> Db : upsert cenários e bindings
Bootstrapper --> Program : ControlPlaneBootstrapSummary
\enduml

O resultado esperado é uma configuração ativa, uma área piloto, células de grelha, perfis e nós de sensor, cenários, artefactos de dataset e bindings entre cenários e datasets. A tabela `control.simulation_runs` fica preparada, mas é escrita pelo simulador quando há runs.

## Fluxo de leitura pela API

`src/NatureProtector.Backoffice.Api/Program.cs` compõe a API de forma condicional. Com o plano de controlo ligado, os endpoints leem PostgreSQL através de @ref NatureProtector.Backoffice.Api.ControlPlane.Services.PostgresControlPlaneService. Com o plano de controlo desligado, os mesmos controladores continuam registados, mas devolvem respostas de indisponibilidade.

Os controladores atuais expõem:

- configurações e versão ativa;
- áreas, contexto, grelha, sensores e cenários;
- runs de simulação persistidas;
- estado operacional por área;
- estado operacional por célula;
- alertas ativos simples.

## Decisões importantes

- O bootstrap é um utilitário de inicialização, não um serviço de negócio contínuo.
- O plano de controlo em PostgreSQL é a fonte usada pelo simulador em modo `ControlPlaneEnabled=true` e pela API quando `BackofficeApi:ControlPlaneEnabled=true`.
- A API não materializa datasets, áreas, sensores ou cenários; lê o que o bootstrap já gravou.
- A ativação de configuração é uma exceção controlada ao perfil de leitura da API.

## Estado atual e limitações

O estado implementado suporta uma baseline local com dados de `Proença-a-Nova`, cenários e sensores materializados em PostgreSQL. O comportamento suportado inclui leitura por API e seleção da configuração ativa.

As limitações conhecidas são claras: não há CRUD completo de plano de controlo, não há comandos de manutenção da pipeline pela API, não há replay manual de quarentena e a existência de estado operacional depende de o simulador e a prevenção já terem corrido.

## Pontos do repositório a consultar

- `src/NatureProtector.Postgres.Bootstrap/Program.cs`
- `src/NatureProtector.Infrastructure.Postgres/Bootstrap/ControlPlaneBootstrapper.cs`
- `src/NatureProtector.Infrastructure.Postgres/Persistence/NatureProtectorControlDbContext.cs`
- `src/NatureProtector.Infrastructure.Postgres/DependencyInjection/ServiceCollectionExtensions.cs`
- `src/NatureProtector.Backoffice.Api/Program.cs`
- `src/NatureProtector.Backoffice.Api/Controllers/`
- `src/NatureProtector.Backoffice.Api/ControlPlane/Services/PostgresControlPlaneService.cs`

## Ligações para páginas relacionadas

- Para o modelo relacional completo, consultar @ref persistence_model.
- Para a utilização do plano de controlo pelo simulador, consultar @ref simulator_flow.
- Para testes que demonstram endpoints e queries, consultar @ref tests_as_documentation.
