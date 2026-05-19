# Relatório NatureProtector

## 1. Introdução

- Contextualizar o NatureProtector como sistema de simulação e apoio operacional para risco de incêndio.
- Explicar a motivação da V1: demonstrar cadeia técnica e metodológica, não validação científica final.
- Indicar o período de progresso coberto e a relação com a apresentação de 2026-05-22.

## 2. Objetivos e escopo

- Definir objetivos obrigatórios da V1: cenários, ingestão, risco, projeções, UI/evidência.
- Separar o que está dentro da demo do que fica para dia 1 ou trabalho futuro.
- Explicitar que RBAC, relatório final e polish UI ainda podem estar em curso.

## 3. Arquitetura

- Descrever Simulator Host, RabbitMQ, Prevention Host, PostgreSQL, Backoffice API, webUI e Influx/Grafana.
- Mostrar fronteiras entre simulação, transporte, processamento e apresentação.
- Referenciar contratos principais e persistência por `SimulationRunId`.

## 4. Modelo de simulação

- Explicar `ScenarioDefinition`, `TruthSnapshot`, `LocalObservation` e payload publicado.
- Descrever scenario B como baseline severa plausível.
- Descrever scenario C como degradação operacional, não terceiro clima.
- Referenciar `degradation-profiles-plan.md`.

## 5. Pipeline de ingestão e processamento

- Documentar `EventEnvelope<SensorReadingProducedPayload>`, RabbitMQ, inbox durável e processing attempts.
- Explicar validação técnica/semântica e bloqueios antes do scoring.
- Descrever retry, quarantine e recovery de `Processing`.

## 6. RiskInput e scoring V1

- Explicar `RiskInput` como fronteira entre pipeline e scoring.
- Distinguir dados completos, parciais e bloqueados.
- Dizer explicitamente que o score V1 é candidate parameter set, não calibração científica.

## 7. Alertas e projeções operacionais

- Descrever `RiskAssessment`, `AlertState`, `OperationalProjection` e estado por área.
- Explicar thresholds/histerese se já estiverem implementados.
- Indicar limitações conhecidas em cooldown/persistência mínima, se aplicável.

## 8. Interface, dashboards e evidência

- Descrever Backoffice API, webUI/Runtime Monitor e Grafana/Influx.
- Indicar quais ecrãs serão usados na apresentação.
- Explicar que evidência técnica principal está no evidence pack.

## 9. Segurança, roles e auditoria

- Descrever o estado de RBAC/roles sem marcar como feito se ainda estiver em curso.
- Explicar rastreabilidade por `SimulationRunId`, metadata de run e logs.
- Incluir perguntas em aberto ao colega responsável por RBAC.

## 10. Validação técnica

- Listar comandos de build e testes usados.
- Incluir limitações: warnings conhecidos, logs antigos, infraestrutura local.
- Distinguir testes técnicos de validação científica.

## 11. Resultados das runs A/B/C

- Para A, indicar estado atual ou ausência de evidência recente.
- Para B, usar run `d8203d4b-1839-4908-87ef-05633c1f1ae5`: 30/30, 0 rejected, 0 quarantined.
- Para C, usar run `36caca67-352c-41f1-80e3-8fe951a1582c`: 24/30, 0 rejected, 0 quarantined.
- Incluir tabela B vs C.

## 12. Limitações

- FWI/KBDI e score como contexto/candidate set, não validação final.
- Degradation profiles adicionais ainda por implementar.
- UI/runtime monitor pode precisar polish.
- RBAC e relatório final podem estar incompletos.

## 13. Trabalho futuro

- Implementar profiles P1 seguros: noisy, stuck, duplicate.
- Fechar RBAC mínimo.
- Curar dashboards e screenshots.
- Preparar demo repetível e documentação final.

## 14. Conclusão

- Resumir o que está demonstrável.
- Dizer o que falta até dia 1.
- Reforçar que a V1 está tecnicamente demonstrável com reservas metodológicas explícitas.
