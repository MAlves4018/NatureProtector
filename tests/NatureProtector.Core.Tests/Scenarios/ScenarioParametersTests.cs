using NatureProtector.Core.Scenarios;
using Xunit;

namespace NatureProtector.Core.Tests.Scenarios;

/// <summary>
/// Unit tests for ScenarioParameters.
/// These tests cover constructor validation, default values
/// and immutable update helpers.
/// </summary>
public class ScenarioParametersTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        // Arrange & Act
        var parameters = new ScenarioParameters(
            baseTemperature: 30.0,
            baseHumidity: 25.0,
            baseWindSpeed: 8.5,
            failureRate: 0.15,
            noiseLevel: 0.40,
            timeAcceleration: 3.0);

        // Assert
        Assert.Equal(30.0, parameters.BaseTemperature);
        Assert.Equal(25.0, parameters.BaseHumidity);
        Assert.Equal(8.5, parameters.BaseWindSpeed);
        Assert.Equal(0.15, parameters.FailureRate);
        Assert.Equal(0.40, parameters.NoiseLevel);
        Assert.Equal(3.0, parameters.TimeAcceleration);
    }

    [Fact]
    public void Ctor_AssignsDefaults_WhenNoArgumentsProvided()
    {
        // Arrange & Act
        var parameters = new ScenarioParameters();

        // Assert
        Assert.Null(parameters.BaseTemperature);
        Assert.Null(parameters.BaseHumidity);
        Assert.Null(parameters.BaseWindSpeed);
        Assert.Equal(0.0, parameters.FailureRate);
        Assert.Equal(0.0, parameters.NoiseLevel);
        Assert.Equal(1.0, parameters.TimeAcceleration);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ctor_Throws_WhenBaseTemperatureIsNotFinite(double invalidTemperature)
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ScenarioParameters(
            baseTemperature: invalidTemperature));

        // Assert
        Assert.Equal("baseTemperature", ex.ParamName);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Ctor_Throws_WhenBaseHumidityIsOutOfRange(double invalidHumidity)
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ScenarioParameters(
            baseHumidity: invalidHumidity));

        // Assert
        Assert.Equal("baseHumidity", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenBaseWindSpeedIsNegative()
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ScenarioParameters(
            baseWindSpeed: -0.1));

        // Assert
        Assert.Equal("baseWindSpeed", ex.ParamName);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Ctor_Throws_WhenFailureRateIsOutOfRange(double invalidFailureRate)
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ScenarioParameters(
            failureRate: invalidFailureRate));

        // Assert
        Assert.Equal("failureRate", ex.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ctor_Throws_WhenFailureRateIsNotFinite(double invalidFailureRate)
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ScenarioParameters(
            failureRate: invalidFailureRate));

        // Assert
        Assert.Equal("failureRate", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenNoiseLevelIsNegative()
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ScenarioParameters(
            noiseLevel: -0.01));

        // Assert
        Assert.Equal("noiseLevel", ex.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ctor_Throws_WhenNoiseLevelIsNotFinite(double invalidNoiseLevel)
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ScenarioParameters(
            noiseLevel: invalidNoiseLevel));

        // Assert
        Assert.Equal("noiseLevel", ex.ParamName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Ctor_Throws_WhenTimeAccelerationIsNotStrictlyPositive(double invalidTimeAcceleration)
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ScenarioParameters(
            timeAcceleration: invalidTimeAcceleration));

        // Assert
        Assert.Equal("timeAcceleration", ex.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ctor_Throws_WhenTimeAccelerationIsNotFinite(double invalidTimeAcceleration)
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ScenarioParameters(
            timeAcceleration: invalidTimeAcceleration));

        // Assert
        Assert.Equal("timeAcceleration", ex.ParamName);
    }

    [Fact]
    public void WithBaseTemperature_ReturnsNewInstance_WithUpdatedValue()
    {
        // Arrange
        var original = new ScenarioParameters(
            baseTemperature: 20.0,
            baseHumidity: 50.0,
            baseWindSpeed: 4.0,
            failureRate: 0.10,
            noiseLevel: 0.20,
            timeAcceleration: 2.0);

        // Act
        var updated = original.WithBaseTemperature(33.0);

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(33.0, updated.BaseTemperature);
        Assert.Equal(original.BaseHumidity, updated.BaseHumidity);
        Assert.Equal(original.BaseWindSpeed, updated.BaseWindSpeed);
        Assert.Equal(original.FailureRate, updated.FailureRate);
        Assert.Equal(original.NoiseLevel, updated.NoiseLevel);
        Assert.Equal(original.TimeAcceleration, updated.TimeAcceleration);

        Assert.Equal(20.0, original.BaseTemperature);
    }

    [Fact]
    public void WithBaseHumidity_ReturnsNewInstance_WithUpdatedValue()
    {
        // Arrange
        var original = new ScenarioParameters(
            baseTemperature: 20.0,
            baseHumidity: 50.0,
            baseWindSpeed: 4.0,
            failureRate: 0.10,
            noiseLevel: 0.20,
            timeAcceleration: 2.0);

        // Act
        var updated = original.WithBaseHumidity(22.0);

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(22.0, updated.BaseHumidity);
        Assert.Equal(original.BaseTemperature, updated.BaseTemperature);
        Assert.Equal(original.BaseWindSpeed, updated.BaseWindSpeed);
        Assert.Equal(original.FailureRate, updated.FailureRate);
        Assert.Equal(original.NoiseLevel, updated.NoiseLevel);
        Assert.Equal(original.TimeAcceleration, updated.TimeAcceleration);
    }

    [Fact]
    public void WithBaseWindSpeed_ReturnsNewInstance_WithUpdatedValue()
    {
        // Arrange
        var original = new ScenarioParameters(
            baseTemperature: 20.0,
            baseHumidity: 50.0,
            baseWindSpeed: 4.0,
            failureRate: 0.10,
            noiseLevel: 0.20,
            timeAcceleration: 2.0);

        // Act
        var updated = original.WithBaseWindSpeed(12.0);

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(12.0, updated.BaseWindSpeed);
        Assert.Equal(original.BaseTemperature, updated.BaseTemperature);
        Assert.Equal(original.BaseHumidity, updated.BaseHumidity);
        Assert.Equal(original.FailureRate, updated.FailureRate);
        Assert.Equal(original.NoiseLevel, updated.NoiseLevel);
        Assert.Equal(original.TimeAcceleration, updated.TimeAcceleration);
    }

    [Fact]
    public void WithExecutionControls_ReturnsNewInstance_WithUpdatedExecutionFields()
    {
        // Arrange
        var original = new ScenarioParameters(
            baseTemperature: 20.0,
            baseHumidity: 50.0,
            baseWindSpeed: 4.0,
            failureRate: 0.10,
            noiseLevel: 0.20,
            timeAcceleration: 2.0);

        // Act
        var updated = original.WithExecutionControls(
            failureRate: 0.30,
            noiseLevel: 0.60,
            timeAcceleration: 5.0);

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(original.BaseTemperature, updated.BaseTemperature);
        Assert.Equal(original.BaseHumidity, updated.BaseHumidity);
        Assert.Equal(original.BaseWindSpeed, updated.BaseWindSpeed);
        Assert.Equal(0.30, updated.FailureRate);
        Assert.Equal(0.60, updated.NoiseLevel);
        Assert.Equal(5.0, updated.TimeAcceleration);

        Assert.Equal(0.10, original.FailureRate);
        Assert.Equal(0.20, original.NoiseLevel);
        Assert.Equal(2.0, original.TimeAcceleration);
    }
}
