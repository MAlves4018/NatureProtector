# Validation gates

Esta pagina lista os gates locais usados para o freeze candidate. Eles provam funcionamento local reproducivel; nao provam readiness cloud, producao, carga, stress ou calibracao cientifica.

## Qualidade de repo

Executar a partir da raiz:

```powershell
dotnet restore .\NatureProtector.sln
dotnet build .\NatureProtector.sln --configuration Release --no-restore
dotnet test .\NatureProtector.sln --configuration Release --no-build
```

Frontend:

```powershell
cd .\webUI
npm ci
npm run check:toolchain
npm run typecheck
npm test
npm run build
```

Se `npm run test:e2e` for executado, o runtime local deve estar ativo. Quando o smoke UI do harness funcional for usado em vez do E2E completo, isso deve ficar registado na evidencia.

## Validacao funcional local

Sequencia suportada:

```powershell
.\scripts\np.ps1 init-local -Force
.\scripts\np.ps1 prepare-local
.\scripts\np.ps1 clean-local
.\scripts\np.ps1 doctor
.\scripts\np.ps1 up
.\scripts\np.ps1 start
.\scripts\np.ps1 health
.\scripts\validation\Invoke-LocalFunctionalValidation.ps1 -Full -Evidence -Ui
.\scripts\np.ps1 stop
.\scripts\np.ps1 down
```

O harness deve validar login local, endpoints protegidos com token, `scenario_b`, `scenario_c` com `missing-readings`, comparacao B/C, DB, RabbitMQ, Prevention Host, UI smoke e shutdown.

