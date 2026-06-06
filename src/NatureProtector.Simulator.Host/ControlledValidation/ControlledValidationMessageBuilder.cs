using System.Text;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Simulator.Host.ControlledValidation;

public sealed class ControlledValidationMessageBuilder
{
    private const string Producer = "NatureProtector.Simulator.Host.ControlledValidation";

    private readonly ControlledValidationScenarioManifest _manifest;
    private readonly IReadOnlyDictionary<string, ValidationFaultCase> _faultCases;

    public ControlledValidationMessageBuilder(ControlledValidationScenarioManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        _manifest = manifest;
        _faultCases = manifest.FaultCases.ToDictionary(
            faultCase => faultCase.FaultCaseId,
            StringComparer.Ordinal);
    }

    public IReadOnlyList<ControlledValidationMessage> BuildP0Messages()
    {
        var messages = new List<ControlledValidationMessage>
        {
            BuildInvalidJson(sequence: 1),
            BuildMissingPayload(sequence: 2),
            BuildInvalidOperationalState(sequence: 3),
            BuildSensorNotFound(sequence: 4)
        };

        messages.AddRange(BuildDuplicatePayloadMismatch(sequence: 5));
        return messages;
    }

    public IReadOnlyList<ControlledValidationMessage> BuildP1Messages()
    {
        return
        [
            BuildProcessingFault(
                ControlledValidationFaultCaseIds.N5TransientFailure,
                sequence: 1,
                value: 31.2),
            BuildProcessingFault(
                ControlledValidationFaultCaseIds.N6PermanentFailure,
                sequence: 2,
                value: 33.4)
        ];
    }

    public IReadOnlyList<ControlledValidationMessage> BuildMessages()
    {
        return _manifest.Phase switch
        {
            ControlledValidationPhases.P3NegativePipeline => BuildP3NegativePipelineMessages(),
            ControlledValidationPhases.P2Extended => BuildP2Messages(),
            ControlledValidationPhases.P2 => BuildP2Messages(),
            ControlledValidationPhases.P1 => BuildP1Messages(),
            _ => BuildP0Messages()
        };
    }

    public IReadOnlyList<ControlledValidationMessage> BuildP2Messages()
    {
        var messages = new List<ControlledValidationMessage>();

        messages.AddRange(BuildMissingReadingsCoverageGap());
        messages.AddRange(BuildDuplicatePayloadIdentical(sequence: 4));
        messages.Add(BuildValueProfileMessage(
            ControlledValidationFaultCaseIds.P2ValueNoise,
            sequence: 6,
            value: 31.8));
        messages.Add(BuildValueProfileMessage(
            ControlledValidationFaultCaseIds.P2ValueBias,
            sequence: 7,
            value: 32.6));
        messages.Add(BuildValueProfileMessage(
            ControlledValidationFaultCaseIds.P2ValueDrift,
            sequence: 8,
            value: 33.1));
        messages.Add(BuildValueProfileMessage(
            ControlledValidationFaultCaseIds.P2ValueOutlier,
            sequence: 9,
            value: 45.0));
        messages.Add(BuildValueProfileMessage(
            ControlledValidationFaultCaseIds.P2ValueStuck,
            sequence: 10,
            value: 33.3));
        messages.Add(BuildValueProfileMessage(
            ControlledValidationFaultCaseIds.P2ValueStuck,
            sequence: 11,
            value: 33.3));
        messages.Add(BuildValueProfileMessage(
            ControlledValidationFaultCaseIds.P2ValueClipping,
            sequence: 12,
            value: 60.0));
        messages.Add(BuildValueProfileMessage(
            ControlledValidationFaultCaseIds.P2ValueRange,
            sequence: 13,
            value: 59.5));
        messages.Add(BuildValueProfileMessage(
            ControlledValidationFaultCaseIds.P2BlockedRangeEligibility,
            sequence: 14,
            value: 61.0));
        messages.Add(BuildTemporalDelayedMessage(sequence: 15));

        return messages;
    }

