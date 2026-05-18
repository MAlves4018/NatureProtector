using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;
using Xunit;

namespace NatureProtector.Core.Tests.Risk;

/// <summary>
/// Unit tests for AreaRiskSnapshot.
/// These tests cover constructor validation and aggregation behaviour.
/// </summary>
public class AreaRiskSnapshotTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var snapshot = new AreaRiskSnapshot(
            id: id,
            timestamp: timestamp,
            aggregateRiskScore: 0.75,
            summary: "  Aggregated summary  ");

        // Assert
        Assert.Equal(id, snapshot.Id);
        Assert.Equal(timestamp, snapshot.Timestamp);
        Assert.Equal(0.75, snapshot.AggregateRiskScore);
        Assert.Equal(RiskLevelExtensions.FromScore(0.75), snapshot.AggregateRiskLevel);
        Assert.Equal("Aggregated summary", snapshot.Summary);
    }

    [Fact]
    public void Ctor_NormalizesWhitespaceSummary_ToNull()
    {
        // Arrange & Act
        var snapshot = new AreaRiskSnapshot(
            id: Guid.NewGuid(),
            timestamp: DateTimeOffset.UtcNow,
            aggregateRiskScore: 0.30,
            summary: "   ");

        // Assert
        Assert.Null(snapshot.Summary);
    }

    [Fact]
    public void Ctor_Throws_WhenIdIsEmpty()
    {
        // Act
        var ex = Assert.Throws<ArgumentException>(() =>
            new AreaRiskSnapshot(
                id: Guid.Empty,
                timestamp: DateTimeOffset.UtcNow,
                aggregateRiskScore: 0.30));

        // Assert
        Assert.Equal("id", ex.ParamName);
        Assert.Contains("must not be an empty GUID", ex.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenTimestampIsDefault()
    {
        // Act
        var ex = Assert.Throws<ArgumentException>(() =>
            new AreaRiskSnapshot(
                id: Guid.NewGuid(),
                timestamp: default,
                aggregateRiskScore: 0.30));

        // Assert
        Assert.Equal("timestamp", ex.ParamName);
        Assert.Contains("must be a valid, non-default value", ex.Message);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ctor_Throws_WhenAggregateRiskScoreIsNotFinite(double invalidScore)
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AreaRiskSnapshot(
                id: Guid.NewGuid(),
                timestamp: DateTimeOffset.UtcNow,
                aggregateRiskScore: invalidScore));

        // Assert
        Assert.Equal("aggregateRiskScore", ex.ParamName);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Ctor_Throws_WhenAggregateRiskScoreIsOutsideRange(double invalidScore)
    {
        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AreaRiskSnapshot(
                id: Guid.NewGuid(),
                timestamp: DateTimeOffset.UtcNow,
                aggregateRiskScore: invalidScore));

        // Assert
        Assert.Equal("aggregateRiskScore", ex.ParamName);
    }

    [Fact]
    public void CreateFromAssessments_Throws_WhenAssessmentsIsNull()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            AreaRiskSnapshot.CreateFromAssessments(
                id: Guid.NewGuid(),
                timestamp: DateTimeOffset.UtcNow,
                assessments: null!));

        // Assert
        Assert.Equal("assessments", ex.ParamName);
    }

    [Fact]
    public void CreateFromAssessments_Throws_WhenAssessmentsIsEmpty()
    {
        // Act
        var ex = Assert.Throws<ArgumentException>(() =>
            AreaRiskSnapshot.CreateFromAssessments(
                id: Guid.NewGuid(),
                timestamp: DateTimeOffset.UtcNow,
                assessments: Array.Empty<RiskAssessment>()));

        // Assert
        Assert.Equal("assessments", ex.ParamName);
        Assert.Contains("At least one risk assessment is required", ex.Message);
    }

    [Fact]
    public void CreateFromAssessments_AggregatesP80AndMaxScore_AndBuildsSummary()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        var assessments = new[]
        {
            new RiskAssessment(Guid.NewGuid(), timestamp, 0.80),
            new RiskAssessment(Guid.NewGuid(), timestamp, 0.20),
            new RiskAssessment(Guid.NewGuid(), timestamp, 0.90)
        };

        var expectedAreaRisk = 0.90;

        // Act
        var snapshot = AreaRiskSnapshot.CreateFromAssessments(
            id: Guid.NewGuid(),
            timestamp: timestamp,
            assessments: assessments);

        // Assert
        Assert.Equal(expectedAreaRisk, snapshot.AggregateRiskScore, 6);
        Assert.Equal(RiskLevelExtensions.FromScore(expectedAreaRisk), snapshot.AggregateRiskLevel);
        Assert.NotNull(snapshot.Summary);
        Assert.Contains("Aggregated from 3 assessments", snapshot.Summary);
        Assert.Contains("2 at High or above", snapshot.Summary);
        Assert.Contains("0.70*p80", snapshot.Summary);
    }
}
