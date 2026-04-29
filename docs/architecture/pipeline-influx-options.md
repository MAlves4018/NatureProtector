# Pipeline e InfluxDB: decisão, medições e próximos passos

## Objetivo

Esta nota regista a decisão tomada sobre o papel de InfluxDB na pipeline de prevenção, a alteração implementada para tornar as escritas de observabilidade configuráveis e não críticas por defeito, as medições observadas durante a execução local e os próximos passos previstos para melhorar o desempenho da pipeline.

## Problema observado

A pipeline de prevenção processa eventos de leitura vindos do simulador através de RabbitMQ. Cada evento é materializado no inbox PostgreSQL, processado pela `ReadingRiskPipeline`, persistido em PostgreSQL, usado para atualizar projeções operacionais e escrito em InfluxDB para observabilidade temporal.

A análise do fluxo mostrou que as escritas para InfluxDB estavam no caminho síncrono do processamento. Por evento aceite, a pipeline podia escrever três measurements:

* `accepted_readings`;
* `risk_assessments`;
* `area_risk_snapshots`.

Estas escritas são úteis para dashboards, análise temporal e diagnóstico, mas não representam o estado operacional durável da pipeline. O estado operacional continua a ser persistido em PostgreSQL.

O problema principal é que, quando InfluxDB está ativo e responde lentamente, o tempo de processamento de cada evento passa a ser fortemente influenciado pelas escritas de observabilidade. Isto aumenta a duração da pipeline, atrasa o `BasicAck` ao broker e pode contribuir para acumulação de mensagens, retries ou falhas durante encerramento e interrupção.

## Decisão arquitetural

A decisão assumida é:

* PostgreSQL é o estado durável e operacional da pipeline;
* InfluxDB é observabilidade temporal e armazenamento de séries temporais;
* falhas de InfluxDB não devem, por defeito, invalidar o processamento operacional de uma leitura já aceite, persistida ou projetada em PostgreSQL;
* o comportamento estrito deve continuar disponível por configuração.

Esta decisão permite separar falhas operacionais reais de falhas de observabilidade. Uma falha ao persistir no PostgreSQL continua a ser uma falha da pipeline. Uma falha ao escrever uma série temporal em InfluxDB deve ser registada, mas não deve, por defeito, causar retry ou quarentena do evento.

## Alteração implementada

A secção `InfluxDb` suporta configuração explícita para ativação global, política de falha e seleção de measurements:

```json
{
  "InfluxDb": {
    "Enabled": true,
    "FailPipelineOnWriteError": false,
    "Url": "http://localhost:8181",
    "Token": "",
    "Organization": "natureprotector",
    "Bucket": "np_telemetry",
    "Writes": {
      "AcceptedReadings": true,
      "RiskAssessments": true,
      "AreaRiskSnapshots": true
    }
  }
}
```

Quando `Enabled=false`, o container de dependency injection resolve `IInfluxWriteService` para `NoOpInfluxWriteService`. Neste modo, a aplicação não cria cliente InfluxDB real, não tenta escrever séries temporais e não depende de InfluxDB para completar a pipeline.

Quando `Enabled=true`, o container resolve `IInfluxWriteService` para `SafeInfluxWriteService`. Este serviço aplica a política configurada, decide se cada measurement deve ser escrita e delega no writer real apenas quando apropriado.

A opção `FailPipelineOnWriteError=false` faz com que falhas de escrita em InfluxDB sejam registadas e toleradas. A opção `FailPipelineOnWriteError=true` preserva o comportamento estrito, relançando a exceção.

As flags `Writes.AcceptedReadings`, `Writes.RiskAssessments` e `Writes.AreaRiskSnapshots` permitem desligar seletivamente cada measurement.

Para medir a pipeline sem InfluxDB, a configuração pretendida deve ser:

```json
{
  "InfluxDb": {
    "Enabled": false,
    "FailPipelineOnWriteError": false,
    "Url": "http://localhost:8181",
    "Token": "",
    "Organization": "natureprotector",
    "Bucket": "np_telemetry",
    "Writes": {
      "AcceptedReadings": false,
      "RiskAssessments": false,
      "AreaRiskSnapshots": false
    }
  }
}
```

Na prática, `Enabled=false` deve ser suficiente para impedir a criação do cliente real e substituir o writer por `NoOpInfluxWriteService`. As flags em `Writes` podem ficar também a `false` para tornar explícito que esta execução é uma medição sem escritas temporais.

## O que não foi alterado

Esta alteração não modificou:

* contratos RabbitMQ;
* envelopes de eventos;
* simulador;
* momento do `BasicAck`;
* ordem funcional da `ReadingRiskPipeline`;
* persistência PostgreSQL;
* semântica de `NormalizedReading`, `RiskInput` ou `OperationalEvent`;
* estrutura de buckets e databases InfluxDB;
* número de consumidores;
* qualquer mecanismo de Redis, fila interna ou background writer.

## Testes e validação funcional

Foram adicionados testes para:

