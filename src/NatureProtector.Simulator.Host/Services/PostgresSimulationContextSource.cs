using System.Text.Json;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Shared.Observability;
using NatureProtector.Simulator.Host.Configuration;

/*
 * Esta origem de contexto materializa uma execução de simulação a partir do
 * control plane persistido em PostgreSQL.
 *
 * Rationale:
 * - O modo control plane deve refletir a configuração realmente bootstrapada no
 *   repositório e ativada pela API/backoffice.
 * - O simulador precisa de transformar registos relacionais em objetos de
 *   domínio prontos a usar na geração de leituras.
 *
 * Design considerations:
 * - A área e o cenário podem ser resolvidos por identificador ou por código.
 * - Só sensores ativos entram na simulação.
 * - Perfis e parâmetros guardados em JSON são convertidos para o modelo de
 *   domínio com defaults conservadores quando faltam campos opcionais.
 */

namespace NatureProtector.Simulator.Host.Services;

public sealed class PostgresSimulationContextSource(
    IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory,
    IOptions<SimulatorOptions> simulatorOptions) : ISimulationContextSource
{
    private readonly SimulatorOptions _options = simulatorOptions.Value;

    /// <summary>
    /// Constrói um contexto de simulação a partir da configuração ativa em
    /// PostgreSQL.
    /// </summary>
    public async Task<SimulationContext> CreateAsync(CancellationToken cancellationToken)
    {
        using var activity = SimulatorHostTelemetry.ActivitySource.StartActivity("natureprotector.simulator.context.create_from_postgres");
        var stopwatch = Stopwatch.StartNew();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var area = await ResolveAreaAsync(dbContext, cancellationToken);
        var scenario = await ResolveScenarioAsync(dbContext, area.Id, cancellationToken);
        var sensors = await dbContext.SensorNodes
            .Include(entity => entity.Profile)
            .Where(entity => entity.AreaId == area.Id && entity.IsActive)
            .OrderBy(entity => entity.Name)
            .ToListAsync(cancellationToken);

        if (sensors.Count == 0)
        {
            throw new InvalidOperationException(
                $"No active sensor nodes were found in PostgreSQL for area '{area.Code}'.");
        }

        using var scenarioDocument = JsonDocument.Parse(scenario.ParametersJson);
        var simulatorOptions = scenarioDocument.RootElement.GetProperty("simulator_options");

        var domainScenario = new Scenario(
            id: scenario.Id,
            name: scenario.Name,
            category: scenario.ScenarioKind,
            parameters: new ScenarioParameters(
                baseTemperature: GetOptionalDouble(simulatorOptions, "BaseTemperature"),
                baseHumidity: GetOptionalDouble(simulatorOptions, "BaseHumidity"),
                baseWindSpeed: GetOptionalDouble(simulatorOptions, "BaseWindSpeed"),
                failureRate: simulatorOptions.GetProperty("FailureRate").GetDouble(),
                noiseLevel: simulatorOptions.GetProperty("NoiseLevel").GetDouble(),
                timeAcceleration: simulatorOptions.GetProperty("TimeAcceleration").GetDouble()),
            description: scenario.Description);

        var startTimestamp = simulatorOptions.TryGetProperty("StartTimestamp", out var timestampElement)
            && timestampElement.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(timestampElement.GetString(), out var parsedTimestamp)
                ? parsedTimestamp
                : _options.StartTimestamp ?? DateTimeOffset.UtcNow;

        var intervalSeconds = simulatorOptions.GetProperty("IntervalSeconds").GetInt32();
        var numberOfCycles = simulatorOptions.GetProperty("NumberOfCycles").GetInt32();

        var context = new SimulationContext(
            areaId: area.Id,
            scenario: domainScenario,
            scenarioCode: scenario.Code,
            sensors: sensors.Select(BuildDomainSensor).ToList().AsReadOnly(),
            startTimestamp: startTimestamp,
            interval: TimeSpan.FromSeconds(intervalSeconds),
            numberOfCycles: numberOfCycles,
            configurationVersionId: scenario.ConfigurationVersionId);

        activity?.SetTag(TelemetryTags.AreaId, context.AreaId);
        activity?.SetTag(TelemetryTags.ScenarioId, context.Scenario.Id);
        activity?.SetTag(TelemetryTags.ScenarioCode, context.ScenarioCode);
        activity?.SetTag(TelemetryTags.ConfigurationVersion, context.ConfigurationVersionId);
        activity?.SetTag(TelemetryTags.Outcome, "completed");
        stopwatch.Stop();
        SimulatorHostTelemetry.ContextCreations.Add(1);
        SimulatorHostTelemetry.ContextCreationDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds);

        return context;
    }

    private async Task<AreaRecord> ResolveAreaAsync(
        NatureProtectorControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (_options.AreaId != Guid.Empty)
        {
            var areaById = await dbContext.Areas
                .SingleOrDefaultAsync(entity => entity.Id == _options.AreaId, cancellationToken);

            if (areaById is not null)
            {
                return areaById;
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.ControlPlaneAreaCode))
        {
            var areaByCode = await dbContext.Areas
                .SingleOrDefaultAsync(entity => entity.Code == _options.ControlPlaneAreaCode, cancellationToken);

            if (areaByCode is not null)
            {
                return areaByCode;
            }
        }

        throw new InvalidOperationException(
            "Control plane area could not be resolved from PostgreSQL using AreaId or ControlPlaneAreaCode.");
    }

    private async Task<ScenarioDefinitionRecord> ResolveScenarioAsync(
        NatureProtectorControlDbContext dbContext,
        Guid areaId,
        CancellationToken cancellationToken)
    {
        if (_options.ScenarioId != Guid.Empty)
        {
            var scenarioById = await dbContext.ScenarioDefinitions
                .SingleOrDefaultAsync(
                    entity => entity.AreaId == areaId && entity.Id == _options.ScenarioId,
                    cancellationToken);

            if (scenarioById is not null)
            {
                return scenarioById;
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.ControlPlaneScenarioCode))
        {
            var scenarioByCode = await dbContext.ScenarioDefinitions
                .SingleOrDefaultAsync(
                    entity => entity.AreaId == areaId && entity.Code == _options.ControlPlaneScenarioCode,
                    cancellationToken);

            if (scenarioByCode is not null)
            {
                return scenarioByCode;
            }
        }

        throw new InvalidOperationException(
            "Control plane scenario could not be resolved from PostgreSQL using ScenarioId or ControlPlaneScenarioCode.");
    }

    private static Sensor BuildDomainSensor(SensorNodeRecord node)
    {
        var profile = node.Profile ?? throw new InvalidOperationException(
            $"Sensor node '{node.Name}' is missing its profile.");

        return new Sensor(
            id: node.Id,
            name: node.Name,
            type: node.Type,
            location: new Location(node.Latitude, node.Longitude, node.AltitudeMeters),
            profile: BuildDomainProfile(profile),
            isActive: node.IsActive);
    }

    private static SensorProfile BuildDomainProfile(SensorProfileRecord profileRecord)
    {
        // Os perfis são guardados como JSON flexível no control plane e aqui são
        // convertidos para a forma tipada que o simulador consome.
        var publication = ParseJsonObject(profileRecord.PublicationPolicyJson);
        var noise = ParseJsonObject(profileRecord.NoiseProfileJson);
        var faults = ParseJsonObject(profileRecord.FaultProfileJson);

        var samplingIntervalSeconds = publication.TryGetValue("sampling_interval_seconds", out var samplingSeconds)
            ? samplingSeconds.GetInt32()
            : 5;
        var communicationMode = publication.TryGetValue("communication_mode", out var communicationModeElement)
            ? communicationModeElement.GetString()
            : null;
        var noiseLevel = noise.TryGetValue("noise_level", out var parsedNoise)
            ? parsedNoise.GetDouble()
            : 0.1;
        var latencyProfile = faults.TryGetValue("latency_profile", out var parsedLatency)
            ? parsedLatency.GetString()
            : null;
        var failureProfile = faults.TryGetValue("failure_profile", out var parsedFailure)
            ? parsedFailure.GetString()
            : null;

        return new SensorProfile(
            id: profileRecord.Id,
            samplingInterval: TimeSpan.FromSeconds(samplingIntervalSeconds),
            communicationMode: string.IsNullOrWhiteSpace(communicationMode) ? "RabbitMq" : communicationMode,
            noiseLevel: noiseLevel,
            latencyProfile: string.IsNullOrWhiteSpace(latencyProfile) ? "Low latency" : latencyProfile,
            failureProfile: string.IsNullOrWhiteSpace(failureProfile) ? "Rare failures" : failureProfile);
    }

    private static Dictionary<string, JsonElement> ParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.OrdinalIgnoreCase);
    }

    private static double? GetOptionalDouble(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.Number
                ? property.GetDouble()
                : null;
    }
}
