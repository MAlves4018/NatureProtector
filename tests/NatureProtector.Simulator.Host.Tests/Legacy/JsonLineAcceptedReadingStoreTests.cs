using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NatureProtector.Prevention.Host.Configuration;
using NatureProtector.Prevention.Host.Persistence;

namespace NatureProtector.Simulator.Host.Tests.Legacy;

public sealed class JsonLineAcceptedReadingStoreTests
{
    [Fact]
    public void Ctor_Throws_WhenAcceptedReadingsPathIsBlank()
    {
        var options = Options.Create(new PreventionOptions
        {
            AcceptedReadingsPath = "   "
        });

        var ex = Assert.Throws<InvalidOperationException>(() => new JsonLineAcceptedReadingStore(
            options,
            new FakeHostEnvironment(Path.GetTempPath())));

        Assert.Equal("Prevention AcceptedReadingsPath must not be null or whitespace.", ex.Message);
    }

    [Fact]
    public void Ctor_CreatesTargetDirectory_ForRelativePath()
    {
        using var tempDirectory = new TempDirectory();
        var relativePath = Path.Combine("runtime", "accepted-readings.ndjson");
        var options = Options.Create(new PreventionOptions
        {
            AcceptedReadingsPath = relativePath
        });

        _ = new JsonLineAcceptedReadingStore(options, new FakeHostEnvironment(tempDirectory.Path));

        Assert.True(Directory.Exists(Path.Combine(tempDirectory.Path, "runtime")));
    }

    [Fact]
    public void Persist_AppendsOneJsonObjectPerLine()
    {
        using var tempDirectory = new TempDirectory();
        var targetPath = Path.Combine(tempDirectory.Path, "accepted.ndjson");
        var store = new JsonLineAcceptedReadingStore(
            Options.Create(new PreventionOptions
            {
                AcceptedReadingsPath = targetPath
            }),
            new FakeHostEnvironment(tempDirectory.Path));
        var first = CreateRecord("Sensor-A", 25.0);
        var second = CreateRecord("Sensor-B", 30.5);

        store.Persist(first);
        store.Persist(second);

        var lines = File.ReadAllLines(targetPath);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"SensorName\":\"Sensor-A\"", lines[0]);
        Assert.Contains("\"SensorName\":\"Sensor-B\"", lines[1]);
    }

    [Fact]
    public void Persist_Throws_WhenRecordIsNull()
    {
        using var tempDirectory = new TempDirectory();
        var store = new JsonLineAcceptedReadingStore(
            Options.Create(new PreventionOptions
            {
                AcceptedReadingsPath = Path.Combine(tempDirectory.Path, "accepted.ndjson")
            }),
            new FakeHostEnvironment(tempDirectory.Path));

        var ex = Assert.Throws<ArgumentNullException>(() => store.Persist(null!));

        Assert.Equal("record", ex.ParamName);
    }

    private static AcceptedReadingRecord CreateRecord(string sensorName, double value)
    {
        return new AcceptedReadingRecord(
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-001",
            Producer: "NatureProtector.Simulator.Host",
            EventType: "SensorReadingProduced",
            AreaId: Guid.NewGuid(),
            EventTime: new DateTimeOffset(2026, 4, 6, 20, 30, 0, TimeSpan.Zero),
            AcceptedAt: new DateTimeOffset(2026, 4, 6, 20, 31, 0, TimeSpan.Zero),
            SimulationRunId: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SensorName: sensorName,
            MetricType: NatureProtector.Shared.Contracts.Readings.SensorMetricType.Temperature,
            Unit: NatureProtector.Shared.Contracts.Readings.MeasurementUnit.Celsius,
            Value: value,
            Latitude: 39.8,
            Longitude: -7.9,
            OperationalState: NatureProtector.Shared.Contracts.Readings.SensorOperationalState.Nominal);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
        }

        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "NatureProtector.Tests";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "NatureProtector.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
