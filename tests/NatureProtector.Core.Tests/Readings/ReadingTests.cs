using NatureProtector.Core.Primitives;
using NatureProtector.Core.Readings;
using Xunit;

namespace NatureProtector.Core.Tests.Readings;

/// <summary>
/// Unit tests for the Reading entity.
/// These tests validate constructor invariants, suitability rules,
/// interval checks and immutable time adjustment behaviour.
/// </summary>
public class ReadingTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var location = new Location(5.0, 5.0);
        var values = new ReadingValues(temperatureCelsius: 25.0);

        // Act
        var reading = new Reading(id, timestamp, location, values);

        // Assert
        Assert.Equal(id, reading.Id);
        Assert.Equal(timestamp, reading.Timestamp);
        Assert.Same(location, reading.Location);
        Assert.Same(values, reading.Values);
    }

    [Fact]
    public void Ctor_Throws_WhenIdIsEmpty()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var location = new Location(1.0, 1.0);
        var values = new ReadingValues();

        // Act
        var ex = Assert.Throws<ArgumentException>(
            () => new Reading(Guid.Empty, timestamp, location, values));

        // Assert
        Assert.Equal("id", ex.ParamName);
        Assert.Contains("must not be an empty GUID", ex.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenTimestampIsDefault()
    {
        // Arrange
        var location = new Location(1.0, 1.0);
        var values = new ReadingValues();

        // Act
        var ex = Assert.Throws<ArgumentException>(
            () => new Reading(Guid.NewGuid(), default, location, values));

        // Assert
        Assert.Equal("timestamp", ex.ParamName);
        Assert.Contains("must be a valid, non-default value", ex.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenLocationIsNull()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var values = new ReadingValues();

        // Act
        var ex = Assert.Throws<ArgumentNullException>(
            () => new Reading(Guid.NewGuid(), timestamp, null!, values));

        // Assert
        Assert.Equal("location", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenValuesIsNull()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var location = new Location(1.0, 1.0);

        // Act
        var ex = Assert.Throws<ArgumentNullException>(
            () => new Reading(Guid.NewGuid(), timestamp, location, null!));

        // Assert
        Assert.Equal("values", ex.ParamName);
    }

    [Theory]
    [InlineData(20.0, null, null, null, true)]
    [InlineData(null, 40.0, null, null, true)]
    [InlineData(null, null, 5.0, null, true)]
    [InlineData(null, null, null, 1.5, true)]
    [InlineData(null, null, null, null, false)]
    public void IsSuitableForRiskModel_ReturnsExpectedValue(
        double? temperature,
        double? humidity,
        double? windSpeed,
        double? precipitation,
        bool expected)
    {
        // Arrange
        var reading = CreateReading(new ReadingValues(
            temperatureCelsius: temperature,
            relativeHumidityPercent: humidity,
            windSpeedMetersPerSecond: windSpeed,
            precipitationMillimetresPerHour: precipitation));

        // Act
        var result = reading.IsSuitableForRiskModel();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsSuitableForRiskModel_ReturnsFalse_WhenOnlyWindDirectionExists()
    {
        // Arrange
        var reading = CreateReading(new ReadingValues(
            windDirectionDegrees: 180.0));

        // Act
        var result = reading.IsSuitableForRiskModel();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsWithin_Throws_WhenIntervalIsInvalid()
    {
        // Arrange
        var reading = CreateReading(new ReadingValues());
        var from = DateTimeOffset.UtcNow;
        var to = from.AddMinutes(-1);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => reading.IsWithin(from, to));

        // Assert
        Assert.Equal("from", ex.ParamName);
        Assert.Contains("earlier than or equal to the end", ex.Message);
    }

    [Fact]
    public void IsWithin_ReturnsTrue_WhenTimestampIsInsideInterval()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var reading = new Reading(
            Guid.NewGuid(),
            timestamp,
            new Location(1.0, 1.0),
            new ReadingValues());

        // Act
        var result = reading.IsWithin(timestamp.AddMinutes(-1), timestamp.AddMinutes(1));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsWithin_ReturnsTrue_WhenTimestampMatchesIntervalBounds()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var reading = new Reading(
            Guid.NewGuid(),
            timestamp,
            new Location(1.0, 1.0),
            new ReadingValues());

        // Act
        var atLowerBound = reading.IsWithin(timestamp, timestamp.AddMinutes(10));
        var atUpperBound = reading.IsWithin(timestamp.AddMinutes(-10), timestamp);

        // Assert
        Assert.True(atLowerBound);
        Assert.True(atUpperBound);
    }

    [Fact]
    public void IsWithin_ReturnsFalse_WhenTimestampIsOutsideInterval()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var reading = new Reading(
            Guid.NewGuid(),
            timestamp,
            new Location(1.0, 1.0),
            new ReadingValues());

        // Act
        var result = reading.IsWithin(timestamp.AddHours(1), timestamp.AddHours(2));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void WithAdjustedTime_ReturnsNewReading_WithNewIdAndShiftedTimestamp()
    {
        // Arrange
        var original = CreateReading(new ReadingValues(temperatureCelsius: 20.0));
        var offset = TimeSpan.FromMinutes(10);

        // Act
        var shifted = original.WithAdjustedTime(offset);

        // Assert
        Assert.NotEqual(original.Id, shifted.Id);
        Assert.Equal(original.Timestamp + offset, shifted.Timestamp);
        Assert.Same(original.Location, shifted.Location);
        Assert.Same(original.Values, shifted.Values);
    }

    [Fact]
    public void WithAdjustedTime_DoesNotMutateOriginalReading()
    {
        // Arrange
        var original = CreateReading(new ReadingValues(relativeHumidityPercent: 35.0));
        var originalId = original.Id;
        var originalTimestamp = original.Timestamp;

        // Act
        var shifted = original.WithAdjustedTime(TimeSpan.FromHours(1));

        // Assert
        Assert.Equal(originalId, original.Id);
        Assert.Equal(originalTimestamp, original.Timestamp);
        Assert.NotEqual(original.Id, shifted.Id);
        Assert.Equal(originalTimestamp.AddHours(1), shifted.Timestamp);
    }

    private static Reading CreateReading(ReadingValues values) =>
        new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new Location(1.0, 1.0),
            values);
}