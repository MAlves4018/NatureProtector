using System.Text;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Simulator.Host.ControlledValidation;

namespace NatureProtector.Simulator.Host.Tests.ControlledValidation;

public sealed class ControlledValidationMessageBuilderTests
{
    [Fact]
    public void BuildP0Messages_CreatesExpectedMessages()
    {
        var manifest = CreateManifest();
        var builder = new ControlledValidationMessageBuilder(manifest);

        var messages = builder.BuildP0Messages();

        Assert.Equal(6, messages.Count);
        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N1InvalidJson &&
            message.Kind == ControlledValidationMessageKind.RawInvalidJson &&
            message.EventId is null);
        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N1MissingPayload &&
            message.Kind == ControlledValidationMessageKind.EnvelopeWithoutPayload);
        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N2InvalidOperationalState);
        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N3SensorNotFound);
        Assert.Equal(
            2,
            messages.Count(message =>
                message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N4DuplicatePayloadMismatch));
        Assert.All(messages, message => Assert.Equal(64, message.BodySha256.Length));
    }

    [Fact]
    public void BuildP0Messages_InvalidJsonContainsSidecarMarker()
    {
        var manifest = CreateManifest();
        var builder = new ControlledValidationMessageBuilder(manifest);

        var message = builder.BuildP0Messages().Single(message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N1InvalidJson);
        var rawBody = Encoding.UTF8.GetString(message.Body);

        Assert.Contains(manifest.ControlledValidationRunId.ToString(), rawBody, StringComparison.Ordinal);
        Assert.Contains(manifest.RunLabel, rawBody, StringComparison.Ordinal);
        Assert.Contains(ControlledValidationFaultCaseIds.N1InvalidJson, rawBody, StringComparison.Ordinal);
        Assert.Contains(message.CorrelationId!, rawBody, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildP0Messages_MissingPayloadOmitsPayloadField()
    {
        var builder = new ControlledValidationMessageBuilder(CreateManifest());

        var message = builder.BuildP0Messages().Single(message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N1MissingPayload);
        var rawBody = Encoding.UTF8.GetString(message.Body);

        Assert.Contains("\"eventType\":\"SensorReadingProduced\"", rawBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"payload\"", rawBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildP0Messages_InvalidOperationalStateUsesInvalidPayload()
    {
        var builder = new ControlledValidationMessageBuilder(CreateManifest());

        var message = builder.BuildP0Messages().Single(message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N2InvalidOperationalState);
        var envelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(message.Body);

        Assert.NotNull(envelope);
        Assert.Equal(SensorOperationalState.Invalid, envelope!.Payload.OperationalState);
        Assert.Equal("invalid_operational_state", message.FaultCase.ExpectedReasonCode);
    }

    [Fact]
    public void BuildP0Messages_SensorNotFoundUsesMissingSensorId()
    {
        var manifest = CreateManifest();
        var builder = new ControlledValidationMessageBuilder(manifest);

        var message = builder.BuildP0Messages().Single(message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N3SensorNotFound);
        var envelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(message.Body);

        Assert.NotNull(envelope);
        Assert.Equal(manifest.SensorNotFoundId, envelope!.Payload.SensorId);
        Assert.NotEqual(manifest.NominalSensorId, envelope.Payload.SensorId);
        Assert.Equal(SensorOperationalState.Nominal, envelope.Payload.OperationalState);
        Assert.Equal("sensor_not_found", message.FaultCase.ExpectedReasonCode);
    }

    [Fact]
    public void BuildP0Messages_DuplicateMismatchUsesSameEventIdAndDivergentPayload()
    {
        var builder = new ControlledValidationMessageBuilder(CreateManifest());

        var messages = builder.BuildP0Messages()
            .Where(message => message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N4DuplicatePayloadMismatch)
            .ToArray();
        var setup = Assert.Single(messages, message => message.IsSetupMessage);
        var trigger = Assert.Single(messages, message => !message.IsSetupMessage);
        var setupEnvelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(setup.Body);
        var triggerEnvelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(trigger.Body);

        Assert.NotNull(setupEnvelope);
        Assert.NotNull(triggerEnvelope);
        Assert.Equal(setupEnvelope!.EventId, triggerEnvelope!.EventId);
        Assert.NotEqual(setupEnvelope.Payload.Value, triggerEnvelope.Payload.Value);
        Assert.Equal("duplicate_payload_mismatch", trigger.FaultCase.ExpectedReasonCode);
    }

    [Fact]
    public void BuildP1Messages_CreatesN5AndN6ProcessingFaultMessages()
    {
        var manifest = CreateManifest(
            "p1-smoke",
            ControlledValidationScenarioManifest.CreateDefaultP1FaultCases(),
            ControlledValidationPhases.P1);
        var builder = new ControlledValidationMessageBuilder(manifest);

        var messages = builder.BuildP1Messages();

        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N5TransientFailure &&
            message.Kind == ControlledValidationMessageKind.EnvelopeWithPayload &&
            message.CorrelationId == "cv:p1-smoke:N5_TRANSIENT_FAILURE:001");
        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N6PermanentFailure &&
            message.Kind == ControlledValidationMessageKind.EnvelopeWithPayload &&
            message.CorrelationId == "cv:p1-smoke:N6_PERMANENT_FAILURE:002");
        Assert.All(messages, message =>
        {
            var envelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(message.Body);
            Assert.NotNull(envelope);
            Assert.Equal(manifest.NominalSensorId, envelope!.Payload.SensorId);
            Assert.Equal(SensorOperationalState.Nominal, envelope.Payload.OperationalState);
        });
    }

    [Fact]
    public void BuildP3NegativePipelineMessages_CreatesExpectedMessages()
    {
        var manifest = CreateManifest(
            "p3-smoke",
            ControlledValidationScenarioManifest.CreateDefaultP3NegativePipelineFaultCases(),
            ControlledValidationPhases.P3NegativePipeline);
        var builder = new ControlledValidationMessageBuilder(manifest);

        var messages = builder.BuildP3NegativePipelineMessages();

        Assert.Equal(11, messages.Count);
        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RejectInvalidJson &&
            message.Kind == ControlledValidationMessageKind.RawInvalidJson &&
            message.EventId is null);
        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RejectMissingPayload &&
            message.Kind == ControlledValidationMessageKind.EnvelopeWithoutPayload);

        var missingPayload = messages.Single(message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RejectMissingPayload);
        Assert.DoesNotContain(
            "\"payload\"",
            Encoding.UTF8.GetString(missingPayload.Body),
            StringComparison.OrdinalIgnoreCase);

        var unsupportedEventType = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(
            messages.Single(message =>
                message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RejectUnsupportedEventType).Body);
        Assert.NotNull(unsupportedEventType);
        Assert.Equal(EventTypes.ReadingAccepted, unsupportedEventType!.EventType);

        var unsupportedSchemaVersion = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(
            messages.Single(message =>
                message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RejectUnsupportedSchemaVersion).Body);
        Assert.NotNull(unsupportedSchemaVersion);
        Assert.Equal("9.9", unsupportedSchemaVersion!.SchemaVersion);

        var invalidState = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(
            messages.Single(message =>
                message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RejectInvalidOperationalState).Body);
        Assert.NotNull(invalidState);
        Assert.Equal(SensorOperationalState.Invalid, invalidState!.Payload.OperationalState);

        var sensorNotFound = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(
            messages.Single(message =>
                message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3QuarantineSensorNotFound).Body);
        Assert.NotNull(sensorNotFound);
        Assert.Equal(manifest.SensorNotFoundId, sensorNotFound!.Payload.SensorId);
        Assert.NotEqual(manifest.NominalSensorId, sensorNotFound.Payload.SensorId);

        var duplicateMismatch = messages
            .Where(message => message.FaultCase.FaultCaseId ==
                ControlledValidationFaultCaseIds.P3QuarantineDuplicatePayloadMismatch)
            .ToArray();
        var setup = Assert.Single(duplicateMismatch, message => message.IsSetupMessage);
        var trigger = Assert.Single(duplicateMismatch, message => !message.IsSetupMessage);
        var setupEnvelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(setup.Body);
        var triggerEnvelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(trigger.Body);
        Assert.NotNull(setupEnvelope);
        Assert.NotNull(triggerEnvelope);
        Assert.Equal(setupEnvelope!.EventId, triggerEnvelope!.EventId);
        Assert.NotEqual(setupEnvelope.Payload.Value, triggerEnvelope.Payload.Value);

        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RetryTransientThenSuccess &&
            message.CorrelationId == "cv:p3-smoke:P3_RETRY_TRANSIENT_THEN_SUCCESS:009");
        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RetryExhaustedToQuarantine &&
            message.CorrelationId == "cv:p3-smoke:P3_RETRY_EXHAUSTED_TO_QUARANTINE:010");
        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3PermanentFailureToQuarantine &&
            message.CorrelationId == "cv:p3-smoke:P3_PERMANENT_FAILURE_TO_QUARANTINE:011");
        Assert.All(messages, message => Assert.Equal(64, message.BodySha256.Length));
    }

    [Fact]
    public void BuildP2Messages_CreatesMissingReadingsCoverageGapWithoutRejectedExpectations()
    {
        var manifest = CreateManifest(
            "p2-smoke",
            ControlledValidationScenarioManifest.CreateDefaultP2FaultCases(),
            ControlledValidationPhases.P2);
        var builder = new ControlledValidationMessageBuilder(manifest);

        var messages = builder.BuildP2Messages()
            .Where(message => message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2MissingReadings)
            .ToArray();

        Assert.Equal(3, messages.Length);
        Assert.All(messages, message =>
        {
            Assert.Equal(ControlledValidationExpectedOutcome.CoverageGap, message.FaultCase.ExpectedOutcome);
            Assert.Equal(5, message.FaultCase.ExpectedEvents);
            Assert.Equal(3, message.FaultCase.ExpectedPublishedEvents);
            Assert.Equal(2, message.FaultCase.ExpectedCoverageGap);
            Assert.Equal("missing-readings", message.FaultCase.ExpectedReasonCode);
            var envelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(message.Body);
            Assert.NotNull(envelope);
            Assert.Equal(SensorOperationalState.Nominal, envelope!.Payload.OperationalState);
        });
    }

    [Fact]
    public void BuildP2Messages_DuplicatePayloadIdenticalReplaysSameSerializedEnvelope()
    {
        var manifest = CreateManifest(
            "p2-smoke",
            ControlledValidationScenarioManifest.CreateDefaultP2FaultCases(),
            ControlledValidationPhases.P2);
        var builder = new ControlledValidationMessageBuilder(manifest);

        var messages = builder.BuildP2Messages()
            .Where(message => message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2DuplicatePayloadIdentical)
            .ToArray();
        var first = Assert.Single(messages, message => message.IsSetupMessage);
        var duplicate = Assert.Single(messages, message => !message.IsSetupMessage);

        Assert.Equal(first.EventId, duplicate.EventId);
        Assert.Equal(first.CorrelationId, duplicate.CorrelationId);
        Assert.Equal(first.BodySha256, duplicate.BodySha256);
        Assert.Equal(Encoding.UTF8.GetString(first.Body), Encoding.UTF8.GetString(duplicate.Body));
        Assert.Equal("idempotent_duplicate", duplicate.FaultCase.ExpectedReasonCode);
    }

    [Fact]
    public void BuildP2Messages_ValueProfilesRemainNominalAndAccepted()
    {
        var manifest = CreateManifest(
            "p2-smoke",
            ControlledValidationScenarioManifest.CreateDefaultP2FaultCases(),
            ControlledValidationPhases.P2);
        var builder = new ControlledValidationMessageBuilder(manifest);

        var messages = builder.BuildP2Messages()
            .Where(message => message.FaultCase.FaultLayer == ControlledValidationFaultLayer.ValueDegradation)
            .ToArray();

        Assert.Equal(8, messages.Length);
        Assert.Contains(messages, message => message.FaultCase.ValueProfile == "noise");
        Assert.Contains(messages, message => message.FaultCase.ValueProfile == "bias");
        Assert.Contains(messages, message => message.FaultCase.ValueProfile == "drift");
        Assert.Contains(messages, message => message.FaultCase.ValueProfile == "outlier-nominal");
        Assert.Contains(messages, message => message.FaultCase.ValueProfile == "stuck-value");
        Assert.Contains(messages, message => message.FaultCase.ValueProfile == "clipping-nominal");
        Assert.Contains(messages, message => message.FaultCase.ValueProfile == "range-boundary");
        Assert.Equal(
            2,
            messages.Count(message => message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2ValueStuck));
        Assert.All(messages, message =>
        {
            Assert.Equal(ControlledValidationExpectedOutcome.ValueDegraded, message.FaultCase.ExpectedOutcome);
            var envelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(message.Body);
            Assert.NotNull(envelope);
            Assert.Equal(manifest.NominalSensorId, envelope!.Payload.SensorId);
            Assert.Equal(SensorOperationalState.Nominal, envelope.Payload.OperationalState);
            Assert.InRange(envelope.Payload.Value, -50.0, 60.0);
        });
    }

    [Fact]
    public void BuildP2Messages_BlockedRangeEligibilityUsesOutOfRangeValueWithoutInvalidOperationalState()
    {
        var manifest = CreateManifest(
            "p2-smoke",
            ControlledValidationScenarioManifest.CreateDefaultP2FaultCases(),
            ControlledValidationPhases.P2);
        var builder = new ControlledValidationMessageBuilder(manifest);

        var message = builder.BuildP2Messages().Single(message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2BlockedRangeEligibility);
        var envelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(message.Body);

        Assert.Equal(ControlledValidationFaultLayer.Eligibility, message.FaultCase.FaultLayer);
        Assert.Equal(ControlledValidationExpectedOutcome.BlockedEligibility, message.FaultCase.ExpectedOutcome);
        Assert.Equal("temperature_out_of_candidate_range", message.FaultCase.ExpectedReasonCode);
        Assert.NotNull(envelope);
        Assert.Equal(manifest.NominalSensorId, envelope!.Payload.SensorId);
        Assert.Equal(SensorOperationalState.Nominal, envelope.Payload.OperationalState);
        Assert.True(envelope.Payload.Value > 60.0);
    }

    [Fact]
    public void BuildP2Messages_TemporalDelayedUsesDelayedStateAndExplicitLag()
    {
        var manifest = CreateManifest(
            "p2-smoke",
            ControlledValidationScenarioManifest.CreateDefaultP2FaultCases(),
            ControlledValidationPhases.P2);
        var builder = new ControlledValidationMessageBuilder(manifest);

        var message = builder.BuildP2Messages().Single(message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2TemporalDelayed);
        var envelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(message.Body);

        Assert.Equal(ControlledValidationFaultLayer.TemporalQuality, message.FaultCase.FaultLayer);
        Assert.Equal(ControlledValidationExpectedOutcome.TemporalQuality, message.FaultCase.ExpectedOutcome);
        Assert.Equal("delayed-reading", message.FaultCase.ExpectedReasonCode);
        Assert.NotNull(envelope);
        Assert.Equal(SensorOperationalState.Delayed, envelope!.Payload.OperationalState);
        Assert.NotNull(envelope.IngestTime);
        Assert.True(envelope.IngestTime.Value - envelope.EventTime >= TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void BuildMessages_RoutesP2ExtendedToP2ExtendedCases()
    {
        var manifest = CreateManifest(
            "p2-extended-smoke",
            ControlledValidationScenarioManifest.CreateDefaultP2FaultCases(),
            ControlledValidationPhases.P2Extended);
        var builder = new ControlledValidationMessageBuilder(manifest);

        var messages = builder.BuildMessages();

        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2ValueOutlier);
        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2BlockedRangeEligibility);
        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2TemporalDelayed);
        Assert.DoesNotContain(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N1InvalidJson);
    }

    [Fact]
    public void BuildMessages_RoutesP3NegativePipelineToP3Cases()
    {
        var manifest = CreateManifest(
            "p3-smoke",
            ControlledValidationScenarioManifest.CreateDefaultP3NegativePipelineFaultCases(),
            ControlledValidationPhases.P3NegativePipeline);
        var builder = new ControlledValidationMessageBuilder(manifest);

        var messages = builder.BuildMessages();

        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RejectUnsupportedEventType);
        Assert.Contains(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RetryExhaustedToQuarantine);
        Assert.DoesNotContain(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2MissingReadings);
        Assert.DoesNotContain(messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N1InvalidJson);
    }

    private static ControlledValidationScenarioManifest CreateManifest(
        string runLabel = "p0-smoke",
        IReadOnlyList<ValidationFaultCase>? faultCases = null,
        string phase = ControlledValidationPhases.P0)
    {
        return new ControlledValidationScenarioManifest(
            controlledValidationRunId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            runLabel: runLabel,
            scenarioCode: "scenario_b",
            areaId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            simulationRunId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            eventTime: new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero),
            nominalSensorId: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            nominalSensorName: "sensor-p0-001",
            sensorNotFoundId: Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            faultCases: faultCases ?? ControlledValidationScenarioManifest.CreateDefaultP0FaultCases(),
            phase: phase);
    }
}
