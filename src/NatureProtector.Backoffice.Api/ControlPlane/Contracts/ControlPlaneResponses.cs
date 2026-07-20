namespace NatureProtector.Backoffice.Api.ControlPlane.Contracts;

public sealed record ConfigurationVersionResponse(
    int VersionNumber,
    bool IsActive,
    string? Description,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    int AreaCount,
    int GridCellCount,
    int SensorNodeCount,
    int ScenarioCount,
    int SimulationRunCount);


public sealed record AreaGeoJSONResponse(
    Guid Id,
    string? GeometryGeoJson
);

public sealed record AreaContextResponse(
    string VegetationType,
    double VegetationDensity,
    double PopulationExposure,
    double CriticalInfrastructureExposure,
    string Seasonality);

public sealed record AreaSummaryResponse(
    Guid Id,
    string Code,
    string Name,
    string? CountryCode,
    int ConfigurationVersionNumber,
    int GridCellCount,
    int SensorNodeCount,
    int ScenarioCount);

public sealed record AreaDetailResponse(
    string Code,
    string Name,
    string? CountryCode,
    int ConfigurationVersionNumber,
    string? GeometryGeoJson,
    string? MetadataJson,
    AreaContextResponse? Context,
    int GridCellCount,
    int SensorNodeCount,
    int ScenarioCount);

public sealed record GridCellResponse(
    string CellCode,
    IReadOnlyList<Tuple<Guid, string>> sensorNodeIds,
    int ConfigurationVersionNumber,
    double CentroidLatitude,
    double CentroidLongitude,
    double? AltitudeMeters,
    double? SlopeDegrees,
    double? AspectDegrees,
    string? LandCoverClass,
    string? DominantForestType,
    string? DominantFuelModel,
    double? TreeCoverDensity,
    string? StructuralHazard,
    string? ConjuncturalHazard,
    int SensorNodeCount);

public sealed record SensorNodeResponse(
    Guid Id,
    string Name,
    string Type,
    int ConfigurationVersionNumber,
    string CellCode,
    string ProfileName,
    string? SensorFamily,
    string? NetworkName,
    double Latitude,
    double Longitude,
    double? AltitudeMeters,
    bool IsActive,
    string? InstallationProfile);

public sealed record ScenarioResponse(
    Guid Id,
    string Code,
    string Name,
    string ScenarioKind,
    int ConfigurationVersionNumber,
    string? Description,
    string? BaseScenarioCode,
    int DatasetBindingCount);

public sealed record SimulationRunResponse(
    Guid Id,
    string AreaCode,
    string ScenarioCode,
    string ScenarioName,
    string Status,
    int ConfigurationVersionNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    DateTimeOffset LogicalStartTimestamp,
    int IntervalSeconds,
    int NumberOfCycles,
    int? ExecutionSeed,
    string? MetadataJson);

public sealed record AreaOperationalStateResponse(
    string AreaCode,
    int ConfigurationVersionNumber,
    DateTimeOffset SnapshotTimestamp,
    double? AggregateRiskScore,
    string AggregateRiskLevel,
    string Severity,
    string? Summary,
    int AssessmentCount,
    DateTimeOffset UpdatedAt,
    string? AlertState = null,
    string? CoverageStatus = null,
    string? FreshnessStatus = null,
    string? CarryForwardStatus = null,
    DateTimeOffset? LastAssessmentTimestamp = null,
    DateTimeOffset? LastProjectionUpdatedAt = null,
    string? OperationalStatusReason = null);

public sealed record CellOperationalStateResponse(
    string AreaCode,
    string CellCode,
    int ConfigurationVersionNumber,
    DateTimeOffset SnapshotTimestamp,
    double RiskScore,
    string RiskLevel,
    string Severity,
    string? Summary,
    Guid? SensorId,
    string? SensorName,
    DateTimeOffset UpdatedAt,
    string? CoverageStatus = null,
    string? FreshnessStatus = null,
    string? CarryForwardStatus = null,
    DateTimeOffset? LastAcceptedReadingTimestamp = null,
    DateTimeOffset? LastRiskAssessmentTimestamp = null,
    string? OperationalStatusReason = null);

public sealed record AlertStateResponse(
    Guid Id,
    string AreaCode,
    int ConfigurationVersionNumber,
    string AlertCode,
    string Severity,
    string Status,
    string Message,
    DateTimeOffset TriggeredAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    string? AlertState = null);

