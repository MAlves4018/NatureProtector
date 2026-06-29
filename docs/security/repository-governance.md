# Repository governance target

## Status

This document describes the target GitHub configuration for NatureProtector. The
repository files define the intended checks, but remote rulesets and environment
protection must be applied and then evidenced by the repository owner.

## Protected branch

Target branch: `master`.

Recommended active ruleset:

- require a pull request before merging;
- require at least one approval;
- dismiss stale approvals after new commits;
- require review from CODEOWNERS;
- require approval of the most recent push by someone other than its author;
- require conversation resolution;
- block force pushes and branch deletion;
- apply the rules to administrators unless a documented emergency bypass is
  explicitly required;
- require the pull request branch to be up to date before merge.

## Candidate required checks

Confirm the exact status-check context names from a successful pull request
before activating enforcement. The intended checks are:

- `Backend build and tests`;
- `Backend Docker integration tests`;
- `Frontend typecheck, tests, build`;
- `Frontend Node 22 compatibility`;
- `CodeQL (csharp)`;
- `CodeQL (javascript-typescript)`;
- `Dependency review`;
- `Audits and secret canaries`.

Do not guess a status context. A required check with a stale or incorrect name can
block every pull request.

## Environments

Create two GitHub environments:

### staging

- deployment only from `master` or an explicitly documented release branch;
- environment-scoped secrets only;
- no long-lived service-account key files;
- smoke tests and rollback evidence retained with the deployment.

### production

- deployment only from the authorised release ref;
- at least one required human reviewer;
- prevent self-review where the repository plan supports it;
- environment-scoped secrets only;
- no deployment job may read production secrets before approval;
- retain release manifest, image digest, SBOM, attestation and smoke result.

## Evidence to retain

- exported ruleset JSON;
- environment configuration JSON without secret values;
- screenshot or API evidence of required checks;
- pull request demonstrating that an unmet check blocks merge;
- staging deployment requiring the expected ref;
- production deployment waiting for approval.

## Limitations

Files in `.github/` do not prove remote enforcement. Until the settings above
are applied and exercised, classify the capability as `IMPLEMENTED_NOT_PROVED`.
