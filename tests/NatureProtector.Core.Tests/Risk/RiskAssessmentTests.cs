using NatureProtector.Core.Areas;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Readings;
using NatureProtector.Core.Risk;
using Xunit;

namespace NatureProtector.Core.Tests.Risk;

/// <summary>
/// Unit tests for RiskAssessment.
/// These tests cover constructor validation, derived risk level logic
/// and creation from RuleSet + Reading + AreaContext.
/// </summary>
public class RiskAssessmentTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var assessment = new RiskAssessment(
            id: id,
            timestamp: timestamp,
            riskScore: 0.65,
            explanationSummary: "  High temperature and wind  ");

        // Assert
        Assert.Equal(id, assessment.Id);
        Assert.Equal(timestamp, assessment.Timestamp);
        Assert.Equal(0.65, assessment.RiskScore);
        Assert.Equal(RiskLevelExtensions.FromScore(0.65), assessment.RiskLevel);
        Assert.Equal("High temperature and wind", assessment.ExplanationSummary);
    }

    [Fact]
    public void Ctor_NormalizesWhitespaceExplanation_ToNull()
    {
        // Arrange & Act
        var assessment = new RiskAssessment(
            id: Guid.NewGuid(),
            timestamp: DateTimeOffset.UtcNow,
            riskScore: 0.40,
            explanationSummary: "   ");

        // Assert
        Assert.Null(assessment.ExplanationSummary);
    }

    [Fact]
    public void Ctor_Throws_WhenIdIsEmpty()
    {
        // Act
        var ex = Assert.Throws<ArgumentException>(() =>
            new RiskAssessment(Guid.Empty, DateTimeOffset.UtcNow, 0.50));

        // Assert
        Assert.Equal("id", ex.ParamName);
        Assert.Contains("must not be an empty GUID", ex.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenTimestampIsDefault()
    {
        // Act
        var ex = Assert.Throws<ArgumentException>(() =>
            new RiskAssessment(Guid.NewGuid(), default, 0.50));

        // Assert
        Assert.Equal("timestamp", ex.ParamName);
        Assert.Contains("must be a valid, non-default value", ex.Message);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ctor_Throws_WhenRiskScoreIsNotFinite(double invalidScore)
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RiskAssessment(Guid.NewGuid(), DateTimeOffset.UtcNow, invalidScore));

        // Assert
        Assert.Equal("riskScore", ex.ParamName);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Ctor_Throws_WhenRiskScoreIsOutsideRange(double invalidScore)
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RiskAssessment(Guid.NewGuid(), DateTimeOffset.UtcNow, invalidScore));

        // Assert
        Assert.Equal("riskScore", ex.ParamName);
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.20)]
    [InlineData(0.45)]
    [InlineData(0.65)]
    [InlineData(0.85)]
    public void CalculateLevel_ReturnsExpectedMapping(double riskScore)
    {
        // Arrange
        var assessment = new RiskAssessment(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            riskScore);

        // Act
        var level = assessment.CalculateLevel();

        // Assert
        Assert.Equal(RiskLevelExtensions.FromScore(riskScore), level);
    }

    [Fact]
    public void Create_Throws_WhenRuleSetIsNull()
    {
        // Arrange
        var reading = CreateReading();
        var areaContext = CreateAreaContext();

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            RiskAssessment.Create(
                id: Guid.NewGuid(),
                timestamp: DateTimeOffset.UtcNow,
                ruleSet: null!,
                reading: reading,
                areaContext: areaContext));

        // Assert
        Assert.Equal("ruleSet", ex.ParamName);
    }

    [Fact]
    public void Create_Throws_WhenReadingIsNull()
    {
        // Arrange
        var ruleSet = CreateRuleSet();
        var areaContext = CreateAreaContext();

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            RiskAssessment.Create(
                id: Guid.NewGuid(),
                timestamp: DateTimeOffset.UtcNow,
                ruleSet: ruleSet,
                reading: null!,
                areaContext: areaContext));

        // Assert
        Assert.Equal("reading", ex.ParamName);
    }

    [Fact]
    public void Create_Throws_WhenAreaContextIsNull()
    {
        // Arrange
        var ruleSet = CreateRuleSet();
        var reading = CreateReading();

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            RiskAssessment.Create(
                id: Guid.NewGuid(),
                timestamp: DateTimeOffset.UtcNow,
                ruleSet: ruleSet,
                reading: reading,
                areaContext: null!));

        // Assert
        Assert.Equal("areaContext", ex.ParamName);
    }

    [Fact]
    public void Create_BuildsAssessment_FromRuleSetAndInputs()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var ruleSet = CreateRuleSet();

        var reading = new Reading(
            id: Guid.NewGuid(),
            timestamp: timestamp,
            location: new Location(38.7167, -9.1333),
            values: new ReadingValues(
                temperatureCelsius: 35.0,
                relativeHumidityPercent: 20.0,
                windSpeedMetersPerSecond: 10.0));

        var areaContext = new AreaContext(
            vegetationType: "Forest",
            vegetationDensity: 0.80,
            populationExposure: 0.20,
            criticalInfrastructureExposure: 0.10,
            seasonality: "Summer");

        var expectedScore = ruleSet.CalculateScore(reading, areaContext);
        var expectedExplanation = ruleSet.BuildExplanationSummary(reading, areaContext);

        // Act
        var assessment = RiskAssessment.Create(
            id: id,
            timestamp: timestamp,
            ruleSet: ruleSet,
            reading: reading,
            areaContext: areaContext);

        // Assert
        Assert.Equal(id, assessment.Id);
        Assert.Equal(timestamp, assessment.Timestamp);
        Assert.Equal(expectedScore, assessment.RiskScore, 6);
        Assert.Equal(RiskLevelExtensions.FromScore(expectedScore), assessment.RiskLevel);
        Assert.Equal(expectedExplanation, assessment.ExplanationSummary);
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