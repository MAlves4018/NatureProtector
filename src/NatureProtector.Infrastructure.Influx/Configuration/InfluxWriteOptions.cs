namespace NatureProtector.Infrastructure.Influx.Configuration;

public sealed class InfluxWriteOptions
{
    public bool AcceptedReadings { get; set; } = true;
    public bool RiskAssessments { get; set; } = true;
    public bool AreaRiskSnapshots { get; set; } = true;
}
