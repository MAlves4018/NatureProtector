# Final convergence remediation — 2026-06-28

This snapshot corrects the audited convergence candidate without replacing the current deployment implementation.

Implemented corrections:

- fixed the canonical `scripts/np.ps1` evidence-directory call and first-run directory creation;
- added fail-fast JWT validation and moved the local key to Development configuration;
- disabled local runtime process launch outside Development;
- replaced the token-shaped `.env.example` value and removed the whole-file Gitleaks exemption;
- added CODEOWNERS and Dependabot configuration;
- hardened Phase 7 performance promotion and historical B/C provenance;
- converted deployment hashes into non-blocking historical snapshot observations;
- enabled enforced frontend lint and formatting after remediating the findings;
- replaced the unauthenticated ShellCheck download with the signed Ubuntu package repository.

The current deployment remains authoritative. Binary Authorization and remote-readiness overlays from older construction snapshots were not copied over the current deployment and remain a separate semantic reconciliation task.
