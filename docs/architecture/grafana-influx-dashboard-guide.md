# Grafana, InfluxDB e Construção de Dashboards

## Missão deste documento

Este guia existe para explicar, de forma prática e consistente com o estado real do repositório, como devemos:

- ligar o `Grafana` ao `InfluxDB`;
- validar que a datasource está funcional;
- descobrir a estrutura das tabelas;
- escolher colunas, métricas e dimensões;
- escrever queries úteis para painéis;
- pensar dashboards que façam sentido para o NatureProtector.

O objetivo não é apenas "fazer um gráfico aparecer". O objetivo é construir uma frente de observabilidade que seja previsível, explicável e fácil de evoluir.

## O caminho suportado hoje

Neste repositório, o caminho de leitura que está alinhado com a baseline atual é:

```text
Grafana -> Infinity datasource -> /api/v3/query_sql -> InfluxDB 3 Core
```

Hoje, a datasource de referência é:

- `NatureProtectorInfinityJson`

Ela está provisionada em:

- [`../../infra/grafana/provisioning/datasources/influxdb.yml`](../../infra/grafana/provisioning/datasources/influxdb.yml)

### Porque usamos esta datasource

Durante a integração, verificámos duas coisas importantes:

1. `Grafana`, `InfluxDB`, token e rede entre contentores estavam corretos.
2. O problema vinha da combinação entre `Infinity` e certos cabeçalhos `Accept` por omissão.

Por isso, o caminho suportado no repositório passou a ser uma datasource `Infinity` em `JSON`, com `Accept: application/json`, usando `proxy` e URL base `http://influxdb:8181`.

## Pré-condições antes de abrir o Grafana

Antes de criar ou testar painéis, devemos garantir que a stack está de pé e que existe realmente dados para observar.

### 1. Levantar a baseline local

```powershell
.\infra\scripts\up.ps1
.\infra\scripts\smoke-test.ps1
```

Resultado esperado:

- `np-grafana` ativo em `http://localhost:3000`
- `np-influxdb` ativo em `http://localhost:8181`

### 2. Bootstrap do plano de controlo

```powershell
.\scripts\postgres\bootstrap-control-plane.ps1
```

Resultado esperado:

- área `proenca-a-nova` carregada;
- sensores, cenários e artefactos indexados em `PostgreSQL`.

### 3. Arrancar os hosts que produzem dados

Em terminais separados:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet run --project .\src\NatureProtector.Prevention.Host
```

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet run --project .\src\NatureProtector.Simulator.Host
```

Resultado esperado:

- o simulador publica leituras;
- o `Prevention.Host` consome, avalia risco e escreve em `InfluxDB`.

Sem estes dois hosts, o Grafana pode estar correto e mesmo assim mostrar painéis vazios.

## Como validar a datasource

### Pela interface do Grafana

Credenciais por omissão:

- `admin / admin`

Abrir:

- `http://localhost:3000`

Depois, num painel novo, usar a datasource `NatureProtectorInfinityJson` e testar primeiro esta query:

- URL: `/api/v3/query_sql`
- Query params:
  - `db = np_telemetry`
  - `format = json`
  - `q = SHOW TABLES`

Resultado esperado:

- uma tabela com `accepted_readings`, `risk_assessments` e `area_risk_snapshots`.

### Pela API do Grafana

Se quisermos confirmar que a datasource provisionada existe mesmo:

```powershell
$pair = 'admin:admin'
$b64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($pair))
Invoke-RestMethod -Uri 'http://localhost:3000/api/datasources/uid/natureprotector-infinity-json' `
  -Headers @{ Authorization = "Basic $b64" } | ConvertTo-Json -Depth 6
```

Resultado esperado:

- `uid = natureprotector-infinity-json`
- `url = http://influxdb:8181`
- `httpHeaderName1 = Accept`
- `bearerToken` presente como `secureJsonField`

## Configuração correta de um painel

Ao criar um painel com esta datasource, o preenchimento base deve ser este:

- `Data source`: `NatureProtectorInfinityJson`
- `Type`: `JSON`
- `Parser`: `JSONata` ou `JQ`
- `Source`: `URL`
- `Format`: `Table`
- `Method`: `GET`
- `URL`: `/api/v3/query_sql`

Em `URL Query Params`, devemos acrescentar:

- `db`
- `format`
- `q`

Exemplo:

- `db = np_telemetry`
- `format = json`
- `q = SHOW TABLES`

