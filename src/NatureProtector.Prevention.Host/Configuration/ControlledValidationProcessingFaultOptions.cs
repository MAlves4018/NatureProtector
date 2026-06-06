namespace NatureProtector.Prevention.Host.Configuration;

public sealed class ControlledValidationProcessingFaultOptions
{
    public const string SectionName = "ControlledValidation:ProcessingFaults";

    public bool Enabled { get; set; }

    public string[] AllowedEnvironments { get; set; } = ["Development", "Evidence"];

    public bool EnableBuiltInP3Cases { get; set; }

    public string[] AllowedRunLabelPrefixes { get; set; } = [];

    public ProcessingFaultCaseOptions[] Cases { get; set; } = [];
}

public sealed class ProcessingFaultCaseOptions
{
    public string? RunLabel { get; set; }

    public string? FaultCaseId { get; set; }

    public string? FaultKind { get; set; }

    public string? CorrelationId { get; set; }

    public Guid? EventId { get; set; }

    public int FailAttempts { get; set; } = 1;
}