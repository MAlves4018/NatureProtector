# Open gates and truthful limitations

## Not executed in the construction environment

- `.NET restore`, compilation and test suite: the required .NET SDK was unavailable.
- PowerShell runtime gates and PSScriptAnalyzer: PowerShell was unavailable.
- Terraform init/validate: Terraform CLI and provider access were unavailable.
- GitHub workflow dispatch and callback: no repository credential or reachable API deployment was provided.
- GCP inventory, staging deployment, rollback, costs and production: no cloud execution was performed.

## Deliberately blocked

- production plan;
- production rollback;
- immutable destroy plan;
- destroy execution;
- live cost collection;
- qualified live inventory and smoke operations whose complete input contracts are not yet safe to expose.

## Promotion requirements

Before claiming remote proof:

1. run the .NET tests, including the updated authorization endpoint inventory;
2. execute the wrappers on a protected branch;
3. configure callback URL and secret;
4. ingest workflow artifacts with hashes;
5. prove staging before production;
6. preserve signed release, deployment, smoke, rollback and cost evidence;
7. enable production/destroy only after dedicated workflows and immutable plans exist.
