# Configuration and secret boundary

## JWT

`appsettings.json` contains no signing key. Development keeps a documented local
key in `appsettings.Development.json`. Staging and production must supply:

```text
Jwt__SigningKey=<secret supplied by the environment secret provider>
```

The API now fails during startup when the key is missing, shorter than 32
characters, or visibly resembles a development/placeholder value. The issuer,
audience and token lifetime are also validated.

## Local runtime process launch

`BackofficeApi:LocalRuntimeProcessLaunchEnabled` controls the legacy/local
`ProcessStartInfo` path that invokes `dotnet run` for the Simulator.

- Development may set it to `true`.
- Staging and production must keep it `false`.
- A future cloud deployment must use an environment-specific distributed
  orchestrator rather than enabling the local process path.

The API fails during startup if the option is enabled outside Development.

## Remaining protected-file action

The repository snapshot still contains a token-shaped value in `.env.example`
and a path-wide Gitleaks allowlist. Those files were deliberately not modified
in this wave because the owner-defined execution rules protect `.env` and
`.env.example` from automated changes.

The owner must explicitly authorise and perform the paired remediation:

1. replace the token-shaped value with an unmistakably invalid placeholder;
2. adjust the local bootstrap instructions so a real local token is created in
   the untracked `.env`;
3. remove the `.env.example` path-wide Gitleaks allowlist;
4. run Gitleaks against the full repository and history;
5. rotate the old value if there is any possibility that it was accepted by a
   real InfluxDB instance.

Until this action is complete, the W1 secret-boundary gate remains blocked.
