# Phase 6 — Frontend decomposition

## Scope

Phase 6 decomposes the two largest frontend aggregation points without changing routes, API calls, runtime contracts, or public import paths.

## Workspace structure

`Workspace.tsx` remains the state and navigation orchestrator. Presentation is separated into:

- `workspace/WorkspaceTopBar.tsx` — area, window and refresh controls;
- `workspace/WorkspaceSections.tsx` — monitoring, scenario, evidence, flow and model views;
- `workspace/WorkspaceShared.tsx` — reusable panels, tables, charts and pure formatting/build functions;
- `workspace/workspaceConstants.ts` — tab definitions and static evidence/model catalogues.

The decomposition contract reconstructs the original Phase 5 source slice after removing only the new `export` modifiers. A changed, missing or duplicated original block fails the enforced quality gate.

## Type structure

The stable import path remains `app/types`. `types/index.tsx` is now a barrel over:

- `auth.ts`;
- `geography.ts`;
- `runtime.ts`;
- `scenario.ts`.

All 61 prior public exports remain available through the same barrel path. Existing consumers do not need import changes.

## Guardrail

Run:

```bash
python tools/frontend-audit/validate.py --repo .
```

The guardrail validates source-slice preservation, file-size boundaries, the `Workspace` public export, the lazy route import, barrel contents, type module hashes, and the complete public type export set.
