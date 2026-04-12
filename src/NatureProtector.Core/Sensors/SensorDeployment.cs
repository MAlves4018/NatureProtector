using NatureProtector.Core.Primitives;

/*
 * This class represents the placement of a Sensor within the configured control plane.
 *
 * Rationale:
 * - The Sensor entity should remain lightweight and independent from deployment
 *   concerns such as area ownership, cell assignment and installation profile.
 * - SensorDeployment captures those configuration concerns without forcing them
 *   into Sensor itself.
 *
 * Design considerations:
 * - Deployment identity is explicit because placement can change over time
 *   without invalidating the Sensor identity.
 * - DeploymentLocation is stored independently and must stay coherent with
 *   the associated Sensor location.
 * - Network membership is optional because the first wave may define sensors
 *   before a full logical network is introduced.
 */

namespace NatureProtector.Core.Sensors;

public sealed class SensorDeployment
{
    /// <summary>
    /// Globally unique identifier of the deployment.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Identifier of the area where the sensor is deployed.
    /// </summary>
    public Guid AreaId { get; }

    /// <summary>
    /// Identifier of the grid cell where the sensor is deployed.
    /// </summary>
    public Guid GridCellId { get; }

    /// <summary>
    /// Optional identifier of the logical sensor network.
    /// </summary>
    public Guid? SensorNetworkId { get; }

    /// <summary>
    /// Sensor associated with this deployment.
    /// </summary>
    public Sensor Sensor { get; }

    /// <summary>
    /// Deployment location used by the control plane.
    /// </summary>
    public Location DeploymentLocation { get; }

    /// <summary>
    /// Human-readable installation profile.
    /// </summary>
    public string InstallationProfile { get; }

    /// <summary>
    /// Indicates whether the sensor is the primary deployment for the cell.
    /// </summary>
    public bool IsPrimaryForCell { get; }

    public SensorDeployment(
        Guid id,
        Guid areaId,
        Guid gridCellId,
        Sensor sensor,
        Location deploymentLocation,
        string installationProfile,
        Guid? sensorNetworkId = null,
        bool isPrimaryForCell = false)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Deployment identifier must not be an empty GUID.",
                nameof(id));
        }

        if (areaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Area identifier must not be an empty GUID.",
                nameof(areaId));
        }

        if (gridCellId == Guid.Empty)
        {
            throw new ArgumentException(
                "Grid cell identifier must not be an empty GUID.",
                nameof(gridCellId));
        }

        if (string.IsNullOrWhiteSpace(installationProfile))
        {
            throw new ArgumentException(
                "Installation profile must not be null or whitespace.",
                nameof(installationProfile));
        }

        Sensor = sensor ?? throw new ArgumentNullException(nameof(sensor));
        DeploymentLocation = deploymentLocation ?? throw new ArgumentNullException(nameof(deploymentLocation));

        if (Sensor.Location.DistanceTo(DeploymentLocation) > 1.0)
        {
            throw new InvalidOperationException(
                "Deployment location must match the associated sensor location in the current baseline model.");
        }

        Id = id;
        AreaId = areaId;
        GridCellId = gridCellId;
        SensorNetworkId = sensorNetworkId;
        InstallationProfile = installationProfile.Trim();
        IsPrimaryForCell = isPrimaryForCell;
    }

    /// <summary>
    /// Creates a new deployment associated with a different sensor network.
    /// </summary>
    public SensorDeployment WithSensorNetwork(Guid? sensorNetworkId)
    {
        if (sensorNetworkId == Guid.Empty)
        {
            throw new ArgumentException(
                "Sensor network identifier must not be an empty GUID.",
                nameof(sensorNetworkId));
        }

        return new SensorDeployment(
            id: Id,
            areaId: AreaId,
            gridCellId: GridCellId,
            sensor: Sensor,
            deploymentLocation: DeploymentLocation,
            installationProfile: InstallationProfile,
            sensorNetworkId: sensorNetworkId,
            isPrimaryForCell: IsPrimaryForCell);
    }
}
