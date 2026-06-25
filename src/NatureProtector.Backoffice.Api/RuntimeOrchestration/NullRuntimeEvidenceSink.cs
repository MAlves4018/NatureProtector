namespace NatureProtector.Backoffice.Api.RuntimeOrchestration;

public sealed class NullRuntimeEvidenceSink : IRuntimeEvidenceSink
{
    public static NullRuntimeEvidenceSink Instance { get; } = new();

    private NullRuntimeEvidenceSink()
    {
    }

    public bool IsAvailable => false;
    public string AvailabilityMessage => "Runtime evidence sink is disabled.";

    public Task<RuntimeEvidenceReference> CreateAsync(
        string category,
        DateTimeOffset requestedAtUtc,
        string label,
        CancellationToken cancellationToken)
        => throw new InvalidOperationException(AvailabilityMessage);

    public Task WriteJsonAsync(
        RuntimeEvidenceReference evidence,
        string fileName,
        object value,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task WriteTextAsync(
        RuntimeEvidenceReference evidence,
        string fileName,
        string value,
        CancellationToken cancellationToken)
        => Task.CompletedTask;
}