* validar que `NoOpInfluxWriteService` não lança exceções;
* validar que `SafeInfluxWriteService` tolera falhas quando `FailPipelineOnWriteError=false`;
* validar que `SafeInfluxWriteService` relança exceções quando `FailPipelineOnWriteError=true`;
* validar que as flags por measurement impedem chamadas ao writer real;
* validar o registo correto em dependency injection;
* validar que a pipeline completa o processamento quando a única falha é uma falha de InfluxDB tolerada;
* validar que o evento não entra em retry ou quarentena apenas por falha de observabilidade tolerada.

## Medições observadas

Foram feitas medições locais com a pipeline em execução, usando o simulador, o `Prevention.Host`, PostgreSQL, RabbitMQ e InfluxDB. Uma das execuções foi interrompida antes de completar os 20 ciclos, mas ainda assim produziu informação suficiente para observar o comportamento da pipeline.

### Execução com InfluxDB ativo

A execução observada não deve ser considerada uma medição sem InfluxDB, porque os logs mostram chamadas reais para InfluxDB:

```text
natureprotector.prevention.influx.write.risk_assessment
natureprotector.prevention.influx.write.area_risk_snapshot
POST http://localhost:8181/api/v2/write
http.response.status_code: 204
influx_write_ms=813
influx_write_ms=984
influx_write_ms=2810
```

Nesta execução, o simulador foi interrompido após 2 ciclos completos.

Resumo do simulador:

| Métrica                    | Valor observado |
| -------------------------- | --------------: |
| Ciclos executados          |               2 |
| Mensagens publicadas       |              12 |
| Mensagens por ciclo        |               6 |
| Duração até cancelamento   |         52.16 s |
| Tamanho de batch por ciclo |               6 |

Resumo do `Prevention.Host`:

| Métrica             | Valor observado |
| ------------------- | --------------: |
| Eventos recebidos   |              12 |
| Eventos validados   |              12 |
| Eventos com ack     |              12 |
| Eventos processados |              11 |

O evento em falta ficou associado ao encerramento da aplicação e não a uma falha normal de validação ou persistência. O log registou uma exceção durante o shutdown:

```text
ObjectDisposedException: Cannot access a disposed object.
Object name: 'IServiceProvider'.
```

Isto indica que a aplicação foi interrompida enquanto ainda havia trabalho pendente ou tentativa de retry e atualização interna.

### Duração global da pipeline

A métrica agregada de processamento mostrou:

| Métrica                     |       Valor |
| --------------------------- | ----------: |
| Eventos processados         |          11 |
| Soma total de processamento | 29592.53 ms |
| Média por evento            |  2690.23 ms |
| Mínimo                      |  1609.39 ms |
| Máximo                      | 10127.50 ms |

A média de cerca de 2.69 s por evento é demasiado alta para a pipeline de prevenção. A decomposição por operação mostra que o custo principal não veio do PostgreSQL, mas das escritas síncronas para InfluxDB.

### Escritas PostgreSQL

As operações PostgreSQL ficaram normalmente na ordem dos poucos milissegundos:

| Operação PostgreSQL  | Count |      Soma |    Média |  Mínimo |   Máximo |
| -------------------- | ----: | --------: | -------: | ------: | -------: |
| `accepted_reading`   |    12 | 107.74 ms |  8.98 ms | 3.48 ms | 58.00 ms |
| `risk_assessment`    |    12 |  91.84 ms |  7.65 ms | 3.73 ms | 40.39 ms |
| `cell_projection`    |    12 |  91.24 ms |  7.60 ms | 3.39 ms | 42.69 ms |
| `area_risk_snapshot` |    11 |  68.32 ms |  6.21 ms | 3.35 ms | 26.04 ms |
| `area_projection`    |    11 | 149.89 ms | 13.63 ms | 8.05 ms | 51.30 ms |

Estas medições indicam que, nesta execução, PostgreSQL não foi o gargalo principal. Mesmo somando várias operações por evento, o custo de persistência e projeção em PostgreSQL ficou muito abaixo do custo observado nas escritas para InfluxDB.

### Escritas InfluxDB

As escritas InfluxDB apresentaram tempos muito superiores:

| Measurement InfluxDB  | Count |        Soma |      Média |    Mínimo |     Máximo |
| --------------------- | ----: | ----------: | ---------: | --------: | ---------: |
| `risk_assessments`    |    11 | 10312.47 ms |  937.50 ms | 299.66 ms | 2810.21 ms |
| `area_risk_snapshots` |    11 | 17876.36 ms | 1625.12 ms | 130.83 ms | 9114.44 ms |

Em eventos individuais, os logs confirmam que InfluxDB dominou quase todo o tempo da pipeline. Um exemplo observado:

```text
pipeline_total_ms=1837
risk_assessment_influx_ms=813
snapshot_influx_ms=985
```

Neste caso, só as duas escritas para InfluxDB somaram aproximadamente 1798 ms, quase todo o tempo total da pipeline.

Outro exemplo:

```text
pipeline_total_ms=2984
accepted_reading_persist_ms=4
risk_assessment_persist_ms=4
save_cell_projection_ms=3
risk_assessment_influx_ms=2811
snapshot_persist_ms=8
snapshot_influx_ms=131
save_area_projection_ms=8
```

