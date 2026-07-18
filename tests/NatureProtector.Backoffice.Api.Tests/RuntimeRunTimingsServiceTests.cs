using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Backoffice.Api.Tests.TestInfrastructure;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.Infrastructure.Postgres.Projection;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RuntimeRunTimingsServiceTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RuntimeRunTimings_RunDoesNotExist_ReturnsNull()
    {
        await using var scope = new SqliteControlDbContextScope();
        var service = new PostgresControlPlaneService(scope.Factory);

        var timings = await service.GetRuntimeRunTimingsAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(timings);
    }

    [Fact]
    public async Task RuntimeRunTimings_RunWithoutAttempts_ReturnsRunDurationAndLimitations()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedTimingRuntimeAsync(scope);
        var service = new PostgresControlPlaneService(scope.Factory);

        var timings = await service.GetRuntimeRunTimingsAsync(seed.RunId, CancellationToken.None);

        Assert.NotNull(timings);
        Assert.Equal(seed.RunId, timings!.SimulationRunId);
        Assert.Equal(10_000, timings.RunDurationMs);
        Assert.Equal(0, timings.Attempts.AttemptCount);
        Assert.Empty(timings.Stages);
        Assert.Contains(timings.Limitations, item => item.Contains("processing_attempts", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RuntimeRunTimings_WithSucceededAttempt_ReturnsAttemptDurations()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedTimingRuntimeAsync(scope, includeInbox: true, includeSucceededAttempt: true);
        var service = new PostgresControlPlaneService(scope.Factory);

        var timings = await service.GetRuntimeRunTimingsAsync(seed.RunId, CancellationToken.None);

        Assert.NotNull(timings);
        Assert.Equal(BaseTime.AddSeconds(1), timings!.FirstInboxReceivedAt);
        Assert.Equal(BaseTime.AddSeconds(2), timings.FirstProcessingAttemptStartedAt);
        Assert.Equal(BaseTime.AddSeconds(3), timings.LastProcessingAttemptFinishedAt);
        Assert.Equal(1, timings.Attempts.AttemptCount);
        Assert.Equal(1, timings.Attempts.SuccessfulAttempts);
        Assert.Equal(1_000, timings.Attempts.MinDurationMs);
        Assert.Equal(1_000, timings.Attempts.AvgDurationMs);
        Assert.Equal(1_000, timings.Attempts.MaxDurationMs);
        Assert.Null(timings.Attempts.P50DurationMs);
        Assert.Null(timings.Attempts.P95DurationMs);
        Assert.Contains(timings.Stages, item => item.Stage == "reading_risk_pipeline" && item.Outcome == "Succeeded");
    }

    [Fact]
    public async Task RuntimeRunTimings_WithFailedAndQuarantinedAttempts_ReturnsOutcomeCounts()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedTimingRuntimeAsync(
            scope,
            includeInbox: true,
            includeFailedAttempt: true,
            includeQuarantinedAttempt: true);
        var service = new PostgresControlPlaneService(scope.Factory);

        var timings = await service.GetRuntimeRunTimingsAsync(seed.RunId, CancellationToken.None);

        Assert.NotNull(timings);
        Assert.Equal(2, timings!.Attempts.AttemptCount);
        Assert.Equal(1, timings.Attempts.FailedAttempts);
        Assert.Equal(1, timings.Attempts.QuarantinedAttempts);
        Assert.Equal(2_000, timings.Attempts.P50DurationMs);
        Assert.Equal(2_000, timings.Attempts.P95DurationMs);
        Assert.Equal(2_000, timings.Attempts.P99DurationMs);
        Assert.Contains(timings.Stages, item => item.Outcome == "Failed" && item.ErrorCode == "semantic_error");
        Assert.Contains(timings.Stages, item => item.Outcome == "Quarantined" && item.ErrorCode == "max_attempts");
    }

    [Fact]
    public async Task RuntimeRunTimings_WithRiskAssessment_ReturnsFirstRiskTiming()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedTimingRuntimeAsync(scope, includeRiskAssessment: true);
        var service = new PostgresControlPlaneService(scope.Factory);

        var timings = await service.GetRuntimeRunTimingsAsync(seed.RunId, CancellationToken.None);

        Assert.NotNull(timings);
        Assert.Equal(BaseTime.AddSeconds(5), timings!.FirstRiskAssessmentCreatedAt);
        Assert.Equal(5_000, timings.TimeToFirstRiskAssessmentMs);
    }

    [Fact]
    public async Task RuntimeRunTimings_WithAlert_ReturnsFirstAlertTiming()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedTimingRuntimeAsync(scope, includeAlert: true);
        var service = new PostgresControlPlaneService(scope.Factory);

        var timings = await service.GetRuntimeRunTimingsAsync(seed.RunId, CancellationToken.None);

        Assert.NotNull(timings);
        Assert.Equal(BaseTime.AddSeconds(8), timings!.FirstAlertTriggeredAt);
        Assert.Equal(8_000, timings.TimeToFirstAlertMs);
    }

    private static async Task<TimingSeedIds> SeedTimingRuntimeAsync(
        SqliteControlDbContextScope scope,
        bool includeInbox = false,
        bool includeSucceededAttempt = false,
        bool includeFailedAttempt = false,
        bool includeQuarantinedAttempt = false,
        bool includeRiskAssessment = false,
        bool includeAlert = false)
    {
        var configurationVersionId = Guid.Parse("11000000-0000-0000-0000-000000000001");
        var areaId = Guid.Parse("21000000-0000-0000-0000-000000000001");
        var cellId = Guid.Parse("31000000-0000-0000-0000-000000000001");
        var profileId = Guid.Parse("41000000-0000-0000-0000-000000000001");
        var sensorId = Guid.Parse("51000000-0000-0000-0000-000000000001");
        var scenarioId = Guid.Parse("61000000-0000-0000-0000-000000000001");
        var runId = Guid.Parse("71000000-0000-0000-0000-000000000001");
        var inboxEventId = Guid.Parse("81000000-0000-0000-0000-000000000001");
        var areaStateId = Guid.Parse("91000000-0000-0000-0000-000000000001");

        await scope.SeedAsync(async dbContext =>
        {
            dbContext.ConfigurationVersions.Add(new ConfigurationVersionRecord
            {
                Id = configurationVersionId,
                VersionNumber = 1,
                IsActive = true,
                CreatedAt = BaseTime.AddMinutes(-5),
                CreatedBy = "runtime-timings-tests"
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
                CreatedAt = BaseTime.AddMinutes(-1),
                StartedAt = BaseTime,
                EndedAt = BaseTime.AddSeconds(10),
                LogicalStartTimestamp = BaseTime.AddDays(-1),
                IntervalSeconds = 5,
                NumberOfCycles = 2,
                ExecutionSeed = 12345,
                Status = SimulationRunStatus.Completed,
                MetadataJson = "{}"
            });

            if (includeInbox || includeSucceededAttempt || includeFailedAttempt || includeQuarantinedAttempt)
            {
                dbContext.InboxEvents.Add(new InboxEventRecord
                {
                    Id = inboxEventId,
                    EventId = Guid.NewGuid(),
                    SchemaVersion = "1.0",
                    CorrelationId = "corr-timings",
                    Producer = "tests",
                    EventType = "SensorReadingProduced",
                    AreaId = areaId,
                    EventTime = BaseTime.AddSeconds(1),
                    ReceivedAt = BaseTime.AddSeconds(1),
                    PayloadJson = $$"""{"SimulationRunId":"{{runId}}"}""",
                    EnvelopeJson = "{}",
                    Status = InboxEventStatus.Processed,
                    AttemptCount = 1,
                    LastAttemptAt = BaseTime.AddSeconds(3),
                    LastProcessedAt = BaseTime.AddSeconds(3)
                });
            }

            if (includeSucceededAttempt)
            {
                dbContext.ProcessingAttempts.Add(new ProcessingAttemptRecord
                {
                    Id = Guid.NewGuid(),
                    InboxEventId = inboxEventId,
                    AttemptNumber = 1,
                    Stage = "reading_risk_pipeline",
                    StartedAt = BaseTime.AddSeconds(2),
                    FinishedAt = BaseTime.AddSeconds(3),
                    Outcome = ProcessingAttemptOutcome.Succeeded
                });
            }

            if (includeFailedAttempt)
            {
                dbContext.ProcessingAttempts.Add(new ProcessingAttemptRecord
                {
                    Id = Guid.NewGuid(),
                    InboxEventId = inboxEventId,
                    AttemptNumber = 1,
                    Stage = "reading_risk_pipeline",
                    StartedAt = BaseTime.AddSeconds(4),
                    FinishedAt = BaseTime.AddSeconds(6),
                    Outcome = ProcessingAttemptOutcome.Failed,
                    ErrorCode = "semantic_error",
                    ErrorMessage = "Synthetic failure"
                });
            }

            if (includeQuarantinedAttempt)
            {
                dbContext.ProcessingAttempts.Add(new ProcessingAttemptRecord
                {
                    Id = Guid.NewGuid(),
                    InboxEventId = inboxEventId,
                    AttemptNumber = 2,
                    Stage = "reading_risk_pipeline",
                    StartedAt = BaseTime.AddSeconds(7),
                    FinishedAt = BaseTime.AddSeconds(9),
                    Outcome = ProcessingAttemptOutcome.Quarantined,
                    ErrorCode = "max_attempts",
                    ErrorMessage = "Synthetic quarantine"
                });
            }

            if (includeRiskAssessment)
            {
                dbContext.RiskAssessmentLogs.Add(new RiskAssessmentLogRecord
                {
                    Id = Guid.NewGuid(),
                    AreaId = areaId,
                    SimulationRunId = runId,
                    SensorId = sensorId,
                    GridCellId = cellId,
                    SourceEventId = Guid.NewGuid(),
                    Timestamp = BaseTime.AddSeconds(5),
                    RiskScore = 0.75,
                    RiskLevel = "High",
                    ExplanationSummary = "InputStatus=CompleteEligible; synthetic",
                    CreatedAt = BaseTime.AddSeconds(5)
                });
            }

            if (includeAlert)
            {
                dbContext.AreaOperationalStates.Add(new AreaOperationalStateRecord
                {
                    Id = areaStateId,
                    AreaId = areaId,
                    ConfigurationVersionId = configurationVersionId,
                    SimulationRunId = runId,
                    SnapshotTimestamp = BaseTime.AddSeconds(7),
                    AggregateRiskScore = 0.91,
                    AggregateRiskLevel = "VeryHigh",
                    Severity = "Critical",
                    Summary = "Synthetic state",
                    AssessmentCount = 1,
                    UpdatedAt = BaseTime.AddSeconds(8)
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
                    TriggeredAt = BaseTime.AddSeconds(8),
                    UpdatedAt = BaseTime.AddSeconds(8)
                });
            }

            await Task.CompletedTask;
        });

        return new TimingSeedIds(runId);
    }

    private sealed record TimingSeedIds(Guid RunId);
}
