---
title: Final delivery execution
status: current
owners: engineering, QA, delivery
source_of_truth: scripts/release/Invoke-NP-FinalDelivery.ps1 and config/acceptance/final-delivery.json
last_verified_at: 2026-07-22
review_triggers: acceptance, release packaging, package smoke or delivery evidence changes
---

# Final delivery execution

`Invoke-NP-FinalDelivery.ps1` is the last local gate before delivery. It does not turn old evidence into a new result and it does not build a release archive before the current source snapshot has a strict `Full / PASS` acceptance proof.

## Required source state

An executable final delivery requires:

- a real Git repository;
- a clean working tree, including untracked files;
- an identifiable commit;
- a deterministic source fingerprint;
- PowerShell, Python, .NET, Node, npm, Docker, Git and `psql`;
- `NP_RELIABILITY_AUTH_TOKEN` configured for controlled P3;
- the local runtime explicitly operating as non-production.

The acceptance campaign records the Git commit, clean-tree status and source fingerprint. The finalizer recalculates the fingerprint and rejects an acceptance result produced from another commit or another source tree.

## Plan

Generate preflight evidence without executing acceptance or packaging:

```powershell
.\scripts\release\Invoke-NP-FinalDelivery.ps1 -Mode Plan
```

`PLAN_ONLY` is not a delivery pass.

## Execute the complete closeout

From a committed and clean repository:

```powershell
$env:NP_RELIABILITY_AUTH_TOKEN = '<runtime token>'

.\scripts\release\Invoke-NP-FinalDelivery.ps1 -Mode Execute
```

The finalizer executes these gates in order:

1. preflight and source identity;
2. canonical `Full` acceptance, with P3 execution and non-production acknowledgement;
3. strict acceptance-evidence verification;
4. release-candidate build;
5. clean-install and checksum verification;
6. deliberate package tamper detection;
7. functional smoke of the packaged application.

A failure or absent prerequisite stops later gates. In particular, the package build never runs after a non-passing acceptance campaign.

## Finalize an existing current acceptance run

A current `Full / PASS` run can be reused only when it is below `artifacts/final-acceptance/`, was produced from the same clean commit and has the same source fingerprint:

```powershell
$env:NP_RELIABILITY_AUTH_TOKEN = '<runtime token>'

.\scripts\release\Invoke-NP-FinalDelivery.ps1 `
  -Mode FinalizeExisting `
  -AcceptanceRunRoot .\artifacts\final-acceptance\<run-id>
```

The run is reverified. Merely pointing at a directory does not mark the acceptance gate as valid.

## Delivery output

Every attempt writes below:

```text
artifacts/final-delivery/<run-id>/
```

The dossier includes:

```text
preflight.json
source-identity.json
acceptance-verification.json
acceptance-proof/
final-delivery-summary.json
FINAL-DELIVERY.md
delivery-gates.csv
delivery-manifest.csv
hashes.sha256
release/
gates/
clean-install/
tamper-detection/
functional-package-smoke/
```

The release archive and its external SHA-256 file remain inside the run-scoped dossier. `acceptance-proof/` contains the acceptance identity, stage ledger and hash contracts needed to link the final package to the verified campaign.

## Status contract

| Status | Exit | Meaning |
|---|---:|---|
| `PASS` | 0 | Every delivery gate passed from the same clean Git snapshot. |
| `FAIL` | 1 | A gate executed and violated its contract. |
| `BLOCKED_PREREQUISITE` | 2 | A tool, token, clean source or current acceptance input was absent. |
| `HARNESS_ERROR` | 3 | The finalizer could not execute or reconcile a gate safely. |
| `PLAN_ONLY` | 0 | A plan was written; no delivery claim exists. |

A package or ZIP is not final merely because it was created. The only final local claim is `NATUREPROTECTOR_FINAL_DELIVERY=PASS` together with a valid dossier and matching hashes.

## Independent verification

Verify a generated dossier:

```powershell
python .\scripts\release\verify_final_delivery.py `
  .\artifacts\final-delivery\<run-id> `
  --config .\config\acceptance\final-delivery.json `
  --require-pass
```

The verifier rejects missing files, stale manifests, invalid gate order, mismatched package hashes, absent acceptance proof or a package outside the delivery dossier.
