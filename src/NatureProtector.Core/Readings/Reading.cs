using NatureProtector.Core.Primitives;

/*
 * This class represents a single observation captured at a specific place and time
 * within the Nature Protector domain.
 *
 * Rationale:
 * - A Reading is the domain object that carries measured environmental values
 *   into the preventive analysis pipeline.
 * - In the current target model, Reading intentionally focuses on the observation
 *   itself rather than on transport or infrastructure concerns such as SensorId
 *   or AreaId, which are better handled by DTOs, events or persistence models.
 *
 * Design considerations:
 * - The class is immutable after construction in order to preserve the integrity
 *   of an observed measurement.
 * - Identity, timestamp, location and values are all mandatory.
 * - A small amount of behaviour is kept inside the class because suitability
 *   checks and temporal helpers are closely related to the meaning of a reading.
 */

namespace NatureProtector.Core.Readings;

public sealed class Reading
{
    /// <summary>
    /// Globally unique identifier of the reading.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Instant when the measurement was taken.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Location to which this reading applies.
    /// In most cases this corresponds to the originating sensor location.
    /// </summary>
    public Location Location { get; }

    /// <summary>
    /// Environmental measurement values associated with this reading.
    /// </summary>
    public ReadingValues Values { get; }

    /// <summary>
    /// Creates a new Reading instance.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the reading.
    /// </param>
    /// <param name="timestamp">
    /// Instant at which the observation was captured.
    /// </param>
    /// <param name="location">
    /// Geographic location associated with the reading.
    /// </param>
    /// <param name="values">
    /// Environmental values observed at the given time and location.
    /// </param>
    public Reading(
        Guid id,
        DateTimeOffset timestamp,
        Location location,
        ReadingValues values)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Reading identifier must not be an empty GUID.",
                nameof(id));
        }

        if (timestamp == default)
        {
            throw new ArgumentException(
                "Timestamp must be a valid, non-default value.",
                nameof(timestamp));
        }

        Location = location ?? throw new ArgumentNullException(nameof(location));
        Values = values ?? throw new ArgumentNullException(nameof(values));

        Id = id;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Indicates whether this reading contains enough relevant information
    /// to participate in preventive risk evaluation.
    /// </summary>
    /// <returns>
    /// True when the reading contains at least one preventive signal currently
    /// relevant to the model; otherwise false.
    /// </returns>
    public bool IsSuitableForRiskModel()
    {
        return Values.TemperatureCelsius.HasValue
               || Values.RelativeHumidityPercent.HasValue
               || Values.WindSpeedMetersPerSecond.HasValue
               || Values.PrecipitationMillimetresPerHour.HasValue;
    }

    /// <summary>
    /// Returns true if the reading timestamp falls within the specified interval,
    /// inclusive of both bounds.
    /// </summary>
    /// <param name="from">
    /// Start of the interval.
    /// </param>
    /// <param name="to">
    /// End of the interval.
    /// </param>
    /// <returns>
    /// True when the timestamp is inside the interval; otherwise false.
    /// </returns>
    public bool IsWithin(DateTimeOffset from, DateTimeOffset to)
    {
        if (from > to)
        {
            throw new ArgumentException(
                "The start of the interval must be earlier than or equal to the end.",
                nameof(from));
        }

        return Timestamp >= from && Timestamp <= to;
    }

    /// <summary>
    /// Returns a new Reading with the timestamp adjusted by the given offset.
    /// A new identifier is generated because the result represents a derived reading.
    /// </summary>
    /// <param name="offset">
    /// Temporal offset to apply to the current timestamp.
    /// </param>
    /// <returns>
    /// A new Reading instance with adjusted time and preserved semantic content.
    /// </returns>
    public Reading WithAdjustedTime(TimeSpan offset)
    {
        var newTimestamp = Timestamp + offset;

        return new Reading(
            id: Guid.NewGuid(),
            timestamp: newTimestamp,
            location: Location,
            values: Values);
    }
}