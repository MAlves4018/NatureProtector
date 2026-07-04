# Operations security model

## Separation of duties

- **QA** executes quality and evidence campaigns but cannot mutate cloud resources.
- **Operations** reads cloud state declarations and controls bounded staging operations.
- **ReleaseApprover** reviews approvals and is the only predefined role with production and destroy capabilities.
- **Admin** manages application users and roles but does not automatically receive production deploy or destroy power.

A single owner may hold multiple roles, but the operation record still preserves request and approval as separate steps. Self approval is disabled outside Development unless explicitly configured.

## Closed inputs

The browser sends an operation identifier, environment, repository reference and a definition-specific allowlisted input map. The backend rejects:

- unknown operation identifiers;
- unknown input names;
- secret-like input names;
- control characters and oversized values;
- unsafe repository references;
- incorrect exact confirmations;
- environments outside the operation definition.

There is no command, script, target, Terraform address or arbitrary `gcloud` field.

## Credentials

GitHub credentials and callback secrets are server or runner configuration. They are never returned by an API contract and never enter the frontend bundle.

The callback secret is compared in constant time. Provider callback URLs must use HTTPS, except for loopback development.

## Dangerous operations

Production plan, production rollback and destroy remain unavailable until the repository contains dedicated, immutable and auditable authorities. The UI displays these definitions and their limitation, but the launch button remains disabled and the backend rejects dispatch.
