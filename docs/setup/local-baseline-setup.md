# Configuracao da baseline local

Este documento descreve o setup local do NatureProtector para onboarding e
reprodutibilidade. O objetivo e deixar claro o que pertence a dependencias, o
que pertence a infraestrutura Docker, o que pertence ao runtime da aplicacao, e
como validar cada camada sem apagar dados locais.

## 1. Objetivo da baseline local

A baseline local suporta o fluxo:

```text
Simulator.Host -> RabbitMQ -> Prevention.Host -> PostgreSQL/InfluxDB -> Backoffice.Api/Grafana -> webUI
```

PostgreSQL e o estado operacional duravel. InfluxDB 3 e usado para
observabilidade temporal e dashboards. RabbitMQ transporta eventos. Grafana,
Backoffice.Api e webUI apoiam observabilidade, consulta e desenvolvimento.

## 2. Dependencias, infraestrutura e runtime

Dependencias sao ferramentas instaladas na maquina:

- PowerShell.
- Git.
- Docker CLI, Docker engine e Docker Compose v2.
- .NET SDK esperado pelo repositorio.
- Node.js e npm.

Infraestrutura e o conjunto Docker Compose:

- `np-postgres`.
- `np-rabbitmq`.
- `np-influxdb`.
- `np-grafana`.

Runtime e a aplicacao local:

- Backoffice.Api.
- Prevention.Host.
- webUI.
- Simulator.Host quando corrido em fluxos de cenario.

Esta separacao e intencional: `up.ps1` sobe infraestrutura, mas nao instala
dependencias e nao arranca API/webUI.

## 3. Pre-requisitos

Validar a maquina:

```powershell
.\scripts\setup\Test-LocalPrerequisites.ps1
```

O script e read-only. Ele imprime `[OK]`, `[WARN]` ou `[FAIL]` e devolve exit
code diferente de zero quando falta uma dependencia obrigatoria.

Valida:

- PowerShell e versao.
- Git.
- Docker CLI.
- Docker engine ativo.
- Docker Compose v2.
- .NET SDK compativel com o `TargetFramework` do repo.
- Node.js.
- npm.
- `.env.example`.
- `.env`, quando existe, e estado basico do `INFLUXDB_TOKEN`.
- portas de PostgreSQL, RabbitMQ, InfluxDB, Grafana, Backoffice.Api e webUI.

`.env` em falta e um aviso, nao uma falha de dependencia: o setup guiado e o
`up.ps1` podem cria-lo a partir de `.env.example`.

## 4. Instalar dependencias em falta

Ver sugestoes sem instalar:

```powershell
.\scripts\setup\Install-LocalPrerequisites.ps1 -WhatIf
```

O instalador e opt-in. Ele nao mexe em `.env`, nao sobe Docker Compose, nao
arranca runtime e nao apaga volumes.

Para instalar dependencias suportadas em falta com `winget`:

```powershell
.\scripts\setup\Install-LocalPrerequisites.ps1 -InstallMissing
```

Tambem existem flags individuais:

```powershell
.\scripts\setup\Install-LocalPrerequisites.ps1 -InstallGit
.\scripts\setup\Install-LocalPrerequisites.ps1 -InstallDotNet
.\scripts\setup\Install-LocalPrerequisites.ps1 -InstallNode
.\scripts\setup\Install-LocalPrerequisites.ps1 -InstallDocker
```

Sem `-Yes`, o script pede confirmacao antes de cada instalacao. Docker Desktop
pode exigir privilegios, login, restart e abertura manual depois da instalacao.
O script nao abre Docker Desktop automaticamente.

Depois de instalar ferramentas, abrir uma nova shell e repetir:

```powershell
.\scripts\setup\Test-LocalPrerequisites.ps1
```

## 5. `.env` e configuracao local

Criar `.env` manualmente:

```powershell
Copy-Item .\.env.example .\.env
```

Ou deixar `Setup-LocalEnvironment.ps1`/`up.ps1` criarem o ficheiro quando ele
nao existe. Depois rever os valores locais.

Variaveis principais:

```text
POSTGRES_PORT=5432
RABBITMQ_AMQP_PORT=5672
RABBITMQ_MANAGEMENT_PORT=15672
INFLUXDB_PORT=8181
INFLUXDB_DATABASE=np_telemetry
INFLUXDB_BUCKET=np_telemetry
GRAFANA_PORT=3000
BACKOFFICE_API_PORT=5254
WEBUI_PORT=5173
```

`.env.example` usa um placeholder para `INFLUXDB_TOKEN`. Antes de subir a
infraestrutura, substituir por um token local `apiv3_...`. O reposititorio nao
deve versionar tokens reais.

## 6. InfluxDB, token local e `np_telemetry`

O Docker Compose monta:

```text
data/runtime/influx/admin-token.json -> /run/secrets/influx-admin-token.json
```

Esse ficheiro e derivado de `.env`, e e ignorado pelo Git:

