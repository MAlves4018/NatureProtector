using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Simulator.Host.Configuration;

namespace NatureProtector.Simulator.Host.Tests.TestData;

internal static class SimulatorOptionsMother
{
    public static SimulatorOptions CreateValid()
    {
        return new SimulatorOptions
        {
            Seed = 12345,
            NumberOfCycles = 3,
            IntervalSeconds = 1,
            StartTimestamp = new DateTimeOffset(2026, 4, 6, 12, 0, 0, TimeSpan.Zero),
            AreaId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ScenarioId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ScenarioName = "Scenario A",
            ScenarioDescription = "Simulator test scenario",
            ScenarioCategory = ScenarioCategory.HighRisk,
            BaseTemperature = 31.0,
            BaseHumidity = 33.0,
            BaseWindSpeed = 7.5,
            FailureRate = 0.05,
            NoiseLevel = 0.10,
            TimeAcceleration = 1.0,
            Sensors =
            [
                CreateSensorDefinition(
                    id: Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    name: "Temperature-01",
                    type: SensorType.Temperature,
                    latitude: 39.80,
                    longitude: -7.90),
                CreateSensorDefinition(
                    id: Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    name: "Humidity-01",
                    type: SensorType.Humidity,
                    latitude: 39.81,
                    longitude: -7.91)
            ]
        };
    }

    public static SensorDefinitionOptions CreateSensorDefinition(
        Guid? id = null,
        string name = "Sensor-01",
        SensorType type = SensorType.Temperature,
        double latitude = 39.8,
        double longitude = -7.9,
        double? altitude = 120.0,
        bool isActive = true,
        int samplingIntervalSeconds = 5,
        string communicationMode = " LoRa ",
        double profileNoiseLevel = 0.10,
        string? latencyProfile = " Low latency ",
        string? failureProfile = " Rare failures ")
    {
        return new SensorDefinitionOptions
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Type = type,
            Latitude = latitude,
            Longitude = longitude,
            Altitude = altitude,
            IsActive = isActive,
            SamplingIntervalSeconds = samplingIntervalSeconds,
            CommunicationMode = communicationMode,
            ProfileNoiseLevel = profileNoiseLevel,
            LatencyProfile = latencyProfile,
            FailureProfile = failureProfile
        };
    }
}
