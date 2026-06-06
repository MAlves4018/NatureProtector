using NatureProtector.Simulator.Host.ControlledValidation;

namespace NatureProtector.Simulator.Host.Tests.ControlledValidation;

public sealed class ControlledValidationScenarioManifestTests
{
    [Fact]
    public void Ctor_Throws_WhenRunLabelIsMissing()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ControlledValidationScenarioManifest(
            controlledValidationRunId: Guid.NewGuid(),
            runLabel: "",
            scenarioCode: "scenario_b",
            areaId: Guid.NewGuid(),
            simulationRunId: Guid.NewGuid(),
            eventTime: DateTimeOffset.UtcNow,
            nominalSensorId: Guid.NewGuid(),
            nominalSensorName: "sensor-001",
            sensorNotFoundId: Guid.NewGuid(),
            faultCases: ControlledValidationScenarioManifest.CreateDefaultP0FaultCases()));

        Assert.Equal("runLabel", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenFaultCaseIdsAreDuplicated()
    {
        var duplicated = new ValidationFaultCase(
            ControlledValidationFaultCaseIds.N1InvalidJson,
            ControlledValidationFaultLayer.EventTransport,
            ControlledValidationExpectedOutcome.Rejected,
            "invalid_json",
            "Duplicado intencional para teste.");

        var faultCases = ControlledValidationScenarioManifest
            .CreateDefaultP0FaultCases()
            .Append(duplicated)
            .ToArray();

        var ex = Assert.Throws<ArgumentException>(() => new ControlledValidationScenarioManifest(
            controlledValidationRunId: Guid.NewGuid(),
            runLabel: "p0-smoke",
            scenarioCode: "scenario_b",
            areaId: Guid.NewGuid(),
            simulationRunId: Guid.NewGuid(),
            eventTime: DateTimeOffset.UtcNow,
            nominalSensorId: Guid.NewGuid(),
            nominalSensorName: "sensor-001",
            sensorNotFoundId: Guid.NewGuid(),
            faultCases: faultCases));

        Assert.Equal("faultCases", ex.ParamName);
        Assert.Contains(ControlledValidationFaultCaseIds.N1InvalidJson, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateDefaultP0FaultCases_ContainsExpectedP0ReasonCodes()
    {
        var faultCases = ControlledValidationScenarioManifest.CreateDefaultP0FaultCases();

        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.N1InvalidJson &&
            faultCase.ExpectedReasonCode == "invalid_json");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.N1MissingPayload &&
            faultCase.ExpectedReasonCode == "missing_payload");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.N2InvalidOperationalState &&
            faultCase.ExpectedReasonCode == "invalid_operational_state");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.N3SensorNotFound &&
            faultCase.ExpectedReasonCode == "sensor_not_found");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.N4DuplicatePayloadMismatch &&
            faultCase.ExpectedReasonCode == "duplicate_payload_mismatch");
    }

    [Fact]
    public void CreateDefaultP1FaultCases_ContainsExpectedP1ReasonCodes()
    {
        var faultCases = ControlledValidationScenarioManifest.CreateDefaultP1FaultCases();

        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.N5TransientFailure &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.RetryThenSuccess &&
            faultCase.ExpectedReasonCode == "transient_failure");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.N6PermanentFailure &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.Quarantined &&
            faultCase.ExpectedReasonCode == "permanent_failure");
    }

    [Fact]
    public void CreateDefaultP2FaultCases_ContainsExpectedP2Metadata()
    {
        var faultCases = ControlledValidationScenarioManifest.CreateDefaultP2FaultCases();

        var missing = Assert.Single(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2MissingReadings);
        Assert.Equal(ControlledValidationExpectedOutcome.CoverageGap, missing.ExpectedOutcome);
        Assert.Equal(5, missing.ExpectedEvents);
        Assert.Equal(3, missing.ExpectedPublishedEvents);
        Assert.Equal(2, missing.ExpectedCoverageGap);
        Assert.Equal("missing-readings", missing.ValueProfile);

        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2DuplicatePayloadIdentical &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.IdempotentDuplicate &&
            faultCase.ExpectedReasonCode == "idempotent_duplicate");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2ValueNoise &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.ValueDegraded &&
            faultCase.ValueProfile == "noise");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2ValueBias &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.ValueDegraded &&
            faultCase.ValueProfile == "bias");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2ValueDrift &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.ValueDegraded &&
            faultCase.ValueProfile == "drift");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2ValueOutlier &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.ValueDegraded &&
            faultCase.ValueProfile == "outlier-nominal");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2ValueStuck &&
            faultCase.ExpectedEvents == 2 &&
            faultCase.ExpectedPublishedEvents == 2 &&
            faultCase.ValueProfile == "stuck-value");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2ValueClipping &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.ValueDegraded &&
            faultCase.ValueProfile == "clipping-nominal");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2ValueRange &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.ValueDegraded &&
            faultCase.ValueProfile == "range-boundary");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2BlockedRangeEligibility &&
            faultCase.FaultLayer == ControlledValidationFaultLayer.Eligibility &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.BlockedEligibility &&
            faultCase.ExpectedReasonCode == "temperature_out_of_candidate_range");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2TemporalDelayed &&
            faultCase.FaultLayer == ControlledValidationFaultLayer.TemporalQuality &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.TemporalQuality &&
            faultCase.ExpectedReasonCode == "delayed-reading");
    }

    [Fact]
    public void CreateDefaultP3NegativePipelineFaultCases_ContainsExpectedP3ReasonCodes()
    {
        var faultCases = ControlledValidationScenarioManifest.CreateDefaultP3NegativePipelineFaultCases();

        Assert.Equal(10, faultCases.Count);
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RejectInvalidJson &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.Rejected &&
            faultCase.ExpectedReasonCode == "invalid_json");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RejectMissingPayload &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.Rejected &&
            faultCase.ExpectedReasonCode == "missing_payload");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RejectUnsupportedEventType &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.Rejected &&
            faultCase.ExpectedReasonCode == "unsupported_event_type");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RejectUnsupportedSchemaVersion &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.Rejected &&
            faultCase.ExpectedReasonCode == "unsupported_schema_version");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RejectInvalidOperationalState &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.Rejected &&
            faultCase.ExpectedReasonCode == "invalid_operational_state");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3QuarantineSensorNotFound &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.Quarantined &&
            faultCase.ExpectedReasonCode == "sensor_not_found");

        var duplicateMismatch = Assert.Single(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3QuarantineDuplicatePayloadMismatch);
        Assert.Equal(ControlledValidationExpectedOutcome.Rejected, duplicateMismatch.ExpectedOutcome);
        Assert.Equal("duplicate_payload_mismatch", duplicateMismatch.ExpectedReasonCode);
        Assert.Equal(2, duplicateMismatch.ExpectedEvents);
        Assert.Equal(2, duplicateMismatch.ExpectedPublishedEvents);
        Assert.Equal(0, duplicateMismatch.ExpectedCoverageGap);

        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RetryTransientThenSuccess &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.RetryThenSuccess &&
            faultCase.ExpectedReasonCode == "transient_failure");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RetryExhaustedToQuarantine &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.Quarantined &&
            faultCase.ExpectedReasonCode == "retries_exhausted");
        Assert.Contains(faultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3PermanentFailureToQuarantine &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.Quarantined &&
            faultCase.ExpectedReasonCode == "permanent_failure");
    }
}
