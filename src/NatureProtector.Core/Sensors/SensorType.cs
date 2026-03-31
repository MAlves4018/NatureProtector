/*
 * This enumeration classifies sensors according to their preventive observation role
 * in the Nature Protector domain.
 *
 * Rationale:
 * - The values are aligned with the current target model, which focuses on the
 *   environmental variables needed for preventive assessment and simulation.
 * - The enum is intentionally domain-oriented and decoupled from infrastructure,
 *   vendor-specific hardware or deployment details.
 */

namespace NatureProtector.Core.Sensors;

public enum SensorType
{
    /// <summary>
    /// Sensor primarily measuring air temperature.
    /// </summary>
    Temperature = 0,

    /// <summary>
    /// Sensor primarily measuring relative humidity.
    /// </summary>
    Humidity = 1,

    /// <summary>
    /// Sensor primarily measuring wind-related variables.
    /// </summary>
    Wind = 2,

    /// <summary>
    /// Weather station capable of producing a broader environmental snapshot.
    /// </summary>
    WeatherStation = 3,

    /// <summary>
    /// Composite or virtual sensor derived from multiple data sources.
    /// </summary>
    Composite = 4,
}