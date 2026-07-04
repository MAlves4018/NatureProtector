---
id: NP-REPORT-DELTA
status: CURRENT
owner: Miguel Alves
audience: report editor
source_of_truth: repository code and configuration
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# Report Delta: R13 to 2026-06-28

## New implemented material

- server-side engineering capabilities and policies;
- roles `QA`, `Operations` and `ReleaseApprover`;
- Mission Control, Quality Runs, Evidence Explorer, Deployments, Cloud Resources, Approvals and User/Role Administration;
- closed engineering operation catalog, operation store, confirmation, approval and callback;
- GitHub workflow wrappers for quality, evidence, deployment and cloud;
- updated security model and evidence-level rules;
- advanced cloud/CD implementation, while retaining runtime proof limitations.

## Sections requiring update

| Report area | Required change |
|---|---|
| Chapter 4 Architecture | Add operations plane, server-side capabilities and trust boundaries |
| Chapter 5 Evolution | Add unified documentation/operations convergence methodology |
| Chapter 6 Implementation | Add UI pages, operation catalog, workflows and callback |
| Chapter 8 Quality/Evidence | Add UI-triggered closed suites and hashed-artifact promotion rule |
| Chapter 9 Discussion | Discuss separation of application admin and release authority |
| Chapter 10 Future Work | Remove implemented Operations UI from future work; retain remote/cloud proof and scientific validation |
| Chapter 11 Conclusions | Add auditability across engineering operations without claiming staging/production proof |
| Appendix E | Update containers, endpoints, roles and repository map |
| Appendix H | Update quality/evidence tooling and open runtime gates |
| Appendix I | Update administration and capability model |
| Appendix J | Update GCP/CD implementation and factual proof boundary |
| Appendix K | Replace selected R13 diagrams/screenshots with promoted current diagrams |

## Claims that must remain limited

- Staging has not been proved from the supplied artifacts.
- Production is not deployed/proved.
- Signed release for the final head is not proved by the executor-only ZIP.
- Scientific, territorial, user and institutional validation remain incomplete.
