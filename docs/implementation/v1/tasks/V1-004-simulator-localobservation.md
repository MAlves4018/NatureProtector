# V1-004 - Simulator LocalObservation

## ID

V1-004

## Título

Planear LocalObservation como camada de sensor lógico após TruthSnapshot

## Objetivo

Planear a introdução de `LocalObservation` como camada lógica de observação aplicada após `TruthSnapshot`, preservando a separação entre verdade física, degradação de sensor e transporte.

## Contrato alvo

`LocalObservation` é a observação produzida a partir da verdade física, já com ruído, bias, drift, clipping, stuck value ou erro de instalação.

## Modelo/raciocínio recomendado para Codex

Codex, reasoning medium

## Modo

Plan first. Não implementar código nesta tarefa.

## Dependências

- `V1-003-simulator-truthsnapshot.md` (modelo de verdade física antes da observação).

## Ficheiros prováveis

Produção:
- `src/NatureProtector.Simulator.Host/Services/ReadingGenerationService.cs` [CONFIRMAR]
- `src/NatureProtector.Simulator.Host/Services/SimulationRunner.cs` [CONFIRMAR]

Testes:
- `tests/NatureProtector.Simulator.Host.Tests/Services/*`

## Plano de implementação proposto (sem executar)

### Fase 1 - Definir fronteira da observação
- Entrada: `TruthSnapshot`.
- Saída: `LocalObservation`.
- Garantir que transformação de observação não altera o objeto de verdade física.

### Fase 2 - Encadear efeitos de sensor
- Aplicar efeitos no domínio de observação: ruído, bias, drift, clipping, stuck value, erro de instalação.
- Definir ordem de aplicação de efeitos [CONFIRMAR].

### Fase 3 - Preservar fronteira com transporte
- Entregar `LocalObservation` para criação de evento sem alterar payload RabbitMQ.
- Não introduzir falhas de transporte nesta camada.

## Pseudocódigo

```text
truth = physicalModel.Generate(cell, logicalTime, scenario, seed)
localObservation = sensorModel.Observe(truth, sensorProfile, faultProfile)
event = eventFactory.Create(localObservation)
```

## Testes obrigatórios

- `LocalObservation_CanApplyNoiseWithoutChangingTruth`
- `ScenarioC_DegradesObservationButKeepsTruth`
- `StuckValue_ProducesRepeatedObservationValues`

## Regras e restrições

- Não alterar RabbitMQ.
- Não misturar falha de sensor com falha de transporte.
- Usar `high` reasoning só se for necessário redesenhar o serviço de geração.

## O que não alterar

- Contratos/topologia RabbitMQ em `src/NatureProtector.Shared/Messaging/*`.
- Pipeline de prevenção (`src/NatureProtector.Prevention*`).
- Qualquer lógica de transporte fora da camada de observação.

## Critério de pronto (da tarefa de planeamento)

- Contrato `LocalObservation` descrito com fronteira clara em relação a `TruthSnapshot`.
- Separação explícita entre falhas de sensor e falhas de transporte.
- Testes obrigatórios identificados por nome.
- Sequência de execução documentada: verdade física -> observação local -> evento.

## Riscos

- Acoplamento atual pode dificultar separar observação de geração inline.
- Ordem dos efeitos (noise/bias/drift/clipping/stuck/install) pode alterar comportamento esperado.
- Sem definição de perfil de falha, cenários podem ficar ambíguos.

## Limite de escopo

- Apenas planeamento da camada `LocalObservation`.
- Sem implementação de código nesta tarefa.

## Quando subir de reasoning medium para high

- Subir para `high` apenas se a separação exigir redesenho do serviço de geração ou alteração estrutural que atravesse múltiplas fronteiras do simulador.