### Regra importante

Não devemos pôr `db`, `format` e `q` ao mesmo tempo:

- na `URL` completa;
- e também em `URL Query Params`.

Se fizermos isso, o `InfluxDB` recebe parâmetros duplicados e devolve erros como:

- `duplicate field db`

### Outra regra importante

Não devemos usar URL absoluta como:

```text
http://localhost:8181/api/v3/query_sql
```

no campo do painel, porque a datasource já tem URL base configurada. Se misturarmos as duas coisas, o `Infinity` pode concatenar URLs de forma errada.

## Como configurar as colunas no Infinity

Depois de a query correr, o passo seguinte não é logo escolher o gráfico. No `Infinity`, primeiro devemos dizer ao painel o que cada coluna representa.

Isto faz-se em:

- `Parsing options & Result fields`
- `Columns - optional`

É aqui que o Grafana percebe:

- qual é o campo temporal;
- qual é o valor numérico;
- qual é a coluna categórica.

### Regra prática para preencher `Columns`

Quando a query devolve uma série temporal típica, devemos criar três entradas:

1. coluna temporal
   - `selector = time`
   - `as = Time`
   - `format as = Time`
2. coluna numérica
   - `selector = value` ou outro campo numérico
   - `as = Value`
   - `format as = Number`
3. coluna categórica
   - `selector = sensor_name`, `metric_type`, `risk_level` ou equivalente
   - `as = nome legível`
   - `format as = String`

### O erro mais comum nesta fase

Se a query tiver um campo `time`, mas essa coluna não estiver mapeada como `Time`, o Grafana pode mostrar:

- `Data is missing a time field`

Quando isso acontecer, a primeira coisa a verificar não é a SQL. É a secção `Columns`.

### Ordem recomendada dentro do painel

1. correr a query;
2. abrir `Table view` para confirmar os dados brutos;
3. mapear as colunas em `Columns - optional`;
4. só depois mudar a visualização para `Time series`, `Bar chart`, `Pie chart`, `Stat` ou `Gauge`.

## Processo recomendado para desenhar um dashboard

O processo correto não começa pelo gráfico. Começa pelo dado.

### Passo 1. Descobrir as tabelas

Query:

```sql
SHOW TABLES
```

No estado atual do projeto, as tabelas operacionais mais relevantes são:

- `accepted_readings`
- `risk_assessments`
- `area_risk_snapshots`

### Passo 2. Descobrir as colunas

Podemos inspecionar colunas de forma explícita:

```sql
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = 'accepted_readings'
ORDER BY ordinal_position
```

Ou inspecionar dados reais:

```sql
SELECT *
FROM accepted_readings
ORDER BY time DESC
LIMIT 20
```

### Passo 3. Separar os campos em três grupos

Em quase todos os painéis, devemos pensar nestas três famílias:

1. Campo temporal
   - normalmente `time`
2. Campo numérico
   - o valor que queremos medir
3. Campo categórico
   - o que separa ou agrupa séries

### Passo 4. Só depois escrever a query do gráfico

Evitar `SELECT *` em dashboards finais.

Em vez disso:

- escolher apenas as colunas necessárias;
- agregar se o painel for de barra, pie, stat ou gauge;
- manter o resultado o mais próximo possível do formato que o painel precisa.

## O que existe hoje em cada tabela

### 1. `accepted_readings`

Esta tabela guarda telemetria aceite pela pipeline.

Colunas observadas hoje:

- `time`
- `area_id`
- `sensor_id`
- `sensor_name`
- `metric_type`
- `unit`
- `operational_state`
- `latitude`
- `longitude`
- `value`

Como devemos ler esta tabela:

- `time` é o eixo temporal;
- `value` é a métrica principal;
- `sensor_name`, `sensor_id` e `metric_type` são dimensões úteis para separar séries;
- `unit` ajuda a não misturar grandezas diferentes no mesmo gráfico.

### 2. `risk_assessments`

Esta tabela guarda avaliações de risco individuais.

Colunas observadas hoje:

- `time`
- `area_id`
- `sensor_id`
- `risk_level`
- `risk_score`
- `has_explanation`

Como devemos ler esta tabela:

- `risk_score` é a métrica contínua;
- `risk_level` é a classificação categórica;
- `sensor_id` permite separar avaliações por sensor.

### 3. `area_risk_snapshots`

Esta tabela guarda o estado agregado de risco da área.

Colunas observadas hoje:

