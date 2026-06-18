# Controlo de runtime para desenvolvimento

A consola local de runtime está disponível em:

```text
/dev/runtime
```

É uma superfície local de desenvolvimento para:

- diagnósticos fixos e apenas de leitura;
- arranque de runs do `Simulator.Host` através de endpoints API disponíveis apenas em Development;
- reset de estado runtime em modo dry-run ou confirmado;
- visibilidade de freshness/carry-forward baseada em projeções persistidas.

O frontend nunca envia SQL livre. Risco e alertas são lidos a partir de estado persistido e não são recalculados no browser.

## Superfícies técnicas da UI v2

A M05 acrescenta superfícies técnicas em:

```text
/ui-v2
```

Essas superfícies reutilizam contratos runtime existentes para Pipeline/Observability, QA, Evidence, contexto Admin proporcional, contexto experimental P3 e readiness de staging/demo. Não substituem `/dev/runtime` e não expõem reset de runtime, execução de diagnósticos runtime ou execução P3 como controlos.

Instrumentação runtime ausente é apresentada explicitamente. Em particular, a UI v2 não infere saúde do broker pela ausência de erros e não inventa backlog RabbitMQ, timestamps de publicação ou latência integral por evento.

## Launcher local

Usar um comando para iniciar os serviços locais de runtime:

```powershell
.\scripts\dev\start-local-runtime.ps1 -OpenBrowser
```

Opções úteis:

```powershell
.\scripts\dev\start-local-runtime.ps1 -SkipBootstrap -OpenBrowser
.\scripts\dev\start-local-runtime.ps1 -SkipDocker -NoBrowser
.\scripts\dev\start-local-runtime.ps1 -SkipBootstrap -ForceRestart -OpenBrowser
```

Os logs são escritos em:

```text
docs/evidence/dev-runtime/<timestamp>/
```

O launcher compila `Backoffice.Api` e `Prevention.Host` sequencialmente em Release antes de os iniciar com `dotnet run -c Release --no-build --no-restore`. Isto torna o arranque mais determinístico e evita escritas concorrentes nos outputs `obj/` partilhados.

## Segurança operacional

O reset de runtime existe apenas em Development, bloqueia runs ativas e exige confirmação exata:

```text
RESET_RUNTIME_STATE
```

Limpa apenas tabelas runtime em `control`, `pipeline` e `projection`. Não limpa áreas, sensores, cenários, versões de configuração, datasets, user roles ou volumes Docker.

Antes de usar um reset confirmado para uma demo limpa:

1. inspecionar runs atuais e contagens runtime;
2. executar dry-run reset através de uma identidade autenticada `Sim` ou `Admin`;
3. executar o reset confirmado apenas quando o estado runtime limpo tiver sido escolhido explicitamente;
4. criar uma run curta de rebaseline com `runLabel` claro;
5. validar o `run id` escolhido através dos endpoints de summary, audit e timings.

Não usar eliminação de volumes Docker como caminho normal de rebaseline.

## Evidence por run

Runs iniciadas em `/dev/runtime` com `collectEvidence=true` escrevem um pacote de evidence em:

```text
docs/evidence/dev-runtime/<yyyyMMdd-HHmmss>-<runLabel>/
```

O pacote inclui request/response JSON, runtime summaries antes/depois, outputs de diagnósticos fixos, logs stdout/stderr do simulador quando capturados, `summary.md` e `post-run-report.md`. Os diagnósticos são apenas de leitura e usam dados runtime persistidos; não recalculam risco nem alertas.

## Diagnósticos de cenário

A consola inclui:

- `Scenario definition details`, para inspecionar parâmetros de `control.scenario_definitions` e opções do simulador.
- `Compare latest B vs C`, para comparar as últimas runs persistidas `scenario_b` e `scenario_c` da área selecionada.

`scenario_c` destina-se a comparação degradada ou operacional. Executá-lo com `degradationProfile=none` é permitido, mas aparece com aviso porque pode comportar-se como um cenário limpo. O perfil técnico de degradação atual é `missing-readings`, que omite deterministamente uma parte das leituras publicadas sem alterar scoring, política de alertas, topologia RabbitMQ ou contratos de eventos.

## Workload local de readiness

A M06 acrescenta um workload HTTP local pequeno:

```powershell
.\scripts\performance\run-local-readiness-workload.ps1
```

Mede status codes e tempo decorrido de probes API/web bounded, depois escreve `manifest.json`, `probes.json`, `measurements.csv/json`, `summary.csv/json` e `summary.md`.

O script não executa teste de carga, teste de stress, teste de profundidade de broker ou teste de latência integral por evento. Tratar os timings HTTP como evidence local medida. Backlog do broker, timestamps de publicação e latência integral por evento continuam sem instrumentação até existir uma alteração específica de observabilidade runtime.

## Workload de capacidade sistémica

O Bloco I acrescenta um workload sistémico bounded:

```powershell
.\scripts\performance\run-system-capacity-workload.ps1 -Profile Calibration -UseDevelopmentAdminDefault
.\scripts\performance\run-system-capacity-workload.ps1 -Profile B0 -UseDevelopmentAdminDefault -CalibrationRunDirectory <calibration-run-directory>
```

Os perfis `B0`, `B1` e `B2` exigem um artifact anterior de `Calibration`. O script inicia runs através da API runtime existente e observa o caminho API -> Simulator -> RabbitMQ -> Prevention -> PostgreSQL/InfluxDB usando endpoints persistidos de audit/timings e métricas da RabbitMQ Management API. Escreve `environment.json`, `workload.json`, `measurements.csv/json`, `run-failures.json`, `summary.md/json`, `logs/`, `traces/`, `metrics/` e `runs/` em `artifacts/performance/system-*/`.

O workload reporta apenas uma baseline local reprodutível de capacidade. Não prova readiness de produção, capacidade de stress, tolerância a carga externa nem calibração científica. A latência integral publish-to-UI continua sem claim porque o envelope RabbitMQ atual não persiste timestamp de publicação.
