using NatureProtector.Core.Areas;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Readings;
using NatureProtector.Core.Risk;
using Xunit;

namespace NatureProtector.Core.Tests.Risk;

/// <summary>
/// Unit tests for RuleSet.
/// These tests cover constructor validation, preliminary score calculation
/// and explanation generation.
/// </summary>
public class RuleSetTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var ruleSet = new RuleSet(
            id: id,
            version: " v1.0 ",
            temperatureWeight: 1.0,
            humidityWeight: 2.0,
            windWeight: 3.0,
            vegetationWeight: 4.0);

        // Assert
        Assert.Equal(id, ruleSet.Id);
        Assert.Equal("v1.0", ruleSet.Version);
        Assert.Equal(1.0, ruleSet.TemperatureWeight);
        Assert.Equal(2.0, ruleSet.HumidityWeight);
        Assert.Equal(3.0, ruleSet.WindWeight);
        Assert.Equal(4.0, ruleSet.VegetationWeight);
    }

    [Fact]
    public void Ctor_Throws_WhenIdIsEmpty()
    {
        // Act
        var ex = Assert.Throws<ArgumentException>(() =>
            new RuleSet(
                id: Guid.Empty,
                version: "v1",
                temperatureWeight: 1.0,
                humidityWeight: 1.0,
                windWeight: 1.0,
                vegetationWeight: 1.0));

        // Assert
        Assert.Equal("id", ex.ParamName);
        Assert.Contains("must not be an empty GUID", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_Throws_WhenVersionIsNullOrWhitespace(string? rawVersion)
    {
        // Act
        var ex = Assert.Throws<ArgumentException>(() =>
            new RuleSet(
                id: Guid.NewGuid(),
                version: rawVersion!,
                temperatureWeight: 1.0,
                humidityWeight: 1.0,
                windWeight: 1.0,
                vegetationWeight: 1.0));

        // Assert
        Assert.Equal("version", ex.ParamName);
        Assert.Contains("must not be null or whitespace", ex.Message);
    }

    [Theory]
    [InlineData(double.NaN, "temperatureWeight")]
    [InlineData(double.PositiveInfinity, "humidityWeight")]
    [InlineData(double.NegativeInfinity, "windWeight")]
    public void Ctor_Throws_WhenWeightIsNotFinite(double invalidValue, string paramName)
    {
        // Act
        ArgumentOutOfRangeException ex = paramName switch
        {
            "temperatureWeight" => Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RuleSet(Guid.NewGuid(), "v1", invalidValue, 1.0, 1.0, 1.0)),

            "humidityWeight" => Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RuleSet(Guid.NewGuid(), "v1", 1.0, invalidValue, 1.0, 1.0)),

            "windWeight" => Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RuleSet(Guid.NewGuid(), "v1", 1.0, 1.0, invalidValue, 1.0)),

            _ => throw new InvalidOperationException("Unexpected parameter name in test.")
        };

        // Assert
        Assert.Equal(paramName, ex.ParamName);
    }

    [Theory]
    [InlineData(-0.1, 1.0, 1.0, 1.0, "temperatureWeight")]
    [InlineData(1.0, -0.1, 1.0, 1.0, "humidityWeight")]
    [InlineData(1.0, 1.0, -0.1, 1.0, "windWeight")]
    [InlineData(1.0, 1.0, 1.0, -0.1, "vegetationWeight")]
    public void Ctor_Throws_WhenWeightIsNegative(
        double temperatureWeight,
        double humidityWeight,
        double windWeight,
        double vegetationWeight,
        string expectedParamName)
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RuleSet(
                Guid.NewGuid(),
                "v1",
                temperatureWeight,
                humidityWeight,
                windWeight,
                vegetationWeight));

        // Assert
        Assert.Equal(expectedParamName, ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenAllWeightsAreZero()
    {
        // Act
        var ex = Assert.Throws<ArgumentException>(() =>
            new RuleSet(
                Guid.NewGuid(),
                "v1",
                0.0,
                0.0,
                0.0,
                0.0));

        // Assert
        Assert.Contains("At least one rule weight must be greater than zero", ex.Message);
    }

    [Fact]
    public void CalculateScore_Throws_WhenReadingIsNull()
    {
        // Arrange
        var ruleSet = CreateRuleSet();
        var areaContext = CreateAreaContext();

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            ruleSet.CalculateScore(null!, areaContext));

        // Assert
        Assert.Equal("reading", ex.ParamName);
    }

    [Fact]
    public void CalculateScore_Throws_WhenAreaContextIsNull()
    {
        // Arrange
        var ruleSet = CreateRuleSet();
        var reading = CreateReading();

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            ruleSet.CalculateScore(reading, null!));

        // Assert
        Assert.Equal("areaContext", ex.ParamName);
    }

    [Fact]
    public void CalculateScore_ReturnsOne_WhenAllSignalsAreMaximal()
    {
        // Arrange
        var ruleSet = new RuleSet(
            Guid.NewGuid(),
            "v1",
            temperatureWeight: 1.0,
            humidityWeight: 1.0,
            windWeight: 1.0,
            vegetationWeight: 1.0);

        var reading = new Reading(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new Location(38.7167, -9.1333),
            new ReadingValues(
                temperatureCelsius: 40.0,
                relativeHumidityPercent: 0.0,
                windSpeedMetersPerSecond: 20.0));

        var areaContext = new AreaContext(
            vegetationType: "Forest",
            vegetationDensity: 1.0,
            populationExposure: 0.20,
            criticalInfrastructureExposure: 0.10,
            seasonality: "Summer");

        // Act
        var score = ruleSet.CalculateScore(reading, areaContext);

        // Assert
        Assert.Equal(1.0, score, 6);
    }

    [Fact]
    public void CalculateScore_ReturnsExpectedWeightedAverage()
    {
        // Arrange
        var ruleSet = new RuleSet(
            Guid.NewGuid(),
            "v1",
            temperatureWeight: 2.0,
            humidityWeight: 1.0,
            windWeight: 1.0,
            vegetationWeight: 0.0);

        // Temperature 25 -> (25 - 10) / 30 = 0.5
        // Humidity 40 -> (100 - 40) / 100 = 0.6
        // Wind 10 -> 10 / 20 = 0.5
        // Score = (0.5*2 + 0.6*1 + 0.5*1) / 4 = 0.525
        var reading = new Reading(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new Location(38.7167, -9.1333),
            new ReadingValues(
                temperatureCelsius: 25.0,
                relativeHumidityPercent: 40.0,
                windSpeedMetersPerSecond: 10.0));

        var areaContext = new AreaContext(
            vegetationType: "Forest",
            vegetationDensity: 0.3,
            populationExposure: 0.2,
            criticalInfrastructureExposure: 0.1,
            seasonality: "Summer");

        // Act
        var score = ruleSet.CalculateScore(reading, areaContext);

        // Assert
        Assert.Equal(0.525, score, 6);
    }

    [Fact]
    public void CalculateScore_Throws_WhenNoUsableWeightedSignalsAreAvailable()
    {
        // Arrange
        var ruleSet = new RuleSet(
            Guid.NewGuid(),
            "v1",
            temperatureWeight: 1.0,
            humidityWeight: 1.0,
            windWeight: 1.0,
            vegetationWeight: 0.0);

        var reading = new Reading(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new Location(38.7167, -9.1333),
            new ReadingValues());

        var areaContext = CreateAreaContext();

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ruleSet.CalculateScore(reading, areaContext));

        // Assert
        Assert.Contains("no usable weighted signals are available", ex.Message);
    }

    [Fact]
    public void BuildExplanationSummary_Throws_WhenReadingIsNull()
    {
        // Arrange
        var ruleSet = CreateRuleSet();
        var areaContext = CreateAreaContext();

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            ruleSet.BuildExplanationSummary(null!, areaContext));

        // Assert
        Assert.Equal("reading", ex.ParamName);
    }

    [Fact]
    public void BuildExplanationSummary_Throws_WhenAreaContextIsNull()
    {
        // Arrange
        var ruleSet = CreateRuleSet();
        var reading = CreateReading();

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            ruleSet.BuildExplanationSummary(reading, null!));

        // Assert
        Assert.Equal("areaContext", ex.ParamName);
    }

    [Fact]
    public void BuildExplanationSummary_ReturnsGenericMessage_WhenNoDriverIsDominant()
    {
        // Arrange
        var ruleSet = CreateRuleSet();

        var reading = new Reading(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new Location(38.7167, -9.1333),
            new ReadingValues(
                temperatureCelsius: 15.0,
                relativeHumidityPercent: 90.0,
                windSpeedMetersPerSecond: 1.0));

        var areaContext = new AreaContext(
            vegetationType: "Grassland",
            vegetationDensity: 0.20,
            populationExposure: 0.20,
            criticalInfrastructureExposure: 0.10,
            seasonality: "Spring");

        // Act
        var summary = ruleSet.BuildExplanationSummary(reading, areaContext);

        // Assert
        Assert.Equal(
            "Risk is driven by combined moderate factors rather than a single dominant signal.",
            summary);
    }

    [Fact]
    public void BuildExplanationSummary_ReturnsDominantDrivers_WhenPresent()
    {
        // Arrange
        var ruleSet = CreateRuleSet();

        var reading = new Reading(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new Location(38.7167, -9.1333),
            new ReadingValues(
                temperatureCelsius: 35.0,
                relativeHumidityPercent: 20.0,
                windSpeedMetersPerSecond: 15.0));

        var areaContext = new AreaContext(
            vegetationType: "Forest",
            vegetationDensity: 0.80,
            populationExposure: 0.20,
            criticalInfrastructureExposure: 0.10,
            seasonality: "Summer");

        // Act
        var summary = ruleSet.BuildExplanationSummary(reading, areaContext);

        // Assert
        Assert.Contains("Main contributors:", summary);
        Assert.Contains("elevated temperature", summary);
        Assert.Contains("low relative humidity", summary);
        Assert.Contains("strong wind", summary);
        Assert.Contains("dense vegetation", summary);
    }

    private static RuleSet CreateRuleSet() =>
        new(
            id: Guid.NewGuid(),
            version: "v1",
            temperatureWeight: 1.0,
            humidityWeight: 1.0,
            windWeight: 1.0,
            vegetationWeight: 1.0);

    private static Reading CreateReading() =>
        new(
            id: Guid.NewGuid(),
            timestamp: DateTimeOffset.UtcNow,
            location: new Location(38.7167, -9.1333),
            values: new ReadingValues(
                temperatureCelsius: 25.0,
                relativeHumidityPercent: 50.0,
                windSpeedMetersPerSecond: 5.0));

    private static AreaContext CreateAreaContext() =>
        new(
            vegetationType: "Forest",
            vegetationDensity: 0.50,
            populationExposure: 0.20,
            criticalInfrastructureExposure: 0.10,
            seasonality: "Summer");
}