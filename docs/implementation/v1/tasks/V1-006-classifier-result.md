# V1-006 - ClassifierResult

## ID

V1-006

## Título

ClassifierResult canónico para classificadores técnicos, semânticos, temporais e de qualidade

## Objetivo

Planear a criação de `ClassifierResult` canónico para representar resultados de classificadores técnicos, semânticos, temporais e de qualidade, sem substituir mecanismos atuais de retry/quarentena.

## Contrato alvo

`ClassifierResult` deve conter:
- `classifier_name`
- `status`
- `severity`
- `flags`
- `reasons`
- `evaluated_at`
- `rule_set_version`

## Modelo/raciocínio recomendado para Codex

Codex, reasoning medium

## Modo

Plan first

## Estado de execução atual

Implementado, validação pendente.

## Resultado atual da slice

- `ClassifierResult` canónico criado no domínio de risco.
- Enums mínimos criados: `ClassifierStatus` e `ClassifierSeverity`.
- Agregação passiva criada para apoiar elegibilidade sem alterar arquitetura da pipeline.
- `RiskEligibilityResult` já pode transportar lista de `ClassifierResult` de forma passiva.
- Build/test global ainda não validado por limitação ambiental de `NuGet.Config`.

## Ficheiros prováveis

Produção:
- `src/NatureProtector.Prevention/Readings/*` [CONFIRMAR]
- `src/NatureProtector.Prevention/Risk/*` [CONFIRMAR]
- `src/NatureProtector.Prevention.Host/Processing/*` [CONFIRMAR apenas integração passiva, sem mexer no worker]

Testes:
- `tests/NatureProtector.Prevention.Tests/*`
- `tests/NatureProtector.Prevention.Host.Tests/Processing/DefaultProcessingFailureClassifierTests.cs` (não quebrar)

## Plano de implementação proposto (sem executar)

### Fase 1 - Definir contrato canónico
- Definir estrutura `ClassifierResult` com os campos obrigatórios.
- Definir enums/valores permitidos para `status` e `severity` [CONFIRMAR].

### Fase 2 - Diferenciar domínios de classificação
- Classificações técnicas, semânticas, temporais e de qualidade devem ser representáveis sem colapsar numa única causa.
- Separar claramente classificação observacional de falha de processamento.

### Fase 3 - Agregação para elegibilidade
- Definir forma de agregar múltiplos `ClassifierResult` para apoiar decisão de elegibilidade.
- Não substituir fluxo atual de retry/quarentena.

## Pseudocódigo

```text
result = ClassifierResult.Create(
  classifier_name,
  status,
  severity,
  flags,
  reasons,
  evaluated_at,
  rule_set_version)

aggregate = ClassifierResultAggregator.Combine(results)
eligibility = eligibility.WithClassifierResults(aggregate)
```

## Testes obrigatórios

- `ClassifierResult_CarriesFlagsAndReasons`
- `ClassifierResult_CanRepresentTechnicalSemanticAndTemporalClassification`
- `MultipleClassifierResults_CanBeAggregatedForEligibility`

## Regras e restrições

- Não substituir retry/quarentena existente.
- Não quebrar `DefaultProcessingFailureClassifierTests`.
- Não misturar falha de processamento com qualidade observacional.
- Se for necessário mexer no worker RabbitMQ, parar e justificar antes.

## O que não alterar

- `PreventionWorker` e fluxo de consumo RabbitMQ, salvo decisão explícita posterior.
- Contratos/topologia RabbitMQ em `src/NatureProtector.Shared/Messaging/*`.
- Política atual de retry/quarentena.

## Critério de pronto (da tarefa de planeamento)

- Contrato `ClassifierResult` documentado com todos os campos obrigatórios.
- Estratégia de agregação para elegibilidade definida.
- Fronteira explícita entre falha de processamento e qualidade observacional.
- Testes obrigatórios listados por nome.
- Estado da tarefa: implementada tecnicamente; não validada globalmente enquanto build/test não passarem.

## Riscos

- Sobreposição entre classificadores pode gerar duplicação de `reasons`/`flags`.
- Se status/severity não forem normalizados, agregação fica inconsistente.
- Integração acidental com fluxo de falhas técnicas pode quebrar testes existentes.

## Limite de escopo

- Apenas planeamento documental do contrato e integração de alto nível.
- Sem implementação de código nesta tarefa.

## Quando subir de reasoning medium para high

- Subir para `high` apenas se houver conflito estrutural real entre o contrato `ClassifierResult` e o modelo atual de processamento que exija redesenho profundo.
