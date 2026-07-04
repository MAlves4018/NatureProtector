# Phase 0 — reproducible maintainability baseline

## Status

`PHASE_0_REPOSITORY_AUDIT_HARNESS_IMPLEMENTED`

This phase adds measurement only. It does not alter application code, workflows, cloud configuration, domain semantics, contracts, migrations, scoring, alerts, roles, `.env`, or `.env.example`.

## Purpose

The repository had preliminary maintainability findings, but no single deterministic command to reproduce the underlying measurements. Phase 0 adds a standard-library Python auditor under [`tools/repo-audit/`](../../../tools/repo-audit/) and records the baseline from the repository state immediately before the tool was added.

The auditor inventories:

- files, bytes, categories and lines by language;
- large text and binary files;
- exact byte-for-byte duplicates;
- scripts and static references to them;
- environment variable names and example definitions;
- occurrences of selected values from declared canonical configuration sources;
- conservative observations that require human review.

It never interprets missing static references as proof of dead code and never executes scanned scripts.

## Canonical commands

From the repository root:

```bash
python3 -m unittest discover -s tools/repo-audit/tests -p "test_*.py" -v

python3 tools/repo-audit/audit.py \
  --repo . \
  --config tools/repo-audit/audit-config.json \
  --output artifacts/repo-audit \
  --verify-determinism
```

On Windows, use the repository-approved Python executable or `py -3` in place of `python3`.

## Baseline before Phase 0 additions

The machine-readable source is [`repo-audit-phase0-baseline.json`](repo-audit-phase0-baseline.json).

| Measurement | Baseline |
|---|---:|
| Files included after configured exclusions | 1,271 |
| Text files | 1,035 |
| Included bytes | 61,612,539 |
| Executable scripts under `scripts/` | 137 |
| Scripts referenced directly by workflows | 40 |
| Scripts with no static reference found | 21 |
| Exact duplicate groups | 9 |
| Potential exact-duplicate bytes | 11,120 |
| Environment variable names inventoried | 270 |
| Selected canonical configuration literals tracked | 6 |

The baseline deliberately separates these path categories:

- `source`: 1,146 files;
- `historical`: 64 files;
- `generated`: 37 files;
- `dataset`: 24 files.

This prevents generated EF migrations, datasets and historical evidence from being treated as ordinary source-code debt.

## Interpretation limits

Static analysis cannot establish all runtime consumers. Reflection, dependency injection, dynamic imports, manually invoked scripts and external automation can produce false positives. Therefore:

- `NO_STATIC_REFERENCE_FOUND` means “inspect”, not “delete”;
- exact duplication may be intentional evidence or fixtures;
- a repeated canonical literal may be a policy guard rather than an error;
- an environment variable without `.env.example` may be a CI secret or mandatory runtime input;
- line count and file size identify hotspots but do not determine code quality.

## Phase gate

Phase 0 is complete only when all of the following remain true:

1. unit tests for the auditor pass;
2. Python syntax compilation passes;
3. every repository JSON file remains valid;
4. two independent runs produce byte-identical reports;
5. a scope comparison shows that only Phase 0 audit and documentation files were added;
6. toolchain-dependent application validations are explicitly listed when unavailable rather than claimed as passed.

The next phase may use these outputs to prove low-risk hygiene removals, but no file should be deleted from this baseline solely because the auditor reported it.
