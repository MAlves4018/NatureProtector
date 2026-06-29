---
id: NP-EXPLAIN-EVIDENCE
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: repository code and configuration
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# Explanation: Evidence and Authority

NatureProtector separates four questions that are often conflated:

1. Does code exist?
2. Did a check run?
3. Does the result support this exact claim and snapshot?
4. Is the system authorised for the intended use?

A release attestation supports supply-chain provenance, not scientific validity. A successful UI test supports the tested interaction, not cloud readiness. A deployment smoke test supports reachability and selected behaviours, not long-term reliability. This separation is central to the project narrative and to honest reporting.
