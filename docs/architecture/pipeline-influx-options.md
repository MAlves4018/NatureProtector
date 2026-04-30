# Pipeline e InfluxDB: decisão, medições e próximos passos

## Objetivo

Esta nota regista a decisão tomada sobre o papel de InfluxDB na pipeline de prevenção, a alteração implementada para tornar as escritas de observabilidade configuráveis e não críticas por defeito, a evolução para escrita em batch por evento, as medições observadas durante a execução local e os próximos passos previstos para avaliar o desempenho da pipeline.

O foco desta nota é operacional. A decisão aqui documentada não redefine o modelo de risco, não muda contratos RabbitMQ e não substitui a documentação arquitetural geral.

## Problema observado

A pipeline de prevenção processa eventos de leitura vindos do simulador através de RabbitMQ. Cada evento é materializado na inbox PostgreSQL, processado pela `ReadingEventProcessingService`, validado semanticamente contra o plano de controlo e, quando aplicável, encaminhado para a `ReadingRiskPipeline`.

No fluxo elegível atual, a pipeline:

* persiste a leitura aceite em PostgreSQL;
* normaliza a leitura para `NormalizedReading`;
* avalia elegibilidade para risco;
* constrói `RiskInput`;
* calcula `RiskAssessment`;
* persiste assessment e snapshot em PostgreSQL;
* atualiza projeções operacionais;
* escreve pontos temporais em InfluxDB para observabilidade.

As séries temporais possíveis são:

* `accepted_readings`;
* `risk_assessments`;
* `area_risk_snapshots`.

Estas escritas são úteis para dashboards, análise temporal e diagnóstico, mas não representam o estado operacional durável da pipeline. O estado operacional continua a ser persistido em PostgreSQL.

O problema principal identificado foi que, quando InfluxDB está ativo e responde lentamente, o tempo de processamento de cada evento passa a ser fortemente influenciado pelas escritas de observabilidade. Isto aumenta a duração da tentativa de processamento, pode atrasar a conclusão do evento na inbox e pode contribuir para acumulação de trabalho pendente.

Nota importante: no fluxo atual, o `BasicAck` ao RabbitMQ acontece depois da materialização na inbox e antes do processamento de risco. Portanto, InfluxDB não deve ser descrito como fator que atrasa diretamente o `BasicAck` nominal. O impacto principal é no tempo de processamento interno posterior à inbox, no estado `Processing`, nos retries internos e na velocidade com que a pipeline escoa eventos já materializados.

## Decisão arquitetural

A decisão assumida é:

* PostgreSQL é o estado durável e operacional da pipeline;
* InfluxDB é observabilidade temporal e armazenamento de séries temporais;
* falhas de InfluxDB não devem, por defeito, invalidar o processamento operacional de uma leitura já aceite, persistida ou projetada em PostgreSQL;
* o comportamento estrito deve continuar disponível por configuração;
* a pipeline deve conseguir correr localmente com InfluxDB desligado, sem perder a cadeia funcional RabbitMQ → inbox PostgreSQL → processamento → projeções → API.

Esta decisão permite separar falhas operacionais reais de falhas de observabilidade. Uma falha ao persistir no PostgreSQL continua a ser uma falha da pipeline. Uma falha ao escrever uma série temporal em InfluxDB deve ser registada, mas não deve, por defeito, causar retry ou quarentena do evento.

## Configuração implementada

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

## Perfis operacionais úteis

### Perfil local sem InfluxDB

Para medir a pipeline sem custo de InfluxDB, a configuração pretendida deve ser:

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

### Perfil com InfluxDB parcial

