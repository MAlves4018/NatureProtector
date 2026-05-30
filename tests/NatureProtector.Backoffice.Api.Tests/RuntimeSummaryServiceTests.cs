using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Backoffice.Api.Tests.TestInfrastructure;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.Infrastructure.Postgres.Projection;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RuntimeSummaryServiceTests
{
    [Fact]
    public async Task RuntimeSummary_NoData_ReturnsZeroCountsAndLimitations()
    {
        await using var scope = new SqliteControlDbContextScope();
        var service = new PostgresControlPlaneService(scope.Factory);

        var summary = await service.GetRuntimeSummaryAsync(areaCode: null, recentMinutes: 30, CancellationToken.None);

        Assert.Null(summary.CurrentRun);
        Assert.Null(summary.LatestRun);
        Assert.Equal(0, summary.Pipeline.InboxTotal);
        Assert.Equal(0, summary.Pipeline.AttemptsRecent);
        Assert.Equal(0, summary.Risk.RecentCount);
        Assert.Null(summary.AreaOperationalState);
        Assert.Empty(summary.ActiveAlerts);
        Assert.Contains(summary.Limitations, limitation => limitation.Code == "rabbitmq_metrics_unavailable");
    }

    [Fact]
    public async Task RuntimeSummary_WithPersistedRuntimeData_ReturnsAggregatesWithoutRecalculatingRisk()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedRuntimeAsync(scope, metadataJson: ValidMetadataJson);
        var service = new PostgresControlPlaneService(scope.Factory);

        var summary = await service.GetRuntimeSummaryAsync("proenca-a-nova", 30, CancellationToken.None);

        Assert.NotNull(summary.LatestRun);
        Assert.Equal(seed.RunId, summary.LatestRun!.Id);
        Assert.Equal("scenario_b", summary.LatestRun.ScenarioCode);
        Assert.Equal("corr-123", summary.LatestRun.OrchestratorCorrelationId);
        Assert.Equal(6, summary.LatestRun.RunOverrides!.Requested!.SensorCount);
        Assert.Equal(2, summary.LatestRun.RunOverrides.SelectedSensorNames.Count);

        Assert.Equal(2, summary.Pipeline.InboxTotal);
        Assert.Contains(summary.Pipeline.InboxByStatus, item => item.Status == "Processed" && item.Count == 1);
        Assert.Contains(summary.Pipeline.InboxByStatus, item => item.Status == "RetryPending" && item.Count == 1);
        Assert.Equal(2, summary.Pipeline.AttemptsRecent);
        Assert.Contains(summary.Pipeline.AttemptsByOutcomeAndError, item => item.Outcome == "Succeeded" && item.Count == 1);
        Assert.Contains(summary.Pipeline.AttemptsByOutcomeAndError, item => item.Outcome == "Failed" && item.ErrorCode == "semantic_error" && item.Count == 1);
        Assert.Equal(1, summary.Pipeline.RejectedRecent);
        Assert.Equal(1, summary.Pipeline.RejectedTotal);
        Assert.Contains(summary.Pipeline.RejectedByCode, item => item.Code == "invalid_payload" && item.Count == 1);
        Assert.Equal(1, summary.Pipeline.QuarantinedRecent);
        Assert.Equal(1, summary.Pipeline.QuarantinedTotal);
        Assert.Contains(summary.Pipeline.QuarantinedByCode, item => item.Code == "max_attempts" && item.Count == 1);
        Assert.Single(summary.Pipeline.LatestFailedAttempts);

        Assert.Equal(2, summary.Risk.RecentCount);
        Assert.Equal(0.42, summary.Risk.MinScore);
        Assert.Equal(0.88, summary.Risk.MaxScore);
        Assert.Equal("VeryHigh", summary.AreaOperationalState!.AggregateRiskLevel);
        Assert.Equal("Alarm", summary.AreaOperationalState.AlertState);
        Assert.Equal("Partial", summary.AreaOperationalState.CoverageStatus);
        Assert.Equal("Stale", summary.AreaOperationalState.FreshnessStatus);
        Assert.Equal("CarriedForward", summary.AreaOperationalState.CarryForwardStatus);
        Assert.Equal(summary.AreaOperationalState.SnapshotTimestamp, summary.AreaOperationalState.LastAssessmentTimestamp);
        Assert.Equal(summary.AreaOperationalState.UpdatedAt, summary.AreaOperationalState.LastProjectionUpdatedAt);
        Assert.Equal("coverage=Partial; freshness=Stale; carryForward=CarriedForward", summary.AreaOperationalState.OperationalStatusReason);
        Assert.Single(summary.ActiveAlerts);
        Assert.Equal("Alarm", summary.ActiveAlerts[0].AlertState);
        Assert.Empty(summary.Warnings);
    }

    [Fact]
    public async Task RuntimeSummary_InvalidRunMetadata_ReturnsRawMetadataAndWarning()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedRuntimeAsync(scope, metadataJson: "{invalid-json");
        var service = new PostgresControlPlaneService(scope.Factory);

        var summary = await service.GetRuntimeSummaryAsync("proenca-a-nova", 30, CancellationToken.None);

        Assert.NotNull(summary.LatestRun);
        Assert.Equal(seed.RunId, summary.LatestRun!.Id);
        Assert.Equal("{invalid-json", summary.LatestRun.MetadataJson);
        Assert.Equal("invalid", summary.LatestRun.MetadataJsonStatus);
        Assert.Contains(summary.Warnings, warning => warning.Contains(seed.RunId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task RuntimeRunAudit_WithPersistedRuntimeData_ReturnsRunScopedAudit()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedRuntimeAsync(scope, metadataJson: ValidMetadataJson);
        var service = new PostgresControlPlaneService(scope.Factory);

        var audit = await service.GetRuntimeRunAuditAsync(seed.RunId, CancellationToken.None);

        Assert.NotNull(audit);
        Assert.Equal(seed.RunId, audit!.Run.Id);
        Assert.Equal("scenario_b", audit.Run.ScenarioCode);
        Assert.Equal("none", audit.Run.RunOverrides!.Resolved!.DegradationProfile);
        Assert.Equal(30, audit.ExpectedEvents);
        Assert.Equal(2, audit.AcceptedReadings);
        Assert.Equal(28, audit.MissingEvents);
        Assert.Equal(2, audit.RiskAssessments);
        Assert.Contains(audit.QualityFlagsSummary, item => item.Status == "Delayed" && item.Count == 1);
        Assert.Contains(audit.QualityFlagsSummary, item => item.Status == "Missing" && item.Count == 28);
        Assert.Contains(audit.EligibilitySummary, item => item.Status == "CompleteEligible" && item.Count == 1);
        Assert.Contains(audit.EligibilitySummary, item => item.Status == "PartialButUsable" && item.Count == 1);
        Assert.NotNull(audit.AreaSnapshot);
        Assert.Equal(2, audit.AreaSnapshot!.AssessmentCount);
        Assert.NotNull(audit.ScoreComponents);
        Assert.Equal(0.88, audit.ScoreComponents!.NpScore);
        Assert.NotNull(audit.IndexComparison);
        Assert.Equal("Complete", audit.IndexComparison!.FireWeatherCalculationStatus);
        Assert.Equal("Complete", audit.IndexComparison.KbdiCalculationStatus);
        Assert.Contains(audit.Limitations, item => item.Code == "diagnostics_do_not_recalculate_risk");
    }

    private static async Task<SeededRuntimeIds> SeedRuntimeAsync(
        SqliteControlDbContextScope scope,
        string metadataJson)
    {
        var now = DateTimeOffset.UtcNow;
        var configurationVersionId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var areaId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var cellId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var profileId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var sensorId = Guid.Parse("50000000-0000-0000-0000-000000000001");
        var scenarioId = Guid.Parse("60000000-0000-0000-0000-000000000001");
        var runId = Guid.Parse("70000000-0000-0000-0000-000000000001");
        var inboxEventId = Guid.Parse("80000000-0000-0000-0000-000000000001");
        var retryInboxEventId = Guid.Parse("80000000-0000-0000-0000-000000000002");
        var areaStateId = Guid.Parse("90000000-0000-0000-0000-000000000001");
        var acceptedEventId = Guid.Parse("81000000-0000-0000-0000-000000000001");
        var delayedEventId = Guid.Parse("81000000-0000-0000-0000-000000000002");

        await scope.SeedAsync(async dbContext =>
        {
            dbContext.ConfigurationVersions.Add(new ConfigurationVersionRecord
            {
                Id = configurationVersionId,
                VersionNumber = 1,
                IsActive = true,
                CreatedAt = now.AddHours(-1),
                CreatedBy = "runtime-tests"
            });

            dbContext.Areas.Add(new AreaRecord
            {
                Id = areaId,
                ConfigurationVersionId = configurationVersionId,
                Code = "proenca-a-nova",
                Name = "Proenca-a-Nova",
                CountryCode = "PT"
            });

            dbContext.GridCells.Add(new GridCellRecord
            {
                Id = cellId,
                AreaId = areaId,
                ConfigurationVersionId = configurationVersionId,
                CellCode = "PRO-001",
                CentroidLatitude = 39.75,
                CentroidLongitude = -7.90
            });

            dbContext.SensorProfiles.Add(new SensorProfileRecord
            {
                Id = profileId,
                ConfigurationVersionId = configurationVersionId,
                Name = "pilot-profile"
            });

            dbContext.SensorNodes.Add(new SensorNodeRecord
            {
                Id = sensorId,
                AreaId = areaId,
                GridCellId = cellId,
                ProfileId = profileId,
                ConfigurationVersionId = configurationVersionId,
                Name = "pilot-temperature-0001",
                Type = SensorType.Temperature,
                Latitude = 39.75,
                Longitude = -7.90,
                IsActive = true
            });

            dbContext.ScenarioDefinitions.Add(new ScenarioDefinitionRecord
            {
                Id = scenarioId,
                AreaId = areaId,
                ConfigurationVersionId = configurationVersionId,
                Code = "scenario_b",
                Name = "Scenario B",
                ScenarioKind = ScenarioCategory.HighRisk
            });

            dbContext.SimulationRuns.Add(new SimulationRunRecord
            {
                Id = runId,
                AreaId = areaId,
                ScenarioId = scenarioId,
                ConfigurationVersionId = configurationVersionId,
                ScenarioCode = "scenario_b",
                ScenarioName = "Scenario B",
                CreatedAt = now.AddMinutes(-10),
                StartedAt = now.AddMinutes(-10),
                EndedAt = now.AddMinutes(-9),
                LogicalStartTimestamp = now.AddDays(-1),
                IntervalSeconds = 5,
                NumberOfCycles = 5,
                ExecutionSeed = 12345,
                Status = SimulationRunStatus.Completed,
                MetadataJson = metadataJson
            });

            dbContext.InboxEvents.AddRange(
                new InboxEventRecord
                {
                    Id = inboxEventId,
                    EventId = Guid.NewGuid(),
                    SchemaVersion = "1.0",
                    CorrelationId = "corr-123",
                    Producer = "tests",
                    EventType = "SensorReadingProduced",
                    AreaId = areaId,
                    EventTime = now.AddMinutes(-8),
                    ReceivedAt = now.AddMinutes(-8),
                    PayloadJson = $$"""{"SimulationRunId":"{{runId}}"}""",
                    EnvelopeJson = "{}",
                    Status = InboxEventStatus.Processed,
                    AttemptCount = 1,
                    LastAttemptAt = now.AddMinutes(-8),
                    LastProcessedAt = now.AddMinutes(-8)
                },
                new InboxEventRecord
                {
                    Id = retryInboxEventId,
                    EventId = Guid.NewGuid(),
                    SchemaVersion = "1.0",
                    CorrelationId = "corr-124",
                    Producer = "tests",
                    EventType = "SensorReadingProduced",
                    AreaId = areaId,
                    EventTime = now.AddMinutes(-7),
                    ReceivedAt = now.AddMinutes(-7),
                    PayloadJson = $$"""{"SimulationRunId":"{{runId}}"}""",
                    EnvelopeJson = "{}",
                    Status = InboxEventStatus.RetryPending,
                    AttemptCount = 2,
                    LastAttemptAt = now.AddMinutes(-7),
                    LastErrorCode = "semantic_error"
                });

            dbContext.AcceptedReadingLogs.AddRange(
                new AcceptedReadingLogRecord
                {
                    Id = Guid.NewGuid(),
                    EventId = acceptedEventId,
                    AreaId = areaId,
                    SensorId = sensorId,
                    MetricType = "Temperature",
                    MeasurementUnit = "Celsius",
                    OperationalState = "Nominal",
                    Value = 31.0,
                    EventTime = now.AddMinutes(-8),
                    Producer = "tests",
                    CorrelationId = "corr-123",
                    PayloadJson = $$"""{"SimulationRunId":"{{runId}}"}""",
                    EnvelopeJson = "{}",
                    CreatedAt = now.AddMinutes(-8)
                },
                new AcceptedReadingLogRecord
                {
                    Id = Guid.NewGuid(),
                    EventId = delayedEventId,
                    AreaId = areaId,
                    SensorId = sensorId,
                    MetricType = "Humidity",
                    MeasurementUnit = "Percent",
                    OperationalState = "Delayed",
                    Value = 20.0,
                    EventTime = now.AddMinutes(-7),
                    Producer = "tests",
                    CorrelationId = "corr-124",
                    PayloadJson = $$"""{"SimulationRunId":"{{runId}}"}""",
                    EnvelopeJson = "{}",
                    CreatedAt = now.AddMinutes(-7)
                });

            dbContext.ProcessingAttempts.AddRange(
                new ProcessingAttemptRecord
                {
                    Id = Guid.NewGuid(),
                    InboxEventId = inboxEventId,
                    AttemptNumber = 1,
                    Stage = "reading_risk_pipeline",
                    StartedAt = now.AddMinutes(-8),
                    FinishedAt = now.AddMinutes(-8),
                    Outcome = ProcessingAttemptOutcome.Succeeded
                },
                new ProcessingAttemptRecord
                {
                    Id = Guid.NewGuid(),
                    InboxEventId = retryInboxEventId,
                    AttemptNumber = 2,
                    Stage = "reading_risk_pipeline",
                    StartedAt = now.AddMinutes(-7),
                    FinishedAt = now.AddMinutes(-7),
                    Outcome = ProcessingAttemptOutcome.Failed,
                    ErrorCode = "semantic_error",
                    ErrorMessage = "Synthetic failure"
                });

            dbContext.RejectedEvents.Add(new RejectedEventRecord
            {
                Id = Guid.NewGuid(),
                InboxEventId = retryInboxEventId,
                EventId = Guid.NewGuid(),
                RejectionCode = "invalid_payload",
                RejectionReason = "Synthetic rejection",
                RejectedAt = now.AddMinutes(-6),
                RawBodyUtf8 = "{}",
                MetadataJson = "{\"stage\":\"broker_receive\"}"
            });

            dbContext.QuarantinedEvents.Add(new QuarantinedEventRecord
            {
                Id = Guid.NewGuid(),
                InboxEventId = retryInboxEventId,
                EventId = Guid.NewGuid(),
                FinalAttemptNumber = 3,
                QuarantineCode = "max_attempts",
                QuarantineReason = "Synthetic quarantine",
                QuarantinedAt = now.AddMinutes(-5),
                MetadataJson = "{\"stage\":\"reading_risk_pipeline\"}"
            });

            dbContext.RiskAssessmentLogs.AddRange(
                new RiskAssessmentLogRecord
                {
                    Id = Guid.NewGuid(),
                    AreaId = areaId,
                    SimulationRunId = runId,
                    SensorId = sensorId,
                    GridCellId = cellId,
                    SourceEventId = acceptedEventId,
                    Timestamp = now.AddMinutes(-8),
                    RiskScore = 0.42,
                    RiskLevel = "Medium",
                    ExplanationSummary = "InputStatus=CompleteEligible; synthetic",
                    CreatedAt = now.AddMinutes(-8)
                },
                new RiskAssessmentLogRecord
                {
                    Id = Guid.NewGuid(),
                    AreaId = areaId,
                    SimulationRunId = runId,
                    SensorId = sensorId,
                    GridCellId = cellId,
                    SourceEventId = delayedEventId,
                    Timestamp = now.AddMinutes(-7),
                    RiskScore = 0.88,
                    BaseRisk = 0.76,
                    AdjustedScore = 0.72,
                    Score100 = 72,
                    MeteorologyComponent = 0.8,
                    DroughtComponent = 0.5,
                    TerritoryComponent = 0.7,
                    HazardComponent = 0.7,
                    FuelComponent = 0.6,
                    GeomorphologyComponent = 0.8,
                    ConfidenceFactor = 0.95,
                    IntegrityFactor = 1,
                    DominantDriver = "Meteorology",
                    ParameterSetVersion = "Candidate Parameter Set V1.0",
                    CalculationStatus = "Complete",
                    RiskLevel = "High",
                    ExplanationSummary = "InputStatus=PartialButUsable; synthetic",
                    CreatedAt = now.AddMinutes(-7)
                });

            dbContext.DailyCellStates.Add(new DailyCellStateRecord
            {
                Id = Guid.NewGuid(),
                AreaId = areaId,
                GridCellId = cellId,
                SimulationRunId = runId,
                ConfigurationVersionId = configurationVersionId,
                LogicalDate = now.Date,
                DailyPrecipitationMillimeters = 0.4,
                MaxTemperatureCelsius = 36.1,
                LatestHumidityPercent = 24,
                LatestWindSpeedMetersPerSecond = 8.2,
                AntecedentState = "CandidateDefault",
                DroughtContext = "KBDI candidate",
                FireWeatherIndex = 18.5,
                NormalizedFireWeatherIndex = 0.62,
                FireWeatherCalculationStatus = "Complete",
                KeetchByramDroughtIndex = 420,
                NormalizedKeetchByramDroughtIndex = 0.525,
                KbdiCalculationStatus = "Complete",
                FireIndexProvenance = "calculated_candidate",
                CandidateParameterSetVersion = "Candidate Parameter Set V1.0",
                Provenance = "test",
                LastUpdatedAt = now.AddMinutes(-7),
                CreatedAt = now.AddMinutes(-7),
                UpdatedAt = now.AddMinutes(-7)
            });

            dbContext.AreaRiskSnapshotLogs.Add(new AreaRiskSnapshotLogRecord
            {
                Id = Guid.NewGuid(),
                AreaId = areaId,
                SimulationRunId = runId,
                SnapshotTimestamp = now.AddMinutes(-7),
                AggregateRiskScore = 0.88,
                AggregateRiskLevel = "VeryHigh",
                Summary = "Synthetic run snapshot",
                AssessmentCount = 2,
                CreatedAt = now.AddMinutes(-7)
            });

            dbContext.AreaOperationalStates.Add(new AreaOperationalStateRecord
            {
                Id = areaStateId,
                AreaId = areaId,
                ConfigurationVersionId = configurationVersionId,
                SimulationRunId = runId,
                SnapshotTimestamp = now.AddMinutes(-7),
                AggregateRiskScore = 0.88,
                AggregateRiskLevel = "VeryHigh",
                Severity = "Critical",
                CoverageStatus = "Partial",
                FreshnessStatus = "Stale",
                CarryForwardStatus = "CarriedForward",
                Summary = "Synthetic state",
                AssessmentCount = 2,
                UpdatedAt = now.AddMinutes(-6)
            });

            dbContext.CellOperationalStates.Add(new CellOperationalStateRecord
            {
                Id = Guid.NewGuid(),
                AreaId = areaId,
                GridCellId = cellId,
                SensorId = sensorId,
                SnapshotTimestamp = now.AddMinutes(-7),
                RiskScore = 0.88,
                RiskLevel = "VeryHigh",
                Severity = "Critical",
                CoverageStatus = "Partial",
                FreshnessStatus = "Stale",
                CarryForwardStatus = "CarriedForward",
                UpdatedAt = now.AddMinutes(-6)
            });

            dbContext.AlertStates.Add(new AlertStateRecord
            {
                Id = Guid.NewGuid(),
                AreaId = areaId,
                ConfigurationVersionId = configurationVersionId,
                AreaOperationalStateId = areaStateId,
                AlertCode = "area-risk-high",
                Severity = "Critical",
                Status = "Open",
                Message = "AlertState=Alarm; Synthetic alert.",
                TriggeredAt = now.AddMinutes(-6),
                UpdatedAt = now.AddMinutes(-6)
            });

            await Task.CompletedTask;
        });

        return new SeededRuntimeIds(runId);
    }

    private const string ValidMetadataJson = """
        {
          "sensor_count": 6,
          "scenario_category": "HighRisk",
          "orchestrator_correlation_id": "corr-123",
          "run_overrides": {
            "requested": {
              "sensor_count": 6,
              "number_of_cycles": 5,
              "interval_seconds": 5,
              "seed": 12345,
              "degradation_profile": "none",
              "orchestrator_correlation_id": "corr-123"
            },
            "resolved": {
              "sensor_count": 6,
              "number_of_cycles": 5,
              "interval_seconds": 5,
              "seed": 12345,
              "degradation_profile": "none",
              "orchestrator_correlation_id": "corr-123",
              "selected_sensor_names": [
                "pilot-temperature-0001",
                "pilot-humidity-0001"
              ]
            }
          }
        }
        """;

    private sealed record SeededRuntimeIds(Guid RunId);
}
