using NatureProtector.Core.Primitives;
using NatureProtector.Core.Sensors;

namespace NatureProtector.Core.Tests.Sensors;

public class SensorDeploymentTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        var sensor = CreateSensor();
        var areaId = Guid.NewGuid();
        var gridCellId = Guid.NewGuid();
        var networkId = Guid.NewGuid();

        var deployment = new SensorDeployment(
            id: Guid.NewGuid(),
            areaId: areaId,
            gridCellId: gridCellId,
            sensor: sensor,
            deploymentLocation: sensor.Location,
            installationProfile: "torre reforçada",
            sensorNetworkId: networkId,
            isPrimaryForCell: true);

        Assert.Equal(areaId, deployment.AreaId);
        Assert.Equal(gridCellId, deployment.GridCellId);
        Assert.Equal(networkId, deployment.SensorNetworkId);
        Assert.Same(sensor, deployment.Sensor);
        Assert.Same(sensor.Location, deployment.DeploymentLocation);
        Assert.Equal("torre reforçada", deployment.InstallationProfile);
        Assert.True(deployment.IsPrimaryForCell);
    }

    [Fact]
    public void Ctor_EmptyId_ThrowsArgumentException()
    {
        var sensor = CreateSensor();

        var exception = Assert.Throws<ArgumentException>(() => new SensorDeployment(
            id: Guid.Empty,
            areaId: Guid.NewGuid(),
            gridCellId: Guid.NewGuid(),
            sensor: sensor,
            deploymentLocation: sensor.Location,
            installationProfile: "base"));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void Ctor_EmptyAreaId_ThrowsArgumentException()
    {
        var sensor = CreateSensor();

        var exception = Assert.Throws<ArgumentException>(() => new SensorDeployment(
            id: Guid.NewGuid(),
            areaId: Guid.Empty,
            gridCellId: Guid.NewGuid(),
            sensor: sensor,
            deploymentLocation: sensor.Location,
            installationProfile: "base"));

        Assert.Equal("areaId", exception.ParamName);
    }

    [Fact]
    public void Ctor_EmptyGridCellId_ThrowsArgumentException()
    {
        var sensor = CreateSensor();

        var exception = Assert.Throws<ArgumentException>(() => new SensorDeployment(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            gridCellId: Guid.Empty,
            sensor: sensor,
            deploymentLocation: sensor.Location,
            installationProfile: "base"));

        Assert.Equal("gridCellId", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_MissingInstallationProfile_ThrowsArgumentException(string? installationProfile)
    {
        var sensor = CreateSensor();

        var exception = Assert.Throws<ArgumentException>(() => new SensorDeployment(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            gridCellId: Guid.NewGuid(),
            sensor: sensor,
            deploymentLocation: sensor.Location,
            installationProfile: installationProfile!));

        Assert.Equal("installationProfile", exception.ParamName);
    }

    [Fact]
    public void Ctor_NullSensor_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new SensorDeployment(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            gridCellId: Guid.NewGuid(),
            sensor: null!,
            deploymentLocation: new Location(39.746, -7.925),
            installationProfile: "base"));

        Assert.Equal("sensor", exception.ParamName);
    }

    [Fact]
    public void Ctor_NullDeploymentLocation_ThrowsArgumentNullException()
    {
        var sensor = CreateSensor();

        var exception = Assert.Throws<ArgumentNullException>(() => new SensorDeployment(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            gridCellId: Guid.NewGuid(),
            sensor: sensor,
            deploymentLocation: null!,
            installationProfile: "base"));

        Assert.Equal("deploymentLocation", exception.ParamName);
    }

    [Fact]
    public void Ctor_MismatchedDeploymentLocation_ThrowsInvalidOperationException()
    {
        var sensor = CreateSensor();

        var exception = Assert.Throws<InvalidOperationException>(() => new SensorDeployment(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            gridCellId: Guid.NewGuid(),
            sensor: sensor,
            deploymentLocation: new Location(40.0, -8.0),
            installationProfile: "base"));

        Assert.Contains("Deployment location must match the associated sensor location", exception.Message);
    }

    [Fact]
    public void WithSensorNetwork_ReturnsCopyWithUpdatedNetwork()
    {
        var sensor = CreateSensor();
        var deployment = new SensorDeployment(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            gridCellId: Guid.NewGuid(),
            sensor: sensor,
            deploymentLocation: sensor.Location,
            installationProfile: "base");

        var updated = deployment.WithSensorNetwork(Guid.NewGuid());

        Assert.Equal(deployment.Id, updated.Id);
        Assert.Equal(deployment.AreaId, updated.AreaId);
        Assert.Equal(deployment.GridCellId, updated.GridCellId);
        Assert.NotNull(updated.SensorNetworkId);
        Assert.Null(deployment.SensorNetworkId);
    }

    [Fact]
    public void WithSensorNetwork_NullNetwork_ReturnsCopyWithNetworkCleared()
    {
        var sensor = CreateSensor();
        var deployment = new SensorDeployment(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            gridCellId: Guid.NewGuid(),
            sensor: sensor,
            deploymentLocation: sensor.Location,
            installationProfile: "  base  ",
            sensorNetworkId: Guid.NewGuid(),
            isPrimaryForCell: true);

        var updated = deployment.WithSensorNetwork(null);

        Assert.Equal(deployment.Id, updated.Id);
        Assert.Equal(deployment.AreaId, updated.AreaId);
        Assert.Equal(deployment.GridCellId, updated.GridCellId);
        Assert.Same(deployment.Sensor, updated.Sensor);
        Assert.Same(deployment.DeploymentLocation, updated.DeploymentLocation);
        Assert.Equal("base", updated.InstallationProfile);
        Assert.True(updated.IsPrimaryForCell);
        Assert.Null(updated.SensorNetworkId);
    }

    [Fact]
    public void WithSensorNetwork_EmptyNetworkId_ThrowsArgumentException()
    {
        var sensor = CreateSensor();
        var deployment = new SensorDeployment(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            gridCellId: Guid.NewGuid(),
            sensor: sensor,
            deploymentLocation: sensor.Location,
            installationProfile: "base");

        var exception = Assert.Throws<ArgumentException>(() => deployment.WithSensorNetwork(Guid.Empty));

        Assert.Equal("sensorNetworkId", exception.ParamName);
    }

    private static Sensor CreateSensor()
    {
        return new Sensor(
            id: Guid.NewGuid(),
            name: "PN Sensor 01",
            type: SensorType.Temperature,
            location: new Location(39.746, -7.925),
            profile: new SensorProfile(
                id: Guid.NewGuid(),
                samplingInterval: TimeSpan.FromMinutes(5),
                communicationMode: "MQTT",
                noiseLevel: 0.05,
                latencyProfile: "low",
                failureProfile: "rare"));
    }
}