    public IReadOnlyList<ControlledValidationMessage> BuildP3NegativePipelineMessages()
    {
        var messages = new List<ControlledValidationMessage>
        {
            BuildInvalidJson(
                sequence: 1,
                faultCaseId: ControlledValidationFaultCaseIds.P3RejectInvalidJson),
            BuildMissingPayload(
                sequence: 2,
                faultCaseId: ControlledValidationFaultCaseIds.P3RejectMissingPayload),
            BuildUnsupportedEventType(sequence: 3),
            BuildUnsupportedSchemaVersion(sequence: 4),
            BuildInvalidOperationalState(
                sequence: 5,
                faultCaseId: ControlledValidationFaultCaseIds.P3RejectInvalidOperationalState),
            BuildSensorNotFound(
                sequence: 6,
                faultCaseId: ControlledValidationFaultCaseIds.P3QuarantineSensorNotFound)
        };

        messages.AddRange(BuildDuplicatePayloadMismatch(
            sequence: 7,
            faultCaseId: ControlledValidationFaultCaseIds.P3QuarantineDuplicatePayloadMismatch));
        messages.Add(BuildProcessingFault(
            ControlledValidationFaultCaseIds.P3RetryTransientThenSuccess,
            sequence: 9,
            value: 31.2));
        messages.Add(BuildProcessingFault(
            ControlledValidationFaultCaseIds.P3RetryExhaustedToQuarantine,
            sequence: 10,
            value: 32.8));
        messages.Add(BuildProcessingFault(
            ControlledValidationFaultCaseIds.P3PermanentFailureToQuarantine,
            sequence: 11,
            value: 33.4));

        return messages;
    }

    private ControlledValidationMessage BuildInvalidJson(
        int sequence,
        string faultCaseId = ControlledValidationFaultCaseIds.N1InvalidJson)
    {
        var faultCase = GetFaultCase(faultCaseId);
        var correlationId = ControlledValidationIdentity.CreateCorrelationId(
            _manifest.RunLabel,
            faultCase.FaultCaseId,
            sequence);
        var body = Encoding.UTF8.GetBytes(
            $$"""{"controlledValidationRunId":"{{_manifest.ControlledValidationRunId}}","runLabel":"{{_manifest.RunLabel}}","faultCaseId":"{{faultCase.FaultCaseId}}","correlationId":"{{correlationId}}","rawBodyMarker":""");

        return CreateMessage(
            faultCase,
            sequence,
            ControlledValidationMessageKind.RawInvalidJson,
            eventId: null,
            correlationId,
            body,
            isSetupMessage: false);
    }

    private ControlledValidationMessage BuildMissingPayload(
        int sequence,
        string faultCaseId = ControlledValidationFaultCaseIds.N1MissingPayload)
    {
        var faultCase = GetFaultCase(faultCaseId);
        var eventId = CreateEventId(faultCase.FaultCaseId, sequence);
        var correlationId = ControlledValidationIdentity.CreateCorrelationId(
            _manifest.RunLabel,
            faultCase.FaultCaseId,
            sequence);
        var body = JsonEventSerializer.SerializeToUtf8Bytes(new EnvelopeWithoutPayload(
            SchemaVersion: "1.0",
            EventId: eventId,
            CorrelationId: correlationId,
            Producer: Producer,
            EventType: EventTypes.SensorReadingProduced,
            AreaId: _manifest.AreaId,
            EventTime: _manifest.EventTime.AddSeconds(sequence),
            IngestTime: null));

        return CreateMessage(
            faultCase,
            sequence,
            ControlledValidationMessageKind.EnvelopeWithoutPayload,
            eventId,
            correlationId,
            body,
            isSetupMessage: false);
    }

    private ControlledValidationMessage BuildUnsupportedEventType(int sequence)
    {
        var faultCase = GetFaultCase(ControlledValidationFaultCaseIds.P3RejectUnsupportedEventType);
        var envelope = CreateEnvelope(
            faultCase,
            sequence,
            sensorId: _manifest.NominalSensorId,
            sensorName: _manifest.NominalSensorName,
            value: 28.9,
            operationalState: SensorOperationalState.Nominal) with
        {
            EventType = EventTypes.ReadingAccepted
        };

        return CreateEnvelopeMessage(faultCase, sequence, envelope, isSetupMessage: false);
    }

    private ControlledValidationMessage BuildUnsupportedSchemaVersion(int sequence)
    {
        var faultCase = GetFaultCase(ControlledValidationFaultCaseIds.P3RejectUnsupportedSchemaVersion);
        var envelope = CreateEnvelope(
            faultCase,
            sequence,
            sensorId: _manifest.NominalSensorId,
            sensorName: _manifest.NominalSensorName,
            value: 29.4,
            operationalState: SensorOperationalState.Nominal) with
        {
            SchemaVersion = "9.9"
        };

        return CreateEnvelopeMessage(faultCase, sequence, envelope, isSetupMessage: false);
    }

    private ControlledValidationMessage BuildInvalidOperationalState(
        int sequence,
        string faultCaseId = ControlledValidationFaultCaseIds.N2InvalidOperationalState)
    {
        var faultCase = GetFaultCase(faultCaseId);
        var envelope = CreateEnvelope(
            faultCase,
            sequence,
            sensorId: _manifest.NominalSensorId,
            sensorName: _manifest.NominalSensorName,
            value: 28.4,
            operationalState: SensorOperationalState.Invalid);

        return CreateEnvelopeMessage(faultCase, sequence, envelope, isSetupMessage: false);
    }

