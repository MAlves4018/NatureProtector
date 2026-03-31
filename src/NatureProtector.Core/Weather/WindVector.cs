/*
 * This class represents a wind vector at a given point in time, expressed by
 * speed and direction.
 *
 * Rationale:
 * - Wind is one of the main environmental drivers of wildfire behaviour and
 *   preventive risk assessment.
 * - A dedicated value object keeps wind-related logic isolated and reusable.
 *
 * Design considerations:
 * - Direction is expressed in degrees, where 0 means North and values increase
 *   clockwise, so 90 means East.
 * - The object is immutable after construction.
 * - Helper methods are kept because they are useful for later risk and simulation
 *   logic, even though the target class diagram only shows attributes.
 */

namespace NatureProtector.Core.Weather;

public sealed class WindVector
{
    /// <summary>
    /// Wind speed in meters per second.
    /// </summary>
    public double SpeedMetersPerSecond { get; }

    /// <summary>
    /// Wind direction in degrees, where 0 = North, 90 = East, 180 = South and 270 = West.
    /// </summary>
    public double DirectionDegrees { get; }

    /// <summary>
    /// Creates a new WindVector instance.
    /// </summary>
    /// <param name="speedMetersPerSecond">
    /// Wind speed in meters per second. Must be finite and non-negative.
    /// </param>
    /// <param name="directionDegrees">
    /// Wind direction in degrees. Must be finite and in the range [0, 360).
    /// </param>
    public WindVector(double speedMetersPerSecond, double directionDegrees)
    {
        if (double.IsNaN(speedMetersPerSecond) || double.IsInfinity(speedMetersPerSecond))
        {
            throw new ArgumentOutOfRangeException(
                nameof(speedMetersPerSecond),
                speedMetersPerSecond,
                "Wind speed must be a finite number.");
        }

        if (speedMetersPerSecond < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(speedMetersPerSecond),
                speedMetersPerSecond,
                "Wind speed must not be negative.");
        }

        if (double.IsNaN(directionDegrees) || double.IsInfinity(directionDegrees))
        {
            throw new ArgumentOutOfRangeException(
                nameof(directionDegrees),
                directionDegrees,
                "Direction must be a finite number.");
        }

        if (directionDegrees is < 0.0 or >= 360.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(directionDegrees),
                directionDegrees,
                "Direction must be in the range [0, 360) degrees.");
        }

        SpeedMetersPerSecond = speedMetersPerSecond;
        DirectionDegrees = directionDegrees;
    }

    /// <summary>
    /// Converts the wind vector into North and East components.
    /// </summary>
    /// <returns>
    /// A tuple containing the North and East components of the wind vector.
    /// </returns>
    public (double NorthComponent, double EastComponent) ToComponents()
    {
        if (SpeedMetersPerSecond == 0.0)
        {
            return (0.0, 0.0);
        }

        var radians = DirectionDegrees * Math.PI / 180.0;

        // In this domain convention:
        // - 0 degrees points to North  => cosine contributes fully to North
        // - 90 degrees points to East  => sine contributes fully to East
        var north = SpeedMetersPerSecond * Math.Cos(radians);
        var east = SpeedMetersPerSecond * Math.Sin(radians);

        return (north, east);
    }

    /// <summary>
    /// Returns the unit direction vector corresponding to this wind direction.
    /// </summary>
    /// <returns>
    /// A tuple containing the North and East components of the unit vector.
    /// </returns>
    public (double NorthComponent, double EastComponent) ToUnitVector()
    {
        if (SpeedMetersPerSecond == 0.0)
        {
            return (0.0, 0.0);
        }

        var (north, east) = ToComponents();
        return (north / SpeedMetersPerSecond, east / SpeedMetersPerSecond);
    }

    /// <summary>
    /// Returns a new WindVector with the same speed and a different direction.
    /// </summary>
    /// <param name="newDirectionDegrees">
    /// New wind direction in degrees.
    /// </param>
    public WindVector WithDirection(double newDirectionDegrees)
    {
        return new WindVector(SpeedMetersPerSecond, newDirectionDegrees);
    }

    /// <summary>
    /// Returns a new WindVector with the same direction and a different speed.
    /// </summary>
    /// <param name="newSpeedMetersPerSecond">
    /// New wind speed in meters per second.
    /// </param>
    public WindVector WithSpeed(double newSpeedMetersPerSecond)
    {
        return new WindVector(newSpeedMetersPerSecond, DirectionDegrees);
    }

    /// <summary>
    /// Returns a new WindVector pointing in the opposite direction.
    /// </summary>
    public WindVector Opposite()
    {
        var oppositeDirection = (DirectionDegrees + 180.0) % 360.0;
        return new WindVector(SpeedMetersPerSecond, oppositeDirection);
    }
}