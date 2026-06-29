---
id: NP-CURRENT-STATE
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: repository code and configuration
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# Current Project State

## Executive statement

NatureProtector is an academic, experimental and auditable platform for controlled simulation and processing of environmental information associated with rural-fire risk. It combines a local event-driven runtime, persistent control and projection data, a role-aware Web UI, evidence-oriented quality workflows and a cloud/deployment implementation that remains subject to runtime proof.

The platform must not be represented as an official warning system, a scientifically calibrated wildfire model, an operational digital twin or an authorised replacement for IPMA, ICNF, ANEPC or municipal decision processes.

## Implemented capability groups

1. **Simulation and runtime** - scenario-based generation of sensor readings, run identities, degradation profiles and local orchestration.
2. **Event processing** - RabbitMQ transport, durable inbox, processing attempts, retry, rejection, quarantine and projection updates.
3. **Risk and eligibility** - semantic validation, quality flags, eligibility before scoring, candidate NatureProtector score and candidate comparative indices.
4. **Persistence** - PostgreSQL as the principal system of record; InfluxDB/Grafana as temporal observability support.
5. **Backoffice and UI** - operational views, simulation, runs, risk, pipeline, evidence, quality and engineering operations.
6. **Operations Control Plane** - a closed catalog for quality, evidence, deployment and cloud operations, with server-side capabilities, confirmation, approval and audit records.
7. **Engineering quality** - backend/frontend tests, architecture checks, security scans, package/release workflows and evidence tooling.
8. **Cloud/CD implementation** - Terraform, GKE/Autopilot, Cloud Deploy, Artifact Registry, Workload Identity Federation, signed-release and environment workflows.

## Evidence interpretation

Implementation and proof are different. The current snapshot supports the following wording:

| Area | Current interpretation |
|---|---|
| Source code and configuration | Implemented and statically inspectable |
| Frontend quality gates in the supplied integration result | Proved for that controlled environment |
| Backend/full-stack runtime in the present workspace | Requires execution in an environment with .NET, containers and dependencies |
| Signed release for the current final head | Not proved by the supplied package alone |
| Staging deployment | Not proved in the supplied artifacts |
| Production deployment | Not deployed/proved |
| Scientific or territorial validation | Not completed |
| Operational authorisation | Not granted |

## Canonical narrative

The strongest defensible contribution is not a claim of predicting fires. It is the integration of explicit boundaries between simulated truth, observed values, data quality, eligibility, technical failure, candidate assessment, evidence and authority. This makes it possible to represent complete, partial and blocked information without interpreting missing data as low risk.

## Snapshot rule

This document describes the repository snapshot delivered on 2026-06-28. Later cloud runs, signed releases or report revisions must be incorporated through a new verification event rather than silently changing this record.
