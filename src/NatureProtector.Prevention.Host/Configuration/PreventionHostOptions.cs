namespace NatureProtector.Prevention.Host.Configuration;

public sealed class PreventionHostOptions
{
    public const string SectionName = "PreventionHost";

    public bool PipelinePersistenceEnabled { get; set; } = true;
    public int MaxProcessingAttempts { get; set; } = 3;
    public int[] RetryDelaySeconds { get; set; } = [5, 30];
    public int RetryPollingIntervalSeconds { get; set; } = 5;
}
