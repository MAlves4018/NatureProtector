# V1 Vocabulary Map

## Objetivo

Mapear termos do Proposal para os nomes atuais do repositório e definir a decisão de evolução com foco em compatibilidade.

## Regras operacionais

- Não fazer renames destrutivos nesta fase.
- Priorizar alias e coexistência temporária.
- Tratar `RiskScore` como ativo atual.
- Quando houver conflito entre docs e código, marcar pendência e validar antes de mudar contrato.

## Mapa Proposal -> Repo -> Decisão

| Termo Proposal | Nome atual no repo (estado) | Decisão | Regra de compatibilidade | Nota |
|---|---|---|---|---|
| TruthSnapshot | [CONFIRMAR] | criar | Introduzir contrato novo sem quebrar fluxo atual | Gap conhecido: ausente |
| LocalObservation | [CONFIRMAR] | criar | Introduzir contrato novo com adaptador de entrada | Gap conhecido: ausente |
| OperationalEvent | `EventEnvelope<TPayload>` (parcial) | alias legado | Manter envelope e adicionar payload canónico | Não remover envelope atual |
| EventEnvelope<TPayload> | `EventEnvelope<TPayload>` (ativo) | manter | Base de transporte atual | Ativo atual |
| NormalizedReading | `NormalizedReading` (parcial) | manter | Estender campos sem breaking change imediato | Requer flags/classificação |
| RiskInput | `RiskInput` (mínimo) | manter | Expandir incrementalmente | Estado parcial |
| RiskInputStatus | `RiskInputStatus` (ativo no domínio de risco) | manter | Enum introduzido com `CompleteEligible/PartialButUsable/Blocked`; manter compatibilidade no consumo | Implementado na slice V1-007 |
| RiskAssessment | `RiskAssessment` com `RiskScore/RiskLevel` | renomear mais tarde | Introduzir campos-alvo em paralelo | Evitar rename destrutivo agora |
| RiskScore | `RiskScore` (ativo atual) | manter | Tratar como campo ativo atual | Não marcar como legado implementado |
| base_risk | [CONFIRMAR] | criar | Adicionar como campo V1 paralelo | Convergência gradual |
| adjusted_score | [CONFIRMAR] | criar | Adicionar como campo V1 paralelo | Convergência gradual |
| AlertState | alerta simples (parcial) | manter | Evoluir estados por versão | Sem alertas finais antes de RiskAssessment V1 |
| OperationalProjection | `OperationalProjection` (parcial) | manter | Completar por extensão | Estado parcial |
| AggregateRiskScore | [CONFIRMAR] | criar | Introduzir contrato/campo agregado versionado | Gap conhecido |
| area_aggregated_score | [CONFIRMAR] | alias legado | Alias de transição com `AggregateRiskScore` | Padronização posterior |
| QualityFlags | `QualityFlags` temporário como `List<string>` em `RiskEligibilityResult` e `ClassifierResult` | alias legado | Manter formato temporário enquanto contrato canónico não é definido | Parcial/temporário |
| ClassifierResult | `ClassifierResult` (ativo no domínio de risco) | manter | Contrato canónico criado com agregação passiva para elegibilidade | Implementado com validação pendente |
| CompleteEligible | `RiskInputStatus.CompleteEligible` | manter | Estado ativo no domínio com compatibilidade de API atual | Implementado na slice V1-007 |
| PartialButUsable | `RiskInputStatus.PartialButUsable` | manter | Estado ativo no domínio com compatibilidade de API atual | Implementado na slice V1-007 |
| Blocked | `RiskInputStatus.Blocked` | manter | Estado ativo no domínio; regra final de assessment ainda pendente | Implementado com decisão pendente |

## Decisões pendentes

| ID | Pendência | Impacto | Próxima ação |
|---|---|---|---|
| VOC-P01 | Confirmar localização e nome final de `TruthSnapshot` no código | Médio | Auditoria dirigida ao módulo de contratos |
| VOC-P02 | Confirmar localização e nome final de `LocalObservation` | Médio | Auditoria dirigida ao módulo de ingestão |
| VOC-P03 | Validar cobertura final de integração de `RiskInputStatus` no fluxo completo | Médio | Consolidar validação quando build/test passarem no ambiente |
| VOC-P04 | Decidir estratégia de coexistência `RiskAssessment` atual vs campos `base_risk/adjusted_score` | Alto | RFC curta de migração sem breaking change |
| VOC-P05 | Confirmar naming final `AggregateRiskScore` vs `area_aggregated_score` | Médio | Definir nome canónico e manter alias legado |
| VOC-P06 | Definir contrato canónico de `QualityFlags` (sair de `List<string>` temporário) e validar integração final de `ClassifierResult` no fluxo completo | Alto | Especificação V1 + validação após build/test limpos |
| VOC-P07 | Fechar decisão de `Blocked` no assessment (`ausência` vs `campos numéricos nulos`) | Alto | Decisão de contrato antes de persistência |
