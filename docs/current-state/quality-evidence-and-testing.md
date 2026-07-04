---
id: NP-CURRENT-QUALITY
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: repository code and configuration
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# Quality, Evidence and Testing

## Test layers

NatureProtector separates evidence by layer because a passing unit test does not prove an integrated deployment.

- domain and unit tests;
- persistence and integration tests;
- API authorisation and rate-limit tests;
- frontend type, lint, unit and build gates;
- browser fixture and full-stack paths;
- accessibility checks;
- architecture and dependency rules;
- security, dependency and secret scans;
- package, release and supply-chain checks;
- runtime reliability, observability and performance evidence;
- Terraform and cloud-static checks.

## Evidence model

Every current evidence item should carry producer, source, run/workflow identity, commit or release reference, environment, timestamp, SHA-256 where possible, status, limitations and an evidence class.

Preferred classes:

- `PROVED` - directly supported by a current reproducible execution and artifacts.
- `PARTIAL` - useful evidence with incomplete scope or provenance.
- `IMPLEMENTED_NOT_PROVED` - source exists but current execution has not been demonstrated.
- `BLOCKED` - a required gate could not run or did not pass.
- `HISTORICAL` - valid evidence for an earlier snapshot only.
- `NOT_DONE` - capability or proof does not exist.

## UI execution

The Quality Runs page dispatches only operation IDs from the closed catalog. Evidence Explorer presents operations, artifacts, hashes, limitations and comparisons. The UI must not infer `PROVED` only from a green provider status.

## Current proof boundary

The supplied unified-operations result proves frontend and static structural gates in its controlled workspace. .NET, containers, remote GitHub runs and cloud deployment require execution in the owner's environment or CI.
