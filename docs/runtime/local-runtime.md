# Runtime local

Este documento fixa o runtime local suportado para o freeze candidate.

## Entrada canonica

Usar sempre:

```powershell
.\scripts\np.ps1 doctor
.\scripts\np.ps1 init-local -Force
.\scripts\np.ps1 prepare-local
.\scripts\np.ps1 clean-local
.\scripts\np.ps1 up
.\scripts\np.ps1 start
.\scripts\np.ps1 health
```

Para terminar:

```powershell
.\scripts\np.ps1 stop
.\scripts\np.ps1 down
```

`scripts\workspace.ps1` permanece como compatibilidade. Novos fluxos devem usar `scripts\np.ps1`.

## Servicos persistentes

O runtime persistente local e composto por:

- `Backoffice.Api`;
- `Prevention.Host`;
- `webUI`.

O `Simulator.Host` nao e servico persistente. Ele e lancado por run pela API/UI do Run Orchestrator e deve terminar no fim da run.

## Ambiente Development

O arranque local deve definir explicitamente:

```text
ASPNETCORE_ENVIRONMENT=Development
DOTNET_ENVIRONMENT=Development
BackofficeApi__LocalRuntimeProcessLaunchEnabled=true
```

Isto evita arrancar a API local com o lancamento de runtime desligado.

## `.env` local

`.\scripts\np.ps1 init-local -Force` cria `.env` a partir de `.env.example`, gera `INFLUXDB_TOKEN` com prefixo `apiv3_` e garante:

```text
NP_BOOTSTRAP_ADMIN_USERNAME=admin
NP_BOOTSTRAP_ADMIN_PASSWORD=admin123
```

O ficheiro `.env` e local e nao deve ser versionado.