- `time`
- `area_id`
- `aggregate_risk_level`
- `aggregate_risk_score`
- `assessment_count`
- `severity`

Como devemos ler esta tabela:

- `aggregate_risk_score` é o melhor candidato a série temporal agregada;
- `severity` e `aggregate_risk_level` são boas dimensões para stat, table e state timeline;
- `assessment_count` indica quantas avaliações contribuíram para o snapshot.

## Exemplos práticos: query e configuração no mesmo sítio

Nesta secção, cada exemplo vem completo:

1. query;
2. configuração das colunas;
3. tipo de visualização;
4. leitura do resultado.

Assim fica mais fácil repetir o processo no painel sem saltar entre secções.

## `accepted_readings`

### Exemplo 1. Descoberta rápida da tabela

Query:

```sql
SELECT *
FROM accepted_readings
ORDER BY time DESC
LIMIT 20
```

Configuração das colunas:

- não é obrigatório mapear nada no início;
- usar primeiro `Table view`;
- se quisermos organizar melhor a tabela:
  - `time -> as Time -> format as Time`
  - `value -> as Value -> format as Number`
  - `sensor_name -> as SensorName -> format as String`
  - `metric_type -> as MetricType -> format as String`

Visualização recomendada:

- `Table`

Porque este exemplo existe:

- serve para perceber o schema real;
- ajuda a confirmar nomes de colunas e valores antes de desenhar um gráfico final.

### Exemplo 2. Série temporal de temperatura

Query:

```sql
SELECT time, sensor_name, value
FROM accepted_readings
WHERE metric_type = 'Temperature'
ORDER BY time
```

Configuração das colunas:

- `time -> as Time -> format as Time`
- `value -> as Value -> format as Number`
- `sensor_name -> as SensorName -> format as String`

Visualização recomendada:

- `Time series`

Leitura correta:

- `Time` vai para o eixo X;
- `Value` vai para o eixo Y;
- `SensorName` identifica a origem da leitura.

Nota importante:

Se a coluna `time` não for marcada como `Time`, o painel pode mostrar:

- `Data is missing a time field`

Outra nota prática:

Se esta query devolver muitos sensores ao mesmo tempo, o gráfico pode ficar denso e visualmente confuso. Nesses casos, há duas alternativas melhores:

- filtrar um sensor específico;
- agregar ou reduzir o número de séries no painel.

### Exemplo 3. Série temporal de temperatura para um sensor específico

Query:

```sql
SELECT time, sensor_name, value
FROM accepted_readings
WHERE metric_type = 'Temperature'
  AND sensor_name = 'sim-temp-001'
ORDER BY time
```

Configuração das colunas:

- `time -> as Time -> format as Time`
- `value -> as Value -> format as Number`
- `sensor_name -> as SensorName -> format as String`

Visualização recomendada:

- `Time series`

Porque este exemplo é útil:

- produz uma linha muito mais legível;
- é o melhor ponto de partida quando queremos validar se a telemetria daquele sensor faz sentido.

### Exemplo 4. Contagem de leituras por tipo de métrica

Query:

```sql
SELECT metric_type, COUNT(*) AS reading_count
FROM accepted_readings
GROUP BY metric_type
ORDER BY reading_count DESC
```

Configuração das colunas:

- `metric_type -> as MetricType -> format as String`
- `reading_count -> as ReadingCount -> format as Number`

Visualização recomendada:

- `Bar chart`
- `Pie chart`

Leitura correta:

- `MetricType` é a dimensão;
- `ReadingCount` é a métrica agregada.

### Exemplo 5. Últimas leituras em tabela

Query:

```sql
SELECT time, sensor_name, metric_type, value, unit, operational_state
FROM accepted_readings
ORDER BY time DESC
LIMIT 20
```

Configuração das colunas:

- `time -> as Time -> format as Time`
- `sensor_name -> as SensorName -> format as String`
- `metric_type -> as MetricType -> format as String`
- `value -> as Value -> format as Number`
- `unit -> as Unit -> format as String`
- `operational_state -> as OperationalState -> format as String`

Visualização recomendada:

- `Table`

Porque este exemplo é útil:

- ajuda a ver rapidamente o estado recente da telemetria;
- é um bom painel de apoio para troubleshooting.

## `risk_assessments`

### Exemplo 1. Descoberta rápida da tabela

Query:

```sql
SELECT *
FROM risk_assessments
ORDER BY time DESC
LIMIT 20
```

