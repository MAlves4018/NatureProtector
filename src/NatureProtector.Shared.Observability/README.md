# NatureProtector.Shared.Observability

Este projeto concentra o wiring de observabilidade runtime para os hosts do NatureProtector.

## Âmbito

- Registo de OpenTelemetry nos hosts.
- Registo de exporters Console e OTLP.
- Instrumentação ASP.NET Core, HttpClient, runtime e processo.
- `ActivitySource`, `Meter` e nomes de métricas partilhados pelos hosts runtime.
- Opções de activity tracking para logging.

## Fronteira

`NatureProtector.Shared` continua a ser a fronteira de contratos e messaging. Não deve depender de pacotes `OpenTelemetry*`.

O pacote de instrumentação de processo ainda é beta na linha de packages atual. Este projeto mantém-no isolado aqui para que contratos puros e consumidores que só precisam de message contracts não herdem dependências de exporters ou instrumentation.

Os smoke tests em `NatureProtector.Shared.Tests` validam compatibilidade de arranque com configuração OTLP. Não provam entrega a um collector real.
