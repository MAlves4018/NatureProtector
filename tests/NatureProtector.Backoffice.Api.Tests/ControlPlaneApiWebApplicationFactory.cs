using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Infrastructure.Postgres.Users;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class ControlPlaneApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestAuthScheme = "TestAuth";
    private readonly bool _controlPlaneAvailable;
    private readonly string _availabilityMessage;
    private readonly string _environmentName;
    private readonly bool _authenticated;
    private readonly IReadOnlyList<string> _roles;
    private readonly IRuntimeObservabilityService? _runtimeObservabilityService;

    public ControlPlaneApiWebApplicationFactory(
        bool controlPlaneAvailable = true,
        string availabilityMessage = "Fake control plane available for API tests.",
        string environmentName = "Development",
        IReadOnlyList<string>? roles = null,
        bool authenticated = true,
        IRuntimeObservabilityService? runtimeObservabilityService = null)
    {
        _controlPlaneAvailable = controlPlaneAvailable;
        _availabilityMessage = availabilityMessage;
        _environmentName = environmentName;
        _authenticated = authenticated;
        _roles = roles is { Count: > 0 } ? roles : [RoleRecord.Admin];
        _runtimeObservabilityService = runtimeObservabilityService;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackofficeApi:ControlPlaneEnabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IControlPlaneService>();
            services.AddSingleton<IControlPlaneService>(_ =>
                new FakeControlPlaneService(_controlPlaneAvailable, _availabilityMessage));
            if (_runtimeObservabilityService is not null)
            {
                services.RemoveAll<IRuntimeObservabilityService>();
                services.AddSingleton(_runtimeObservabilityService);
            }
            services.AddSingleton(new TestAuthState(_authenticated, _roles));

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthScheme;
                    options.DefaultChallengeScheme = TestAuthScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthScheme,
                    _ => { });
        });
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestAuthState authState)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!authState.Authenticated)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, UserRecord.AdminIdString),
                new Claim(ClaimTypes.Name, UserRecord.AdminUsername),
            };
            claims.AddRange(authState.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var identity = new ClaimsIdentity(claims, TestAuthScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, TestAuthScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed record TestAuthState(bool Authenticated, IReadOnlyList<string> Roles);

    private sealed class FakeControlPlaneService(
        bool isAvailable,
        string availabilityMessage) : IControlPlaneService
    {
        private readonly List<ConfigurationVersionResponse> _configurations =
        [
            new(1, true, "Pilot configuration v1", new DateTimeOffset(2026, 4, 7, 18, 0, 0, TimeSpan.Zero), "phase-05-tests", 1, 2, 2, 2, 1),
            new(2, false, "Pilot configuration v2", new DateTimeOffset(2026, 4, 7, 19, 0, 0, TimeSpan.Zero), "phase-05-tests", 1, 1, 0, 1, 0)
        ];

        private readonly AreaDetailResponse _activeArea = new(
            "proenca-a-nova",
            "Proenca-a-Nova",
            "PT",
            1,
            "{\"type\":\"Polygon\",\"coordinates\":[]}",
            "{\"source\":\"tests\"}",
            new AreaContextResponse("Mixed forest", 0.73, 0.24, 0.11, "summer_peak"),
            2,
            2,
            2);

        private readonly IReadOnlyList<GridCellResponse> _gridCells =
        [
            new("PRO-001", [Tuple.Create(Guid.Parse("70000000-0000-0000-0000-000000000001"), "Temperature")], 1, 39.75, -7.90, 340, 7.5, 125, "forest", null, null, null, "high", null, 1),
            new("PRO-002", [Tuple.Create(Guid.Parse("70000000-0000-0000-0000-000000000002"), "Humidity")], 1, 39.76, -7.89, 355, 11.2, 210, "shrubs", null, null, null, "medium", null, 1)
        ];

        private readonly IReadOnlyList<SensorNodeResponse> _sensorNodes =
        [
            new(Guid.Parse("70000000-0000-0000-0000-000000000001"), "pro-temp-001", "Temperature", 1, "PRO-001", "pilot-temperature-profile", "pilot", "proenca-a-nova-pilot-network", 39.75, -7.90, 340, true, "field-pilot"),
            new(Guid.Parse("70000000-0000-0000-0000-000000000002"), "pro-hum-002", "Humidity", 1, "PRO-002", "pilot-humidity-profile", "pilot", "proenca-a-nova-pilot-network", 39.76, -7.89, 355, true, "field-pilot")
        ];

        private readonly IReadOnlyList<ScenarioResponse> _scenarios =
        [
            new(Guid.Parse("80000000-0000-0000-0000-000000000001"), "scenario_a", "Scenario A - Base", "Base", 1, "Moderate summer day", null, 1),
            new(Guid.Parse("80000000-0000-0000-0000-000000000002"), "scenario_b", "Scenario B - High Risk", "HighRisk", 1, "Critical fire-weather context", null, 2)
        ];

        private readonly IReadOnlyList<SimulationRunResponse> _simulationRuns =
        [
            new(
                Guid.Parse("90000000-0000-0000-0000-000000000001"),
                "proenca-a-nova",
                "scenario_b",
                "Scenario B - High Risk",
                "Completed",
                1,
                new DateTimeOffset(2026, 4, 7, 20, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 7, 20, 1, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 7, 20, 15, 0, TimeSpan.Zero),
                new DateTimeOffset(2020, 9, 13, 11, 0, 0, TimeSpan.Zero),
                300,
                36,
                42,
                "{\"source\":\"tests\"}")
        ];

        private readonly AreaOperationalStateResponse _areaOperationalState = new(
            "proenca-a-nova",
            1,
            new DateTimeOffset(2026, 4, 7, 20, 14, 0, TimeSpan.Zero),
            0.78,
            "VeryHigh",
            "Critical",
            "Aggregated from 12 assessments; 8 at High or above.",
            12,
            new DateTimeOffset(2026, 4, 7, 20, 14, 30, TimeSpan.Zero),
            "Alarm");

        private readonly IReadOnlyList<CellOperationalStateResponse> _cellOperationalStates =
        [
            new(
                "proenca-a-nova",
                "PRO-001",
                1,
                new DateTimeOffset(2026, 4, 7, 20, 13, 0, TimeSpan.Zero),
                0.72,
                "High",
                "High",
                "Area=... Sensor=... Event=... Metric=Temperature; Value=34.50; Score=0.72.",
                Guid.Parse("70000000-0000-0000-0000-000000000001"),
                "pro-temp-001",
                new DateTimeOffset(2026, 4, 7, 20, 13, 30, TimeSpan.Zero)),
            new(
                "proenca-a-nova",
                "PRO-002",
                1,
                new DateTimeOffset(2026, 4, 7, 20, 14, 0, TimeSpan.Zero),
                0.91,
                "Extreme",
                "Emergency",
                "Area=... Sensor=... Event=... Metric=Humidity; Value=11.00; Score=0.91.",
                Guid.Parse("70000000-0000-0000-0000-000000000002"),
                "pro-hum-002",
                new DateTimeOffset(2026, 4, 7, 20, 14, 30, TimeSpan.Zero))
        ];

        private readonly IReadOnlyList<AlertStateResponse> _activeAlerts =
        [
            new(
                Guid.Parse("91000000-0000-0000-0000-000000000001"),
                "proenca-a-nova",
                1,
                "area-risk-high",
                "Critical",
                "Open",
                "AlertState=Alarm; Area risk is VeryHigh with adjusted score 0.78. Candidate Parameter Set V1.0 (non-official).",
                new DateTimeOffset(2026, 4, 7, 20, 10, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 7, 20, 14, 30, TimeSpan.Zero),
                null,
                "Alarm")
        ];

        public bool IsAvailable => isAvailable;

        public string AvailabilityMessage => availabilityMessage;

        public Task<IReadOnlyList<ConfigurationVersionResponse>> ListConfigurationsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ConfigurationVersionResponse>>(_configurations.OrderByDescending(entity => entity.VersionNumber).ToArray());

        public Task<ConfigurationVersionResponse?> GetActiveConfigurationAsync(CancellationToken cancellationToken)
            => Task.FromResult<ConfigurationVersionResponse?>(_configurations.SingleOrDefault(entity => entity.IsActive));

        public Task<ConfigurationVersionResponse?> ActivateConfigurationAsync(int versionNumber, CancellationToken cancellationToken)
        {
            ConfigurationVersionResponse? activatedConfiguration = null;

            for (var index = 0; index < _configurations.Count; index++)
            {
                var current = _configurations[index];
                var isActive = current.VersionNumber == versionNumber;
                _configurations[index] = current with { IsActive = isActive };

                if (isActive)
                {
                    activatedConfiguration = _configurations[index];
                }
            }

            return Task.FromResult(activatedConfiguration);
        }

        public Task<IReadOnlyList<AreaSummaryResponse>> ListAreasAsync(int? configurationVersion, CancellationToken cancellationToken)
        {
            if (configurationVersion.HasValue && configurationVersion.Value != 1)
            {
                return Task.FromResult<IReadOnlyList<AreaSummaryResponse>>([]);
            }

            return Task.FromResult<IReadOnlyList<AreaSummaryResponse>>(
            [
                new AreaSummaryResponse(Guid.Parse("00000000-0000-0000-0000-000000000001"),"proenca-a-nova", "Proenca-a-Nova", "PT", 1, 2, 2, 2)
            ]);
        }

        public Task<AreaDetailResponse?> GetAreaAsync(string areaCode, int? configurationVersion, CancellationToken cancellationToken)
        {
            if (!string.Equals(areaCode, "proenca-a-nova", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<AreaDetailResponse?>(null);
            }

            if (configurationVersion.HasValue && configurationVersion.Value != 1)
            {
                return Task.FromResult<AreaDetailResponse?>(null);
            }

            return Task.FromResult<AreaDetailResponse?>(_activeArea);
        }

        public Task<IReadOnlyList<GridCellResponse>> ListGridCellsAsync(
            string areaCode,
            int? configurationVersion,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            if (!string.Equals(areaCode, "proenca-a-nova", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IReadOnlyList<GridCellResponse>>([]);
            }

            return Task.FromResult<IReadOnlyList<GridCellResponse>>(_gridCells.Skip(skip).Take(take <= 0 ? 100 : take).ToArray());
        }

        public Task<IReadOnlyList<SensorNodeResponse>> ListSensorNodesAsync(
            string areaCode,
            int? configurationVersion,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            if (!string.Equals(areaCode, "proenca-a-nova", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IReadOnlyList<SensorNodeResponse>>([]);
            }

            return Task.FromResult<IReadOnlyList<SensorNodeResponse>>(_sensorNodes.Skip(skip).Take(take <= 0 ? 100 : take).ToArray());
        }

        public Task<IReadOnlyList<ScenarioResponse>> ListScenariosAsync(
            string areaCode,
            int? configurationVersion,
            CancellationToken cancellationToken)
        {
            if (!string.Equals(areaCode, "proenca-a-nova", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IReadOnlyList<ScenarioResponse>>([]);
            }

            return Task.FromResult(_scenarios);
        }

        public Task<IReadOnlyList<SimulationRunResponse>> ListSimulationRunsAsync(
            string? areaCode,
            string? scenarioCode,
            int? configurationVersion,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            var query = _simulationRuns.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(areaCode))
            {
                query = query.Where(entity => entity.AreaCode == areaCode);
            }

            if (!string.IsNullOrWhiteSpace(scenarioCode))
            {
                query = query.Where(entity => entity.ScenarioCode == scenarioCode);
            }

            if (configurationVersion.HasValue)
            {
                query = query.Where(entity => entity.ConfigurationVersionNumber == configurationVersion.Value);
            }

            return Task.FromResult<IReadOnlyList<SimulationRunResponse>>(query.Skip(skip).Take(take <= 0 ? 100 : take).ToArray());
        }

        public Task<SimulationRunResponse?> GetSimulationRunAsync(Guid runId, CancellationToken cancellationToken)
            => Task.FromResult(_simulationRuns.SingleOrDefault(entity => entity.Id == runId));

        public Task<AreaOperationalStateResponse?> GetAreaOperationalStateAsync(
            string areaCode,
            int? configurationVersion,
            CancellationToken cancellationToken)
        {
            if (!string.Equals(areaCode, "proenca-a-nova", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<AreaOperationalStateResponse?>(null);
            }

            if (configurationVersion.HasValue && configurationVersion.Value != 1)
            {
                return Task.FromResult<AreaOperationalStateResponse?>(null);
            }

            return Task.FromResult<AreaOperationalStateResponse?>(_areaOperationalState);
        }

        public Task<IReadOnlyList<AlertStateResponse>> ListActiveAlertsAsync(
            string? areaCode,
            int? configurationVersion,
            CancellationToken cancellationToken)
        {
            var query = _activeAlerts.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(areaCode))
            {
                query = query.Where(entity => entity.AreaCode == areaCode);
            }

            if (configurationVersion.HasValue)
            {
                query = query.Where(entity => entity.ConfigurationVersionNumber == configurationVersion.Value);
            }

            return Task.FromResult<IReadOnlyList<AlertStateResponse>>(query.ToArray());
        }

        public Task<IReadOnlyList<CellOperationalStateResponse>> ListCellOperationalStatesAsync(
            string areaCode,
            int? configurationVersion,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            if (!string.Equals(areaCode, "proenca-a-nova", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IReadOnlyList<CellOperationalStateResponse>>([]);
            }

            if (configurationVersion.HasValue && configurationVersion.Value != 1)
            {
                return Task.FromResult<IReadOnlyList<CellOperationalStateResponse>>([]);
            }

            return Task.FromResult<IReadOnlyList<CellOperationalStateResponse>>(
                _cellOperationalStates.Skip(skip).Take(take <= 0 ? 100 : take).ToArray());
        }

        public Task<RuntimeSummaryResponse> GetRuntimeSummaryAsync(
            string? areaCode,
            int recentMinutes,
            CancellationToken cancellationToken)
        {
            var run = _simulationRuns.Single();
            return Task.FromResult(new RuntimeSummaryResponse(
                new DateTimeOffset(2026, 4, 7, 20, 16, 0, TimeSpan.Zero),
                recentMinutes,
                areaCode,
                null,
                new RuntimeRunSummaryResponse(
                    run.Id,
                    run.AreaCode,
                    run.ScenarioCode,
                    run.ScenarioName,
                    run.Status,
                    run.ConfigurationVersionNumber,
                    run.CreatedAt,
                    run.StartedAt,
                    run.EndedAt,
                    840,
                    run.LogicalStartTimestamp,
                    run.IntervalSeconds,
                    run.NumberOfCycles,
                    run.ExecutionSeed,
                    run.MetadataJson,
                    "valid",
                    null,
                    null),
                new RuntimePipelineSummaryResponse(
                    2,
                    2,
                    [new RuntimeStatusCountResponse("Processed", 2)],
                    2,
                    [new RuntimeAttemptCountResponse("Succeeded", null, 2)],
                    0,
                    0,
                    [],
                    0,
                    0,
                    [],
                    [],
                    [],
                    []),
                new RuntimeRiskSummaryResponse(
                    2,
                    0.72,
                    0.91,
                    new DateTimeOffset(2026, 4, 7, 20, 14, 0, TimeSpan.Zero),
                    [new RuntimeRiskPointResponse(new DateTimeOffset(2026, 4, 7, 20, 14, 0, TimeSpan.Zero), 0.91, "Extreme")]),
                new RuntimeAreaOperationalSummaryResponse(
                    _areaOperationalState.AreaCode,
                    _areaOperationalState.ConfigurationVersionNumber,
                    _areaOperationalState.SnapshotTimestamp,
                    _areaOperationalState.AggregateRiskScore,
                    _areaOperationalState.AggregateRiskLevel,
                    _areaOperationalState.Severity,
                    _areaOperationalState.Summary,
                    _areaOperationalState.AssessmentCount,
                    _areaOperationalState.UpdatedAt,
                    _areaOperationalState.AlertState),
                _cellOperationalStates.Count,
                _activeAlerts.Select(alert => new RuntimeAlertSummaryResponse(
                    alert.Id,
                    alert.AreaCode,
                    alert.ConfigurationVersionNumber,
                    alert.AlertCode,
                    alert.Severity,
                    alert.Status,
                    alert.Message,
                    alert.TriggeredAt,
                    alert.UpdatedAt,
                    alert.ResolvedAt,
                    alert.AlertState)).ToArray(),
                new RuntimeFreshnessSummaryResponse(
                    2,
                    0,
                    0,
                    new DateTimeOffset(2026, 4, 7, 20, 13, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 4, 7, 20, 14, 0, TimeSpan.Zero),
                    120,
                    300,
                    "Fake freshness summary."),
                new RuntimeScoreComponentSummaryResponse(
                    0.91,
                    0.88,
                    0.91,
                    91,
                    0.8,
                    0.6,
                    0.7,
                    0.7,
                    0.6,
                    0.8,
                    1,
                    1,
                    "Meteorology",
                    "Candidate Parameter Set V1.0",
                    "Complete",
                    null,
                    new DateTimeOffset(2026, 4, 7, 20, 14, 0, TimeSpan.Zero),
                    "VeryHigh",
                    "Muito elevado"),
                new RuntimeIndexComparisonSummaryResponse(
                    17.07,
                    0.213,
                    "CompleteWithCandidateDefaults",
                    16.56,
                    0.0207,
                    "LimitedAntecedentHistory",
                    "candidate_calculated",
                    "antecedent defaults are explicit in fake service",
                    0.0,
                    new DateTimeOffset(2020, 9, 13, 0, 0, 0, TimeSpan.Zero),
                    CalculatedFireWeatherIndex: 17.07,
                    ReferenceFireWeatherIndex: null,
                    FireWeatherIndexValueSource: "calculated_candidate",
                    FireWeatherIpmaClass: "Moderate",
                    FireWeatherIpmaClassLabel: "Moderado",
                    FireWeatherEffisClass: "Moderate",
                    FireWeatherThresholdDistanceToNextClass: 0.13,
                    FireWeatherNextIpmaClass: "High",
                    CalculatedKeetchByramDroughtIndex: 16.56,
                    ReferenceKeetchByramDroughtIndex: null,
                    KbdiValueSource: "calculated_candidate",
                    KbdiDrynessClass: "VeryLowDryness",
                    KbdiDrynessClassLabel: "Secura muito baixa",
                    KbdiAntecedentHistoryQuality: "LimitedAntecedentHistory",
                    KbdiAntecedentDays: 0,
                    PortugueseContextRiskProxyClass: "High",
                    PortugueseContextRiskProxyLabel: "Elevado",
                    TerritorialHazardProxyClass: "High",
                    LocalFwiPercentileStatus: "NotAvailable",
                    LocalFwiPercentile: null,
                    LocalFwiPercentileReason: "historical_local_fwi_distribution_not_materialized"),
                RuntimeLimitations.Default,
                []));
        }

        public Task<RuntimeRunAuditResponse?> GetRuntimeRunAuditAsync(Guid runId, CancellationToken cancellationToken)
        {
            var run = _simulationRuns.SingleOrDefault(item => item.Id == runId);
            if (run is null)
            {
                return Task.FromResult<RuntimeRunAuditResponse?>(null);
            }

            return Task.FromResult<RuntimeRunAuditResponse?>(new RuntimeRunAuditResponse(
                new RuntimeRunSummaryResponse(
                    run.Id,
                    run.AreaCode,
                    run.ScenarioCode,
                    run.ScenarioName,
                    run.Status,
                    run.ConfigurationVersionNumber,
                    run.CreatedAt,
                    run.StartedAt,
                    run.EndedAt,
                    840,
                    run.LogicalStartTimestamp,
                    run.IntervalSeconds,
                    run.NumberOfCycles,
                    run.ExecutionSeed,
                    run.MetadataJson,
                    "valid",
                    null,
                    null),
                ExpectedEvents: 72,
                AcceptedReadings: 70,
                MissingEvents: 2,
                Rejected: 0,
                Quarantined: 0,
                RetryAttempts: 0,
                RiskAssessments: 70,
                QualityFlagsSummary: [new RuntimeStatusCountResponse("Missing", 2)],
                EligibilitySummary: [new RuntimeStatusCountResponse("CompleteEligible", 70)],
                AreaSnapshot: new RuntimeAreaSnapshotAuditResponse(
                    _areaOperationalState.SnapshotTimestamp,
                    _areaOperationalState.AggregateRiskScore,
                    _areaOperationalState.AggregateRiskLevel,
                    _areaOperationalState.AssessmentCount,
                    _areaOperationalState.Summary),
                Limitations: []));
        }

        public Task<RuntimeRunTimingSummaryResponse?> GetRuntimeRunTimingsAsync(Guid runId, CancellationToken cancellationToken)
        {
            var run = _simulationRuns.SingleOrDefault(item => item.Id == runId);
            if (run is null)
            {
                return Task.FromResult<RuntimeRunTimingSummaryResponse?>(null);
            }

            return Task.FromResult<RuntimeRunTimingSummaryResponse?>(new RuntimeRunTimingSummaryResponse(
                run.Id,
                840_000,
                run.StartedAt,
                run.EndedAt,
                new DateTimeOffset(2026, 4, 7, 20, 1, 5, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 7, 20, 1, 10, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 7, 20, 1, 12, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 7, 20, 1, 20, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 7, 20, 10, 0, TimeSpan.Zero),
                5_000,
                10_000,
                20_000,
                540_000,
                new RuntimeAttemptTimingSummaryResponse(2, 2, 0, 0, 1_000, 1_500, 2_000),
                [
                    new RuntimeStageTimingSummaryResponse(
                        "reading_risk_pipeline",
                        "Succeeded",
                        null,
                        2,
                        new DateTimeOffset(2026, 4, 7, 20, 1, 10, TimeSpan.Zero),
                        new DateTimeOffset(2026, 4, 7, 20, 1, 12, TimeSpan.Zero),
                        1_000,
                        1_500,
                        2_000)
                ],
                ["Fake control plane timings."]));
        }

        public Task<AreaGeoJSONResponse?> GetAreaGeoJSONAsync(string areaCode, int? configurationVersion, CancellationToken cancellationToken)
        {
            if (!string.Equals(areaCode, "proenca-a-nova", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<AreaGeoJSONResponse?>(null);
            }

            return Task.FromResult<AreaGeoJSONResponse?>(new AreaGeoJSONResponse(
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                _activeArea.GeometryGeoJson));
        }

        public Task<RuntimeDiagnosticCatalogResponse> ListRuntimeDiagnosticsAsync(CancellationToken cancellationToken)
            => Task.FromResult(new RuntimeDiagnosticCatalogResponse(
            [
                new RuntimeDiagnosticDefinitionResponse("runtime-table-counts", "Runtime table counts", "Fake diagnostic")
            ]));

        public Task<RuntimeDiagnosticResultResponse?> ExecuteRuntimeDiagnosticAsync(
            string diagnosticId,
            RuntimeDiagnosticRequest request,
            CancellationToken cancellationToken)
            => Task.FromResult<RuntimeDiagnosticResultResponse?>(
                diagnosticId == "runtime-table-counts"
                    ? new RuntimeDiagnosticResultResponse(
                        diagnosticId,
                        "Runtime table counts",
                        "Fake diagnostic",
                        ["schema", "table", "count"],
                        [new Dictionary<string, string?> { ["schema"] = "control", ["table"] = "simulation_runs", ["count"] = "1" }],
                        [])
                    : null);

        public Task<RuntimeRunStartResponse> StartRuntimeRunAsync(
            RuntimeRunStartRequest request,
            CancellationToken cancellationToken)
            => Task.FromResult(new RuntimeRunStartResponse(
                Guid.Parse("92000000-0000-0000-0000-000000000001"),
                "fake-correlation",
                "Validated",
                "Fake run request accepted.",
                DateTimeOffset.UtcNow,
                new RuntimeRunOverrideValuesResponse(
                    request.SensorCount,
                    request.NumberOfCycles,
                    request.IntervalSeconds,
                    request.Seed,
                    request.DegradationProfile,
                    "fake-correlation"),
                null,
                [],
                null,
                null));

        public Task<ControlledValidationP3RunResponse> StartControlledValidationP3Async(
            ControlledValidationP3RunRequest request,
            string environmentName,
            CancellationToken cancellationToken)
            => Task.FromResult(new ControlledValidationP3RunResponse(
                Guid.Parse("93000000-0000-0000-0000-000000000001"),
                request.RunLabel ?? "controlled-validation-p3-negative-pipeline-tests",
                "P3NegativePipeline",
                "Validated",
                environmentName,
                "Fake P3 request accepted.",
                DateTimeOffset.UtcNow,
                11,
                10,
                2,
                "docs/evidence/controlled-validation/p3/fake",
                null,
                true,
                null,
                ["Fake control plane does not launch P3 runtime."]));

        public Task<RuntimeResetResponse> ResetRuntimeStateAsync(
            RuntimeResetRequest request,
            CancellationToken cancellationToken)
            => Task.FromResult(new RuntimeResetResponse(
                DateTimeOffset.UtcNow,
                request.DryRun,
                request.DryRun ? "DryRun" : "Completed",
                "Fake reset response.",
                [],
                []));
    }
}
