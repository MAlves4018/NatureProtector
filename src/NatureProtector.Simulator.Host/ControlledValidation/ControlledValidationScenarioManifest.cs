namespace NatureProtector.Simulator.Host.ControlledValidation;

public sealed record ControlledValidationScenarioManifest
{
    public ControlledValidationScenarioManifest(
        Guid controlledValidationRunId,
        string runLabel,
        string scenarioCode,
        Guid areaId,
        Guid simulationRunId,
        DateTimeOffset eventTime,
        Guid nominalSensorId,
        string nominalSensorName,
        Guid sensorNotFoundId,
        IReadOnlyList<ValidationFaultCase> faultCases,
        string phase = ControlledValidationPhases.P0)
    {
        if (controlledValidationRunId == Guid.Empty)
        {
            throw new ArgumentException("controlled_validation_run_id is required.", nameof(controlledValidationRunId));
        }

        if (string.IsNullOrWhiteSpace(runLabel))
        {
            throw new ArgumentException("run_label is required.", nameof(runLabel));
        }

        if (string.IsNullOrWhiteSpace(scenarioCode))
        {
            throw new ArgumentException("scenario_code is required.", nameof(scenarioCode));
        }

        if (string.IsNullOrWhiteSpace(phase))
        {
            throw new ArgumentException("phase is required.", nameof(phase));
        }

        if (areaId == Guid.Empty)
        {
            throw new ArgumentException("area_id is required.", nameof(areaId));
        }

        if (simulationRunId == Guid.Empty)
        {
            throw new ArgumentException("simulation_run_id is required.", nameof(simulationRunId));
        }

        if (nominalSensorId == Guid.Empty)
        {
            throw new ArgumentException("nominal_sensor_id is required.", nameof(nominalSensorId));
        }

        if (string.IsNullOrWhiteSpace(nominalSensorName))
        {
            throw new ArgumentException("nominal_sensor_name is required.", nameof(nominalSensorName));
        }

        if (sensorNotFoundId == Guid.Empty)
        {
            throw new ArgumentException("sensor_not_found_id is required.", nameof(sensorNotFoundId));
        }

        if (sensorNotFoundId == nominalSensorId)
        {
            throw new ArgumentException(
                "sensor_not_found_id must differ from nominal_sensor_id.",
                nameof(sensorNotFoundId));
        }

        if (faultCases is null || faultCases.Count == 0)
        {
            throw new ArgumentException("At least one fault case is required.", nameof(faultCases));
        }

        var duplicateFaultCaseIds = faultCases
            .GroupBy(faultCase => faultCase.FaultCaseId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateFaultCaseIds.Length > 0)
        {
            throw new ArgumentException(
                $"Duplicate fault_case_id values are not allowed: {string.Join(", ", duplicateFaultCaseIds)}.",
                nameof(faultCases));
        }

        ControlledValidationRunId = controlledValidationRunId;
        RunLabel = runLabel;
        ScenarioCode = scenarioCode;
        AreaId = areaId;
        SimulationRunId = simulationRunId;
        EventTime = eventTime;
        NominalSensorId = nominalSensorId;
        NominalSensorName = nominalSensorName;
        SensorNotFoundId = sensorNotFoundId;
        FaultCases = faultCases;
        Phase = phase;
    }

    public Guid ControlledValidationRunId { get; }

    public string RunLabel { get; }

    public string ScenarioCode { get; }

    public Guid AreaId { get; }

    public Guid SimulationRunId { get; }

    public DateTimeOffset EventTime { get; }

    public Guid NominalSensorId { get; }

    public string NominalSensorName { get; }

    public Guid SensorNotFoundId { get; }

    public IReadOnlyList<ValidationFaultCase> FaultCases { get; }

    public string Phase { get; }

    public static IReadOnlyList<ValidationFaultCase> CreateDefaultP0FaultCases()
    {
        return
        [
            new(
                ControlledValidationFaultCaseIds.N1InvalidJson,
                ControlledValidationFaultLayer.EventTransport,
                ControlledValidationExpectedOutcome.Rejected,
                "invalid_json",
                "Raw JSON invalido para provar rejected pre-inbox."),
            new(
                ControlledValidationFaultCaseIds.N1MissingPayload,
                ControlledValidationFaultLayer.EventTransport,
                ControlledValidationExpectedOutcome.Rejected,
                "missing_payload",
                "Envelope parseavel sem payload."),
            new(
                ControlledValidationFaultCaseIds.N2InvalidOperationalState,
                ControlledValidationFaultLayer.EventTransport,
                ControlledValidationExpectedOutcome.Rejected,
                "invalid_operational_state",
                "Envelope parseavel com OperationalState.Invalid."),
            new(
                ControlledValidationFaultCaseIds.N3SensorNotFound,
                ControlledValidationFaultLayer.Processing,
                ControlledValidationExpectedOutcome.Quarantined,
                "sensor_not_found",
                "Envelope valido com SensorId inexistente para quarantine pos-inbox."),
            new(
                ControlledValidationFaultCaseIds.N4DuplicatePayloadMismatch,
                ControlledValidationFaultLayer.EventTransport,
                ControlledValidationExpectedOutcome.Rejected,
                "duplicate_payload_mismatch",
                "Segundo envelope com EventId repetido e payload divergente.")
        ];
    }

