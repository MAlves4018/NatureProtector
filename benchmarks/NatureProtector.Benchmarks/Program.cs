using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

var profile = BenchmarkProfile.Parse(args, Environment.GetEnvironmentVariable("NP_BENCHMARK_PROFILE"));
var benchmarkArgs = BenchmarkProfile.RemoveProfileArgs(args);

Console.WriteLine($"NatureProtector benchmark profile: {profile.Name}");
Console.WriteLine(profile.Description);

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(benchmarkArgs, profile.CreateConfig());

internal sealed record BenchmarkProfile(string Name, string Description, Job Job)
{
    public static BenchmarkProfile Parse(string[] args, string? environmentValue)
    {
        var value = environmentValue;
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], "--profile", StringComparison.OrdinalIgnoreCase))
            {
                value = args[index + 1];
                break;
            }
        }

        return (value ?? "B0").Trim().ToUpperInvariant() switch
        {
            "B0" => new BenchmarkProfile(
                "B0",
                "Fast smoke profile for CI/manual readiness; one launch and one measured iteration.",
                Job.Dry.WithId("B0")),
            "B1" => new BenchmarkProfile(
                "B1",
                "Engineering profile for local comparison before release candidates.",
                Job.ShortRun.WithId("B1")),
            "B2" => new BenchmarkProfile(
                "B2",
                "Nightly/release-candidate profile with deeper measurement.",
                Job.MediumRun.WithId("B2")),
            var invalid => throw new ArgumentOutOfRangeException(nameof(args), invalid, "Benchmark profile must be B0, B1 or B2.")
        };
    }

    public static string[] RemoveProfileArgs(string[] args)
    {
        var filtered = new List<string>(args.Length);
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--profile", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            filtered.Add(args[index]);
        }

        return filtered.ToArray();
    }

    public IConfig CreateConfig()
        => ManualConfig
            .Create(DefaultConfig.Instance)
            .AddJob(Job)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddColumnProvider(DefaultColumnProviders.Instance)
            .AddExporter(JsonExporter.Brief)
            .AddLogger(ConsoleLogger.Default)
            .WithOption(ConfigOptions.DisableOptimizationsValidator, false);
}

public abstract class NatureProtectorBenchmarkBase
{
    [Params(32, 512, 4096)]
    public int BatchSize { get; set; }

    protected static readonly Guid AreaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid SensorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly Guid EventId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    protected static readonly Guid GridCellId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    protected static readonly DateTimeOffset BaseTime = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
}

public class ScoringBenchmarks : NatureProtectorBenchmarkBase
{
    private readonly SimpleRiskScoringService _scoringService = new();
    private RiskInput[] _inputs = [];

    [GlobalSetup]
    public void Setup()
    {
        _inputs = Enumerable.Range(0, BatchSize)
            .Select(index => new RiskInput(
                AreaId,
                SensorId,
                Guid.NewGuid(),
                SensorMetricType.Temperature,
                30 + (index % 9),
                MeasurementUnit.Celsius,
                BaseTime.AddMinutes(index))
            {
                GridCellId = GridCellId,
                Metrics = new RiskInputMetricSet(
                    TemperatureCelsius: 30 + (index % 9),
                    RelativeHumidityPercent: 28 + (index % 22),
                    WindSpeedMetersPerSecond: 6 + (index % 12)),
                TerritorialContext = TerritorialRiskContext.FromCellData(
                    GridCellId,
                    structuralHazard: "muito_alta",
                    landCoverClass: "Matos",
                    dominantForestType: "Eucalipto",
                    dominantFuelModel: "shrub",
                    treeCoverDensity: 63,
                    slopeDegrees: 22,
                    aspectDegrees: 210,
                    altitudeMeters: 490,
                    source: "benchmark_fixture"),
                FireWeatherIndexContext = new FireWeatherIndexContext(
                    FireWeatherIndex: 18.5,
                    KeetchByramDroughtIndex: 84,
                    Provenance: "candidate_benchmark",
                    NormalizedFireWeatherIndex: 0.62,
                    NormalizedKeetchByramDroughtIndex: 0.42)
            })
            .ToArray();
    }

