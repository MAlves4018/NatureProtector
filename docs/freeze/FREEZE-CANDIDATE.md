# Freeze candidate local

Estado pretendido: freeze candidate local reproducivel, sem claims de producao ou cloud readiness.

## Caminho local

```text
C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector
```

## Sequencia canonical

```powershell
.\scripts\np.ps1 doctor
.\scripts\np.ps1 init-local -Force
.\scripts\np.ps1 clean-local
.\scripts\np.ps1 up
.\scripts\np.ps1 start
.\scripts\np.ps1 health
.\scripts\validation\Invoke-LocalFunctionalValidation.ps1 -Full -Evidence -Ui
.\scripts\np.ps1 stop
.\scripts\np.ps1 down
```

## Criterios locais

- `np.ps1` e o entrypoint recomendado.
- `.env` e gerado localmente por `init-local`.
- Limpeza local usa comandos scoped ao compose NatureProtector.
- Runtime persistente local: API, Prevention Host e webUI.
- `Simulator.Host` e one-shot por run, nao servico persistente.
- `scenario_b` limpo e `scenario_c` degradado com `missing-readings` passam no harness funcional.
- Cloud fica em preflight/design ate aprovacao separada.
- Phase 6 cloud readiness is limited to static/read-only preflight. It does not
  prove cloud smoke, production readiness, capacity, or scientific calibration.

## Evidencia

Evidencia de freeze candidate fica em:

```text
C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector.brain\post-beta\FreezeCandidate\05-freeze-candidate\<UTC-RUN-ID>
```
