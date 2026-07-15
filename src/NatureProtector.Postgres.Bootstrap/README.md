# NatureProtector.Postgres.Bootstrap

Este projeto existe para materializar a primeira vaga do plano de controlo em `PostgreSQL`.

Ele não é um host de runtime da aplicação. É um utilitário de bootstrap pensado para:

- materializar dados iniciais no schema `control`;
- importar a área piloto `Proença-a-Nova`;
- importar `grid_cells` a partir da grelha e do `cells_attributes`;
- gerar perfis e a primeira rede de sensores piloto;
- importar os cenários `A/B/C`;
- indexar os artefactos preparados e ligá-los aos cenários.

## O que usa

- [../NatureProtector.Infrastructure.Postgres/README.md](../NatureProtector.Infrastructure.Postgres/README.md)
  - `DbContext`, migration inicial e importador
- [../../data/README.md](../../data/README.md)
- artefactos preparados da área piloto
- `/.env`
  - `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` e `POSTGRES_PORT`

## Como executar

Se o PostgreSQL local estiver ativo, podemos correr:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet run --project .\src\NatureProtector.Postgres.Bootstrap\NatureProtector.Postgres.Bootstrap.csproj
```

Ou usar o helper:

```powershell
.\scripts\postgres\bootstrap-control-plane.ps1
```

Por omissão, o bootstrap mantém compatibilidade local e garante o schema antes de importar dados. Em staging cloud, o schema é responsabilidade do job dedicado `np-postgres-migrations`; nesse caso o job `np-postgres-bootstrap` define:

```text
NP_BOOTSTRAP_SKIP_SCHEMA_MIGRATION=true
```

Com esse valor, o bootstrap não executa `EnsureSchemaAsync` nem `Database.MigrateAsync`; apenas materializa os dados iniciais usando o utilizador runtime `np_app`.

## O que produz

Nesta fase, o bootstrap semeia:

- `control.configuration_versions`
- `control.areas`
- `control.grid_cells`
- `control.sensor_profiles`
- `control.sensor_networks`
- `control.sensor_nodes`
- `control.scenario_definitions`
- `control.simulation_runs` fica preparado no schema, mas não é semeado
- `control.dataset_artifacts`
- `control.scenario_dataset_bindings`

## Limites atuais

- não fecha ainda `pipeline.event_inbox`;
- não fecha ainda `projection.area_operational_state` nem `projection.alert_state`;
- depende do endpoint resolvido por `POSTGRES_HOST`/`POSTGRES_PORT`, com precedência de variáveis de ambiente sobre o `.env`; na baseline local, o default documentado é `localhost:5433`.
