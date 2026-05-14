# V1-002 - Event Catalog

## ID

V1-002

## Título

Catálogo de eventos V1 (atuais, parciais e futuros)

## Fonte Proposal

`Proposal.pdf` ([CONFIRMAR secções de eventos/fluxo])

## Estado atual

Parcial

## Gap

Não existe catálogo único com estado e prioridade para eventos V1.

## Objetivo

Consolidar catálogo de eventos com produtor, consumidor, estado (`ativo`, `parcial`, `alvo`, `futuro`), payload, relação com Proposal, impacto em RabbitMQ e prioridade.

## Modelo/raciocínio recomendado para Codex

Codex, reasoning medium

## Modo: plan first ou implementar diretamente

Plan first

## Ficheiros prováveis

- `docs/contracts/event-catalog.md`
- `src/NatureProtector.Shared/Messaging/EventTypes.cs` (referência)
- `src/NatureProtector.Shared/Messaging/EventEnvelope.cs` (referência)
- `src/NatureProtector.Shared/Messaging/RoutingKeys.cs` (referência)
- `src/NatureProtector.Shared/Messaging/NatureProtectorRabbitMqTopology.cs` (referência)

## Contrato alvo

Catálogo documental V1 de eventos, sem alteração de contratos RabbitMQ.

## Pseudocódigo

```text
1) Levantar eventos formais atuais em Shared/Messaging
2) Mapear produtores e consumidores conhecidos
3) Classificar estado por evento (ativo/parcial/alvo/futuro)
4) Distinguir OperationalEvent (conceito) de EventEnvelope<TPayload> (implementação)
5) Registar pendências de contexto com [CONFIRMAR]
```

## Testes obrigatórios

- Teste documental obrigatório: cada evento listado contém todos os campos exigidos.
- Teste de consistência obrigatório: `OperationalEvent` e `EventEnvelope<TPayload>` aparecem como itens distintos.
- Teste de política obrigatório: nenhum item exige alteração de RabbitMQ nesta fase.

## O que não alterar

- Não alterar contratos RabbitMQ.
- Não criar novos eventos no código.
- Não alterar código de produção/testes nesta tarefa.

## Comandos de validação

```text
[CONFIRMAR] (validação documental/manual)
```

## Critério de pronto

- `docs/contracts/event-catalog.md` criado com eventos atuais/parciais/futuros.
- Campos obrigatórios preenchidos por evento.
- Lista de pendências e próximos passos documentada.

## Riscos

- Produtores/consumidores incompletos sem auditoria adicional dirigida.
- Diferença entre evento formal e código de alerta pode gerar ambiguidade.

## Limite de escopo

- Apenas catálogo documental e decisões de classificação.
- Sem implementação de novos eventos.

## Quando subir de reasoning medium para high

- Só subir para `high` se houver conflito real entre múltiplos documentos/artefactos que bloqueie a classificação dos eventos.