    private ControlledValidationMessage BuildSensorNotFound(
        int sequence,
        string faultCaseId = ControlledValidationFaultCaseIds.N3SensorNotFound)
    {
        var faultCase = GetFaultCase(faultCaseId);
        var envelope = CreateEnvelope(
            faultCase,
            sequence,
            sensorId: _manifest.SensorNotFoundId,
            sensorName: "controlled-validation-missing-sensor",
            value: 29.1,
            operationalState: SensorOperationalState.Nominal);

        return CreateEnvelopeMessage(faultCase, sequence, envelope, isSetupMessage: false);
    }

    private IReadOnlyList<ControlledValidationMessage> BuildDuplicatePayloadMismatch(
        int sequence,
        string faultCaseId = ControlledValidationFaultCaseIds.N4DuplicatePayloadMismatch)
    {
        var faultCase = GetFaultCase(faultCaseId);
        var eventId = CreateEventId(faultCase.FaultCaseId, sequence);
        var firstEnvelope = CreateEnvelope(
            faultCase,
            sequence,
            eventId,
            sensorId: _manifest.NominalSensorId,
            sensorName: _manifest.NominalSensorName,
            value: 30.0,
            operationalState: SensorOperationalState.Nominal);
        var secondEnvelope = CreateEnvelope(
            faultCase,
            sequence + 1,
            eventId,
            sensorId: _manifest.NominalSensorId,
            sensorName: _manifest.NominalSensorName,
            value: 37.5,
            operationalState: SensorOperationalState.Nominal);

        return
        [
            CreateEnvelopeMessage(faultCase, sequence, firstEnvelope, isSetupMessage: true),
            CreateEnvelopeMessage(faultCase, sequence + 1, secondEnvelope, isSetupMessage: false)
        ];
    }

    private ControlledValidationMessage BuildProcessingFault(
        string faultCaseId,
        int sequence,
        double value)
    {
        var faultCase = GetFaultCase(faultCaseId);
        var envelope = CreateEnvelope(
            faultCase,
            sequence,
            sensorId: _manifest.NominalSensorId,
            sensorName: _manifest.NominalSensorName,
            value: value,
            operationalState: SensorOperationalState.Nominal);

        return CreateEnvelopeMessage(faultCase, sequence, envelope, isSetupMessage: false);
    }

    private IReadOnlyList<ControlledValidationMessage> BuildMissingReadingsCoverageGap()
    {
        var faultCase = GetFaultCase(ControlledValidationFaultCaseIds.P2MissingReadings);

        return
        [
            CreateEnvelopeMessage(
                faultCase,
                sequence: 1,
                CreateEnvelope(
                    faultCase,
                    sequence: 1,
                    sensorId: _manifest.NominalSensorId,
                    sensorName: _manifest.NominalSensorName,
                    value: 30.1,
                    operationalState: SensorOperationalState.Nominal),
                isSetupMessage: false),
            CreateEnvelopeMessage(
                faultCase,
                sequence: 2,
                CreateEnvelope(
                    faultCase,
                    sequence: 2,
                    sensorId: _manifest.NominalSensorId,
                    sensorName: _manifest.NominalSensorName,
                    value: 30.4,
                    operationalState: SensorOperationalState.Nominal),
                isSetupMessage: false),
            CreateEnvelopeMessage(
                faultCase,
                sequence: 3,
                CreateEnvelope(
                    faultCase,
                    sequence: 3,
                    sensorId: _manifest.NominalSensorId,
                    sensorName: _manifest.NominalSensorName,
                    value: 30.7,
                    operationalState: SensorOperationalState.Nominal),
                isSetupMessage: false)
        ];
    }

    private IReadOnlyList<ControlledValidationMessage> BuildDuplicatePayloadIdentical(int sequence)
    {
        var faultCase = GetFaultCase(ControlledValidationFaultCaseIds.P2DuplicatePayloadIdentical);
        var envelope = CreateEnvelope(
            faultCase,
            sequence,
            sensorId: _manifest.NominalSensorId,
            sensorName: _manifest.NominalSensorName,
            value: 31.0,
            operationalState: SensorOperationalState.Nominal);
        var setup = CreateEnvelopeMessage(faultCase, sequence, envelope, isSetupMessage: true);

        return
        [
            setup,
            CreateMessage(
                faultCase,
                sequence + 1,
                setup.Kind,
                setup.EventId,
                setup.CorrelationId,
                setup.Body,
                isSetupMessage: false)
        ];
    }