```text
/data/runtime/influx/admin-token.json
```

Scripts envolvidos:

- `scripts/influx/Ensure-InfluxAdminTokenFile.ps1` le `INFLUXDB_TOKEN` de `.env`
  e gera `data/runtime/influx/admin-token.json`.
- `scripts/influx/Ensure-InfluxDatabase.ps1` autentica no InfluxDB 3 e garante
  que `np_telemetry` existe.
- `infra/scripts/up.ps1` chama ambos na ordem correta.

`np-influxdb-init` so prepara permissoes do volume. Ele nao cria databases.

## 7. Setup guiado

Fluxo recomendado para alguem novo:

```powershell
.\scripts\setup\Test-LocalPrerequisites.ps1
.\scripts\setup\Install-LocalPrerequisites.ps1 -WhatIf
.\scripts\setup\Setup-LocalEnvironment.ps1 -StartRuntime -OpenBrowser
```

`Install-LocalPrerequisites.ps1` so instala com flags explicitas, como
`-InstallMissing` ou `-InstallNode`.

Sem flags, o setup guiado prepara infraestrutura e valida baseline:

```powershell
.\scripts\setup\Setup-LocalEnvironment.ps1
```

Executa:

```text
Test-LocalPrerequisites
copy .env.example -> .env, se faltar
up.ps1
Test-LocalBaseline -InfrastructureOnly
```

Com runtime:

```powershell
.\scripts\setup\Setup-LocalEnvironment.ps1 -StartRuntime -OpenBrowser
```

Executa tambem:

```text
start-local-runtime.ps1 -OpenBrowser -ForceRestart
Test-LocalBaseline -Full
```

O setup guiado nao chama `reset-local-infra.ps1`.

## 8. Arranque normal do dia a dia

Para maquina ja preparada:

```powershell
.\infra\scripts\up.ps1
.\scripts\dev\start-local-runtime.ps1 -OpenBrowser -ForceRestart
```

`up.ps1`:

- muda para a raiz do repo;
- cria `.env` se faltar;
- valida Docker CLI, engine e Compose v2 de forma minima;
- gera o token file local de InfluxDB;
- executa `docker compose up -d`;
- garante `np_telemetry`;
- nao instala dependencias;
- nao apaga volumes;
- nao arranca API/webUI.

## 9. Validacao da baseline

Validar infraestrutura:

```powershell
.\scripts\setup\Test-LocalBaseline.ps1 -InfrastructureOnly
```

Valida:

- Docker daemon.
- Containers `np-postgres`, `np-rabbitmq`, `np-influxdb`, `np-grafana`.
- RabbitMQ AMQP e Management.
- PostgreSQL e schema `control`.
- InfluxDB e database `np_telemetry`.
- Grafana.

Validar tudo:

```powershell
.\scripts\setup\Test-LocalBaseline.ps1 -Full
```

Valida tambem:

- Backoffice.Api.
- webUI.
- control plane com areas, celulas, sensores e cenarios.
- endpoints opcionais apenas quando expostos nesta versao.

## 10. Reset destrutivo

Reset destrutivo, apenas com confirmacao textual:

```powershell
.\infra\scripts\reset-local-infra.ps1 -Confirm RESET_LOCAL_INFRA
```

Este comando apaga volumes locais da baseline. Nao e o comando normal. Usar
apenas para reconstrucao total ou teste de reprodutibilidade.

`down.ps1` e diferente:

```powershell
.\infra\scripts\down.ps1
```

Ele para containers com `docker compose down` e preserva volumes.

## 11. Numero minimo de comandos

Maquina ja preparada:

```powershell
.\infra\scripts\up.ps1
.\scripts\dev\start-local-runtime.ps1 -OpenBrowser -ForceRestart
```

Primeira vez com validacao:

```powershell
.\scripts\setup\Test-LocalPrerequisites.ps1
.\scripts\setup\Setup-LocalEnvironment.ps1 -StartRuntime -OpenBrowser
```

## 12. Troubleshooting

PowerShell bloqueia `.ps1`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup\Test-LocalPrerequisites.ps1
```

Docker engine indisponivel:

- abrir Docker Desktop manualmente;
- esperar o engine ficar pronto;
- repetir `Test-LocalPrerequisites.ps1`.

`INFLUXDB_TOKEN` ainda e placeholder:

- editar `.env`;
- definir um token local que comece por `apiv3_`;
- repetir `.\infra\scripts\up.ps1`.

`np_telemetry` nao existe:

```powershell
.\scripts\influx\Ensure-InfluxDatabase.ps1
.\scripts\setup\Test-LocalBaseline.ps1 -InfrastructureOnly
```

Control plane vazio:

```powershell
.\scripts\postgres\bootstrap-control-plane.ps1
.\scripts\setup\Test-LocalBaseline.ps1 -Full
```

Porta ocupada:

- rever `Test-LocalPrerequisites.ps1`;
- alterar a porta correspondente em `.env`;
- repetir `up.ps1`.