Configuração das colunas:

- `time -> as Time -> format as Time`
- `risk_score -> as RiskScore -> format as Number`
- `risk_level -> as RiskLevel -> format as String`
- `sensor_id -> as SensorId -> format as String`

Visualização recomendada:

- `Table`

### Exemplo 2. Evolução do score por sensor

Query:

```sql
SELECT time, sensor_id, risk_score
FROM risk_assessments
ORDER BY time
```

Configuração das colunas:

- `time -> as Time -> format as Time`
- `risk_score -> as RiskScore -> format as Number`
- `sensor_id -> as SensorId -> format as String`

Visualização recomendada:

- `Time series`

Leitura correta:

- `Time` no eixo X;
- `RiskScore` no eixo Y;
- `SensorId` para distinguir a origem da avaliação.

### Exemplo 3. Distribuição por nível de risco

Query:

```sql
SELECT risk_level, COUNT(*) AS total
FROM risk_assessments
GROUP BY risk_level
ORDER BY total DESC
```

Configuração das colunas:

- `risk_level -> as RiskLevel -> format as String`
- `total -> as Total -> format as Number`

Visualização recomendada:

- `Bar chart`
- `Pie chart`

### Exemplo 4. Últimas avaliações de risco

Query:

```sql
SELECT time, sensor_id, risk_level, risk_score, has_explanation
FROM risk_assessments
ORDER BY time DESC
LIMIT 20
```

Configuração das colunas:

- `time -> as Time -> format as Time`
- `sensor_id -> as SensorId -> format as String`
- `risk_level -> as RiskLevel -> format as String`
- `risk_score -> as RiskScore -> format as Number`
- `has_explanation -> as HasExplanation -> format as Number`

Visualização recomendada:

- `Table`

## `area_risk_snapshots`

### Exemplo 1. Descoberta rápida da tabela

Query:

```sql
SELECT *
FROM area_risk_snapshots
ORDER BY time DESC
LIMIT 20
```

Configuração das colunas:

- `time -> as Time -> format as Time`
- `aggregate_risk_score -> as AggregateRiskScore -> format as Number`
- `aggregate_risk_level -> as AggregateRiskLevel -> format as String`
- `severity -> as Severity -> format as String`
- `assessment_count -> as AssessmentCount -> format as Number`

Visualização recomendada:

- `Table`

### Exemplo 2. Evolução do risco agregado

Query:

```sql
SELECT time, aggregate_risk_score, severity
FROM area_risk_snapshots
ORDER BY time
```

Configuração das colunas:

- `time -> as Time -> format as Time`
- `aggregate_risk_score -> as AggregateRiskScore -> format as Number`
- `severity -> as Severity -> format as String`

Visualização recomendada:

- `Time series`

Leitura correta:

- `AggregateRiskScore` é a métrica principal;
- `Severity` funciona como contexto auxiliar, sobretudo em tabela ou tooltip.

### Exemplo 3. Último estado agregado

Query:

```sql
SELECT time, aggregate_risk_score, aggregate_risk_level, severity, assessment_count
FROM area_risk_snapshots
ORDER BY time DESC
LIMIT 1
```

Configuração das colunas:

- `time -> as Time -> format as Time`
- `aggregate_risk_score -> as AggregateRiskScore -> format as Number`
- `aggregate_risk_level -> as AggregateRiskLevel -> format as String`
- `severity -> as Severity -> format as String`
- `assessment_count -> as AssessmentCount -> format as Number`

Visualização recomendada:

- `Stat`
- `Gauge`

Nota prática:

Neste tipo de painel, a coluna numérica principal costuma ser:

- `AggregateRiskScore`

As restantes servem como contexto textual ou campos complementares.

### Exemplo 4. Histórico recente do estado da área

Query:

```sql
SELECT time, aggregate_risk_level, severity, aggregate_risk_score, assessment_count
FROM area_risk_snapshots
ORDER BY time DESC
LIMIT 20
```

Configuração das colunas:

- `time -> as Time -> format as Time`
- `aggregate_risk_level -> as AggregateRiskLevel -> format as String`
- `severity -> as Severity -> format as String`
- `aggregate_risk_score -> as AggregateRiskScore -> format as Number`
- `assessment_count -> as AssessmentCount -> format as Number`

Visualização recomendada:

- `Table`

## Recomendações de design para os primeiros dashboards

## Dashboard 1. Telemetria aceite

Objetivo:

