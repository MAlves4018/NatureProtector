# V1-003 - Simulator TruthSnapshot

## ID

V1-003

## Título

Introduzir TruthSnapshot como modelo interno no simulador (planeamento)

## Objetivo

Planear a introdução de `TruthSnapshot` como modelo interno do estado físico simulado antes de erro observacional ou falha de transporte, sem alterar RabbitMQ.

## Estado atual

- Auditoria atual: `TruthSnapshot` ausente nominalmente.
- Geração de valores físicos ocorre inline em `ReadingGenerationService`.

## Contrato alvo

`TruthSnapshot` representa o estado físico simulado antes de qualquer etapa de observação por sensor ou perturbação de transporte.

## Modelo/raciocínio recomendado para Codex

Codex, reasoning medium

## Modo

Plan first. Não implementar código nesta tarefa.

## Ficheiros prováveis

Produção:
- `src/NatureProtector.Simulator.Host/Services/ReadingGenerationService.cs`
- `src/NatureProtector.Simulator.Host/Services/SimulationRunner.cs`

Testes:
- `tests/NatureProtector.Simulator.Host.Tests/Services/*`

## Plano de implementação proposto (sem executar)

### Fase 1 - Extrair modelo físico
- Isolar cálculo físico atualmente inline para produzir `TruthSnapshot` determinístico.
- Definir fronteira clara entre `physicalModel.Generate(...)` e etapas seguintes.

### Fase 2 - Aplicar observação de sensor
- Consumir `TruthSnapshot` no `sensorModel.Observe(...)`.
- Garantir que erro observacional é aplicado depois do estado físico base.

### Fase 3 - Criar evento sem alterar RabbitMQ
- Manter criação de evento via factory usando observação atual.
- Não alterar payload, routing keys, exchange, filas ou contratos RabbitMQ.

## Pseudocódigo

```text
truth = physicalModel.Generate(cell, logicalTime, scenario, seed)
observation = sensorModel.Observe(truth, sensorProfile)
event = eventFactory.Create(observation)
```

## Testes obrigatórios

- `TruthSnapshot_IsDeterministicForSameSeed`
- `ScenarioB_TruthIsStableAcrossRunsWithSameSeed`
- `ScenarioC_CanReuseTruthBeforeApplyingFaults`

## Regras e restrições

- Não alterar payload RabbitMQ.
- Não alterar a pipeline de prevenção.
- Não implementar FWI/KBDI aqui.
- Se a alteração exigir mexer em mais de 4 ficheiros de produção, parar e propor decomposição.

## O que não alterar

- `src/NatureProtector.Prevention*`
- Contratos e topologia RabbitMQ em `src/NatureProtector.Shared/Messaging/*`

## Critério de pronto (da tarefa de planeamento)

- Estratégia de introdução de `TruthSnapshot` documentada.
- Sequência `truth -> observation -> event` explícita.
- Testes obrigatórios identificados por nome.
- Guard rail de decomposição (>4 ficheiros de produção) definido.

## Riscos

- Acoplamento oculto entre geração física e lógica de observação pode aumentar escopo.
- Determinismo pode depender de fontes de seed/tempo não centralizadas.
- Introdução de modelo interno pode exigir ajustes em fixtures de teste.

## Limite de escopo

- Apenas planeamento e documentação da tarefa.
- Sem alterações em código de produção, testes, contratos ou infraestrutura.

## Quando subir de reasoning medium para high

- Subir para `high` apenas se surgirem conflitos técnicos reais entre determinismo, desenho de modelo e fronteiras de integração que bloqueiem a decomposição.
