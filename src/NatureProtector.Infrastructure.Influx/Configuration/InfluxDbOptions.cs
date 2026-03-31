namespace NatureProtector.Infrastructure.Influx.Configuration;

public sealed class InfluxDbOptions
{
    public const string SectionName = "InfluxDb";

    public string Url { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
}