namespace NatureProtector.Simulator.Host.ControlledValidation;

public static class ControlledValidationFaultCaseIds
{
    public const string N1InvalidJson = "N1_INVALID_JSON";
    public const string N1MissingPayload = "N1_MISSING_PAYLOAD";
    public const string N2InvalidOperationalState = "N2_INVALID_OPERATIONAL_STATE";
    public const string N3SensorNotFound = "N3_SENSOR_NOT_FOUND";
    public const string N4DuplicatePayloadMismatch = "N4_DUPLICATE_PAYLOAD_MISMATCH";
    public const string N5TransientFailure = "N5_TRANSIENT_FAILURE";
    public const string N6PermanentFailure = "N6_PERMANENT_FAILURE";
    public const string P2MissingReadings = "P2_MISSING_READINGS";
    public const string P2DuplicatePayloadIdentical = "P2_DUPLICATE_PAYLOAD_IDENTICAL";
    public const string P2ValueNoise = "P2_VALUE_NOISE";
    public const string P2ValueBias = "P2_VALUE_BIAS";
    public const string P2ValueDrift = "P2_VALUE_DRIFT";
    public const string P2ValueOutlier = "P2_VALUE_OUTLIER";
    public const string P2ValueStuck = "P2_VALUE_STUCK";
    public const string P2ValueClipping = "P2_VALUE_CLIPPING";
    public const string P2ValueRange = "P2_VALUE_RANGE";
    public const string P2BlockedRangeEligibility = "P2_BLOCKED_RANGE_ELIGIBILITY";
    public const string P2TemporalDelayed = "P2_TEMPORAL_DELAYED";
    public const string P2TemporalOutOfOrder = "P2_TEMPORAL_OUT_OF_ORDER";
    public const string P3RejectInvalidJson = "P3_REJECT_INVALID_JSON";
    public const string P3RejectMissingPayload = "P3_REJECT_MISSING_PAYLOAD";
    public const string P3RejectUnsupportedEventType = "P3_REJECT_UNSUPPORTED_EVENT_TYPE";
    public const string P3RejectUnsupportedSchemaVersion = "P3_REJECT_UNSUPPORTED_SCHEMA_VERSION";
    public const string P3RejectInvalidOperationalState = "P3_REJECT_INVALID_OPERATIONAL_STATE";
    public const string P3QuarantineSensorNotFound = "P3_QUARANTINE_SENSOR_NOT_FOUND";
    public const string P3QuarantineDuplicatePayloadMismatch = "P3_QUARANTINE_DUPLICATE_PAYLOAD_MISMATCH";
    public const string P3RetryTransientThenSuccess = "P3_RETRY_TRANSIENT_THEN_SUCCESS";
    public const string P3RetryExhaustedToQuarantine = "P3_RETRY_EXHAUSTED_TO_QUARANTINE";
    public const string P3PermanentFailureToQuarantine = "P3_PERMANENT_FAILURE_TO_QUARANTINE";
}
