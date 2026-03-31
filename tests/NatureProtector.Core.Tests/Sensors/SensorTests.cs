using NatureProtector.Core.Primitives;
using NatureProtector.Core.Sensors;
using Xunit;

namespace NatureProtector.Core.Tests.Sensors;

/// <summary>
/// Unit tests for the Sensor entity.
/// These tests validate constructor invariants, property assignment
/// and activation state transitions according to the current model.
/// </summary>
public class SensorTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var profile = CreateProfile();
        var location = new Location(38.7223, -9.1393);

        // Act
        var sensor = new Sensor(
            id: id,
            name: "  Sensor A  ",
            type: SensorType.Temperature,
            location: location,
            profile: profile);

        // Assert
        Assert.Equal(id, sensor.Id);
        Assert.Equal("Sensor A", sensor.Name);
        Assert.Equal(SensorType.Temperature, sensor.Type);
        Assert.Same(location, sensor.Location);
        Assert.Same(profile, sensor.Profile);
        Assert.True(sensor.IsActive);
    }

    [Fact]
    public void Ctor_AssignsInactiveState_WhenProvided()
    {
        // Arrange
        var profile = CreateProfile();
        var location = new Location(38.7223, -9.1393);

        // Act
        var sensor = new Sensor(
            id: Guid.NewGuid(),
            name: "Sensor A",
            type: SensorType.Humidity,
            location: location,
            profile: profile,
            isActive: false);

        // Assert
        Assert.False(sensor.IsActive);
    }

    [Fact]
    public void Ctor_Throws_WhenIdIsEmpty()
    {
        // Arrange
        var profile = CreateProfile();
        var location = new Location(38.7223, -9.1393);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => new Sensor(
            id: Guid.Empty,
            name: "Sensor A",
            type: SensorType.Wind,
            location: location,
            profile: profile));

        // Assert
        Assert.Equal("id", ex.ParamName);
        Assert.Contains("must not be an empty GUID", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_Throws_WhenNameIsNullOrWhitespace(string? rawName)
    {
        // Arrange
        var profile = CreateProfile();
        var location = new Location(38.7223, -9.1393);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => new Sensor(
            id: Guid.NewGuid(),
            name: rawName!,
            type: SensorType.Temperature,
            location: location,
            profile: profile));

        // Assert
        Assert.Equal("name", ex.ParamName);
        Assert.Contains("must not be null or whitespace", ex.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenTypeIsInvalid()
    {
        // Arrange
        var profile = CreateProfile();
        var location = new Location(38.7223, -9.1393);

        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Sensor(
            id: Guid.NewGuid(),
            name: "Sensor A",
            type: (SensorType)999,
            location: location,
            profile: profile));

        // Assert
        Assert.Equal("type", ex.ParamName);
        Assert.Contains("Invalid sensor type value", ex.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenLocationIsNull()
    {
        // Arrange
        var profile = CreateProfile();

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => new Sensor(
            id: Guid.NewGuid(),
            name: "Sensor A",
            type: SensorType.Composite,
            location: null!,
            profile: profile));

        // Assert
        Assert.Equal("location", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenProfileIsNull()
    {
        // Arrange
        var location = new Location(38.7223, -9.1393);

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => new Sensor(
            id: Guid.NewGuid(),
            name: "Sensor A",
            type: SensorType.Composite,
            location: location,
            profile: null!));

        // Assert
        Assert.Equal("profile", ex.ParamName);
    }

    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        // Arrange
        var sensor = CreateSensor();

        // Act
        sensor.Deactivate();

        // Assert
        Assert.False(sensor.IsActive);
    }

    [Fact]
    public void Activate_SetsIsActiveToTrue()
    {
        // Arrange
        var sensor = new Sensor(
            id: Guid.NewGuid(),
            name: "Sensor A",
            type: SensorType.Temperature,
            location: new Location(38.7223, -9.1393),
            profile: CreateProfile(),
            isActive: false);

        // Act
        sensor.Activate();

        // Assert
        Assert.True(sensor.IsActive);
    }

    [Fact]
    public void Activate_AfterDeactivate_RestoresActiveState()
    {
        // Arrange
        var sensor = CreateSensor();

        // Act
        sensor.Deactivate();
        sensor.Activate();

        // Assert
        Assert.True(sensor.IsActive);
    }

    private static Sensor CreateSensor()
    {
        return new Sensor(
            id: Guid.NewGuid(),
            name: "Sensor A",
            type: SensorType.Temperature,
            location: new Location(38.7223, -9.1393),
            profile: CreateProfile());
    }

    private static SensorProfile CreateProfile()
    {
        return new SensorProfile(
            id: Guid.NewGuid(),
            samplingInterval: TimeSpan.FromMinutes(5),
            communicationMode: "MQTT",
            noiseLevel: 0.10,
            latencyProfile: "Low latency",
            failureProfile: "Rare failures");
    }
}