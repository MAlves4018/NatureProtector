# Configuração da baseline local

Este documento é a primeira checklist assistida de configuração para executar a baseline local atual do NatureProtector. É intencionalmente conservador: documenta o que o repositório já suporta e evita alterar a lógica da pipeline, os contratos RabbitMQ, o cálculo de risco, Aspire/AppHost ou o build do frontend.

## Âmbito

O caminho de runtime local suportado é:

`Simulator.Host -> RabbitMQ -> Prevention.Host -> PostgreSQL/InfluxDB -> Backoffice.Api/Grafana`

A baseline estável continua a ser o backend .NET mais a infraestrutura local. A pasta `webUI` contém um candidato de frontend em Vite, mas ainda não está descrita no README da raiz como uma web UI final integrada.

## Pré-requisitos

Necessários para a baseline backend:

- .NET SDK 9.0
- Docker Desktop ou outro motor Docker
- Docker Compose v2, disponível como `docker compose`
- PowerShell, Windows PowerShell 5.1 ou PowerShell 7+

Recomendado:

- Git, para fluxos normais de trabalho no repositório

Necessários apenas para o candidato de frontend atual:

- Node.js
- npm

Apenas ferramentas de documentação/relatório:

- MiKTeX, ou outra distribuição LaTeX, é relevante para o relatório LaTeX em `docs/report`.
- Strawberry Perl é útil em alguns fluxos LaTeX/MiKTeX, especialmente ferramentas como `latexmk` ou auxiliares de glossário/índice, mas não é necessário para a baseline backend.

Para o relatório LaTeX, validar pelo menos:

```powershell
perl -v
pdflatex --version
latexmk -v
```

Se `latexmk` não estiver disponível, instalar pelo MiKTeX Package Manager ou executar:

```powershell
mpm --install=latexmk
```

Depois de instalar MiKTeX, Strawberry Perl ou pacotes novos, pode ser necessário fechar e reabrir o PowerShell para o `PATH` ser recarregado.

Assim, os scripts de configuração tratam Node/npm, Strawberry Perl e MiKTeX como avisos, exceto quando o frontend ou o fluxo de documentação for a tarefa a executar.

## Ficheiro de ambiente

Criar um `.env` local a partir do exemplo:

```powershell
Copy-Item .\.env.example .\.env
```

Rever as portas e credenciais locais antes de iniciar o Docker. O ficheiro Compose usa os valores de `.env`, incluindo:

* RabbitMQ AMQP: `RABBITMQ_AMQP_PORT`, exemplo por omissão `5672`
* Gestão RabbitMQ: `RABBITMQ_MANAGEMENT_PORT`, exemplo por omissão `15672`
* PostgreSQL: `POSTGRES_PORT`, exemplo por omissão `5432`
* InfluxDB: `INFLUXDB_PORT`, exemplo por omissão `8181`
* Grafana: `GRAFANA_PORT`, exemplo por omissão `3000`

Se uma porta já estiver ocupada, alterar o valor correspondente em `.env` antes de iniciar o Compose.

## Iniciar serviços Docker

O helper existente cria `.env` a partir de `.env.example` quando está em falta e depois inicia os serviços da baseline:

```powershell
.\infra\scripts\up.ps1
.\infra\scripts\smoke-test.ps1
```

A baseline Compose inicia atualmente:

* `np-rabbitmq`
* `np-postgres`
* `np-influxdb`
* `np-grafana`

## Bootstrap do control plane

Depois de o PostgreSQL estar a correr, semear o control plane:

```powershell
.\scripts\postgres\bootstrap-control-plane.ps1
```

Isto executa o projeto `NatureProtector.Postgres.Bootstrap` e materializa os dados iniciais de `control` necessários ao `Simulator.Host`, `Prevention.Host` e `Backoffice.Api`.

## Iniciar os hosts .NET

Em terminais separados a partir da raiz do repositório:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet run --project .\src\NatureProtector.Backoffice.Api
```

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet run --project .\src\NatureProtector.Prevention.Host
```

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet run --project .\src\NatureProtector.Simulator.Host
```

`Backoffice.Api` usa `http://localhost:5254` no seu perfil de arranque.

## Validar a baseline

