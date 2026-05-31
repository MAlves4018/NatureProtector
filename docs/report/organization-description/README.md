# NatureProtector — Descrição da Organização do Repositório

Template LaTeX para o documento curto de descrição da organização do repositório.

## Compilar

A partir da pasta `docs/organization-description`:

```powershell
latexmk -pdf organization-description.tex
```

Ou a partir da raiz do repositório:

```powershell
latexmk -pdf -cd docs/organization-description/organization-description.tex
```

Também podes usar:

```powershell
.\docs\organization-description\build-organization-description.ps1
```

## Atualizar antes da entrega

- Confirmar URL do repositório.
- Confirmar branch/tag/commit de entrega.
- Confirmar estrutura real de `src/`, `tests/`, `docs/`, `infra/` e `scripts/`.
- Atualizar comandos de execução se forem diferentes.
- Remover placeholders `[A preencher: ...]`.