Neste evento, a escrita `risk_assessment_influx_ms=2811` foi praticamente o fator dominante da duração total.

## Interpretação das medições

As medições confirmam a hipótese inicial: com InfluxDB ativo no caminho síncrono, o tempo total da pipeline passa a ser condicionado pela latência das escritas temporais.

A comparação entre PostgreSQL e InfluxDB é clara:

* as operações PostgreSQL ficaram geralmente abaixo de 15 ms em média;
* as escritas InfluxDB ficaram entre centenas de milissegundos e vários segundos;
* a média global da pipeline ficou acima de 2.6 s por evento;
* o encerramento da aplicação durante processamento ainda pendente produziu exceções relacionadas com cancelamento, `TaskCanceledException`, `HttpException` e `ObjectDisposedException`.

Isto reforça a decisão de tratar InfluxDB como observabilidade não crítica por defeito. Também mostra que tornar as escritas configuráveis é necessário, mas não suficiente quando se pretende manter InfluxDB ativo com bom desempenho.

## Impacto da decisão

A alteração melhora a robustez e a diagnosticabilidade da baseline local. A pipeline deixa de depender obrigatoriamente de InfluxDB para completar o processamento operacional e passa a permitir perfis diferentes de observabilidade.

Exemplos de perfis possíveis:

* InfluxDB completo: todas as measurements ativas;
* InfluxDB parcial: apenas algumas measurements ativas;
* InfluxDB desligado: pipeline sem escritas temporais;
* modo estrito: falhas InfluxDB continuam a falhar a pipeline, se configurado.

A configuração também permite medir separadamente o custo de cada perfil. Isto é importante para justificar tecnicamente a próxima decisão: manter chamadas síncronas simples, desligar parte da observabilidade, ou introduzir escrita em batch.

## Limitações restantes

Esta alteração não resolve totalmente o custo de throughput quando InfluxDB está ativo e a responder lentamente. O writer real continua síncrono por chamada lógica quando a measurement está ativa.

Também não existe ainda buffering, retry assíncrono ou background writer. Em modo tolerante, uma falha de InfluxDB pode fazer perder pontos de observabilidade temporal, embora o estado operacional permaneça persistido em PostgreSQL.

Outra limitação observada é que execuções interrompidas durante processamento podem gerar erros que não representam necessariamente falhas funcionais da pipeline em regime normal. Por isso, as medições de desempenho devem ser feitas, sempre que possível, deixando o simulador terminar a execução completa ou reduzindo temporariamente o número de ciclos para obter uma execução completa mais curta.

## Evolução para batch writes

As medições confirmaram que o PostgreSQL não foi o gargalo principal da baseline local. As operações de persistência e projeção ficaram, na generalidade, na ordem de poucos milissegundos por evento, enquanto as chamadas HTTP para InfluxDB dominaram o `pipeline_total_ms` quando a observabilidade temporal estava ativa.

Perante este resultado, a primeira otimização escolhida foi introduzir escrita em batch síncrona por evento. Em vez de escrever `accepted_readings`, `risk_assessments` e `area_risk_snapshots` em chamadas separadas, a pipeline passa a construir um batch lógico e a enviá-lo numa única operação ao writer real de InfluxDB.

Esta alteração reduz o número de chamadas HTTP por evento sem alterar:

* RabbitMQ;
* envelopes e contratos de eventos;
* momento do `BasicAck`;
* persistência PostgreSQL;
* número de consumidores.

O papel arquitetural também se mantém inalterado:

* PostgreSQL continua a ser o estado operacional durável;
* InfluxDB continua a ser observabilidade temporal;
* falhas de InfluxDB continuam toleráveis por configuração quando `FailPipelineOnWriteError=false`;
* `Enabled=false` continua a permitir execução local sem dependência operacional de InfluxDB.

Ficam fora desta etapa mecanismos mais pesados, como background writer, Redis, filas internas ou outras estratégias de desacoplamento adicional. O objetivo desta versão é validar a hipótese principal com a menor alteração segura: reduzir o overhead das chamadas InfluxDB agrupando os pontos disponíveis por evento.

## Próximos passos

O próximo passo recomendado é repetir as medições com perfis controlados e comparáveis:

1. InfluxDB desligado;
2. InfluxDB individual, antes do batch, se a baseline comparativa ainda estiver disponível;
3. InfluxDB batch;
4. InfluxDB parcial, se essa comparação continuar relevante.

Para a medição sem InfluxDB, deve ser confirmado nos logs que não aparecem:

```text
natureprotector.prevention.influx.write.*
influx_write_ms=
POST http://localhost:8181/api/v2/write
```

Se estes logs aparecerem, a configuração `Enabled=false` não está a ser lida pelo `Prevention.Host` ou o registo em dependency injection não está a substituir corretamente o writer real por `NoOpInfluxWriteService`.

Ficam fora da próxima etapa imediata soluções mais pesadas como Redis, múltiplos consumidores, envelopes agregados, envelopes mais leves ou reestruturação da persistência PostgreSQL.
