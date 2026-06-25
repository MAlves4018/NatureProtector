# G9 pre-integration runbook

## Inputs

- owner-provided baseline archive;
- G8.2 repository archive;
- public repository identity `MAlves4018/NatureProtector`;
- no GCP projects or secrets.

## Validation sequence

```powershell
python scripts/cloud/Test-G9Convergence.py
python scripts/cloud/Test-G81Static.py
python scripts/cloud/Test-G82Static.py
python scripts/cloud/Test-G82Adversarial.py --repository-root . --output artifacts/g82-adversarial.json
```

Then execute, in the owner environment:

```powershell
dotnet tool restore
dotnet restore .\NatureProtector.sln --configfile NuGet.Config
dotnet build .\NatureProtector.sln --no-restore -c Release
dotnet test .\NatureProtector.sln --no-build -c Release -m:1
cd webUI
npm ci
npm run typecheck
npm test -- --run
npm run build
```

Terraform roots must be checked with Terraform 1.15.6 using `init -backend=false`, `fmt -check` and `validate`. No `apply` is permitted during G9 or G10.

## Expected result

```text
G9_REPOSITORY_CONVERGENCE_IMPLEMENTED
INTEGRATION_CANDIDATE
CLOUD_NOT_PROVISIONED
PRODUCTION_NO_GO
```