    [Benchmark]
    public double CreateAssessments()
    {
        var total = 0.0;
        foreach (var input in _inputs)
        {
            total += _scoringService.CreateAssessment(input).AdjustedScore;
        }

        return total;
    }
}

public class TemporalClassifierBenchmarks : NatureProtectorBenchmarkBase
{
    private NormalizedReading[] _readings = [];

    [GlobalSetup]
    public void Setup()
    {
        _readings = Enumerable.Range(0, BatchSize)
            .Select(index => new NormalizedReading(
                EventId: Guid.NewGuid(),
                CorrelationId: $"benchmark-{index}",
                AreaId: AreaId,
                SensorId: SensorId,
                SensorName: "bench-sensor",
                MetricType: SensorMetricType.WindSpeed,
                Value: 8 + (index % 10),
                Unit: MeasurementUnit.MetersPerSecond,
                Latitude: 39.75,
                Longitude: -7.92,
                OperationalState: index % 17 == 0 ? SensorOperationalState.Delayed : SensorOperationalState.Nominal,
                EventTime: BaseTime.AddSeconds(index * 30),
                IngestTime: BaseTime.AddSeconds(index * 30).AddMinutes(index % 17 == 0 ? 20 : 1)))
            .ToArray();
    }

    [Benchmark]
    public int ClassifyBatch()
    {
        var flagCount = 0;
        var latest = BaseTime;
        foreach (var reading in _readings)
        {
            flagCount += ReadingTemporalClassifier.Classify(reading, TimeSpan.FromMinutes(5), latest).Count;
            latest = reading.EventTime;
        }

        return flagCount;
    }
}

public class TerritorialMappingBenchmarks : NatureProtectorBenchmarkBase
{
    private string[] _hazards = [];
    private string[] _landCovers = [];

    [GlobalSetup]
    public void Setup()
    {
        _hazards = ["muito_alta", "alta", "média", "baixa", "muito-baixa", "unknown"];
        _landCovers = ["Eucalipto", "Matos", "Mosaico cultural", "Pastagem", "Albufeira", "unknown"];
    }

    [Benchmark]
    public double MapTerritorialContext()
    {
        var total = 0.0;
        for (var index = 0; index < BatchSize; index++)
        {
            var context = TerritorialRiskContext.FromCellData(
                GridCellId,
                structuralHazard: _hazards[index % _hazards.Length],
                landCoverClass: _landCovers[index % _landCovers.Length],
                dominantForestType: null,
                dominantFuelModel: null,
                treeCoverDensity: null,
                slopeDegrees: 10 + (index % 25),
                aspectDegrees: index % 360,
                altitudeMeters: 250 + (index % 450),
                source: "benchmark_fixture");
            total += context.TerritoryComponent;
        }

        return total;
    }
}

public class SerializationBenchmarks : NatureProtectorBenchmarkBase
{
    private EventEnvelope<SensorReadingProducedPayload>[] _envelopes = [];

    [GlobalSetup]
    public void Setup()
    {
        _envelopes = Enumerable.Range(0, BatchSize)
            .Select(index => new EventEnvelope<SensorReadingProducedPayload>(
                SchemaVersion: "v1",
                EventId: Guid.NewGuid(),
                CorrelationId: $"benchmark-{index}",
                Producer: "NatureProtector.Benchmarks",
                EventType: EventTypes.SensorReadingProduced,
                AreaId: AreaId,
                EventTime: BaseTime.AddSeconds(index),
                IngestTime: BaseTime.AddSeconds(index + 1),
                Payload: new SensorReadingProducedPayload(
                    SimulationRunId: Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                    SensorId: SensorId,
                    SensorName: "bench-sensor",
                    MetricType: SensorMetricType.Temperature,
                    Unit: MeasurementUnit.Celsius,
                    Value: 33 + (index % 7),
                    Latitude: 39.75,
                    Longitude: -7.92,
                    OperationalState: SensorOperationalState.Nominal)))
            .ToArray();
    }

    [Benchmark]
    public int SerializeEnvelopeBatch()
    {
        var totalBytes = 0;
        foreach (var envelope in _envelopes)
        {
            totalBytes += JsonEventSerializer.SerializeToUtf8Bytes(envelope).Length;
        }

        return totalBytes;
    }
}
