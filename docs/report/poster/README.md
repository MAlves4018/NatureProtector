# NatureProtector Poster V1

Template LaTeX A3 landscape para cartaz do NatureProtector.

## Estrutura

```text
poster/
├── poster.tex
├── config/poster-style.tex
├── content/poster-content.tex
├── assets/
├── figures/
└── references/
```

## Compilar

A partir da raiz do repositório:

```powershell
latexmk -pdf -cd docs/report/poster.tex
```

Ou dentro de `docs/poster`:

```powershell
latexmk -pdf poster.tex
```

## Notas

- O template é independente do relatório.
- Não usa `fncychap`, `minitoc` nem `glossaries`.
- Para logotipo, colocar `assets/logo.png`.
- Para diagramas, colocar ficheiros em `figures/` e usar `\includegraphics`.
- O cartaz é A3 horizontal por defeito.
