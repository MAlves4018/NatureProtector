---
id: NP-CURRENT-DATA-RISK
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: repository code and configuration
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# Data, Risk and Scientific Boundaries

## Data and scenarios

The project uses the pilot area of Proença-a-Nova and controlled scenarios such as nominal, high-risk and degraded-pipeline cases. Simulation parameters include sensor count, cycles, seed, intervals and degradation profiles such as missing values, noise, bias, drift, stuck values, outliers, clipping, lag, duplicates and out-of-order events.

A scenario is an experimental input contract, not a record of an actual wildfire event unless separately supported by traceable source data.

## Quality before scoring

The processing chain evaluates semantic validity, temporal context, coverage, quality flags and eligibility before computing or exposing a candidate score. Blocked or incomplete input must remain explicit.

## Candidate methods

- NatureProtector score: a project-specific candidate engineering score.
- FWI: implemented comparison component; not claimed as official or conformant without the required official method, inputs and validation.
- KBDI: candidate dryness indicator; not calibrated or territorially validated for operational use.
- Portuguese Context Proxy: a technical comparison proxy; not an official RCM/IPMA/ICNF product.

## Authorised claims

The system can demonstrate architecture, contracts, traceability, controlled simulation, quality handling, candidate calculations and technical evidence. It cannot claim scientific validation, operational prediction accuracy, generalisation to Portugal, official warning authority or institutional adoption.

## Priority scientific gaps

- complete data lineage and reproduction contracts;
- candidate-method calibration and validation;
- territorial and temporal evaluation;
- independent comparison with authoritative sources;
- user/institutional evaluation;
- analysis of uncertainty and decision consequences.
