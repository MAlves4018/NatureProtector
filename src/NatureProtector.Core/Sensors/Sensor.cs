using NatureProtector.Core.Primitives;

/*
 * This class represents a physical or logical sensor in the Nature Protector domain.
 *
 * Rationale:
 * - A Sensor is a domain entity with its own identity, physical location and
 *   operational state.
 * - In the current target model, a sensor is intentionally lightweight:
 *   it exposes only the attributes required for simulation, observation and
 *   preventive assessment.
 *
 * Design considerations:
 * - Identity and descriptive properties are immutable after construction.
 * - The sensor location is also treated as stable in this baseline model.
 * - Operational state is intentionally limited to activation status.
 * - Behaviour previously related to area ownership, network identifiers and
 *   health checks was removed because it belongs to the older model and is
 *   not part of the current target class design.
 */

namespace NatureProtector.Core.Sensors;

public sealed class Sensor
{
    /// <summary>
    /// Globally unique identifier of the sensor.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Human-readable name of the sensor.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Functional type of the sensor.
    /// </summary>
    public SensorType Type { get; }

    /// <summary>
    /// Location where the sensor is placed or to which it is logically associated.
    /// </summary>
    public Location Location { get; }

    /// <summary>
    /// Operational profile associated with the sensor.
    /// </summary>
    public SensorProfile Profile { get; }

    /// <summary>
    /// Indicates whether the sensor is currently active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Creates a new Sensor instance.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the sensor.
    /// </param>
    /// <param name="name">
    /// Human-readable sensor name.
    /// </param>
    /// <param name="type">
    /// Functional type of the sensor.
    /// </param>
    /// <param name="location">
    /// Physical or logical sensor location.
    /// </param>
    /// <param name="profile">
    /// Operational profile associated with the sensor.
    /// </param>
    /// <param name="isActive">
    /// Initial activation state of the sensor.
    /// </param>
    public Sensor(
        Guid id,
        string name,
        SensorType type,
        Location location,
        SensorProfile profile,
        bool isActive = true)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Sensor identifier must not be an empty GUID.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Sensor name must not be null or whitespace.",
                nameof(name));
        }

        if (!Enum.IsDefined(typeof(SensorType), type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "Invalid sensor type value.");
        }

        Id = id;
        Name = name.Trim();
        Type = type;
        Location = location ?? throw new ArgumentNullException(nameof(location));
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        IsActive = isActive;
    }

    /// <summary>
    /// Marks the sensor as active.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Marks the sensor as inactive.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}