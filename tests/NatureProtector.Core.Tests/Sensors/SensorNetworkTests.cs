using NatureProtector.Core.Primitives;
using NatureProtector.Core.Sensors;
using Xunit;

namespace NatureProtector.Core.Tests.Sensors;

/// <summary>
/// Unit tests for the SensorNetwork aggregate.
/// These tests cover constructor invariants, initial membership loading
/// and sensor collection management according to the current model.
/// </summary>
public class SensorNetworkTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var network = new SensorNetwork(
            id: id,
            name: "  Network A  ");

        // Assert
        Assert.Equal(id, network.Id);
        Assert.Equal("Network A", network.Name);
        Assert.Empty(network.Sensors);
    }

    [Fact]
    public void Ctor_Throws_WhenIdIsEmpty()
    {
        // Act
        var ex = Assert.Throws<ArgumentException>(() => new SensorNetwork(
            id: Guid.Empty,
            name: "Network A"));

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
        // Act
        var ex = Assert.Throws<ArgumentException>(() => new SensorNetwork(
            id: Guid.NewGuid(),
            name: rawName!));

        // Assert
        Assert.Equal("name", ex.ParamName);
        Assert.Contains("must not be null or whitespace", ex.Message);
    }

    [Fact]
    public void Ctor_WithInitialSensors_AddsSensors()
    {
        // Arrange
        var sensor1 = CreateSensor("Sensor A");
        var sensor2 = CreateSensor("Sensor B");

        // Act
        var network = new SensorNetwork(
            id: Guid.NewGuid(),
            name: "Network A",
            sensors: new[] { sensor1, sensor2 });

        // Assert
        Assert.Equal(2, network.Sensors.Count);
        Assert.Contains(network.Sensors, s => s.Id == sensor1.Id);
        Assert.Contains(network.Sensors, s => s.Id == sensor2.Id);
    }

    [Fact]
    public void Ctor_WithDuplicateInitialSensors_IgnoresDuplicatesById()
    {
        // Arrange
        var sensor = CreateSensor("Sensor A");

        // Act
        var network = new SensorNetwork(
            id: Guid.NewGuid(),
            name: "Network A",
            sensors: new[] { sensor, sensor });

        // Assert
        Assert.Single(network.Sensors);
    }

    [Fact]
    public void Ctor_Throws_WhenInitialSensorsContainsNull()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => new SensorNetwork(
            id: Guid.NewGuid(),
            name: "Network A",
            sensors: new Sensor[] { CreateSensor("Sensor A"), null! }));

        // Assert
        Assert.Equal("sensor", ex.ParamName);
    }

    [Fact]
    public void AddSensor_Throws_WhenSensorIsNull()
    {
        // Arrange
        var network = CreateNetwork();

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => network.AddSensor(null!));

        // Assert
        Assert.Equal("sensor", ex.ParamName);
    }

    [Fact]
    public void AddSensor_AddsSensor_WhenNotPresent()
    {
        // Arrange
        var network = CreateNetwork();
        var sensor = CreateSensor("Sensor A");

        // Act
        network.AddSensor(sensor);

        // Assert
        Assert.Single(network.Sensors);
        Assert.Contains(network.Sensors, s => s.Id == sensor.Id);
    }

    [Fact]
    public void AddSensor_IgnoresDuplicateSensorId()
    {
        // Arrange
        var network = CreateNetwork();
        var sensor = CreateSensor("Sensor A");

        // Act
        network.AddSensor(sensor);
        network.AddSensor(sensor);

        // Assert
        Assert.Single(network.Sensors);
    }

    [Fact]
    public void RemoveSensor_ReturnsFalse_WhenSensorDoesNotExist()
    {
        // Arrange
        var network = CreateNetwork();

        // Act
        var removed = network.RemoveSensor(Guid.NewGuid());

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public void RemoveSensor_ReturnsTrue_AndRemovesSensor_WhenSensorExists()
    {
        // Arrange
        var network = CreateNetwork();
        var sensor = CreateSensor("Sensor A");
        network.AddSensor(sensor);

        // Act
        var removed = network.RemoveSensor(sensor.Id);

        // Assert
        Assert.True(removed);
        Assert.Empty(network.Sensors);
    }

    [Fact]
    public void TryGetSensor_ReturnsTrue_WhenSensorExists()
    {
        // Arrange
        var network = CreateNetwork();
        var sensor = CreateSensor("Sensor A");
        network.AddSensor(sensor);

        // Act
        var found = network.TryGetSensor(sensor.Id, out var resolved);

        // Assert
        Assert.True(found);
        Assert.Same(sensor, resolved);
    }

    [Fact]
    public void TryGetSensor_ReturnsFalse_WhenSensorDoesNotExist()
    {
        // Arrange
        var network = CreateNetwork();

        // Act
        var found = network.TryGetSensor(Guid.NewGuid(), out var resolved);

        // Assert
        Assert.False(found);
        Assert.Null(resolved);
    }

    private static SensorNetwork CreateNetwork()
    {
        return new SensorNetwork(
            id: Guid.NewGuid(),
            name: "Network A");
    }

    private static Sensor CreateSensor(string name)
    {
        return new Sensor(
            id: Guid.NewGuid(),
            name: name,
            type: SensorType.WeatherStation,
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