public sealed record RuntimeSummaryResponse(
    DateTimeOffset GeneratedAtUtc,
    int RecentWindowMinutes,
    string? AreaCode,
    RuntimeRunSummaryResponse? CurrentRun,
    RuntimeRunSummaryResponse? LatestRun,
    RuntimePipelineSummaryResponse Pipeline,
    RuntimeRiskSummaryResponse Risk,
    RuntimeAreaOperationalSummaryResponse? AreaOperationalState,
    int CellOperationalStateCount,
    IReadOnlyList<RuntimeAlertSummaryResponse> ActiveAlerts,
    RuntimeFreshnessSummaryResponse? Freshness,
    RuntimeScoreComponentSummaryResponse? ScoreComponents,
    RuntimeIndexComparisonSummaryResponse? IndexComparison,
    IReadOnlyList<RuntimeLimitationResponse> Limitations,
    IReadOnlyList<string> Warnings);

public sealed record RuntimeScoreComponentSummaryResponse(
    double? NpScore,
    double? BaseRisk,
    double? AdjustedScore,
    int? Score100,
    double? MeteorologyComponent,
    double? DroughtComponent,
    double? TerritoryComponent,
    double? HazardComponent,
    double? FuelComponent,
    double? GeomorphologyComponent,
    double? ConfidenceFactor,
    double? IntegrityFactor,
    string? DominantDriver,
    string? ParameterSetVersion,
    string? CalculationStatus,
    string? Limitations,
    DateTimeOffset? LatestAssessmentTimestamp,
    string? NpRiskClass = null,
    string? NpRiskClassLabel = null);

public sealed record RuntimeIndexComparisonSummaryResponse(
    double? FireWeatherIndex,
    double? NormalizedFireWeatherIndex,
    string? FireWeatherCalculationStatus,
    double? KeetchByramDroughtIndex,
    double? NormalizedKeetchByramDroughtIndex,
    string? KbdiCalculationStatus,
    string? Provenance,
    string? Limitations,
    double? DailyPrecipitationMillimeters,
    DateTimeOffset? LogicalDate,
    double? CalculatedFireWeatherIndex = null,
    double? ReferenceFireWeatherIndex = null,
    string? FireWeatherIndexValueSource = null,
    string? FireWeatherIpmaClass = null,
    string? FireWeatherIpmaClassLabel = null,
    string? FireWeatherEffisClass = null,
    double? FireWeatherThresholdDistanceToNextClass = null,
    string? FireWeatherNextIpmaClass = null,
    double? CalculatedKeetchByramDroughtIndex = null,
    double? ReferenceKeetchByramDroughtIndex = null,
    string? KbdiValueSource = null,
    string? KbdiDrynessClass = null,
    string? KbdiDrynessClassLabel = null,
    string? KbdiAntecedentHistoryQuality = null,
    int? KbdiAntecedentDays = null,
    string? PortugueseContextRiskProxyClass = null,
    string? PortugueseContextRiskProxyLabel = null,
    string? TerritorialHazardProxyClass = null,
    string? LocalFwiPercentileStatus = null,
    double? LocalFwiPercentile = null,
    string? LocalFwiPercentileReason = null);

public sealed record RuntimeRunSummaryResponse(
    Guid Id,
    string AreaCode,
    string ScenarioCode,
    string ScenarioName,
    string Status,
    int ConfigurationVersionNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    double? DurationSeconds,
    DateTimeOffset LogicalStartTimestamp,
    int IntervalSeconds,
    int NumberOfCycles,
    int? ExecutionSeed,
    string? MetadataJson,
    string MetadataJsonStatus,
    string? OrchestratorCorrelationId,
    RuntimeRunOverridesResponse? RunOverrides);

public sealed record RuntimeRunAuditResponse(
    RuntimeRunSummaryResponse Run,
    int? ExpectedEvents,
    int AcceptedReadings,
    int? MissingEvents,
    int Rejected,
    int Quarantined,
    int RetryAttempts,
    int RiskAssessments,
    IReadOnlyList<RuntimeStatusCountResponse> QualityFlagsSummary,
    IReadOnlyList<RuntimeStatusCountResponse> EligibilitySummary,
    RuntimeAreaSnapshotAuditResponse? AreaSnapshot,
    IReadOnlyList<RuntimeLimitationResponse> Limitations,
    RuntimeScoreComponentSummaryResponse? ScoreComponents = null,
    RuntimeIndexComparisonSummaryResponse? IndexComparison = null,
    RuntimeDataScopeResponse? DataScope = null);

public sealed record RuntimeAreaSnapshotAuditResponse(
    DateTimeOffset SnapshotTimestamp,
    double AggregateRiskScore,
    string AggregateRiskLevel,
    int AssessmentCount,
    string? Summary);

