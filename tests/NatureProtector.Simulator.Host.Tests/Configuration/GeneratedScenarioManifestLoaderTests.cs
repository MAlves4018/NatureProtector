using NatureProtector.Core.Scenarios;
using NatureProtector.Simulator.Host.Configuration;
using NatureProtector.Simulator.Host.Tests.TestData;

namespace NatureProtector.Simulator.Host.Tests.Configuration;

public sealed class GeneratedScenarioManifestLoaderTests
{
    [Fact]
    public void ApplyIfConfigured_DoesNothing_WhenManifestPathIsMissing()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.ScenarioManifestPath = null;

        GeneratedScenarioManifestLoader.ApplyIfConfigured(options, Directory.GetCurrentDirectory());

        Assert.Equal("Scenario A", options.ScenarioName);
        Assert.Equal(3, options.NumberOfCycles);
    }

    [Fact]
    public void ApplyIfConfigured_AppliesSingleManifestOverrides_FromRelativePath()
    {
        var options = SimulatorOptionsMother.CreateValid();
        using var tempDirectory = new TempDirectory();
        File.WriteAllText(Path.Combine(tempDirectory.Path, "manifest.json"), """
        {
          "simulator_options": {
            "AreaId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "ScenarioId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "ScenarioName": "Manifest Scenario",
            "ScenarioDescription": "Scenario loaded from manifest",
            "ScenarioCategory": "Failure",
            "StartTimestamp": "2026-04-07T10:15:00Z",
            "BaseTemperature": 37.5,
            "BaseHumidity": 18.0,
            "BaseWindSpeed": 12.0,
            "FailureRate": 0.25,
            "NoiseLevel": 0.45,
            "DegradationProfile": "missing-readings",
            "TimeAcceleration": 2.0,
            "NumberOfCycles": 9,
            "IntervalSeconds": 4
          }
        }
        """);

        options.ScenarioManifestPath = "manifest.json";

        GeneratedScenarioManifestLoader.ApplyIfConfigured(options, tempDirectory.Path);

        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), options.AreaId);
        Assert.Equal(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), options.ScenarioId);
        Assert.Equal("Manifest Scenario", options.ScenarioName);
        Assert.Equal("Scenario loaded from manifest", options.ScenarioDescription);
        Assert.Equal(ScenarioCategory.Failure, options.ScenarioCategory);
        Assert.Equal(new DateTimeOffset(2026, 4, 7, 10, 15, 0, TimeSpan.Zero), options.StartTimestamp);
        Assert.Equal(37.5, options.BaseTemperature);
        Assert.Equal(18.0, options.BaseHumidity);
        Assert.Equal(12.0, options.BaseWindSpeed);
        Assert.Equal(0.25, options.FailureRate);
        Assert.Equal(0.45, options.NoiseLevel);
        Assert.Equal("missing-readings", options.DegradationProfile);
        Assert.Equal(2.0, options.TimeAcceleration);
        Assert.Equal(9, options.NumberOfCycles);
        Assert.Equal(4, options.IntervalSeconds);
    }

    [Fact]
    public void ApplyIfConfigured_SelectsScenarioFromCatalog_ByScenarioKey()
    {
        var options = SimulatorOptionsMother.CreateValid();
        using var tempDirectory = new TempDirectory();
        File.WriteAllText(Path.Combine(tempDirectory.Path, "catalog.json"), """
        {
          "scenarios": [
            {
              "scenario_key": "scenario-a",
              "scenario_id": "11111111-aaaa-bbbb-cccc-111111111111",
              "simulator_options": {
                "ScenarioName": "Catalog A",
                "NumberOfCycles": 5
              }
            },
            {
              "scenario_key": "scenario-b",
              "scenario_id": "22222222-aaaa-bbbb-cccc-222222222222",
              "simulator_options": {
                "ScenarioName": "Catalog B",
                "ScenarioCategory": "Exercise",
                "NumberOfCycles": 7
              }
            }
          ]
        }
        """);

        options.ScenarioManifestPath = "catalog.json";
        options.ScenarioManifestScenarioKey = "scenario-b";

        GeneratedScenarioManifestLoader.ApplyIfConfigured(options, tempDirectory.Path);

        Assert.Equal("Catalog B", options.ScenarioName);
        Assert.Equal(ScenarioCategory.Exercise, options.ScenarioCategory);
        Assert.Equal(7, options.NumberOfCycles);
        Assert.Equal("Simulator test scenario", options.ScenarioDescription);
        Assert.Null(options.DegradationProfile);
    }

    [Fact]
    public void ApplyIfConfigured_SelectsScenarioC_WithExplicitDegradationProfile()
    {
        var options = SimulatorOptionsMother.CreateValid();
        using var tempDirectory = new TempDirectory();
        File.WriteAllText(Path.Combine(tempDirectory.Path, "catalog.json"), """
        {
          "scenarios": [
            {
              "scenario_key": "scenario_b",
              "scenario_id": "22222222-aaaa-bbbb-cccc-222222222222",
              "simulator_options": {
                "ScenarioName": "Catalog B",
                "ScenarioCategory": "HighRisk"
              }
            },
            {
              "scenario_key": "scenario_c",
              "scenario_id": "33333333-aaaa-bbbb-cccc-333333333333",
              "simulator_options": {
                "ScenarioName": "Catalog C",
                "ScenarioCategory": "Failure",
                "DegradationProfile": "missing-readings"
              }
            }
          ]
        }
        """);

        options.ScenarioManifestPath = "catalog.json";
        options.ScenarioManifestScenarioKey = "scenario_c";

        GeneratedScenarioManifestLoader.ApplyIfConfigured(options, tempDirectory.Path);

        Assert.Equal("Catalog C", options.ScenarioName);
        Assert.Equal(ScenarioCategory.Failure, options.ScenarioCategory);
        Assert.Equal("missing-readings", options.DegradationProfile);
    }

    [Fact]
    public void ApplyIfConfigured_Throws_WhenFileDoesNotExist()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.ScenarioManifestPath = "missing-manifest.json";

        var ex = Assert.Throws<FileNotFoundException>(() =>
            GeneratedScenarioManifestLoader.ApplyIfConfigured(options, Directory.GetCurrentDirectory()));

        Assert.Contains("Configured ScenarioManifestPath was not found", ex.Message);
    }

    [Fact]
    public void ApplyIfConfigured_Throws_WhenCatalogPathIsUsedWithoutScenarioKey()
    {
        var options = SimulatorOptionsMother.CreateValid();
        using var tempDirectory = new TempDirectory();
        File.WriteAllText(Path.Combine(tempDirectory.Path, "catalog.json"), """
        {
          "scenarios": [
            {
              "scenario_key": "scenario-a",
              "scenario_id": "11111111-aaaa-bbbb-cccc-111111111111",
              "simulator_options": {
                "ScenarioName": "Catalog A"
              }
            }
          ]
        }
        """);

        options.ScenarioManifestPath = "catalog.json";
        options.ScenarioManifestScenarioKey = null;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            GeneratedScenarioManifestLoader.ApplyIfConfigured(options, tempDirectory.Path));

        Assert.Contains("ScenarioManifestScenarioKey must be configured", ex.Message);
    }

    [Fact]
    public void ApplyIfConfigured_Throws_WhenScenariosNodeIsNotAnArray()
    {
        var options = SimulatorOptionsMother.CreateValid();
        using var tempDirectory = new TempDirectory();
        File.WriteAllText(Path.Combine(tempDirectory.Path, "catalog.json"), """
        {
          "scenarios": {
            "scenario_key": "scenario-a"
          }
        }
        """);

        options.ScenarioManifestPath = "catalog.json";
        options.ScenarioManifestScenarioKey = "scenario-a";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            GeneratedScenarioManifestLoader.ApplyIfConfigured(options, tempDirectory.Path));

        Assert.Contains("invalid 'scenarios' node", ex.Message);
    }

    [Fact]
    public void ApplyIfConfigured_Throws_WhenScenarioKeyIsNotFound()
    {
        var options = SimulatorOptionsMother.CreateValid();
        using var tempDirectory = new TempDirectory();
        File.WriteAllText(Path.Combine(tempDirectory.Path, "catalog.json"), """
        {
          "scenarios": [
            {
              "scenario_key": "scenario-a",
              "scenario_id": "11111111-aaaa-bbbb-cccc-111111111111",
              "simulator_options": {
                "ScenarioName": "Catalog A"
              }
            }
          ]
        }
        """);

        options.ScenarioManifestPath = "catalog.json";
        options.ScenarioManifestScenarioKey = "missing-scenario";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            GeneratedScenarioManifestLoader.ApplyIfConfigured(options, tempDirectory.Path));

        Assert.Contains("was not found in the generated scenario catalog", ex.Message);
    }

    [Fact]
    public void ApplyIfConfigured_ParsesNumericOverrides_FromStringValues()
    {
        var options = SimulatorOptionsMother.CreateValid();
        using var tempDirectory = new TempDirectory();
        File.WriteAllText(Path.Combine(tempDirectory.Path, "manifest.json"), """
        {
          "simulator_options": {
            "BaseTemperature": "37.5",
            "BaseHumidity": "18.0",
            "BaseWindSpeed": "12.0",
            "FailureRate": "0.25",
            "NoiseLevel": "0.45",
            "TimeAcceleration": "2.0",
            "NumberOfCycles": "9",
            "IntervalSeconds": "4"
          }
        }
        """);

        options.ScenarioManifestPath = "manifest.json";

        GeneratedScenarioManifestLoader.ApplyIfConfigured(options, tempDirectory.Path);

        Assert.Equal(37.5, options.BaseTemperature);
        Assert.Equal(18.0, options.BaseHumidity);
        Assert.Equal(12.0, options.BaseWindSpeed);
        Assert.Equal(0.25, options.FailureRate);
        Assert.Equal(0.45, options.NoiseLevel);
        Assert.Equal(2.0, options.TimeAcceleration);
        Assert.Equal(9, options.NumberOfCycles);
        Assert.Equal(4, options.IntervalSeconds);
    }

    [Fact]
    public void ApplyIfConfigured_FallsBack_WhenNumericStringValuesAreInvalid()
    {
        var options = SimulatorOptionsMother.CreateValid();
        using var tempDirectory = new TempDirectory();
        File.WriteAllText(Path.Combine(tempDirectory.Path, "manifest.json"), """
        {
          "simulator_options": {
            "BaseTemperature": "invalid",
            "FailureRate": "bad-value",
            "NumberOfCycles": "not-an-int",
            "IntervalSeconds": "still-not-an-int"
          }
        }
        """);

        options.ScenarioManifestPath = "manifest.json";

        GeneratedScenarioManifestLoader.ApplyIfConfigured(options, tempDirectory.Path);

        Assert.Equal(31.0, options.BaseTemperature);
        Assert.Equal(0.05, options.FailureRate);
        Assert.Equal(3, options.NumberOfCycles);
        Assert.Equal(1, options.IntervalSeconds);
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