- perceber o que está a entrar na pipeline;
- confirmar que sensores e métricas estão vivos.

Painéis recomendados:

- últimas leituras aceites;
- série temporal de temperatura;
- série temporal de humidade;
- série temporal de vento;
- contagem de leituras por `metric_type`.

Fonte principal:

- `accepted_readings`

## Dashboard 2. Avaliação de risco

Objetivo:

- perceber como o risco está a ser calculado por sensor;
- ver distribuição por nível de risco.

Painéis recomendados:

- últimas avaliações;
- série temporal de `risk_score` por sensor;
- contagem por `risk_level`;
- percentagem de avaliações com explicação.

Fonte principal:

- `risk_assessments`

## Dashboard 3. Estado agregado da área

Objetivo:

- perceber a saúde operacional da área piloto num relance.

Painéis recomendados:

- `gauge` com `aggregate_risk_score`;
- `stat` com `severity`;
- `stat` com `assessment_count`;
- série temporal do `aggregate_risk_score`;
- tabela de snapshots recentes.

Fonte principal:

- `area_risk_snapshots`

## Dashboard 4. Consola operacional futura

Objetivo:

- combinar observabilidade temporal com estado de configuração e alertas.

Nota importante:

Nem tudo deve vir do `InfluxDB`.

Quando quisermos mostrar:

- cenários;
- área ativa;
- configuração;
- runs de simulação;
- alertas ativos;

devemos admitir dashboards com múltiplas fontes:

- `InfluxDB` para telemetria e séries temporais;
- `Backoffice.Api` ou outra fonte relacional para estado de controlo.

## Armadilhas mais comuns

### 1. Misturar a URL completa com `Query Params`

Errado:

- URL com `?db=np_telemetry&format=json&q=...`
- e os mesmos campos também em `URL Query Params`

Correto:

- URL apenas `/api/v3/query_sql`
- parâmetros separados em `URL Query Params`

### 2. Usar a datasource antiga

O caminho de trabalho documentado é:

- `NatureProtectorInfinityJson`

Não devemos usar a datasource antiga baseada em `csv`, porque foi precisamente essa configuração que originou os erros de `mime type`.

### 3. Usar `localhost` dentro do painel

A datasource já conhece o `InfluxDB`. O painel só deve usar o caminho relativo:

- `/api/v3/query_sql`

### 4. Misturar unidades num mesmo gráfico

Na tabela `accepted_readings`, não devemos pôr no mesmo eixo:

- `Temperature`
- `Humidity`
- `WindSpeed`

Cada uma destas métricas deve ter o seu próprio gráfico ou pelo menos o seu próprio eixo e unidade.

### 5. Começar por dashboards bonitos antes de validar a estrutura

A ordem correta é:

1. `SHOW TABLES`
2. `SELECT * ... LIMIT 20`
3. escolher colunas
4. escrever query do gráfico
5. só depois configurar o visual final

## Ordem prática recomendada para evoluir a observabilidade

1. Validar a datasource `NatureProtectorInfinityJson`.
2. Criar um painel de descoberta com `SHOW TABLES`.
3. Criar tabelas simples para as três medições principais.
4. Criar séries temporais mínimas por tabela.
5. Criar um dashboard de área com `area_risk_snapshots`.
6. Criar um dashboard de sensores com `accepted_readings`.
7. Só depois introduzir painéis mais compostos, thresholds, cores e layout final.

## Onde mexer no repositório

- datasource provisionada:
  - [`../../infra/grafana/provisioning/datasources/influxdb.yml`](../../infra/grafana/provisioning/datasources/influxdb.yml)
- provisioning de dashboards:
  - [`../../infra/grafana/provisioning/dashboards/dashboards.yml`](../../infra/grafana/provisioning/dashboards/dashboards.yml)
- dashboards versionados:
  - [`../../infra/grafana/dashboards/`](../../infra/grafana/dashboards/)
- escrita para o `InfluxDB`:
  - [`../../src/NatureProtector.Infrastructure.Influx/Services/InfluxWriteService.cs`](../../src/NatureProtector.Infrastructure.Influx/Services/InfluxWriteService.cs)

## Fecho

Se tivermos de resumir a lógica deste guia numa frase, ela é esta:

primeiro percebemos o dado, depois escolhemos a pergunta, só depois desenhamos o painel.

É isso que evita dashboards frágeis, queries arbitrárias e confusão entre telemetria, risco e estado operacional.
