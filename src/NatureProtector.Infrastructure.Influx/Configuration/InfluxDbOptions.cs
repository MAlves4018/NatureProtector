namespace NatureProtector.Infrastructure.Influx.Configuration;

public sealed class InfluxDbOptions
{
    private InfluxWriteOptions _writes = new();

    public const string SectionName = "InfluxDb";
    public bool Enabled { get; set; } = true;
    public bool FailPipelineOnWriteError { get; set; }

    public string Url { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public InfluxWriteOptions Writes
    {
        get => _writes;
        set => _writes = value ?? new InfluxWriteOptions();
    }
}
