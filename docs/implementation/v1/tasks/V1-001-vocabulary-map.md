# V1-001 - Vocabulary Map

## ID

V1-001

## Título

Mapa de vocabulário Proposal -> repositório (V1)

## Fonte Proposal

`Proposal.pdf` ([CONFIRMAR secções exatas])

## Estado atual

Parcial

## Gap

Vocabulário do Proposal e nomes atuais do repositório ainda sem mapa operativo único.

## Objetivo

Definir mapeamento controlado de termos e decisões de evolução (`manter`, `criar`, `renomear mais tarde`, `alias legado`, `não implementar agora`) com foco em compatibilidade.

## Modelo/raciocínio recomendado para Codex

Codex, reasoning medium

## Modo: plan first ou implementar diretamente

Plan first

## Ficheiros prováveis

- `docs/contracts/v1-vocabulary-map.md`
- `docs/implementation/v1/v1-proposal-to-implementation-map.md` ([CONFIRMAR ligação por ID])
- `docs/implementation/v1/v1-implementation-plan.md` ([CONFIRMAR referência de workstream])

## Contrato alvo

Vocabulário V1 consolidado com aliases de transição definidos.

## Pseudocódigo

```text
1) Ler termos do Proposal alvo
2) Mapear para nomes atuais no repo
3) Classificar decisão por termo (manter/criar/renomear mais tarde/alias legado/não implementar agora)
4) Registar regras de compatibilidade sem rename destrutivo
5) Listar pendências com próxima ação
```

## Testes obrigatórios

- Teste documental obrigatório: cada termo da lista mínima aparece no mapa com decisão explícita.
- Teste de consistência obrigatório: `RiskScore` marcado como ativo atual.
- Teste de política obrigatório: nenhuma decisão implica rename destrutivo imediato.

## O que não alterar

- Não alterar código.
- Não alterar contratos RabbitMQ.
- Não executar renames destrutivos.

## Comandos de validação

```text
[CONFIRMAR] (validação documental/manual)
```

## Critério de pronto

- `docs/contracts/v1-vocabulary-map.md` criado/atualizado com todos os termos mínimos.
- Decisão explícita por termo.
- Lista de pendências com impacto e próxima ação.

## Riscos

- Ambiguidade de naming em termos ainda não auditados no código.
- Sobreposição temporária de campos durante coexistência de contrato.

## Limite de escopo

- Apenas documentação e decisão de mapeamento.
- Sem implementação técnica de contratos.

## Quando subir de reasoning medium para high

- Só subir para `high` se surgirem conflitos reais entre múltiplos documentos/artefactos que bloqueiem decisão de mapeamento.
