# Shared PowerShell tooling

`NatureProtector.Tooling` contains small, side-effect-free primitives used by repository entrypoints.

Rules:

- entrypoint scripts keep their existing parameters and orchestration responsibilities;
- domain, cloud-policy and workflow-specific helpers remain local;
- shared functions use the `Np` noun prefix to avoid global name collisions;
- importing the module must not read `.env`, execute external commands or mutate files;
- consumers pass behavior explicitly, such as required repository sentinels, quote handling and environment precedence.

Import from a script below `scripts/<area>`:

```powershell
Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force
```
