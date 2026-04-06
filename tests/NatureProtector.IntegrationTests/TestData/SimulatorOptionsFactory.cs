using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Simulator.Host.Configuration;

namespace NatureProtector.IntegrationTests.TestData;

internal static class SimulatorOptionsFactory
{
    public static SimulatorOptions CreateValid()
    {
        return new SimulatorOptions
        {
            Seed = 12345,
            NumberOfCycles = 2,
            IntervalSeconds = 1,
            StartTimestamp = new DateTimeOffset(2026, 4, 6, 12, 0, 0, TimeSpan.Zero),
            AreaId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ScenarioId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ScenarioName = "Scenario A",
            ScenarioDescription = "Integration scenario",
            ScenarioCategory = ScenarioCategory.HighRisk,
            BaseTemperature = 31.0,
            BaseHumidity = 33.0,
            BaseWindSpeed = 7.5,
            FailureRate = 0.05,
            NoiseLevel = 0.10,
            TimeAcceleration = 1.0,
            Sensors = []
        };
    }

    public static SensorDefinitionOptions CreateSensor(
        Guid id,
        string name,
        SensorType type)
    {
        return new SensorDefinitionOptions
        {
            Id = id,
            Name = name,
            Type = type,
            Latitude = 39.8,
            Longitude = -7.9,
            Altitude = 120.0,
            IsActive = true,
            SamplingIntervalSeconds = 5,
            CommunicationMode = "LoRa",
            ProfileNoiseLevel = 0.10,
            LatencyProfile = "Low latency",
            FailureProfile = "Rare failures"
        };
    }
}
