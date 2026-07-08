# Evidence collection

Evidencia local do freeze candidate deve ficar fora do repo principal, em:

```text
C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector.brain\post-beta\FreezeCandidate
```

Para a Fase 5, usar:

```text
05-freeze-candidate\<UTC-RUN-ID>
```

## Conteudo minimo

Cada run folder deve conter:

- comandos executados;
- logs por comando;
- inventario de ficheiros alterados;
- inventario/classificacao de scripts;
- resumo de testes;
- resumo de validacao funcional;
- blockers e riscos aceites;
- manifest de evidencia.

Segredos nao devem ser impressos em excesso. `.env` nao deve ser copiado para pacotes de evidencia.

