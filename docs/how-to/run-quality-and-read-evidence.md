---
id: NP-HOWTO-QUALITY
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: repository code and configuration
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# How to Run Quality and Read Evidence

1. Sign in with the `QA` role or another role that contains the required capability.
2. Open **Quality Runs**.
3. Select a closed suite such as `frontend-fast` or `quality-all`.
4. Provide a branch, commit or release reference allowed by the backend.
5. Start the operation. The backend records the requester and dispatches the authoritative workflow.
6. Follow status and provider reference in the operation timeline.
7. Open **Evidence Explorer** and inspect artifact names, references, hashes and limitations.
8. Treat the result as fully proved only when the expected artifacts and hashes are present and refer to the requested snapshot.
