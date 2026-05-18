namespace NatureProtector.Prevention.Host.Configuration;

public sealed class PreventionHostOptions
{
    public const string SectionName = "PreventionHost";

    public bool PipelinePersistenceEnabled { get; set; } = true;
    public ushort ConsumerPrefetchCount { get; set; } = 1;
    public int MaxProcessingAttempts { get; set; } = 3;
    public int[] RetryDelaySeconds { get; set; } = [5, 30];
    public int RetryPollingIntervalSeconds { get; set; } = 5;
    public int ProcessingLeaseTimeoutSeconds { get; set; } = 300;
}