Para reduzir custo mantendo alguma evidência temporal, pode ser usado um perfil parcial. Por exemplo, manter só leituras aceites:

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
      "RiskAssessments": false,
      "AreaRiskSnapshots": false
    }
  }
}
```

Este perfil é útil quando se quer confirmar entrada de leituras ao longo do tempo sem pagar o custo de escrever todas as séries derivadas.

### Perfil estrito

Para validar comportamento de falha, pode ser usado:

```json
{
  "InfluxDb": {
    "Enabled": true,
    "FailPipelineOnWriteError": true
  }
}
```

Neste modo, uma falha real de InfluxDB volta a falhar a tentativa de processamento. Este perfil é útil para testes de robustez, mas não deve ser o default local se o objetivo for demonstrar a cadeia operacional principal.

## Evolução para batch writes por evento

A primeira versão da pipeline fazia escritas lógicas separadas para as séries temporais de InfluxDB. As medições mostraram que, quando InfluxDB estava ativo, essas chamadas dominavam o tempo total do pipeline.

A otimização escolhida foi introduzir escrita em batch síncrona por evento. Em vez de tratar `accepted_readings`, `risk_assessments` e `area_risk_snapshots` como chamadas independentes ao writer real, a pipeline constrói um batch lógico de telemetria para o evento processado e envia esse batch através de `IInfluxWriteService.WriteBatchAsync(...)`.

Isto significa:

* não são batches aleatórios de eventos;
* não são envelopes RabbitMQ agregados;
* não se junta uma série de mensagens do broker num único evento;
* o contrato externo continua a ser uma leitura por `EventEnvelope<SensorReadingProducedPayload>`;
* o batch é interno à observabilidade, por evento processado;
* um evento elegível pode produzir até três pontos InfluxDB no mesmo batch lógico;
* um evento aceite mas não elegível para risco produz apenas o ponto de `accepted_readings`, se essa measurement estiver ativa;
* eventos rejeitados antes da inbox ou quarentenados antes da `ReadingRiskPipeline` não produzem telemetria derivada de risco.

Esta alteração reduz o número de chamadas HTTP por evento quando InfluxDB está ativo, sem alterar:

* RabbitMQ;
* envelopes e contratos de eventos;
* momento do `BasicAck`;
* persistência PostgreSQL;
* score atual;
* retries e quarentena;
* número de consumidores;
* API;
* simulador.

O papel arquitetural também se mantém inalterado:

* PostgreSQL continua a ser o estado operacional durável;
* InfluxDB continua a ser observabilidade temporal;
* falhas de InfluxDB continuam toleráveis por configuração quando `FailPipelineOnWriteError=false`;
* `Enabled=false` continua a permitir execução local sem dependência operacional de InfluxDB.

Ficam fora desta etapa mecanismos mais pesados, como background writer, Redis, filas internas, múltiplos consumidores ou outras estratégias de desacoplamento adicional.

## Relação com `NormalizedReading`, `RiskInput` e elegibilidade

A pipeline passou a ter fronteiras internas mais explícitas:

```text
EventEnvelope<SensorReadingProducedPayload>
  -> NormalizedReading
  -> RiskEligibilityResult
  -> RiskInput
  -> IRiskScoringService
  -> RiskAssessment
