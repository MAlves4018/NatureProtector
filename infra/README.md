# Infraestrutura Local

Esta pasta contém a baseline local de infraestrutura e os scripts operacionais usados para levantar, desligar e inspecionar o ambiente de desenvolvimento.

## Componentes da baseline atual

- RabbitMQ
  - broker de mensagens usado para o transporte de eventos
- PostgreSQL
  - reservado para o plano de controlo e persistência relacional futura
- InfluxDB
  - armazenamento de telemetria, avaliações de risco e snapshots operacionais
- Grafana
  - ponto de observabilidade e dashboards

O Compose está definido em [../docker-compose.yml](../docker-compose.yml) e as variáveis base estão em [../.env.example](../.env.example).

## Scripts disponíveis

- [scripts/up.ps1](scripts/up.ps1)
  - sobe a baseline local e cria `.env` a partir de `.env.example` quando necessário
- [scripts/down.ps1](scripts/down.ps1)
  - desce os serviços
- [scripts/logs.ps1](scripts/logs.ps1)
  - acompanha os logs do Compose
- [scripts/smoke-test.ps1](scripts/smoke-test.ps1)
  - mostra o estado dos contentores

## Arranque recomendado

Para levantar a baseline local, devemos executar:

```powershell
.\infra\scripts\up.ps1
.\infra\scripts\smoke-test.ps1
```

Para observar os logs, devemos executar:

```powershell
.\infra\scripts\logs.ps1
```

Para desligar tudo, devemos executar:

```powershell
.\infra\scripts\down.ps1
```

## O que já está preparado

- RabbitMQ sobe com a interface de gestão exposta.
- PostgreSQL sobe com um volume persistente montado.
- InfluxDB sobe e fica disponível para integração com o `Prevention.Host`.
- Grafana arranca com provisioning de datasource e dashboard base.

## O que ainda não está totalmente fechado

- [postgres/init/01-init.sql](postgres/init/01-init.sql) existe, mas está vazio. Isto significa que a presença de PostgreSQL na baseline ainda não corresponde a um plano de controlo operacional.
- O dashboard atual em [grafana/dashboards/natureprotector-overview.json](grafana/dashboards/natureprotector-overview.json) é, neste momento, sobretudo um checklist de setup.
- O datasource de Grafana em [grafana/provisioning/datasources/influxdb.yml](grafana/provisioning/datasources/influxdb.yml) assume que o token de InfluxDB já existe e foi configurado. Esse passo ainda exige atenção manual.

## Relação com o código

- O [../src/NatureProtector.Simulator.Host/README.md](../src/NatureProtector.Simulator.Host/README.md) publica eventos para RabbitMQ.
- O [../src/NatureProtector.Prevention.Host/README.md](../src/NatureProtector.Prevention.Host/README.md) consome do RabbitMQ e escreve em InfluxDB.
- PostgreSQL já está pronto do ponto de vista de baseline, mas ainda não é usado pelos projetos `.NET`.
