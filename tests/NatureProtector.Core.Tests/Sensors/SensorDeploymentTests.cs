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