Executar os scripts de validação só de leitura a partir da raiz do repositório:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup\Test-LocalPrerequisites.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup\Test-LocalBaseline.ps1
```

Se a política local de PowerShell permitir execução de scripts, também é possível usar:

```powershell
.\scripts\setup\Test-LocalPrerequisites.ps1
.\scripts\setup\Test-LocalBaseline.ps1
```

As verificações manuais também são úteis:

* Gestão RabbitMQ: abrir `http://localhost:15672`, ou a porta configurada em `.env`, e verificar a fila `np.ingestion.readings`.
* PostgreSQL: verificar que o bootstrap criou `control.*` e que o processamento em runtime cria/atualiza `pipeline.*` e `projection.*`.
* InfluxDB: verificar que `np-influxdb` está a correr se for necessário usar Grafana/telemetria temporal. Se `InfluxDb:Enabled=false`, a indisponibilidade de Influx não deve bloquear a pipeline backend principal.
* Grafana: abrir `http://localhost:3000`, ou a porta configurada em `.env`, e verificar o datasource/dashboard provisionado.
* Backoffice API: chamar `http://localhost:5254/api/control/configurations/active` e `http://localhost:5254/api/control/areas` depois de a API estar a correr.

## Modos InfluxDB

`src/NatureProtector.Prevention.Host/appsettings.json` tem atualmente:

```json
"InfluxDb": {
  "Enabled": false,
  "FailPipelineOnWriteError": false
}
```

Com `InfluxDb:Enabled=false`, a injeção de dependências resolve um writer Influx no-op. A pipeline de prevenção consegue processar através de RabbitMQ, PostgreSQL, projeções e Backoffice API sem escrever dados de séries temporais.

Com `InfluxDb:Enabled=true`, o host cria um writer Influx real. Nesse modo, o `.env` local tem de fornecer um `INFLUXDB_TOKEN` válido, organização e valores de bucket/base de dados. `FailPipelineOnWriteError=false` mantém falhas de escrita Influx como não críticas para a pipeline operacional; colocá-lo a `true` faz com que falhas de escrita Influx façam falhar a tentativa de processamento.

## Candidato de frontend

O repositório contém `webUI/package.json`, `package-lock.json`, `vite.config.ts` e source React em `webUI/src`.

O frontend deve ser tratado como candidato de UI e não como parte obrigatória da baseline backend. Para validar o estado atual:

```powershell
Set-Location .\webUI
npm install
npm run build
npm run dev
```

Se os scripts `build` ou `dev` não existirem no `package.json` de uma branch local, usar o fluxo Vite explícito:

```powershell
npx vite build
npx vite
```

Iniciar o Vite apenas depois de o build passar. O proxy Vite mapeia `/api` para `http://localhost:5254`, por isso o `Backoffice.Api` deve estar a correr para vistas apoiadas pela API.

Limitações ou pontos a validar no frontend:

* confirmar que `vite.config.ts` está compatível com o modo ESM;
* confirmar que os imports de estilos existem no caminho esperado;
* confirmar que os aliases usados no código estão definidos no `vite.config.ts` e/ou `tsconfig.json`;
* confirmar que `npm run build` passa antes de considerar a UI operacional.

Por estes pontos, o frontend deve ser tratado como ainda não totalmente configurado até o build provar o contrário.

## Resolução de problemas

Docker não está a correr:

* Iniciar o Docker Desktop ou o motor Docker local.
* Voltar a executar `docker info` e depois `.\infra\scripts\up.ps1`.

Portas ocupadas:

* Verificar os valores em `.env`.
* Alterar a porta em conflito antes de iniciar o Compose.
* Voltar a executar `docker compose up -d`.

`.env` está em falta:

* Executar `Copy-Item .\.env.example .\.env`, ou usar `.\infra\scripts\up.ps1`.
* Rever credenciais e portas depois de o ficheiro ser criado.

PowerShell bloqueia a execução de `.ps1`:

* Executar o script apenas para o processo atual com `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup\Test-LocalPrerequisites.ps1`.
* Fazer o mesmo para `Test-LocalBaseline.ps1`, se necessário.

Acesso ao `NuGet.Config` é negado:

* Fechar IDEs ou terminais que possam estar a bloquear ficheiros.
* Voltar a executar o comando a partir de uma shell normal de utilizador na raiz do repositório.
* Se o caminho do repositório estiver sincronizado por software de backup/cloud, pausar a sincronização durante o build.

`npm install` falha:

* Confirmar `node --version` e `npm --version`.
* Executar a partir de `webUI`, não da raiz do repositório.
* Se a falha for um problema de resolução de pacotes, inspecionar a diferença entre `package.json` e `package-lock.json` antes de alterar dependências.

`npx vite build` ou `npm run build` falha:

* Corrigir primeiro a configuração do frontend; não iniciar o Vite como workaround.
* Verificar ficheiros em falta, aliases em falta e configuração TypeScript em falta.
* Manter a configuração backend separada das correções frontend.

InfluxDB não tem token ou configuração:

* Confirmar `INFLUXDB_TOKEN`, `INFLUXDB_ORGANIZATION` e `INFLUXDB_BUCKET` em `.env`.
* Manter `InfluxDb:Enabled=false` para a baseline backend mais simples.
* Não gerar nem rodar tokens automaticamente nesta primeira camada de setup.