    public static IReadOnlyList<ValidationFaultCase> CreateDefaultP1FaultCases()
    {
        return
        [
            new(
                ControlledValidationFaultCaseIds.N5TransientFailure,
                ControlledValidationFaultLayer.Processing,
                ControlledValidationExpectedOutcome.RetryThenSuccess,
                "transient_failure",
                "Envelope valido que deve falhar uma vez e depois passar via retry."),
            new(
                ControlledValidationFaultCaseIds.N6PermanentFailure,
                ControlledValidationFaultLayer.Processing,
                ControlledValidationExpectedOutcome.Quarantined,
                "permanent_failure",
                "Envelope valido que deve terminar em quarentena por falha permanente controlada.")
        ];
    }

    public static IReadOnlyList<ValidationFaultCase> CreateDefaultP2FaultCases()
    {
        return
        [
            new(
                ControlledValidationFaultCaseIds.P2MissingReadings,
                ControlledValidationFaultLayer.CoverageGap,
                ControlledValidationExpectedOutcome.CoverageGap,
                "missing-readings",
                "Coverage gap observacional: cinco leituras esperadas, tres publicadas e duas omitidas antes do RabbitMQ.",
                expectedEvents: 5,
                expectedPublishedEvents: 3,
                expectedCoverageGap: 2,
                valueProfile: "missing-readings"),
            new(
                ControlledValidationFaultCaseIds.P2DuplicatePayloadIdentical,
                ControlledValidationFaultLayer.Idempotency,
                ControlledValidationExpectedOutcome.IdempotentDuplicate,
                "idempotent_duplicate",
                "Mesmo EventId e payload serializado igual; deve ser idempotencia nominal, nao erro.",
                expectedEvents: 2,
                expectedPublishedEvents: 2,
                expectedCoverageGap: 0,
                valueProfile: "duplicate"),
            new(
                ControlledValidationFaultCaseIds.P2ValueNoise,
                ControlledValidationFaultLayer.ValueDegradation,
                ControlledValidationExpectedOutcome.ValueDegraded,
                "noise",
                "Leitura valida com valor degradado por ruido controlado para diversidade M1.",
                expectedEvents: 1,
                expectedPublishedEvents: 1,
                expectedCoverageGap: 0,
                valueProfile: "noise"),
            new(
                ControlledValidationFaultCaseIds.P2ValueBias,
                ControlledValidationFaultLayer.ValueDegradation,
                ControlledValidationExpectedOutcome.ValueDegraded,
                "bias",
                "Leitura valida com enviesamento controlado para diversidade M1.",
                expectedEvents: 1,
                expectedPublishedEvents: 1,
                expectedCoverageGap: 0,
                valueProfile: "bias"),
            new(
                ControlledValidationFaultCaseIds.P2ValueDrift,
                ControlledValidationFaultLayer.ValueDegradation,
                ControlledValidationExpectedOutcome.ValueDegraded,
                "drift",
                "Leitura valida com drift controlado para diversidade M1.",
                expectedEvents: 1,
                expectedPublishedEvents: 1,
                expectedCoverageGap: 0,
                valueProfile: "drift"),
            new(
                ControlledValidationFaultCaseIds.P2ValueOutlier,
                ControlledValidationFaultLayer.ValueDegradation,
                ControlledValidationExpectedOutcome.ValueDegraded,
                "outlier-nominal",
                "Leitura valida com valor alto mas dentro da gama candidata V1; deve permanecer accepted/risk.",
                expectedEvents: 1,
                expectedPublishedEvents: 1,
                expectedCoverageGap: 0,
                valueProfile: "outlier-nominal"),
            new(
                ControlledValidationFaultCaseIds.P2ValueStuck,
                ControlledValidationFaultLayer.ValueDegradation,
                ControlledValidationExpectedOutcome.ValueDegraded,
                "stuck-value",
                "Duas leituras validas com valor repetido para diversidade M1, sem converter flatline em M3 negativo.",
                expectedEvents: 2,
                expectedPublishedEvents: 2,
                expectedCoverageGap: 0,
                valueProfile: "stuck-value"),
            new(
                ControlledValidationFaultCaseIds.P2ValueClipping,
                ControlledValidationFaultLayer.ValueDegradation,
                ControlledValidationExpectedOutcome.ValueDegraded,
                "clipping-nominal",
                "Leitura valida no limite superior da gama candidata V1; deve permanecer accepted/risk.",
                expectedEvents: 1,
                expectedPublishedEvents: 1,
                expectedCoverageGap: 0,
                valueProfile: "clipping-nominal"),
            new(
                ControlledValidationFaultCaseIds.P2ValueRange,
                ControlledValidationFaultLayer.ValueDegradation,
                ControlledValidationExpectedOutcome.ValueDegraded,
                "range-boundary",
                "Leitura valida perto do limite superior da gama candidata V1; deve permanecer accepted/risk.",
                expectedEvents: 1,
                expectedPublishedEvents: 1,
                expectedCoverageGap: 0,
                valueProfile: "range-boundary"),
            new(
                ControlledValidationFaultCaseIds.P2BlockedRangeEligibility,
                ControlledValidationFaultLayer.Eligibility,
                ControlledValidationExpectedOutcome.BlockedEligibility,
                "temperature_out_of_candidate_range",
                "Leitura aceite como observacao mas bloqueada para scoring por valor fora da gama candidata V1.",
                expectedEvents: 1,
                expectedPublishedEvents: 1,
                expectedCoverageGap: 0,
                valueProfile: "blocked-range"),
            new(
                ControlledValidationFaultCaseIds.P2TemporalDelayed,
                ControlledValidationFaultLayer.TemporalQuality,
                ControlledValidationExpectedOutcome.TemporalQuality,
                "delayed-reading",
                "Leitura valida com atraso temporal explicito; deve ficar PartialButUsable e produzir risco.",
                expectedEvents: 1,
                expectedPublishedEvents: 1,
                expectedCoverageGap: 0,
                valueProfile: "temporal-delay")
        ];
    }

