# Tasks README (V1)

## Objetivo

Definir padrão único para criação de tarefas executáveis a partir do plano V1.

## Estado Atual de IDs

- Tarefas ativas usam o padrão `V1-000`, `V1-001`, `V1-002`, etc.
- Os ficheiros bootstrap com padrão `V1-TASK-*` foram arquivados em `docs/implementation/v1/tasks/archive/bootstrap-placeholders/` para evitar ambiguidade.

## Estrutura Recomendada de Tarefa

| Campo | Obrigatório | Descrição |
|---|---|---|
| Task ID | Sim | Identificador único (ex.: `V1-001`) |
| Título | Sim | Nome curto e objetivo |
| Origem no Proposal | Sim | Secção/requisito de origem |
| Gap associado | Sim | Referência à matriz de gaps |
| Escopo | Sim | O que entra e o que fica fora |
| Critérios de aceitação | Sim | Itens testáveis |
| Dependências | Não | Bloqueios/predecessoras |
| Evidências esperadas | Sim | Quais provas devem ser anexadas |
| Estado | Sim | Backlog / Em curso / Concluída |

## Template de Tarefa (copiar e preencher)

```md
# V1-XXX - <titulo>

## Contexto

## Origem no Proposal

## Gap associado

## Escopo
- In:
- Out:

## Critérios de aceitação
- [ ]
- [ ]

## Evidências esperadas
- 

## Dependências
- 

## Estado
Backlog
```

## Convenções

- Não declarar implementação concluída sem evidência.
- Ligar cada tarefa a uma linha da matriz `Proposal -> Repo -> Gap -> Tarefa`.
- Atualizar estado apenas após validação contra DoD.
