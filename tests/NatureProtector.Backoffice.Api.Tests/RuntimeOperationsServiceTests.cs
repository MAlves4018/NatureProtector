using Microsoft.EntityFrameworkCore;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Backoffice.Api.Tests.TestInfrastructure;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.Infrastructure.Postgres.Projection;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RuntimeOperationsServiceTests
{
    [Fact]
    public async Task RuntimeDiagnostics_ListIncludesRequiredDiagnostics()
    {
        await using var scope = new SqliteControlDbContextScope();
        var service = new PostgresControlPlaneService(scope.Factory);

        var catalog = await service.ListRuntimeDiagnosticsAsync(CancellationToken.None);

        Assert.Contains(catalog.Diagnostics, item => item.Id == "runtime-table-counts");
        Assert.Contains(catalog.Diagnostics, item => item.Id == "latest-run-risk-by-metric");
        Assert.Contains(catalog.Diagnostics, item => item.Id == "recent-alert-transitions");
        Assert.Contains(catalog.Diagnostics, item => item.Id == "scenario-definition-details");
        Assert.Contains(catalog.Diagnostics, item => item.Id == "compare-latest-b-vs-c");
    }

    [Fact]
    public async Task RuntimeDiagnostics_UnknownDiagnostic_ReturnsNull()
    {
        await using var scope = new SqliteControlDbContextScope();
        var service = new PostgresControlPlaneService(scope.Factory);

        var result = await service.ExecuteRuntimeDiagnosticAsync(
            "unknown",
            new RuntimeDiagnosticRequest("proenca-a-nova", 30),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RuntimeDiagnostics_RuntimeTableCounts_ReturnsRuntimeCounts()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedRuntimeAsync(scope, activeRun: false);
        var service = new PostgresControlPlaneService(scope.Factory);

        var result = await service.ExecuteRuntimeDiagnosticAsync(
            "runtime-table-counts",
            new RuntimeDiagnosticRequest("proenca-a-nova", 30),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(result!.Rows, row => row["schema"] == "control" && row["table"] == "simulation_runs" && row["count"] == "1");
        Assert.Contains(result.Rows, row => row["schema"] == "projection" && row["table"] == "risk_assessment_log" && row["count"] == "1");
    }

    [Fact]
    public async Task RuntimeDiagnostics_InboxByStatus_ReturnsGroupedRows()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedRuntimeAsync(scope, activeRun: false);
        var service = new PostgresControlPlaneService(scope.Factory);

        var result = await service.ExecuteRuntimeDiagnosticAsync(
            "inbox-by-status",
            new RuntimeDiagnosticRequest("proenca-a-nova", 30),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(result!.Rows, row => row["status"] == "Processed" && row["count"] == "1");
    }

    [Fact]
    public async Task RuntimeDiagnostics_LatestRunRiskByMetric_JoinsAcceptedReadingsWithoutScoring()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedRuntimeAsync(scope, activeRun: false);
        var service = new PostgresControlPlaneService(scope.Factory);

        var result = await service.ExecuteRuntimeDiagnosticAsync(
            "latest-run-risk-by-metric",
            new RuntimeDiagnosticRequest("proenca-a-nova", 30),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(result!.Rows, row => row["metricType"] == "Temperature" && row["avgScore"] == "0.72");
    }

    [Fact]
    public async Task RuntimeDiagnostics_ScenarioDefinitionDetails_ReturnsSimulatorOptionsFlags()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedRuntimeAsync(scope, activeRun: false);
        var service = new PostgresControlPlaneService(scope.Factory);

        var result = await service.ExecuteRuntimeDiagnosticAsync(
            "scenario-definition-details",
            new RuntimeDiagnosticRequest("proenca-a-nova", 30, "scenario_b"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(result!.Rows, row =>
            row["code"] == "scenario_b" &&
            row["hasSimulatorOptions"] == "True" &&
            row["hasWeatherParameters"] == "True");
    }

    [Fact]
    public async Task StartRuntimeRun_ScenarioCWithoutDegradation_ReturnsWarning()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedRuntimeAsync(scope, activeRun: false);
        var service = new PostgresControlPlaneService(scope.Factory);

        var result = await service.StartRuntimeRunAsync(
            ValidRunRequest() with { ScenarioCode = "scenario_c", DegradationProfile = "none" },
            CancellationToken.None);

        Assert.Contains(result.Warnings, warning => warning.Contains("scenario_c", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StartRuntimeRun_SensorCountAboveActiveSensors_IsRejected()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedRuntimeAsync(scope, activeRun: false);
        var service = new PostgresControlPlaneService(scope.Factory);

        var result = await service.StartRuntimeRunAsync(
            ValidRunRequest() with { SensorCount = 99 },
            CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Contains("exceeds", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartRuntimeRun_ParallelRunBlockedByDefault_IsRejected()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedRuntimeAsync(scope, activeRun: true);
        var service = new PostgresControlPlaneService(scope.Factory);

        var result = await service.StartRuntimeRunAsync(
            ValidRunRequest(),
            CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Contains("Parallel runs", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartRuntimeRun_ValidRequestWithLaunchDisabled_ReturnsValidated()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedRuntimeAsync(scope, activeRun: false);
        var service = new PostgresControlPlaneService(scope.Factory);

        var result = await service.StartRuntimeRunAsync(
            ValidRunRequest(),
            CancellationToken.None);

        Assert.Equal("Validated", result.Status);
        Assert.Contains(result.Warnings, warning => warning.Contains("launch is disabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResetRuntimeState_DryRun_DoesNotDeleteRows()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedRuntimeAsync(scope, activeRun: false);
        var service = new PostgresControlPlaneService(scope.Factory);

        var result = await service.ResetRuntimeStateAsync(
            new RuntimeResetRequest("runtime-only", "", DryRun: true),
            CancellationToken.None);

        Assert.Equal("DryRun", result.Status);
        Assert.Contains(result.After, item => item.Schema == "control" && item.Table == "simulation_runs" && item.Count == 1);
    }

    [Fact]
    public async Task ResetRuntimeState_InvalidConfirmation_IsRejected()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedRuntimeAsync(scope, activeRun: false);
        var service = new PostgresControlPlaneService(scope.Factory);

        var result = await service.ResetRuntimeStateAsync(
            new RuntimeResetRequest("runtime-only", "wrong", DryRun: false),
            CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
    }

    [Fact]
    public async Task ResetRuntimeState_ActiveRun_IsRejected()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedRuntimeAsync(scope, activeRun: true);
        var service = new PostgresControlPlaneService(scope.Factory);

        var result = await service.ResetRuntimeStateAsync(
            new RuntimeResetRequest("runtime-only", "RESET_RUNTIME_STATE", DryRun: false),
            CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Contains("active run", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetRuntimeState_ValidReset_ClearsRuntimeAndKeepsControlPlane()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedRuntimeAsync(scope, activeRun: false);
        var service = new PostgresControlPlaneService(scope.Factory);

        var result = await service.ResetRuntimeStateAsync(
            new RuntimeResetRequest("runtime-only", "RESET_RUNTIME_STATE", DryRun: false),
            CancellationToken.None);

        Assert.Equal("Completed", result.Status);

        await using var dbContext = scope.CreateDbContext();
        Assert.Equal(0, await dbContext.SimulationRuns.CountAsync());
        Assert.Equal(0, await dbContext.InboxEvents.CountAsync());
        Assert.Equal(1, await dbContext.Areas.CountAsync());
        Assert.Equal(1, await dbContext.SensorNodes.CountAsync());
        Assert.Equal(2, await dbContext.ScenarioDefinitions.CountAsync());
    }

    private static RuntimeRunStartRequest ValidRunRequest()
        => new(
            "proenca-a-nova",
            "scenario_b",
            SensorCount: 1,
            NumberOfCycles: 5,
            IntervalSeconds: 5,
            Seed: 12345,
            DegradationProfile: "none",
            CollectEvidence: false,
            WaitForCompletion: false,
            TimeoutSeconds: 180,
            AllowParallelRun: false,
            RunLabel: "tests");

    private static async Task SeedRuntimeAsync(SqliteControlDbContextScope scope, bool activeRun)
    {
        var now = DateTimeOffset.UtcNow;
        var configurationVersionId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var areaId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var cellId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var profileId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var sensorId = Guid.Parse("50000000-0000-0000-0000-000000000001");
        var scenarioId = Guid.Parse("60000000-0000-0000-0000-000000000001");
        var runId = Guid.Parse("70000000-0000-0000-0000-000000000001");
        var eventId = Guid.Parse("80000000-0000-0000-0000-000000000001");
        var inboxId = Guid.Parse("81000000-0000-0000-0000-000000000001");
        var areaStateId = Guid.Parse("90000000-0000-0000-0000-000000000001");

        await scope.SeedAsync(async dbContext =>
        {
            dbContext.ConfigurationVersions.Add(new ConfigurationVersionRecord
            {
                Id = configurationVersionId,
                VersionNumber = 1,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "runtime-ops-tests"
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
                ScenarioKind = ScenarioCategory.HighRisk,
                ParametersJson = """
                    {
                      "simulator_options": {
                        "BaseTemperature": 35,
                        "BaseHumidity": 25,
                        "BaseWindSpeed": 8,
                        "FailureRate": 0.0,
                        "NoiseLevel": 0.1,
                        "TimeAcceleration": 1.0
                      }
                    }
                    """
            });

            dbContext.ScenarioDefinitions.Add(new ScenarioDefinitionRecord
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000002"),
                AreaId = areaId,
                ConfigurationVersionId = configurationVersionId,
                Code = "scenario_c",
                Name = "Scenario C",
                ScenarioKind = ScenarioCategory.HighRisk,
                ParametersJson = """
                    {
                      "simulator_options": {
                        "BaseTemperature": 35,
                        "BaseHumidity": 25,
                        "BaseWindSpeed": 8,
                        "FailureRate": 0.0,
                        "NoiseLevel": 0.1,
                        "TimeAcceleration": 1.0
                      }
                    }
                    """
            });

            dbContext.SimulationRuns.Add(new SimulationRunRecord
            {
                Id = runId,
                AreaId = areaId,
                ScenarioId = scenarioId,
                ConfigurationVersionId = configurationVersionId,
                ScenarioCode = "scenario_b",
                ScenarioName = "Scenario B",
                CreatedAt = now.AddMinutes(-5),
                StartedAt = now.AddMinutes(-5),
                EndedAt = activeRun ? null : now.AddMinutes(-4),
                LogicalStartTimestamp = now.AddHours(-1),
                IntervalSeconds = 5,
                NumberOfCycles = 5,
                ExecutionSeed = 12345,
                Status = activeRun ? SimulationRunStatus.Running : SimulationRunStatus.Completed,
                MetadataJson = $$"""
                    {
                      "sensor_count": 1,
                      "run_overrides": {
                        "resolved": {
                          "sensor_count": 1,
                          "number_of_cycles": 5,
                          "interval_seconds": 5,
                          "seed": 12345,
                          "degradation_profile": "none",
                          "orchestrator_correlation_id": "corr-tests",
                          "selected_sensor_names": ["pilot-temperature-0001"]
                        }
                      }
                    }
                    """
            });

            dbContext.InboxEvents.Add(new InboxEventRecord
            {
                Id = inboxId,
                EventId = eventId,
                SchemaVersion = "1.0",
                CorrelationId = "corr-tests",
                Producer = "tests",
                EventType = "SensorReadingProduced",
                AreaId = areaId,
                EventTime = now.AddHours(-1),
                ReceivedAt = now.AddMinutes(-4),
                PayloadJson = "{}",
                EnvelopeJson = "{}",
                Status = InboxEventStatus.Processed,
                AttemptCount = 1
            });

            dbContext.ProcessingAttempts.Add(new ProcessingAttemptRecord
            {
                Id = Guid.NewGuid(),
                InboxEventId = inboxId,
                AttemptNumber = 1,
                Stage = "reading_risk_pipeline",
                StartedAt = now.AddMinutes(-4),
                FinishedAt = now.AddMinutes(-4),
                Outcome = ProcessingAttemptOutcome.Succeeded
            });

            dbContext.AcceptedReadingLogs.Add(new AcceptedReadingLogRecord
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                AreaId = areaId,
                SensorId = sensorId,
                MetricType = "Temperature",
                MeasurementUnit = "Celsius",
                OperationalState = "Active",
                Value = 34.5,
                EventTime = now.AddHours(-1),
                IngestTime = now.AddMinutes(-4),
                Producer = "tests",
                CorrelationId = "corr-tests",
                PayloadJson = $$"""{"SimulationRunId":"{{runId}}"}""",
                EnvelopeJson = "{}",
                CreatedAt = now.AddMinutes(-4)
            });

            dbContext.RiskAssessmentLogs.Add(new RiskAssessmentLogRecord
            {
                Id = Guid.NewGuid(),
                AreaId = areaId,
                SensorId = sensorId,
                GridCellId = cellId,
                SourceEventId = eventId,
                Timestamp = now.AddHours(-1),
                RiskScore = 0.72,
                RiskLevel = "High",
                CreatedAt = now.AddMinutes(-4)
            });

            dbContext.AreaRiskSnapshotLogs.Add(new AreaRiskSnapshotLogRecord
            {
                Id = Guid.NewGuid(),
                AreaId = areaId,
                SimulationRunId = runId,
                SnapshotTimestamp = now.AddHours(-1),
                AggregateRiskScore = 0.72,
                AggregateRiskLevel = "High",
                Summary = "Aggregated from 1 assessments; 1 at High or above.",
                AssessmentCount = 1,
                CreatedAt = now.AddMinutes(-4)
            });

            dbContext.AreaOperationalStates.Add(new AreaOperationalStateRecord
            {
                Id = areaStateId,
                AreaId = areaId,
                ConfigurationVersionId = configurationVersionId,
                SimulationRunId = runId,
                SnapshotTimestamp = now.AddHours(-1),
                AggregateRiskScore = 0.72,
                AggregateRiskLevel = "High",
                Severity = "High",
                Summary = "Synthetic state",
                AssessmentCount = 1,
                UpdatedAt = now.AddMinutes(-4)
            });

            dbContext.CellOperationalStates.Add(new CellOperationalStateRecord
            {
                Id = Guid.NewGuid(),
                AreaId = areaId,
                GridCellId = cellId,
                SensorId = sensorId,
                SnapshotTimestamp = now.AddHours(-1),
                RiskScore = 0.72,
                RiskLevel = "High",
                Severity = "High",
                UpdatedAt = now.AddMinutes(-4)
            });

            dbContext.AlertStates.Add(new AlertStateRecord
            {
                Id = Guid.NewGuid(),
                AreaId = areaId,
                ConfigurationVersionId = configurationVersionId,
                AreaOperationalStateId = areaStateId,
                AlertCode = "area-risk-high",
                Severity = "High",
                Status = "Resolved",
                Message = "AlertState=Warning; Synthetic alert.",
                TriggeredAt = now.AddMinutes(-4),
                UpdatedAt = now.AddMinutes(-3),
                ResolvedAt = now.AddMinutes(-3)
            });

            await Task.CompletedTask;
        });
    }
}
