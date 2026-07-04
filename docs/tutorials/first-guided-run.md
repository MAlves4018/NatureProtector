---
id: NP-TUTORIAL-FIRST-RUN
status: CURRENT
owner: Miguel Alves
audience: new developer, presenter
source_of_truth: repository code and configuration
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# Tutorial: First Guided Run

1. Prepare `.env` from `.env.example` and review local-only values.
2. Run `./scripts/workspace.ps1 setup` and `up -StartRuntime -OpenBrowser` on the supported Windows/PowerShell environment.
3. Sign in with the development account only in `Development`.
4. Open **Scenario Lab -> Run Orchestrator**.
5. Select `scenario_b`, 6 sensors, 5 cycles, seed `12345`, no degradation.
6. Start the run and follow its lifecycle.
7. Inspect processing attempts, risk assessments and the evidence view.
8. Confirm that the simulator process terminates after the run.

The values historically observed for this profile are examples, not guaranteed acceptance values for every future snapshot. Use the current run identity and timestamps when making claims.
