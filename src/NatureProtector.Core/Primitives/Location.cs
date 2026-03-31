using System;

/*
 * This class represents a geographic location in the Nature Protector domain.
 *
 * Rationale:
 * - Location is treated as a small immutable domain primitive used consistently
 *   across areas, risk cells, sensors, readings and weather snapshots.
 * - Keeping location logic in a dedicated type prevents coordinate handling from
 *   being duplicated throughout the domain model.
 *
 * Design considerations:
 * - The constructor enforces valid latitude and longitude ranges.
 * - Altitude is optional because not all domain operations require it.
 * - CellId is optional and allows the location to carry an application-level
 *   grid or tiling identifier when such information is available.
 * - DistanceTo provides a reusable approximate geodesic distance using the
 *   Haversine formula, which is sufficient for the current phase.
 */

namespace NatureProtector.Core.Primitives;

public sealed class Location
{
    /// <summary>
    /// Latitude in decimal degrees, using the WGS84 reference system.
    /// </summary>
    public double Latitude { get; }

    /// <summary>
    /// Longitude in decimal degrees, using the WGS84 reference system.
    /// </summary>
    public double Longitude { get; }

    /// <summary>
    /// Optional altitude in meters above sea level.
    /// </summary>
    public double? Altitude { get; }

    /// <summary>
    /// Optional logical identifier of the grid cell or tiling key associated
    /// with this location.
    /// </summary>
    public string? CellId { get; }

    /// <summary>
    /// Creates a new geographic location.
    /// </summary>
    /// <param name="latitude">
    /// Latitude in decimal degrees.
    /// </param>
    /// <param name="longitude">
    /// Longitude in decimal degrees.
    /// </param>
    /// <param name="altitude">
    /// Optional altitude in meters above sea level.
    /// </param>
    /// <param name="cellId">
    /// Optional logical grid or tiling identifier.
    /// </param>
    public Location(
        double latitude,
        double longitude,
        double? altitude = null,
        string? cellId = null)
    {
        if (latitude is < -90.0 or > 90.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                latitude,
                "Latitude must be in the range [-90, 90] degrees.");
        }

        if (longitude is < -180.0 or > 180.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude),
                longitude,
                "Longitude must be in the range [-180, 180] degrees.");
        }

        Latitude = latitude;
        Longitude = longitude;
        Altitude = altitude;
        CellId = string.IsNullOrWhiteSpace(cellId) ? null : cellId.Trim();
    }

    /// <summary>
    /// Computes an approximate distance in meters to another location
    /// using the Haversine formula.
    /// </summary>
    /// <param name="other">
    /// The other location to compare with.
    /// </param>
    /// <returns>
    /// Approximate surface distance in meters.
    /// </returns>
    public double DistanceTo(Location other)
    {
        ArgumentNullException.ThrowIfNull(other);

        const double EarthRadiusMeters = 6_371_000.0;

        static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

        var lat1 = ToRadians(Latitude);
        var lon1 = ToRadians(Longitude);
        var lat2 = ToRadians(other.Latitude);
        var lon2 = ToRadians(other.Longitude);

        var dLat = lat2 - lat1;
        var dLon = lon2 - lon1;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    /// <summary>
    /// Returns a new Location instance with the same coordinates and altitude,
    /// but a different cell identifier.
    /// </summary>
    /// <param name="newCellId">
    /// New logical grid or tiling identifier.
    /// </param>
    /// <returns>
    /// A new immutable Location instance.
    /// </returns>
    public Location WithCellId(string? newCellId)
    {
        var trimmed = string.IsNullOrWhiteSpace(newCellId)
            ? null
            : newCellId.Trim();

        return new Location(Latitude, Longitude, Altitude, trimmed);
    }
}