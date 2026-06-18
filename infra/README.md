# Infraestrutura Local

Esta pasta contém a baseline local de infraestrutura e os scripts operacionais usados para levantar, desligar e inspecionar o ambiente de desenvolvimento.

## Componentes da baseline atual

- RabbitMQ
  - broker de mensagens usado para o transporte de eventos
- PostgreSQL
  - plano de controlo, inbox durável, logs operacionais e projeções persistidas
- InfluxDB
  - armazenamento de telemetria, avaliações de risco e snapshots operacionais
- Grafana
  - ponto de observabilidade e dashboards

O Compose está definido em [../docker-compose.yml](../docker-compose.yml) e as variáveis base estão em [../.env.example](../.env.example).

## Scripts disponíveis

- [scripts/up.ps1](scripts/up.ps1)
  - sobe a baseline local; exige `.env` existente e nao cria nem altera esse ficheiro
- [scripts/down.ps1](scripts/down.ps1)
  - desce os serviços
- [scripts/logs.ps1](scripts/logs.ps1)
  - acompanha os logs do Compose
- [scripts/smoke-test.ps1](scripts/smoke-test.ps1)
  - mostra o estado dos contentores

## Arranque recomendado

Para levantar a baseline local, devemos executar:

```powershell
.\scripts\workspace.ps1 up
.\infra\scripts\smoke-test.ps1
```

Para observar os logs, devemos executar:

```powershell
.\infra\scripts\logs.ps1
```

Para desligar tudo, devemos executar:

```powershell
.\scripts\workspace.ps1 down
```

## O que já está preparado

- RabbitMQ sobe com a interface de gestão exposta.
- PostgreSQL sobe com um volume persistente montado.
- InfluxDB sobe e fica disponível para integração com o `Prevention.Host`.
- Grafana arranca com provisioning de datasource e dashboard base.

## O que ainda não está totalmente fechado

- [postgres/init/01-init.sql](postgres/init/01-init.sql) existe, mas está vazio. Isso já não é um bloqueio funcional: o schema real é criado por migrations e o seed do plano de controlo é feito pelo bootstrap [../scripts/postgres/bootstrap-control-plane.ps1](../scripts/postgres/bootstrap-control-plane.ps1).
- O dashboard atual em [grafana/dashboards/natureprotector-overview.json](grafana/dashboards/natureprotector-overview.json) é, neste momento, sobretudo um checklist de setup.
- O datasource de Grafana em [grafana/provisioning/datasources/influxdb.yml](grafana/provisioning/datasources/influxdb.yml) assume que o token de InfluxDB já existe em `.env`. Com a baseline local por omissão isso costuma ficar resolvido, mas continua a ser um ponto sensível se o `.env` for alterado manualmente.

## Relação com o código

- O [../src/NatureProtector.Simulator.Host/README.md](../src/NatureProtector.Simulator.Host/README.md) publica eventos para RabbitMQ.
- O [../src/NatureProtector.Prevention.Host/README.md](../src/NatureProtector.Prevention.Host/README.md) consome do RabbitMQ, escreve em InfluxDB e usa PostgreSQL para inbox e projeções duráveis.
- O [../src/NatureProtector.Backoffice.Api/README.md](../src/NatureProtector.Backoffice.Api/README.md) lê `control` e `projection` por HTTP.
- O [../src/NatureProtector.Postgres.Bootstrap/README.md](../src/NatureProtector.Postgres.Bootstrap/README.md) materializa o seed inicial em PostgreSQL.
- O detalhe consolidado desta camada está em [../docs/architecture/postgresql-architecture.md](../docs/architecture/postgresql-architecture.md).