    private ControlledValidationMessage BuildValueProfileMessage(
        string faultCaseId,
        int sequence,
        double value)
    {
        var faultCase = GetFaultCase(faultCaseId);
        var envelope = CreateEnvelope(
            faultCase,
            sequence,
            sensorId: _manifest.NominalSensorId,
            sensorName: _manifest.NominalSensorName,
            value: value,
            operationalState: SensorOperationalState.Nominal);

        return CreateEnvelopeMessage(faultCase, sequence, envelope, isSetupMessage: false);
    }

    private ControlledValidationMessage BuildTemporalDelayedMessage(int sequence)
    {
        var faultCase = GetFaultCase(ControlledValidationFaultCaseIds.P2TemporalDelayed);
        var eventTime = _manifest.EventTime.AddSeconds(sequence);
        var envelope = CreateEnvelope(
            faultCase,
            sequence,
            sensorId: _manifest.NominalSensorId,
            sensorName: _manifest.NominalSensorName,
            value: 34.2,
            operationalState: SensorOperationalState.Delayed,
            eventTime: eventTime,
            ingestTime: eventTime.AddMinutes(10));

        return CreateEnvelopeMessage(faultCase, sequence, envelope, isSetupMessage: false);
    }

    private ControlledValidationMessage CreateEnvelopeMessage(
        ValidationFaultCase faultCase,
        int sequence,
        EventEnvelope<SensorReadingProducedPayload> envelope,
        bool isSetupMessage)
    {
        var body = JsonEventSerializer.SerializeToUtf8Bytes(envelope);

        return CreateMessage(
            faultCase,
            sequence,
            ControlledValidationMessageKind.EnvelopeWithPayload,
            envelope.EventId,
            envelope.CorrelationId,
            body,
            isSetupMessage);
    }

    private EventEnvelope<SensorReadingProducedPayload> CreateEnvelope(
        ValidationFaultCase faultCase,
        int sequence,
        Guid sensorId,
        string sensorName,
        double value,
        SensorOperationalState operationalState,
        DateTimeOffset? eventTime = null,
        DateTimeOffset? ingestTime = null)
    {
        return CreateEnvelope(
            faultCase,
            sequence,
            CreateEventId(faultCase.FaultCaseId, sequence),
            sensorId,
            sensorName,
            value,
            operationalState,
            eventTime,
            ingestTime);
    }

    private EventEnvelope<SensorReadingProducedPayload> CreateEnvelope(
        ValidationFaultCase faultCase,
        int sequence,
        Guid eventId,
        Guid sensorId,
        string sensorName,
        double value,
        SensorOperationalState operationalState,
        DateTimeOffset? eventTime = null,
        DateTimeOffset? ingestTime = null)
    {
        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: eventId,
            CorrelationId: ControlledValidationIdentity.CreateCorrelationId(
                _manifest.RunLabel,
                faultCase.FaultCaseId,
                sequence),
            Producer: Producer,
            EventType: EventTypes.SensorReadingProduced,
            AreaId: _manifest.AreaId,
            EventTime: eventTime ?? _manifest.EventTime.AddSeconds(sequence),
            IngestTime: ingestTime,
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: _manifest.SimulationRunId,
                SensorId: sensorId,
                SensorName: sensorName,
                MetricType: SensorMetricType.Temperature,
                Unit: MeasurementUnit.Celsius,
                Value: value,
                Latitude: 39.80,
                Longitude: -7.92,
                OperationalState: operationalState));
    }

    private Guid CreateEventId(string faultCaseId, int sequence)
    {
        return ControlledValidationIdentity.CreateDeterministicGuid(
            $"{_manifest.ControlledValidationRunId:N}:{faultCaseId}:{sequence}");
    }

    private ValidationFaultCase GetFaultCase(string faultCaseId)
    {
        return _faultCases.TryGetValue(faultCaseId, out var faultCase)
            ? faultCase
            : throw new InvalidOperationException($"Fault case '{faultCaseId}' is not defined in the manifest.");
    }

    private static ControlledValidationMessage CreateMessage(
        ValidationFaultCase faultCase,
        int sequence,
        ControlledValidationMessageKind kind,
        Guid? eventId,
        string? correlationId,
        byte[] body,
        bool isSetupMessage)
    {
        return new ControlledValidationMessage(
            FaultCase: faultCase,
            Sequence: sequence,
            Kind: kind,
            EventId: eventId,
            CorrelationId: correlationId,
            Body: body,
            BodySha256: ControlledValidationIdentity.ComputeRawBodySha256(body),
            IsSetupMessage: isSetupMessage);
    }

    private sealed record EnvelopeWithoutPayload(
        string SchemaVersion,
        Guid EventId,
        string CorrelationId,
        string Producer,
        string EventType,
        Guid AreaId,
        DateTimeOffset EventTime,
        DateTimeOffset? IngestTime);
}
