---
id: NP-CURRENT-GATES
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: repository code and configuration
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# Limitations and Open Gates

## Technical proof gates

- .NET restore/build/test on the final integrated tree.
- PowerShell runtime and PSScriptAnalyzer.
- Terraform init/validate against selected environments.
- ShellCheck/actionlint or equivalent workflow validation.
- Docker/full-stack execution.
- GitHub workflow dispatch and authenticated callback.
- Staging deployment, smoke and observability.
- Rollback rehearsal.
- Cost/inventory collection.
- Production promotion, observation and teardown/destroy proof.

## Documentation gates

- Full current report source was not supplied with the 379-page PDF; this delivery therefore provides an integration-ready report delta and supplement rather than silently rewriting a PDF.
- Human review is still required for acknowledgements, final institutional wording and the final selection of report/presentation figures.
- Generated diagrams are fact-checked against the supplied repository but have not been independently validated by an external reviewer.

## Scientific and product gates

- scientific calibration and external validation;
- user study/instrument execution;
- territorial generalisation;
- official operational authorisation;
- independent security/architecture review;
- long-running production evidence.

## Wording rule

Use `implemented`, `statically verified`, `historically executed`, `partially proved` or `blocked` precisely. Avoid `real-time`, `live`, `validated`, `production-ready`, `official alert` or `real data` unless the exact claim is supported by current evidence.
