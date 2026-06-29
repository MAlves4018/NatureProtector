---
id: NP-HOWTO-STAGING
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: repository code and configuration
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# How to Review a Staging Deployment

1. Confirm that a successful immutable release exists for the intended reference.
2. Use **Deployments** to request a staging plan.
3. Review environment, release/run identity, deployment mode and confirmation phrase.
4. Dispatch staging deployment only with the `Operations` capability.
5. Observe the workflow and collect smoke, readiness, manifest and provenance evidence.
6. Do not promote production unless staging evidence is current, complete and associated with the exact release.
7. Use the rollback operation with a known release target when the qualification gate fails.
