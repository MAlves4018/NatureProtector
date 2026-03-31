/*
 * This class represents a logical grouping of sensors within the Nature Protector domain.
 *
 * Rationale:
 * - SensorNetwork allows the model to express deployment topology and grouping,
 *   such as a set of related field sensors, a weather station cluster or a
 *   composite observation network.
 * - The target diagram shows SensorNetwork as the owner of zero or more sensors.
 *
 * Design considerations:
 * - The network keeps an internal private collection of sensors and exposes
 *   a read-only view to preserve aggregate control.
 * - Only the network identity and name are part of the stable external shape.
 * - Lightweight management methods are kept because they are useful in code,
 *   even though the diagram only shows attributes.
 */

namespace NatureProtector.Core.Sensors;

public sealed class SensorNetwork
{
    /// <summary>
    /// Globally unique identifier of the sensor network.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Human-readable name of the sensor network.
    /// </summary>
    public string Name { get; }

    private readonly List<Sensor> _sensors = [];

    /// <summary>
    /// Read-only view of the sensors that belong to this network.
    /// </summary>
    public IReadOnlyCollection<Sensor> Sensors => _sensors.AsReadOnly();

    /// <summary>
    /// Creates a new SensorNetwork instance.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the network.
    /// </param>
    /// <param name="name">
    /// Human-readable name of the network.
    /// </param>
    /// <param name="sensors">
    /// Optional initial sensors to associate with the network.
    /// </param>
    public SensorNetwork(Guid id, string name, IEnumerable<Sensor>? sensors = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Sensor network identifier must not be an empty GUID.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Sensor network name must not be null or whitespace.",
                nameof(name));
        }

        Id = id;
        Name = name.Trim();

        if (sensors is null)
        {
            return;
        }

        foreach (var sensor in sensors)
        {
            AddSensor(sensor);
        }
    }

    /// <summary>
    /// Adds a sensor to the network if it is not already present.
    /// </summary>
    /// <param name="sensor">
    /// Sensor instance to add.
    /// </param>
    public void AddSensor(Sensor sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        // Guard against duplicate membership by sensor identity.
        if (_sensors.Any(existing => existing.Id == sensor.Id))
        {
            return;
        }

        _sensors.Add(sensor);
    }

    /// <summary>
    /// Removes a sensor from the network by its identifier.
    /// </summary>
    /// <param name="sensorId">
    /// Identifier of the sensor to remove.
    /// </param>
    /// <returns>
    /// True if the sensor existed and was removed, false otherwise.
    /// </returns>
    public bool RemoveSensor(Guid sensorId)
    {
        var sensor = _sensors.FirstOrDefault(s => s.Id == sensorId);
        return sensor is not null && _sensors.Remove(sensor);
    }

    /// <summary>
    /// Attempts to retrieve a sensor from the network by its identifier.
    /// </summary>
    /// <param name="sensorId">
    /// Identifier of the sensor to locate.
    /// </param>
    /// <param name="sensor">
    /// When this method returns true, contains the matching sensor;
    /// otherwise null.
    /// </param>
    /// <returns>
    /// True if a matching sensor was found, false otherwise.
    /// </returns>
    public bool TryGetSensor(Guid sensorId, out Sensor? sensor)
    {
        sensor = _sensors.FirstOrDefault(s => s.Id == sensorId);
        return sensor is not null;
    }
}