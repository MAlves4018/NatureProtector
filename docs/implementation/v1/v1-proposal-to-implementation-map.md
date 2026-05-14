# V1 Proposal -> Implementation Map

## 1. Objetivo do Plano

Definir a estrutura de rastreabilidade entre o conteúdo do `Proposal.pdf` e o estado real do repositório, para converter proposta em tarefas executáveis sem assumir implementação já concluída.

## 2. Fontes Usadas

| Fonte | Tipo | Localização | Estado de validação | Notas |
|---|---|---|---|---|
| Proposal principal | PDF | `C:/Users/Miguel/UNI/6sem/PS/Rand/Pesquisa2/Tex9/Pesquisa2/Proposal.pdf` | Pendente |  |
| Código-fonte | Repositório | `NatureProtector/` | Pendente |  |
| Testes | Repositório | `NatureProtector/tests/` | Pendente |  |
| Contratos | Documentação/artefactos | `NatureProtector/docs/contracts/` | Pendente |  |

## 3. Hierarquia de Verdade

| Prioridade | Fonte de verdade | Regra de decisão | Exemplo de uso |
|---|---|---|---|
| 1 | Código executável no repositório | Em conflito com texto, prevalece comportamento observável do código |  |
| 2 | Contratos formais (incl. RabbitMQ) | Não alterar sem versão explícita e migração definida |  |
| 3 | Testes automatizados | Definem comportamento esperado quando consistentes com contratos |  |
| 4 | Proposal | Fonte de intenção e direção, não prova de implementação |  |

## 4. Matriz Proposal -> Repo -> Gap -> Tarefa

| ID | Secção do Proposal | Requisito/afirmação | Evidência no repo | Gap identificado | Tarefa proposta | Prioridade | Dono | Estado |
|---|---|---|---|---|---|---|---|---|
| MAP-001 | Modelo de dados operacional | Snapshot canónico do estado por célula/tempo | Gap conhecido (auditoria detalhada pendente) | `TruthSnapshot` ausente | Criar contrato `TruthSnapshot` v1 com campos mínimos + **teste obrigatório** de serialização/deserialização do contrato | Alta | [PREENCHER] | Ausente |
| MAP-002 | Ingestão/observação local | Observação local normalizada na origem | Gap conhecido (auditoria detalhada pendente) | `LocalObservation` ausente | Criar contrato `LocalObservation` v1 + **teste obrigatório** de mapeamento de input bruto -> contrato | Alta | [PREENCHER] | Ausente |
| MAP-003 | Eventos operacionais | Evento operacional canónico para pipeline | Existe base parcial via `EventEnvelope<TPayload>` | `OperationalEvent` parcial | Introduzir payload canónico `OperationalEvent` sobre envelope existente + **teste obrigatório** de compatibilidade com `EventEnvelope<TPayload>` | Alta | [PREENCHER] | Parcial |
| MAP-004 | Normalização de leituras | Leitura normalizada com classificação rica | `NormalizedReading` existe (sem flags ricas) | `NormalizedReading` parcial | Estender `NormalizedReading` com flags/classificação rica + **teste obrigatório** de classificação por cenário (normal/anómalo) | Alta | [PREENCHER] | Parcial |
| MAP-005 | Input de risco | Input de risco completo para cálculo | `RiskInput` existe em versão mínima e sem score | `RiskInput` ainda parcial face ao contrato-alvo V1 completo | Expandir `RiskInput` para campos mínimos de operação + **teste obrigatório** de validação de campos obrigatórios | Média | [PREENCHER] | Parcial |
| MAP-006 | Avaliação de risco | Estrutura de avaliação alinhada ao contrato-alvo | Implementação atual usa `RiskScore/RiskLevel` | `RiskAssessment` divergente (`input_status/base_risk/adjusted_score`) | Adaptar `RiskAssessment` para contrato-alvo mantendo ponte de compatibilidade + **teste obrigatório** de mapeamento legado -> novo | Alta | [PREENCHER] | Divergente |
| MAP-007 | Estado diário por célula | Estado diário consolidado da célula | Gap conhecido (auditoria detalhada pendente) | `DailyCellState` ausente | Criar contrato `DailyCellState` v1 + **teste obrigatório** de agregação diária com input mínimo | Média | [PREENCHER] | Ausente |
| MAP-008 | Classificação | Resultado canónico de classificador | `ClassifierResult`, `ClassifierStatus` e `ClassifierSeverity` criados no domínio de risco; agregação passiva disponível | Integração de consumo ainda parcial no fluxo completo | Integrar consumo incremental em elegibilidade sem alterar retry/quarentena + **teste obrigatório** de integração | Média | [PREENCHER] | Implementado |
| MAP-009 | Qualidade de dados | Flags de qualidade reutilizáveis | `QualityFlags` presente de forma temporária como `List<string>` em `RiskEligibilityResult` e `ClassifierResult` | `QualityFlags` parcial (contrato ainda não canónico) | Evoluir para contrato canónico de `QualityFlags` + **teste obrigatório** de combinação de flags e prioridade | Média | [PREENCHER] | Parcial |
| MAP-010 | Alertas operacionais | Estado de alerta com transições claras | Existe alerta simples `area-risk-high` | `AlertState` parcial | Evoluir `AlertState` para estados/transições mínimas + **teste obrigatório** de transição de estado (baixo->alto->resolvido) | Alta | [PREENCHER] | Parcial |
| MAP-011 | Elegibilidade operacional | Estados de elegibilidade para uso de dados | `RiskInputStatus` e estados `CompleteEligible/PartialButUsable/Blocked` introduzidos no domínio | Decisão final pendente para `Blocked`: ausência de assessment vs campos numéricos nulos | Consolidar regra final de `Blocked` e cobertura de integração + **teste obrigatório** de decisão de elegibilidade por flags | Alta | [PREENCHER] | Parcial |
| MAP-012 | Projeção operacional | Projeção com contrato e campos completos | Existe implementação parcial | `OperationalProjection` parcial | Completar contrato/campos mínimos de `OperationalProjection` + **teste obrigatório** de projeção com dados incompletos controlados | Média | [PREENCHER] | Parcial |
| MAP-013 | Índices finais de risco | Índices finais `FWI` e `KBDI` disponíveis no fecho | Gap conhecido (auditoria detalhada pendente) | `FWI/KBDI` finais ausentes | Introduzir saída final de `FWI/KBDI` no pipeline + **teste obrigatório** de presença de ambos os índices no output final | Alta | [PREENCHER] | Ausente |

## 5. Notas de Preenchimento

- Preencher cada linha apenas com evidência verificável.
- Não marcar itens como implementados sem referência concreta.
- Sempre ligar tarefas ao plano em `docs/implementation/v1/v1-implementation-plan.md`.
