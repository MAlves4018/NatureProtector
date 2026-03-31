using NatureProtector.Core.Readings;
using Xunit;

namespace NatureProtector.Core.Tests.Readings;

/// <summary>
/// Unit tests for ReadingValues.
/// These tests cover construction, validation rules,
/// immutable update helpers and combination behaviour.
/// </summary>
public class ReadingValuesTests
{
    [Fact]
    public void Ctor_AssignsValues_WhenValid()
    {
        // Arrange & Act
        var values = new ReadingValues(
            temperatureCelsius: 30.0,
            relativeHumidityPercent: 25.0,
            windSpeedMetersPerSecond: 5.0,
            windDirectionDegrees: 180.0,
            precipitationMillimetresPerHour: 1.5);

        // Assert
        Assert.Equal(30.0, values.TemperatureCelsius);
        Assert.Equal(25.0, values.RelativeHumidityPercent);
        Assert.Equal(5.0, values.WindSpeedMetersPerSecond);
        Assert.Equal(180.0, values.WindDirectionDegrees);
        Assert.Equal(1.5, values.PrecipitationMillimetresPerHour);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ctor_Throws_WhenTemperatureIsNotFinite(double invalid)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReadingValues(temperatureCelsius: invalid));

        Assert.Equal("temperatureCelsius", ex.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ctor_Throws_WhenHumidityIsNotFinite(double invalid)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReadingValues(relativeHumidityPercent: invalid));

        Assert.Equal("relativeHumidityPercent", ex.ParamName);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    public void Ctor_Throws_WhenHumidityOutsideRange(double humidity)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReadingValues(relativeHumidityPercent: humidity));

        Assert.Equal("relativeHumidityPercent", ex.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ctor_Throws_WhenWindSpeedIsNotFinite(double invalid)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReadingValues(windSpeedMetersPerSecond: invalid));

        Assert.Equal("windSpeedMetersPerSecond", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenWindSpeedIsNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReadingValues(windSpeedMetersPerSecond: -0.1));

        Assert.Equal("windSpeedMetersPerSecond", ex.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ctor_Throws_WhenWindDirectionIsNotFinite(double invalid)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReadingValues(windDirectionDegrees: invalid));

        Assert.Equal("windDirectionDegrees", ex.ParamName);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(360.1)]
    public void Ctor_Throws_WhenWindDirectionOutsideRange(double direction)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReadingValues(windDirectionDegrees: direction));

        Assert.Equal("windDirectionDegrees", ex.ParamName);
    }

    [Fact]
    public void Ctor_AllowsDirection360()
    {
        var values = new ReadingValues(windDirectionDegrees: 360.0);

        Assert.Equal(360.0, values.WindDirectionDegrees);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ctor_Throws_WhenPrecipitationIsNotFinite(double invalid)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReadingValues(precipitationMillimetresPerHour: invalid));

        Assert.Equal("precipitationMillimetresPerHour", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenPrecipitationIsNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReadingValues(precipitationMillimetresPerHour: -0.1));

        Assert.Equal("precipitationMillimetresPerHour", ex.ParamName);
    }

    [Fact]
    public void WithTemperature_ReturnsNewInstance_WithUpdatedTemperature()
    {
        // Arrange
        var original = new ReadingValues(
            temperatureCelsius: 20.0,
            relativeHumidityPercent: 40.0);

        // Act
        var updated = original.WithTemperature(25.0);

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(25.0, updated.TemperatureCelsius);
        Assert.Equal(40.0, updated.RelativeHumidityPercent);
        Assert.Equal(20.0, original.TemperatureCelsius);
    }

    [Fact]
    public void WithRelativeHumidity_ReturnsNewInstance_WithUpdatedHumidity()
    {
        // Arrange
        var original = new ReadingValues(
            temperatureCelsius: 20.0,
            relativeHumidityPercent: 40.0);

        // Act
        var updated = original.WithRelativeHumidity(35.0);

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(20.0, updated.TemperatureCelsius);
        Assert.Equal(35.0, updated.RelativeHumidityPercent);
        Assert.Equal(40.0, original.RelativeHumidityPercent);
    }

    [Fact]
    public void WithWind_ReturnsNewInstance_WithUpdatedWindFields()
    {
        // Arrange
        var original = new ReadingValues(
            windSpeedMetersPerSecond: 3.0,
            windDirectionDegrees: 90.0);

        // Act
        var updated = original.WithWind(5.0, 180.0);

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(5.0, updated.WindSpeedMetersPerSecond);
        Assert.Equal(180.0, updated.WindDirectionDegrees);
        Assert.Equal(3.0, original.WindSpeedMetersPerSecond);
        Assert.Equal(90.0, original.WindDirectionDegrees);
    }

    [Fact]
    public void WithPrecipitation_ReturnsNewInstance_WithUpdatedPrecipitation()
    {
        // Arrange
        var original = new ReadingValues(precipitationMillimetresPerHour: 1.0);

        // Act
        var updated = original.WithPrecipitation(2.5);

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(2.5, updated.PrecipitationMillimetresPerHour);
        Assert.Equal(1.0, original.PrecipitationMillimetresPerHour);
    }

    [Fact]
    public void CombineWith_Throws_WhenOtherIsNull()
    {
        // Arrange
        var values = new ReadingValues(temperatureCelsius: 20.0);

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => values.CombineWith(null!));

        // Assert
        Assert.Equal("other", ex.ParamName);
    }

    [Fact]
    public void CombineWith_AveragesOverlappingValues_AndPreservesAvailableOnes()
    {
        // Arrange
        var left = new ReadingValues(
            temperatureCelsius: 20.0,
            relativeHumidityPercent: 40.0,
            windSpeedMetersPerSecond: 4.0,
            precipitationMillimetresPerHour: null);

        var right = new ReadingValues(
            temperatureCelsius: 30.0,
            relativeHumidityPercent: null,
            windSpeedMetersPerSecond: 6.0,
            windDirectionDegrees: 180.0,
            precipitationMillimetresPerHour: 2.0);

        // Act
        var combined = left.CombineWith(right);

        // Assert
        Assert.Equal(25.0, combined.TemperatureCelsius);
        Assert.Equal(40.0, combined.RelativeHumidityPercent);
        Assert.Equal(5.0, combined.WindSpeedMetersPerSecond);
        Assert.Equal(180.0, combined.WindDirectionDegrees);
        Assert.Equal(2.0, combined.PrecipitationMillimetresPerHour);
    }

    [Fact]
    public void CombineWith_ReturnsNulls_WhenBothSidesAreNull()
    {
        // Arrange
        var left = new ReadingValues();
        var right = new ReadingValues();

        // Act
        var combined = left.CombineWith(right);

        // Assert
        Assert.Null(combined.TemperatureCelsius);
        Assert.Null(combined.RelativeHumidityPercent);
        Assert.Null(combined.WindSpeedMetersPerSecond);
        Assert.Null(combined.WindDirectionDegrees);
        Assert.Null(combined.PrecipitationMillimetresPerHour);
    }
}