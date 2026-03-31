using NatureProtector.Core.Sensors;
using Xunit;

namespace NatureProtector.Core.Tests.Sensors;

/// <summary>
/// Unit tests for SensorProfile.
/// These tests cover constructor invariants and immutable update helpers.
/// </summary>
public class SensorProfileTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var samplingInterval = TimeSpan.FromSeconds(30);

        // Act
        var profile = new SensorProfile(
            id: id,
            samplingInterval: samplingInterval,
            communicationMode: "  MQTT  ",
            noiseLevel: 0.15,
            latencyProfile: "  Low latency  ",
            failureProfile: "  Rare failures  ");

        // Assert
        Assert.Equal(id, profile.Id);
        Assert.Equal(samplingInterval, profile.SamplingInterval);
        Assert.Equal("MQTT", profile.CommunicationMode);
        Assert.Equal(0.15, profile.NoiseLevel);
        Assert.Equal("Low latency", profile.LatencyProfile);
        Assert.Equal("Rare failures", profile.FailureProfile);
    }

    [Fact]
    public void Ctor_Throws_WhenIdIsEmpty()
    {
        // Act
        var ex = Assert.Throws<ArgumentException>(() => new SensorProfile(
            id: Guid.Empty,
            samplingInterval: TimeSpan.FromSeconds(30),
            communicationMode: "MQTT",
            noiseLevel: 0.10,
            latencyProfile: "Low latency",
            failureProfile: "Rare failures"));

        // Assert
        Assert.Equal("id", ex.ParamName);
        Assert.Contains("must not be an empty GUID", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ctor_Throws_WhenSamplingIntervalIsNotPositive(int seconds)
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SensorProfile(
            id: Guid.NewGuid(),
            samplingInterval: TimeSpan.FromSeconds(seconds),
            communicationMode: "MQTT",
            noiseLevel: 0.10,
            latencyProfile: "Low latency",
            failureProfile: "Rare failures"));

        // Assert
        Assert.Equal("samplingInterval", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenNoiseLevelIsNaN()
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SensorProfile(
            id: Guid.NewGuid(),
            samplingInterval: TimeSpan.FromSeconds(30),
            communicationMode: "MQTT",
            noiseLevel: double.NaN,
            latencyProfile: "Low latency",
            failureProfile: "Rare failures"));

        // Assert
        Assert.Equal("noiseLevel", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenNoiseLevelIsInfinity()
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SensorProfile(
            id: Guid.NewGuid(),
            samplingInterval: TimeSpan.FromSeconds(30),
            communicationMode: "MQTT",
            noiseLevel: double.PositiveInfinity,
            latencyProfile: "Low latency",
            failureProfile: "Rare failures"));

        // Assert
        Assert.Equal("noiseLevel", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenNoiseLevelIsNegative()
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SensorProfile(
            id: Guid.NewGuid(),
            samplingInterval: TimeSpan.FromSeconds(30),
            communicationMode: "MQTT",
            noiseLevel: -0.01,
            latencyProfile: "Low latency",
            failureProfile: "Rare failures"));

        // Assert
        Assert.Equal("noiseLevel", ex.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_Throws_WhenCommunicationModeIsInvalid(string? value)
    {
        // Act
        var ex = Assert.Throws<ArgumentException>(() => new SensorProfile(
            id: Guid.NewGuid(),
            samplingInterval: TimeSpan.FromSeconds(30),
            communicationMode: value!,
            noiseLevel: 0.10,
            latencyProfile: "Low latency",
            failureProfile: "Rare failures"));

        // Assert
        Assert.Equal("communicationMode", ex.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_Throws_WhenLatencyProfileIsInvalid(string? value)
    {
        // Act
        var ex = Assert.Throws<ArgumentException>(() => new SensorProfile(
            id: Guid.NewGuid(),
            samplingInterval: TimeSpan.FromSeconds(30),
            communicationMode: "MQTT",
            noiseLevel: 0.10,
            latencyProfile: value!,
            failureProfile: "Rare failures"));

        // Assert
        Assert.Equal("latencyProfile", ex.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_Throws_WhenFailureProfileIsInvalid(string? value)
    {
        // Act
        var ex = Assert.Throws<ArgumentException>(() => new SensorProfile(
            id: Guid.NewGuid(),
            samplingInterval: TimeSpan.FromSeconds(30),
            communicationMode: "MQTT",
            noiseLevel: 0.10,
            latencyProfile: "Low latency",
            failureProfile: value!));

        // Assert
        Assert.Equal("failureProfile", ex.ParamName);
    }

    [Fact]
    public void WithSamplingInterval_ReturnsNewInstance_WithUpdatedSamplingInterval()
    {
        // Arrange
        var profile = CreateProfile();

        // Act
        var updated = profile.WithSamplingInterval(TimeSpan.FromMinutes(2));

        // Assert
        Assert.NotSame(profile, updated);
        Assert.Equal(profile.Id, updated.Id);
        Assert.Equal(TimeSpan.FromMinutes(2), updated.SamplingInterval);
        Assert.Equal(profile.CommunicationMode, updated.CommunicationMode);
        Assert.Equal(profile.NoiseLevel, updated.NoiseLevel);
        Assert.Equal(profile.LatencyProfile, updated.LatencyProfile);
        Assert.Equal(profile.FailureProfile, updated.FailureProfile);
    }

    [Fact]
    public void WithCommunication_ReturnsNewInstance_WithUpdatedCommunicationFields()
    {
        // Arrange
        var profile = CreateProfile();

        // Act
        var updated = profile.WithCommunication(
            communicationMode: "LoRaWAN",
            latencyProfile: "Medium latency",
            failureProfile: "Intermittent failures");

        // Assert
        Assert.NotSame(profile, updated);
        Assert.Equal(profile.Id, updated.Id);
        Assert.Equal(profile.SamplingInterval, updated.SamplingInterval);
        Assert.Equal("LoRaWAN", updated.CommunicationMode);
        Assert.Equal(profile.NoiseLevel, updated.NoiseLevel);
        Assert.Equal("Medium latency", updated.LatencyProfile);
        Assert.Equal("Intermittent failures", updated.FailureProfile);
    }

    [Fact]
    public void WithNoiseLevel_ReturnsNewInstance_WithUpdatedNoiseLevel()
    {
        // Arrange
        var profile = CreateProfile();

        // Act
        var updated = profile.WithNoiseLevel(0.25);

        // Assert
        Assert.NotSame(profile, updated);
        Assert.Equal(profile.Id, updated.Id);
        Assert.Equal(profile.SamplingInterval, updated.SamplingInterval);
        Assert.Equal(profile.CommunicationMode, updated.CommunicationMode);
        Assert.Equal(0.25, updated.NoiseLevel);
        Assert.Equal(profile.LatencyProfile, updated.LatencyProfile);
        Assert.Equal(profile.FailureProfile, updated.FailureProfile);
    }

    private static SensorProfile CreateProfile()
    {
        return new SensorProfile(
            id: Guid.NewGuid(),
            samplingInterval: TimeSpan.FromSeconds(30),
            communicationMode: "MQTT",
            noiseLevel: 0.10,
            latencyProfile: "Low latency",
            failureProfile: "Rare failures");
    }
}