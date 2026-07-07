# Script inventory

`scripts\np.ps1` e o entrypoint recomendado para clone-to-run local e freeze candidate.

Classificacao usada na Fase 5:

| Classe | Significado |
| --- | --- |
| `CANONICAL` | Entrada recomendada ou harness funcional local principal. |
| `USED_BY_CANONICAL` | Script chamado pelo fluxo local canonico. |
| `LEGACY_COMPAT` | Mantido por compatibilidade; nao deve ser removido sem fase dedicada. |
| `EVIDENCE_ONLY` | Recolha/empacotamento de evidencia. |
| `DEV_HELPER` | Ferramenta auxiliar de desenvolvimento, dados, testes ou qualidade. |
| `DEAD_CANDIDATE` | Backup, ficheiro quebrado ou artefacto obsoleto candidato a limpeza. |
| `DANGEROUS_OR_STALE` | Script cloud/destrutivo/staging que exige aprovacao explicita. |
| `UNCERTAIN_DO_NOT_REMOVE` | Uso atual nao provado nesta fase; manter. |

## Superficies locais principais

- `scripts\np.ps1`: canonico.
- `scripts\validation\Invoke-LocalFunctionalValidation.ps1`: harness funcional local.
- `scripts\setup\New-LocalDotEnv.ps1`: gera `.env` local.
- `scripts\docker\*.ps1`: infraestrutura Docker scoped ao projeto.
- `scripts\runtime\*.ps1`: arranque, health e stop de runtime persistente local.
- `scripts\workspace.ps1`: compatibilidade.
- `scripts\dev\start-local-runtime.ps1`: compatibilidade de baixo nivel.

Nao remover scripts classificados como legacy, cloud-only ou incertos sem uma fase dedicada de cleanup.

