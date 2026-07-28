---
title: Final delivery status
status: current
owners: engineering, QA, delivery
source_of_truth: config/acceptance/final-delivery.json
last_verified_at: 2026-07-22
---

# NatureProtector final delivery status

## Implemented in the repository

The repository now contains a closed five-layer delivery path:

1. generated functional catalogue and traceability;
2. canonical acceptance orchestration;
3. P0 runtime coverage;
4. live UI, accessibility, limiter and bounded-performance coverage;
5. final delivery gating and package verification.

The final layer is implemented by `scripts/release/Invoke-NP-FinalDelivery.ps1`. It requires a clean Git source, binds acceptance evidence to the same commit and source fingerprint, executes or strictly verifies a `Full / PASS` campaign, builds the release candidate and then runs clean-install, tamper-detection and functional-package-smoke gates.

## Proven in the construction workspace

The construction workspace has proved the static contracts, synthetic fail-closed behaviour and repository audits. It has not produced a live final-delivery `PASS`, because this environment does not provide PowerShell, .NET, Docker or PostgreSQL tooling.

Therefore, the current truthful status is:

```text
FINAL_DELIVERY_HARNESS_READY
LIVE_FINAL_DELIVERY_PASS_NOT_EXECUTED_HERE
```

No historical B/C, P3, performance or release output is promoted to current evidence.

## Required live closeout

On the equipped Windows repository, after committing the Phase 5 source and confirming a clean tree:

```powershell
$env:NP_RELIABILITY_AUTH_TOKEN = '<runtime token>'
.\scripts\release\Invoke-NP-FinalDelivery.ps1 -Mode Execute
```

The project is locally deliverable only when the command ends with:

```text
NATUREPROTECTOR_FINAL_DELIVERY=PASS
```

and `scripts/release/verify_final_delivery.py --require-pass` accepts the generated dossier.

## Claim boundary

A `PASS` proves the versioned local acceptance and package contracts for the recorded commit and environment. It does not prove production/cloud authorization, scientific calibration of the candidate risk model, external user validation or operational readiness outside the tested local topology.

## External weather-data boundary

The IPMA integration remains **observability-only**. It writes provenance-rich external observations for dashboards and evidence; it is not an input to RabbitMQ, Prevention or the candidate risk calculation, and the final delivery gate does not raise that scientific claim.
