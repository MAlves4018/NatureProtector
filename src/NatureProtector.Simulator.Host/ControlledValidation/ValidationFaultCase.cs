namespace NatureProtector.Simulator.Host.ControlledValidation;

public sealed record ValidationFaultCase
{
    public ValidationFaultCase(
        string faultCaseId,
        ControlledValidationFaultLayer faultLayer,
        ControlledValidationExpectedOutcome expectedOutcome,
        string? expectedReasonCode,
        string description,
        int? expectedEvents = null,
        int? expectedPublishedEvents = null,
        int? expectedCoverageGap = null,
        string? valueProfile = null)
    {
        if (string.IsNullOrWhiteSpace(faultCaseId))
        {
            throw new ArgumentException("fault_case_id is required.", nameof(faultCaseId));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("description is required.", nameof(description));
        }

        FaultCaseId = faultCaseId;
        FaultLayer = faultLayer;
        ExpectedOutcome = expectedOutcome;
        ExpectedReasonCode = expectedReasonCode;
        Description = description;
        ExpectedEvents = expectedEvents;
        ExpectedPublishedEvents = expectedPublishedEvents;
        ExpectedCoverageGap = expectedCoverageGap;
        ValueProfile = valueProfile;
    }

    public string FaultCaseId { get; }

    public ControlledValidationFaultLayer FaultLayer { get; }

    public ControlledValidationExpectedOutcome ExpectedOutcome { get; }

    public string? ExpectedReasonCode { get; }

    public string Description { get; }

    public int? ExpectedEvents { get; }

    public int? ExpectedPublishedEvents { get; }

    public int? ExpectedCoverageGap { get; }

    public string? ValueProfile { get; }
}
