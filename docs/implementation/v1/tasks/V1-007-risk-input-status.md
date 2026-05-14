# V1-007 - RiskInputStatus

## ID

V1-007

## Título

Introduzir RiskInputStatus (CompleteEligible, PartialButUsable, Blocked)

## Objetivo

Planear a introdução de `RiskInputStatus` para classificar a elegibilidade de entrada de risco sem alterar RabbitMQ nem reescrever toda a `ReadingRiskPipeline`.

## Contrato alvo

```csharp
public enum RiskInputStatus
{
    CompleteEligible,
    PartialButUsable,
    Blocked
}
```

## Modelo/raciocínio recomendado para Codex

Codex, reasoning medium

## Modo

Plan first

## Estado de execução atual

Implementada, validação pendente.

## Resultado atual da slice

- `RiskInputStatus` introduzido com `CompleteEligible`, `PartialButUsable` e `Blocked`.
- `RiskEligibilityResult` evoluído para transportar `Status`, `Reasons`, `ObservationalConfidence`, `OperationalIntegrity`.
- `QualityFlags` introduzido de forma parcial/temporária como `List<string>`.
- `RiskEligibilityResult` passou também a suportar `ClassifierResults` de forma passiva (`IReadOnlyList<ClassifierResult>`).
- Compatibilidade mantida para consumo atual (`IsEligible`/`ReasonCode`).
- `RiskAssessment V1` continua pendente/divergente (não implementado nesta slice).

## Ficheiros prováveis

Produção:
- `src/NatureProtector.Prevention/Risk/RiskInput.cs`
- `src/NatureProtector.Prevention/Risk/RiskEligibilityService.cs`

Testes:
- `tests/NatureProtector.Prevention.Tests/Risk/*`

## Regras obrigatórias

- `RiskInput` não contém score.
- `Blocked` nunca gera score `0`.
- `Blocked` deve gerar ausência de assessment numérico **ou** assessment com campos numéricos nulos (decisão posterior).
- Não alterar RabbitMQ.
- Não reescrever `ReadingRiskPipeline` inteira.
- Não tocar em PostgreSQL nesta slice, salvo justificação explícita de que o estado já é persistido.

## Plano de alteração (sem implementar)

### Fase 1 - Definir estado de elegibilidade
- Introduzir `RiskInputStatus` no domínio de risco.
- Associar estado ao resultado de elegibilidade sem alterar score nesta fase.

### Fase 2 - Aplicar regras de decisão
- `CompleteEligible`: todos os métricos críticos presentes e válidos.
- `PartialButUsable`: degradação controlada, ainda com base mínima para cálculo.
- `Blocked`: falta métrica crítica ou invalidação forte; não produz score numérico.

### Fase 3 - Integrar de forma mínima
- Propagar `RiskInputStatus` para os pontos de uso diretos de `RiskInput`.
- Evitar refatoração ampla da pipeline; aplicar integração localizada.

### Fase 4 - Decisão pendente para `Blocked`
- Opção A: ausência de assessment numérico.
- Opção B: assessment existente com campos numéricos nulos.
- Registar decisão posterior antes de tocar em persistência.

## Pseudocódigo

```text
eligibility = EvaluateEligibility(reading, qualityFlags, classifierResults)

if eligibility.hasAllRequiredMetrics:
    status = CompleteEligible
elif eligibility.hasMinimumUsableSet:
    status = PartialButUsable
else:
    status = Blocked

riskInput = RiskInput.From(reading, status, requiredMetrics, optionalMetrics)

if status == Blocked:
    assessment = NoNumericAssessment()   // ou NumericFieldsNull(), decisão posterior
else:
    assessment = scoringService.Score(riskInput)
```

## Testes obrigatórios

- `CompleteEligible_AllRequiredMetricsPresent`
- `PartialButUsable_DegradedButStillUsable`
- `Blocked_MissingCriticalMetric`
- `Blocked_DoesNotProduceRiskScoreZero`

## O que não alterar

- Contratos/topologia RabbitMQ em `src/NatureProtector.Shared/Messaging/*`.
- Estrutura completa da `ReadingRiskPipeline`.
- Persistência PostgreSQL nesta slice (exceto justificação explícita e limitada).

## Critério de pronto (da tarefa de planeamento)

- Enum `RiskInputStatus` definido na documentação da tarefa.
- Regras operacionais por estado explicitadas.
- Estratégia de `Blocked` sem score zero definida como decisão pendente controlada.
- Testes obrigatórios listados por nome.
- Estado da tarefa: implementada tecnicamente, sem conclusão final enquanto build/test não passarem.

## Riscos

- Ambiguidade sobre representação final de `Blocked` no assessment.
- Integração parcial pode deixar pontos de cálculo antigos sem usar novo estado.
- Mudança de semântica pode afetar testes existentes de elegibilidade.

## Limite de escopo

- Apenas planeamento documental desta alteração.
- Sem implementação de código nesta tarefa.

## Limitações de validação

- `dotnet build` e `dotnet test` falharam por limitação ambiental de acesso a `C:\Users\Miguel\AppData\Roaming\NuGet\NuGet.Config`.
- A slice permanece com validação pendente até execução limpa de build/test.

## Quando subir de reasoning medium para high

- Subir para `high` apenas se houver conflito estrutural real entre elegibilidade, produção de assessment e persistência existente.
