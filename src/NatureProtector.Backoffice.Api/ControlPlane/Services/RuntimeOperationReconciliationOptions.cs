namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

public sealed class RuntimeOperationReconciliationOptions
{
    public const string SectionName = "RuntimeOperationReconciliation";

    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 2;
    public int BatchSize { get; set; } = 50;
}
