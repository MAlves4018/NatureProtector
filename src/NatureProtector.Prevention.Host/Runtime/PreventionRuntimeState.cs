namespace NatureProtector.Prevention.Host.Runtime;

public sealed record PreventionRuntimeSnapshot(
    bool Ready,
    DateTimeOffset UpdatedAtUtc,
    string Reason);

public sealed class PreventionRuntimeState
{
    private readonly object _syncRoot = new();
    private PreventionRuntimeSnapshot _snapshot = new(
        Ready: false,
        UpdatedAtUtc: DateTimeOffset.UtcNow,
        Reason: "Prevention consumer has not started.");

    public PreventionRuntimeSnapshot Snapshot
    {
        get
        {
            lock (_syncRoot)
            {
                return _snapshot;
            }
        }
    }

    public void MarkReady(string reason)
        => Update(ready: true, reason);

    public void MarkNotReady(string reason)
        => Update(ready: false, reason);

    private void Update(bool ready, string reason)
    {
        lock (_syncRoot)
        {
            _snapshot = new PreventionRuntimeSnapshot(
                ready,
                DateTimeOffset.UtcNow,
                string.IsNullOrWhiteSpace(reason) ? "No readiness reason supplied." : reason);
        }
    }
}