```

Esta alteração não muda a função do InfluxDB. A consequência operacional para esta nota é apenas a seguinte:

* a leitura aceite pode ser registada em PostgreSQL e, se configurado, em InfluxDB como `accepted_readings`;
* se a leitura for elegível, a pipeline calcula risco e pode escrever `risk_assessments` e `area_risk_snapshots`;
* se a leitura não for elegível, a pipeline termina com sucesso sem score, sem `RiskAssessment`, sem snapshot e sem projeções de risco;
* nesse caso, InfluxDB não deve receber pontos derivados de risco, apenas a evidência temporal da leitura aceite, se `AcceptedReadings=true`.

O serviço de elegibilidade default continua permissivo nesta fase, pelo que o comportamento nominal atual da baseline não muda.

## O que não foi alterado

Esta alteração não modificou:

* contratos RabbitMQ;
* envelopes de eventos;
* simulador;
* momento do `BasicAck`;
* semântica da inbox, retry e quarentena;
* persistência PostgreSQL;
* cálculo de score atual;
* thresholds do `SimpleRiskScoringService`;
* estrutura de buckets e databases InfluxDB;
* número de consumidores;
* qualquer mecanismo de Redis, fila interna ou background writer.

Também não implementa índices reais como FWI, KBDI ou Haines. A preparação para esses modelos está a ser feita noutras fronteiras da pipeline, nomeadamente `NormalizedReading`, `RiskInput` e elegibilidade.

## Testes e validação funcional

Foram adicionados ou atualizados testes para:

* validar que `NoOpInfluxWriteService` não lança exceções;
* validar que `SafeInfluxWriteService` tolera falhas quando `FailPipelineOnWriteError=false`;
* validar que `SafeInfluxWriteService` relança exceções quando `FailPipelineOnWriteError=true`;
* validar que as flags por measurement impedem chamadas ao writer real;
* validar o registo correto em dependency injection;
* validar que a pipeline completa o processamento quando a única falha é uma falha de InfluxDB tolerada;
* validar que o evento não entra em retry ou quarentena apenas por falha de observabilidade tolerada;
* validar que a pipeline continua a persistir o estado operacional quando InfluxDB está desligado;
* validar que o caminho de leitura inelegível termina sem score, sem snapshot e sem projeções de risco.

A validação de build e testes deve ser feita de forma sequencial, evitando correr builds ou testes em paralelo no Windows quando há risco de lock em `bin/` ou `obj/`.

Comandos recomendados:

```powershell
dotnet build .\NatureProtector.sln --nologo --no-restore -m:1
dotnet test .\NatureProtector.sln --nologo -v minimal -m:1 --no-restore
```

Se o restore falhar por acesso ao `NuGet.Config` global do utilizador, isso deve ser tratado como problema de ambiente e não como falha funcional desta alteração, desde que `--no-restore` passe sobre um workspace já restaurado.

## Medições observadas

Foram feitas medições locais com a pipeline em execução, usando o simulador, o `Prevention.Host`, PostgreSQL, RabbitMQ e InfluxDB. Uma das execuções foi interrompida antes de completar os 20 ciclos, mas ainda assim produziu informação suficiente para observar o comportamento da pipeline.

### Execução com InfluxDB ativo antes da otimização de batch

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

* InfluxDB completo;
* InfluxDB parcial;
* InfluxDB desligado;
* modo estrito;
* batch por evento com todas as measurements ativas;
* batch por evento apenas com as measurements selecionadas.

A configuração também permite medir separadamente o custo de cada perfil. Isto é importante para justificar tecnicamente decisões futuras: manter chamadas síncronas simples, desligar parte da observabilidade, usar batch por evento, ou introduzir um writer assíncrono/background.

## Limitações restantes

Esta alteração não resolve totalmente o custo de throughput quando InfluxDB está ativo e a responder lentamente. O batch por evento reduz overhead, mas continua a ser síncrono. Se uma chamada batch demorar muito, a tentativa de processamento continua bloqueada até a chamada terminar ou falhar.

Também não existe ainda buffering, retry assíncrono ou background writer. Em modo tolerante, uma falha de InfluxDB pode fazer perder pontos de observabilidade temporal, embora o estado operacional permaneça persistido em PostgreSQL.

Outra limitação observada é que execuções interrompidas durante processamento podem gerar erros que não representam necessariamente falhas funcionais da pipeline em regime normal. Por isso, as medições de desempenho devem ser feitas, sempre que possível, deixando o simulador terminar a execução completa ou reduzindo temporariamente o número de ciclos para obter uma execução completa mais curta.

Ainda há limitações fora do eixo InfluxDB:

* a consulta `GetLatestByAreaAsync` continua a ser tema separado de escalabilidade;
* a implementação de índices reais ainda depende de pesquisa, cadence, inputs meteorológicos e estado de modelo;
* a semântica de elegibilidade ainda é permissiva no serviço default;
* não há persistência explícita de motivos de inelegibilidade como artefacto próprio.

## Próximos passos

O próximo passo recomendado é repetir as medições com perfis controlados e comparáveis:

1. InfluxDB desligado;
2. InfluxDB batch completo;
3. InfluxDB batch parcial;
4. InfluxDB estrito apenas se for necessário testar comportamento de falha;
5. execução completa de 20 ciclos sem interrupção, sempre que possível.

Para a medição sem InfluxDB, deve ser confirmado nos logs que não aparecem:

```text
natureprotector.prevention.influx.write.*
influx_write_ms=
POST http://localhost:8181/api/v2/write
```

Se estes logs aparecerem, a configuração `Enabled=false` não está a ser lida pelo `Prevention.Host` ou o registo em dependency injection não está a substituir corretamente o writer real por `NoOpInfluxWriteService`.

Para a medição com batch, deve ser confirmado que a telemetria InfluxDB aparece como operação batch por evento e não como três chamadas separadas por measurement.

Ficam fora da próxima etapa imediata:

* Redis;
* múltiplos consumidores;
* envelopes agregados;
* envelopes mais leves;
* background writer;
* reestruturação da persistência PostgreSQL;
* implementação de FWI, KBDI ou Haines antes de fechar a pesquisa e os requisitos de inputs/cadência/estado.
