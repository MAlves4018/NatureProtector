using NatureProtector.Core.Weather;
using Xunit;

namespace NatureProtector.Core.Tests.Weather;

/// <summary>
/// Unit tests for WindVector.
/// These tests cover construction invariants, vector decomposition,
/// immutable update helpers and opposite-direction logic.
/// </summary>
public class WindVectorTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        // Arrange & Act
        var wind = new WindVector(10.0, 90.0);

        // Assert
        Assert.Equal(10.0, wind.SpeedMetersPerSecond);
        Assert.Equal(90.0, wind.DirectionDegrees);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ctor_Throws_WhenSpeedIsNotFinite(double invalidSpeed)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new WindVector(invalidSpeed, 0.0));

        Assert.Equal("speedMetersPerSecond", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenSpeedIsNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new WindVector(-0.1, 0.0));

        Assert.Equal("speedMetersPerSecond", ex.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ctor_Throws_WhenDirectionIsNotFinite(double invalidDirection)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new WindVector(1.0, invalidDirection));

        Assert.Equal("directionDegrees", ex.ParamName);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(360.0)]
    public void Ctor_Throws_WhenDirectionOutsideRange(double direction)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new WindVector(1.0, direction));

        Assert.Equal("directionDegrees", ex.ParamName);
    }

    [Theory]
    [InlineData(0.0, 0.0, 0.0, 0.0)]
    [InlineData(10.0, 0.0, 10.0, 0.0)]
    [InlineData(10.0, 90.0, 0.0, 10.0)]
    [InlineData(10.0, 180.0, -10.0, 0.0)]
    [InlineData(10.0, 270.0, 0.0, -10.0)]
    public void ToComponents_ReturnsExpectedComponents(
        double speed,
        double direction,
        double expectedNorth,
        double expectedEast)
    {
        // Arrange
        var wind = new WindVector(speed, direction);

        // Act
        var (north, east) = wind.ToComponents();

        // Assert
        Assert.Equal(expectedNorth, north, 6);
        Assert.Equal(expectedEast, east, 6);
    }

    [Fact]
    public void ToUnitVector_ReturnsZeroVector_WhenSpeedIsZero()
    {
        // Arrange
        var wind = new WindVector(0.0, 45.0);

        // Act
        var (north, east) = wind.ToUnitVector();

        // Assert
        Assert.Equal(0.0, north, 6);
        Assert.Equal(0.0, east, 6);
    }

    [Fact]
    public void ToUnitVector_NormalizesDirectionVector()
    {
        // Arrange
        var wind = new WindVector(10.0, 90.0);

        // Act
        var (north, east) = wind.ToUnitVector();

        // Assert
        Assert.Equal(0.0, north, 6);
        Assert.Equal(1.0, east, 6);
    }

    [Fact]
    public void WithDirection_ReturnsNewVector_WithUpdatedDirection()
    {
        // Arrange
        var original = new WindVector(5.0, 90.0);

        // Act
        var updated = original.WithDirection(180.0);

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(5.0, updated.SpeedMetersPerSecond);
        Assert.Equal(180.0, updated.DirectionDegrees);
        Assert.Equal(90.0, original.DirectionDegrees);
    }

    [Fact]
    public void WithSpeed_ReturnsNewVector_WithUpdatedSpeed()
    {
        // Arrange
        var original = new WindVector(5.0, 90.0);

        // Act
        var updated = original.WithSpeed(8.0);

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(8.0, updated.SpeedMetersPerSecond);
        Assert.Equal(90.0, updated.DirectionDegrees);
        Assert.Equal(5.0, original.SpeedMetersPerSecond);
    }

    [Fact]
    public void Opposite_ReturnsVectorWithOppositeDirection()
    {
        // Arrange
        var wind = new WindVector(5.0, 45.0);

        // Act
        var opposite = wind.Opposite();

        // Assert
        Assert.Equal(5.0, opposite.SpeedMetersPerSecond);
        Assert.Equal(225.0, opposite.DirectionDegrees, 6);
    }

    [Fact]
    public void Opposite_WrapsDirectionCorrectly()
    {
        // Arrange
        var wind = new WindVector(3.0, 270.0);

        // Act
        var opposite = wind.Opposite();

        // Assert
        Assert.Equal(90.0, opposite.DirectionDegrees, 6);
    }
}