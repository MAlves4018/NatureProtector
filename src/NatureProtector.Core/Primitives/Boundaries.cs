/*
 * This class represents a simple rectangular geographic boundary box,
 * defined by minimum and maximum latitude and longitude values.
 *
 * Rationale:
 * - For the current phase, a bounding box is sufficient to express the
 *   coarse geographic extent of an Area.
 * - More advanced geometric representations, such as arbitrary polygons,
 *   can be introduced later without invalidating this abstraction.
 *
 * Design considerations:
 * - The constructor enforces valid geographic coordinate ranges.
 * - The constructor also enforces a strictly valid box, meaning the minimum
 *   coordinates must be lower than the maximum coordinates.
 * - The Contains operation is intentionally simple and checks whether a
 *   Location falls inside or on the edges of the box.
 */

namespace NatureProtector.Core.Primitives;

public sealed class Boundaries
{
    /// <summary>
    /// Minimum latitude in decimal degrees.
    /// </summary>
    public double MinLatitude { get; }

    /// <summary>
    /// Maximum latitude in decimal degrees.
    /// </summary>
    public double MaxLatitude { get; }

    /// <summary>
    /// Minimum longitude in decimal degrees.
    /// </summary>
    public double MinLongitude { get; }

    /// <summary>
    /// Maximum longitude in decimal degrees.
    /// </summary>
    public double MaxLongitude { get; }

    /// <summary>
    /// Creates a new rectangular boundary box.
    /// </summary>
    /// <param name="minLatitude">
    /// Southern latitude boundary in decimal degrees.
    /// </param>
    /// <param name="maxLatitude">
    /// Northern latitude boundary in decimal degrees.
    /// </param>
    /// <param name="minLongitude">
    /// Western longitude boundary in decimal degrees.
    /// </param>
    /// <param name="maxLongitude">
    /// Eastern longitude boundary in decimal degrees.
    /// </param>
    public Boundaries(double minLatitude, double maxLatitude, double minLongitude, double maxLongitude)
    {
        if (minLatitude is < -90.0 or > 90.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minLatitude),
                minLatitude,
                "Minimum latitude must be in the range [-90, 90] degrees.");
        }

        if (maxLatitude is < -90.0 or > 90.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxLatitude),
                maxLatitude,
                "Maximum latitude must be in the range [-90, 90] degrees.");
        }

        if (minLongitude is < -180.0 or > 180.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minLongitude),
                minLongitude,
                "Minimum longitude must be in the range [-180, 180] degrees.");
        }

        if (maxLongitude is < -180.0 or > 180.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxLongitude),
                maxLongitude,
                "Maximum longitude must be in the range [-180, 180] degrees.");
        }

        if (minLatitude >= maxLatitude)
        {
            throw new ArgumentException(
                "Minimum latitude must be strictly lower than maximum latitude.",
                nameof(minLatitude));
        }

        if (minLongitude >= maxLongitude)
        {
            throw new ArgumentException(
                "Minimum longitude must be strictly lower than maximum longitude.",
                nameof(minLongitude));
        }

        MinLatitude = minLatitude;
        MaxLatitude = maxLatitude;
        MinLongitude = minLongitude;
        MaxLongitude = maxLongitude;
    }

    /// <summary>
    /// Indicates whether the specified location falls within these boundaries.
    /// Points on the edges are considered inside.
    /// </summary>
    /// <param name="location">
    /// Location to test against the current boundary box.
    /// </param>
    /// <returns>
    /// True when the location lies inside or on the edges of the box; otherwise false.
    /// </returns>
    public bool Contains(Location location)
    {
        ArgumentNullException.ThrowIfNull(location);

        return location.Latitude >= MinLatitude &&
               location.Latitude <= MaxLatitude &&
               location.Longitude >= MinLongitude &&
               location.Longitude <= MaxLongitude;
    }
}