    public static IReadOnlyList<ValidationFaultCase> CreateDefaultP3NegativePipelineFaultCases()
    {
        return
        [
            new(
                ControlledValidationFaultCaseIds.P3RejectInvalidJson,
                ControlledValidationFaultLayer.EventTransport,
                ControlledValidationExpectedOutcome.Rejected,
                "invalid_json",
                "Raw JSON invalido para provar rejected tecnico pre-inbox em P3."),
            new(
                ControlledValidationFaultCaseIds.P3RejectMissingPayload,
                ControlledValidationFaultLayer.EventTransport,
                ControlledValidationExpectedOutcome.Rejected,
                "missing_payload",
                "Envelope parseavel sem payload para rejected tecnico P3."),
            new(
                ControlledValidationFaultCaseIds.P3RejectUnsupportedEventType,
                ControlledValidationFaultLayer.EventTransport,
                ControlledValidationExpectedOutcome.Rejected,
                "unsupported_event_type",
                "Envelope parseavel com EventType nao suportado pelo prevention consumer."),
            new(
                ControlledValidationFaultCaseIds.P3RejectUnsupportedSchemaVersion,
                ControlledValidationFaultLayer.EventTransport,
                ControlledValidationExpectedOutcome.Rejected,
                "unsupported_schema_version",
                "Envelope parseavel com SchemaVersion nao suportada."),
            new(
                ControlledValidationFaultCaseIds.P3RejectInvalidOperationalState,
                ControlledValidationFaultLayer.EventTransport,
                ControlledValidationExpectedOutcome.Rejected,
                "invalid_operational_state",
                "Envelope parseavel com OperationalState.Invalid."),
            new(
                ControlledValidationFaultCaseIds.P3QuarantineSensorNotFound,
                ControlledValidationFaultLayer.Processing,
                ControlledValidationExpectedOutcome.Quarantined,
                "sensor_not_found",
                "Envelope valido com SensorId inexistente para quarantine semantica pos-inbox."),
            new(
                ControlledValidationFaultCaseIds.P3QuarantineDuplicatePayloadMismatch,
                ControlledValidationFaultLayer.EventTransport,
                ControlledValidationExpectedOutcome.Rejected,
                "duplicate_payload_mismatch",
                "Par de envelopes com EventId repetido; setup valido e trigger divergente rejeitado.",
                expectedEvents: 2,
                expectedPublishedEvents: 2,
                expectedCoverageGap: 0),
            new(
                ControlledValidationFaultCaseIds.P3RetryTransientThenSuccess,
                ControlledValidationFaultLayer.Processing,
                ControlledValidationExpectedOutcome.RetryThenSuccess,
                "transient_failure",
                "Envelope valido que deve falhar uma vez, agendar retry e depois passar."),
            new(
                ControlledValidationFaultCaseIds.P3RetryExhaustedToQuarantine,
                ControlledValidationFaultLayer.Processing,
                ControlledValidationExpectedOutcome.Quarantined,
                "retries_exhausted",
                "Envelope valido que falha de forma transiente ate esgotar a politica de retry."),
            new(
                ControlledValidationFaultCaseIds.P3PermanentFailureToQuarantine,
                ControlledValidationFaultLayer.Processing,
                ControlledValidationExpectedOutcome.Quarantined,
                "permanent_failure",
                "Envelope valido que deve terminar em quarentena por falha permanente controlada.")
        ];
    }
}
