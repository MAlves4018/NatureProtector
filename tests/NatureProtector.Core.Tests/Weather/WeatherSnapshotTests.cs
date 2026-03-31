using NatureProtector.Core.Primitives;
using NatureProtector.Core.Weather;
using Xunit;

namespace NatureProtector.Core.Tests.Weather;

/// <summary>
/// Unit tests for WeatherSnapshot.
/// These tests validate constructor invariants, hot and dry heuristics,
/// immutable updates and merge behaviour.
/// </summary>
public class WeatherSnapshotTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var location = new Location(1.0, 1.0);
        var wind = new WindVector(5.0, 90.0);

        // Act
        var snapshot = new WeatherSnapshot(
            id: id,
            timestamp: timestamp,
            location: location,
            temperatureCelsius: 30.0,
            relativeHumidityPercent: 25.0,
            wind: wind);

        // Assert
        Assert.Equal(id, snapshot.Id);
        Assert.Equal(timestamp, snapshot.Timestamp);
        Assert.Same(location, snapshot.Location);
        Assert.Equal(30.0, snapshot.TemperatureCelsius);
        Assert.Equal(25.0, snapshot.RelativeHumidityPercent);
        Assert.Same(wind, snapshot.Wind);
    }

    [Fact]
    public void Ctor_Throws_WhenIdIsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(() => new WeatherSnapshot(
            Guid.Empty,
            DateTimeOffset.UtcNow));

        Assert.Equal("id", ex.ParamName);
        Assert.Contains("must not be an empty GUID", ex.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenTimestampIsDefault()
    {
        var ex = Assert.Throws<ArgumentException>(() => new WeatherSnapshot(
            Guid.NewGuid(),
            default));

        Assert.Equal("timestamp", ex.ParamName);
        Assert.Contains("must be a valid, non-default value", ex.Message);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ctor_Throws_WhenTemperatureIsNotFinite(double invalid)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new WeatherSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            temperatureCelsius: invalid));

        Assert.Equal("temperatureCelsius", ex.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ctor_Throws_WhenHumidityIsNotFinite(double invalid)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new WeatherSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            relativeHumidityPercent: invalid));

        Assert.Equal("relativeHumidityPercent", ex.ParamName);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    public void Ctor_Throws_WhenHumidityOutsideRange(double humidity)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new WeatherSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            relativeHumidityPercent: humidity));

        Assert.Equal("relativeHumidityPercent", ex.ParamName);
    }

    [Theory]
    [InlineData(30.0, 30.0, true)]
    [InlineData(29.9, 30.0, false)]
    [InlineData(30.0, 30.1, false)]
    [InlineData(null, 20.0, false)]
    [InlineData(35.0, null, false)]
    public void IsHotAndDry_ReturnsExpectedValue(
        double? temperature,
        double? humidity,
        bool expected)
    {
        // Arrange
        var snapshot = new WeatherSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            temperatureCelsius: temperature,
            relativeHumidityPercent: humidity);

        // Act
        var result = snapshot.IsHotAndDry();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void WithUpdatedWind_ReturnsNewSnapshot_WithUpdatedWind()
    {
        // Arrange
        var original = new WeatherSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            location: new Location(1.0, 1.0),
            temperatureCelsius: 22.0,
            relativeHumidityPercent: 55.0,
            wind: new WindVector(2.0, 90.0));

        var newWind = new WindVector(5.0, 180.0);

        // Act
        var updated = original.WithUpdatedWind(newWind);

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal(original.Timestamp, updated.Timestamp);
        Assert.Same(original.Location, updated.Location);
        Assert.Equal(original.TemperatureCelsius, updated.TemperatureCelsius);
        Assert.Equal(original.RelativeHumidityPercent, updated.RelativeHumidityPercent);
        Assert.Same(newWind, updated.Wind);
    }

    [Fact]
    public void MergeWith_Throws_WhenOtherIsNull()
    {
        // Arrange
        var snapshot = CreateSnapshot(DateTimeOffset.UtcNow);

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => snapshot.MergeWith(null!));

        // Assert
        Assert.Equal("nextSnapshot", ex.ParamName);
    }

    [Fact]
    public void MergeWith_AveragesNumericValues_AndUsesNewerTimestamp()
    {
        // Arrange
        var earlier = CreateSnapshot(
            timestamp: DateTimeOffset.UtcNow,
            temperature: 20.0,
            humidity: 40.0,
            wind: new WindVector(2.0, 90.0),
            location: new Location(1.0, 1.0));

        var later = CreateSnapshot(
            timestamp: earlier.Timestamp.AddMinutes(10),
            temperature: 30.0,
            humidity: 60.0,
            wind: null,
            location: null);

        // Act
        var merged = earlier.MergeWith(later);

        // Assert
        Assert.NotEqual(Guid.Empty, merged.Id);
        Assert.NotEqual(earlier.Id, merged.Id);
        Assert.NotEqual(later.Id, merged.Id);
        Assert.Equal(later.Timestamp, merged.Timestamp);
        Assert.Equal(25.0, merged.TemperatureCelsius);
        Assert.Equal(50.0, merged.RelativeHumidityPercent);

        // The merge logic keeps nextSnapshot values when present,
        // otherwise it falls back to the current snapshot values.
        Assert.Same(earlier.Wind, merged.Wind);
        Assert.Same(earlier.Location, merged.Location);
    }

    [Fact]
    public void MergeWith_UsesNextSnapshotLocationAndWind_WhenProvided()
    {
        // Arrange
        var earlierLocation = new Location(1.0, 1.0);
        var laterLocation = new Location(2.0, 2.0);

        var earlierWind = new WindVector(2.0, 90.0);
        var laterWind = new WindVector(8.0, 180.0);

        var earlier = CreateSnapshot(
            timestamp: DateTimeOffset.UtcNow,
            location: earlierLocation,
            wind: earlierWind);

        var later = CreateSnapshot(
            timestamp: earlier.Timestamp.AddMinutes(5),
            location: laterLocation,
            wind: laterWind);

        // Act
        var merged = earlier.MergeWith(later);

        // Assert
        Assert.Same(laterLocation, merged.Location);
        Assert.Same(laterWind, merged.Wind);
    }

    [Fact]
    public void MergeWith_CanKeepCurrentTimestamp_WhenCurrentIsNewer()
    {
        // Arrange
        var newer = CreateSnapshot(
            timestamp: DateTimeOffset.UtcNow.AddMinutes(10),
            temperature: 32.0);

        var older = CreateSnapshot(
            timestamp: DateTimeOffset.UtcNow,
            temperature: 28.0);

        // Act
        var merged = newer.MergeWith(older);

        // Assert
        Assert.Equal(newer.Timestamp, merged.Timestamp);
        Assert.Equal(30.0, merged.TemperatureCelsius);
    }

    private static WeatherSnapshot CreateSnapshot(
        DateTimeOffset timestamp,
        double? temperature = null,
        double? humidity = null,
        WindVector? wind = null,
        Location? location = null) =>
        new(
            Guid.NewGuid(),
            timestamp,
            location,
            temperature,
            humidity,
            wind);
}