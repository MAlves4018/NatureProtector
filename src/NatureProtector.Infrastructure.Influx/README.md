# NatureProtector.Infrastructure.Influx

Este projeto adapta a solução ao InfluxDB. Hoje ele é um módulo de escrita, não um módulo completo de leitura, consulta ou agregação histórica.

## O que faz hoje

- Escreve leituras aceites na medição `accepted_readings`.
- Escreve avaliações de risco na medição `risk_assessments`.
- Escreve snapshots agregados por área na medição `area_risk_snapshots`.

## Ficheiros principais

- `Configuration/InfluxDbOptions.cs`
  - contrato de configuração
- `DependencyInjection/ServiceCollectionExtensions.cs`
  - extensão para registo no container
- `Services/IInfluxWriteService.cs`
  - fronteira de escrita usada pelo host de prevenção
- `Services/InfluxWriteService.cs`
  - implementação concreta baseada no cliente oficial

## Configuração obrigatória

A secção `InfluxDb` deve definir:

- `Url`
- `Token`
- `Organization`
- `Bucket`

Se algum destes valores faltar, o serviço falha logo na construção. Isto é útil porque evita correr o host com configuração incompleta sem percebermos porquê.

## Relação com o resto da solução

- O `Prevention.Host` usa este projeto para escrever telemetria operacional.
- O módulo depende de `NatureProtector.Core` para níveis de risco e snapshots.
- O módulo depende de `NatureProtector.Shared` para os envelopes e payloads de leitura aceites.

## O que ainda não faz

- Não lê nem consulta dados do InfluxDB.
- Não faz batching explícito nem políticas avançadas de retenção.
- Não modela ainda séries de alertas, recomendações ou projeções.
- Não fecha a integração com dashboards de produto final; a baseline atual de Grafana ainda está numa fase de setup.
