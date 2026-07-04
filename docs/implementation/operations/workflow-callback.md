# Workflow callback configuration

The operation wrappers can report status back to:

```text
POST /api/control/operations/callback
X-NatureProtector-Operations-Secret: <secret>
```

Configure in GitHub:

- repository variable `OPERATIONS_CALLBACK_URL` with the complete HTTPS endpoint;
- repository secret `OPERATIONS_CALLBACK_SECRET` matching `Operations:CallbackSecret` in the API runtime.

The reporter is `scripts/operations/report-operation-callback.py`.

Direct wrappers report `Succeeded` or `Failed` and hash their output tree deterministically. Dispatch-only wrappers report `Queued`; they do not claim that the child workflow passed. A later child-workflow integration may report the final state using the same callback contract.

When URL or secret is absent, the reporter writes `SKIPPED_UNCONFIGURED` and returns success. This keeps workflows usable while making the missing status bridge explicit.
