# V1-005 - Quality Flags

## ID

V1-005

## Título

Catálogo V1 de QualityFlags (contrato de domínio/prevenção)

## Objetivo

Planear a introdução de um catálogo V1 de `QualityFlags` na camada de domínio/prevenção, sem alterar ainda toda a pipeline.

## Flags mínimas

- `MissingValue`
- `Outlier`
- `BiasSuspected`
- `DriftSuspected`
- `StuckValue`
- `ClippedValue`
- `Duplicate`
- `Delayed`
- `Stale`
- `OutOfOrder`
- `SemanticMismatch`
- `TransportFailure`

## Modelo/raciocínio recomendado para Codex

Codex, reasoning medium

## Modo

Plan first

## Estado de execução atual

Parcial, validação pendente.

## Resultado atual da slice

- `QualityFlags` está disponível de forma temporária como `List<string>`.
- Presente em `RiskEligibilityResult`.
- Presente em `ClassifierResult`.
- Contrato canónico de flags ainda pendente.
- Build/test global ainda não validado por limitação ambiental de `NuGet.Config`.

## Ficheiros prováveis

Produção:
- `src/NatureProtector.Prevention/Readings/*`
- `src/NatureProtector.Prevention/Risk/*`

Testes:
- `tests/NatureProtector.Prevention.Tests/*`

## Plano de implementação proposto (sem executar)

### Fase 1 - Definir contrato de flags
- Definir tipo de domínio para conjunto de flags (`QualityFlagSet` ou equivalente) [CONFIRMAR nome final].
- Garantir representação canónica das 12 flags mínimas.

### Fase 2 - Regras de composição (sem bloquear)
- Definir semântica de composição/acumulação de flags.
- Garantir que criar flags não bloqueia leitura por si só.

### Fase 3 - Anexação ao domínio de risco/elegibilidade
- Permitir anexar flags ao resultado de elegibilidade (`EligibilityResult` ou equivalente) [CONFIRMAR tipo exato].
- Não alterar lógica de score nesta slice.

## Pseudocódigo

```text
flags = QualityFlagSet.Empty()
flags = flags.Add(MissingValue)
flags = flags.Add(Outlier)
eligibility = eligibility.WithQualityFlags(flags)
```

## Testes obrigatórios

- `CanCreateQualityFlagSet`
- `QualityFlagSet_DoesNotDuplicateFlags`
- `QualityFlags_CanBeAttachedToEligibilityResult`

## Regras e restrições

- Não bloquear leituras só por criar flags.
- Não alterar RabbitMQ.
- Não alterar score.
- Não criar BD/migração nesta slice.

## O que não alterar

- Contratos/topologia RabbitMQ em `src/NatureProtector.Shared/Messaging/*`.
- Algoritmo de scoring em `src/NatureProtector.Prevention/Risk/*` (exceto anexação passiva de flags, se necessária).
- Persistência/migrações de base de dados.

## Critério de pronto (da tarefa de planeamento)

- Catálogo mínimo de 12 flags definido.
- Estratégia de composição sem duplicados documentada.
- Estratégia de anexação a elegibilidade documentada.
- Testes obrigatórios listados por nome.
- Estado da tarefa: parcial; não validada globalmente enquanto build/test não passarem.

## Riscos

- Ambiguidade entre flags semânticas e flags de transporte se não houver fronteira clara.
- Acoplamento com elegibilidade pode introduzir efeitos indiretos no fluxo.
- Nome/tipo final pode divergir do que já existe parcialmente no domínio.

## Limite de escopo

- Apenas planeamento de contrato de flags no domínio/prevenção.
- Sem implementação de pipeline completa.
- Sem alterações de persistência.

## Quando subir de reasoning medium para high

- Subir para `high` apenas se surgir conflito técnico real entre contrato de flags e desenho atual de elegibilidade/risco que exija redesenho estrutural.