public sealed record RuntimeRunOverridesResponse(
    RuntimeRunOverrideValuesResponse? Requested,
    RuntimeRunOverrideValuesResponse? Resolved,
    IReadOnlyList<string> SelectedSensorNames);

public sealed record RuntimeRunOverrideValuesResponse(
    int? SensorCount,
    int? NumberOfCycles,
    int? IntervalSeconds,
    int? Seed,
    string? DegradationProfile,
    string? OrchestratorCorrelationId,
    IReadOnlyList<string>? DegradationProfiles = null);

public sealed record RuntimePipelineSummaryResponse(
    int InboxTotal,
    int InboxRecent,
    IReadOnlyList<RuntimeStatusCountResponse> InboxByStatus,
    int AttemptsRecent,
    IReadOnlyList<RuntimeAttemptCountResponse> AttemptsByOutcomeAndError,
    int RejectedRecent,
    int RejectedTotal,
    IReadOnlyList<RuntimeCodeCountResponse> RejectedByCode,
    int QuarantinedRecent,
    int QuarantinedTotal,
    IReadOnlyList<RuntimeCodeCountResponse> QuarantinedByCode,
    IReadOnlyList<RuntimeRejectedEventResponse> LatestRejected,
    IReadOnlyList<RuntimeQuarantinedEventResponse> LatestQuarantined,
    IReadOnlyList<RuntimeProcessingAttemptResponse> LatestFailedAttempts);

public sealed record RuntimeStatusCountResponse(
    string Status,
    int Count);

public sealed record RuntimeAttemptCountResponse(
    string Outcome,
    string? ErrorCode,
    int Count);

public sealed record RuntimeCodeCountResponse(
    string Code,
    int Count);

public sealed record RuntimeRejectedEventResponse(
    Guid Id,
    Guid? EventId,
    string RejectionCode,
    string RejectionReason,
    DateTimeOffset RejectedAt,
    string? MetadataJson);

public sealed record RuntimeQuarantinedEventResponse(
    Guid Id,
    Guid EventId,
    int FinalAttemptNumber,
    string QuarantineCode,
    string QuarantineReason,
    DateTimeOffset QuarantinedAt,
    string? MetadataJson);

public sealed record RuntimeProcessingAttemptResponse(
    Guid Id,
    Guid InboxEventId,
    int AttemptNumber,
    string Stage,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string Outcome,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record RuntimeRiskSummaryResponse(
    int RecentCount,
    double? MinScore,
    double? MaxScore,
    DateTimeOffset? LatestTimestamp,
    IReadOnlyList<RuntimeRiskPointResponse> RecentScores);

public sealed record RuntimeRiskPointResponse(
    DateTimeOffset Timestamp,
    double RiskScore,
    string RiskLevel);

public sealed record RuntimeAreaOperationalSummaryResponse(
    string AreaCode,
    int ConfigurationVersionNumber,
    DateTimeOffset SnapshotTimestamp,
    double? AggregateRiskScore,
    string AggregateRiskLevel,
    string Severity,
    string? Summary,
    int AssessmentCount,
    DateTimeOffset UpdatedAt,
    string? AlertState,
    string? CoverageStatus = null,
    string? FreshnessStatus = null,
    string? CarryForwardStatus = null,
    DateTimeOffset? LastAssessmentTimestamp = null,
    DateTimeOffset? LastProjectionUpdatedAt = null,
    string? OperationalStatusReason = null);

public sealed record RuntimeAlertSummaryResponse(
    Guid Id,
    string AreaCode,
    int ConfigurationVersionNumber,
    string AlertCode,
    string Severity,
    string Status,
    string Message,
    DateTimeOffset TriggeredAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    string? AlertState);

public sealed record RuntimeLimitationResponse(
    string Code,
    string Message);

public static class RuntimeLimitations
{
    public static IReadOnlyList<RuntimeLimitationResponse> Default { get; } =
    [
        new("runtime_observability_separate_endpoint", "RabbitMQ metrics, evidence files and service health are exposed by /api/control/runtime/observability/* endpoints and /health; the runtime summary does not embed those payloads."),
        new("classifier_payload_details_not_persisted", "PostgreSQL runtime APIs expose persisted risk and operational projections, and run audit/diagnostics expose eligibility summaries, but detailed classifier payloads are not persisted.")
    ];

    public static IReadOnlyList<RuntimeLimitationResponse> ControlPlaneUnavailable(string availabilityMessage) =>
    [
        new("control_plane_unavailable", availabilityMessage),
        new("postgres_runtime_projection_unavailable", "PostgreSQL-backed runtime summaries, diagnostics and projections are unavailable while the control plane is disabled.")
    ];
}
