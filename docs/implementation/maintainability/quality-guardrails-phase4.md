# Phase 4 — Progressive quality guardrails

## Scope

Phase 4 introduces a single, machine-readable quality policy without rewriting legacy code in bulk. The rollout model has two states:

- `enforce`: a regression fails enforcement mode;
- `report`: findings are recorded as evidence but do not fail the workflow.

The default mode is `report`. Promotion to `enforce` requires either a zero baseline or a reviewed suppression policy.

## Enforced in Phase 4

- static validation of the quality policy;
- TypeScript strict type checking across the frontend;
- Ruff import and correctness rules for Python.

## Report-only in Phase 4

- full Biome coverage over all frontend TypeScript/TSX files;
- full frontend formatter drift;
- .NET analyzer output;
- PSScriptAnalyzer warnings/errors;
- ShellCheck warnings.

## Canonical files

- `config/quality/quality-gates.json`: gate commands and rollout state;
- `config/quality/quality-baseline.json`: measured baseline and promotion rule;
- `tools/quality-gates/run.py`: cross-platform orchestrator and evidence writer;
- `.editorconfig`: repository formatting and .NET style policy;
- `pyproject.toml`: Ruff policy;
- `PSScriptAnalyzerSettings.psd1`: PowerShell policy;
- `.shellcheckrc`: shell policy;
- `webUI/biome.quality.jsonc`: complete frontend coverage.

## Commands

Report mode:

```bash
python tools/quality-gates/run.py --repo . --mode report --output-dir artifacts/quality
```

Enforcement mode:

```bash
python tools/quality-gates/run.py --repo . --mode enforce --output-dir artifacts/quality
```

Static policy validation:

```bash
python tools/quality-gates/validate.py --repo .
```

The normal `scripts/np.ps1 validate` path checks the policy structure but does not execute the complete quality suite. The dedicated GitHub workflow installs the required tools and records the complete report.

## Deliberate exclusions

Phase 4 does not:

- bulk-format production code;
- enable every analyzer as an error;
- suppress unknown findings globally;
- upgrade application dependencies;
- modify domain behavior, cloud resources, contracts, migrations, scoring, alerts, or roles.
