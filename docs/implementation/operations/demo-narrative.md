# Demonstration narrative

1. **Login as QA** — show that Quality and Evidence are available while cloud mutation is absent.
2. **Open Mission Control** — explain Code → Quality → Evidence → Release → Cloud and the difference between state and proof.
3. **Launch a static quality operation in Development simulation mode** — the timeline records validation and dispatch, clearly labelled `DEMONSTRATION_ONLY`.
4. **Open Evidence Explorer** — compare two operations and show artifact provenance, hashes and limitations.
5. **Login as Operations** — inspect staging configuration. Emphasize `DeclaredNotObserved` before any live inventory run.
6. **Inspect staging plan/deploy** — show exact confirmations and delegation to existing workflows.
7. **Login as ReleaseApprover** — show pending approvals and the separation from Admin.
8. **Open production/destroy definitions** — demonstrate that dangerous actions remain visible but blocked until their missing authorities exist.
9. **Return to Release Readiness** — show that the dashboard derives status from evidence rather than displaying an invented completion percentage.

For an offline presentation, use Development `Operations:Mode=Simulation`. Never describe simulated dispatch as a completed GitHub or cloud run.
