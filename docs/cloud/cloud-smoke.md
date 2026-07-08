# Cloud Smoke Plan

Status: `CONFIGURED_NOT_EXECUTED`.

Cloud smoke has not been run for this freeze candidate. Local B/C validation is
not a substitute for smoke on a deployed cloud URL.

## Future Smoke Checks

When a staging URL exists, cloud smoke must verify:

- API health endpoints;
- webUI health and dashboard load;
- admin login using the staged bootstrap credentials;
- protected API endpoints with a token;
- control plane catalog/runtime surfaces;
- `scenario_b` nominal run through the cloud control plane;
- `scenario_c` degraded run with `missing-readings`;
- B/C comparison and expected degradation evidence;
- PostgreSQL persistence for runs and audit state;
- RabbitMQ queues/consumers and Prevention processing;
- Prevention Host health/log evidence;
- evidence export and checksum capture;
- Cloud Run/GKE/Cloud Deploy logs for the release;
- rollback or shutdown procedure, if authorized for the environment.

## Required Inputs

- staging frontend origin;
- API origin or edge URL;
- release manifest path;
- bootstrap admin username;
- bootstrap admin password secret/version, not plaintext in git;
- cloud evidence directory;
- exact rollback/teardown authorization, if smoke includes cleanup.

## Pass Rule

Staging smoke must pass before any production approval request. Production
smoke must be a separate run after an explicitly approved production deploy.
