# Repository maintainability audit

This directory contains a dependency-free, read-only repository inventory used before maintainability refactors.

## What it measures

- files, bytes, categories and lines by language;
- largest text files;
- exact byte-for-byte duplicate groups;
- scripts under `scripts/` and their static references from workflows, automation, documentation, tests and source, including normal Python module imports;
- environment variable names and repository example definitions;
- repetition of selected values from declared canonical configuration sources;
- review-only observations with conservative classifications.

The tool does **not** claim that an unreferenced script is dead, that a duplicate is removable, or that a large file is defective. Dynamic loading, manual entrypoints, fixtures, generated files and historical evidence require human review.

## Canonical command

From the repository root:

```bash
python3 tools/repo-audit/audit.py \
  --repo . \
  --config tools/repo-audit/audit-config.json \
  --output artifacts/repo-audit \
  --verify-determinism
```

On Windows, `py -3` or the repository-approved Python executable may replace `python3`.

## Tests

```bash
python3 -m unittest discover -s tools/repo-audit/tests -p "test_*.py" -v
```

The implementation uses only the Python standard library. It does not install packages, execute repository scripts, call cloud services, read secret values from the environment, or modify scanned files.

## Outputs

- `summary.json`: stable aggregate metrics;
- `report.md`: human-readable summary;
- `file-inventory.csv`: complete file inventory and hashes;
- `hotspots.csv`: text files ordered by line count;
- `exact-duplicates.csv`: exact duplicate groups;
- `script-inventory.csv` and `script-references.csv`: static script reachability evidence;
- `environment-variables.csv`: variable names, definitions and references, without runtime values;
- `configuration-literals.csv`: selected canonical literals and repository occurrences;
- `observations.json`: conservative review candidates;
- `manifest.json`: SHA-256 manifest of every generated report.

The default `artifacts/` destination is already ignored by the repository.

## Configuration policy

`audit-config.json` makes scope decisions explicit:

- excluded local/build paths;
- generated code patterns;
- dataset and historical-evidence categories;
- text and script extensions;
- line-to-language mapping;
- canonical configuration values that should be counted;
- reporting thresholds.

Changing this file changes the meaning of the baseline and therefore must be reviewed like code.
