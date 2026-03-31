/*
 * This class represents the operational profile of a sensor.
 *
 * Rationale:
 * - SensorProfile groups together sampling and communication characteristics
 *   that affect how a sensor behaves during simulation and ingestion.
 * - Keeping this information outside Sensor avoids turning Sensor into a large
 *   infrastructure-heavy entity and makes configuration easier to evolve.
 *
 * Design considerations:
 * - The profile is immutable after construction.
 * - SamplingInterval must be strictly positive.
 * - NoiseLevel must be non-negative.
 * - CommunicationMode, LatencyProfile and FailureProfile are treated as required
 *   descriptive strings in the current baseline because the target model expects
 *   them as explicit attributes.
 */

namespace NatureProtector.Core.Sensors;

public sealed class SensorProfile
{
    /// <summary>
    /// Globally unique identifier of the sensor profile.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Interval between two consecutive observations produced by the sensor.
    /// </summary>
    public TimeSpan SamplingInterval { get; }

    /// <summary>
    /// Logical communication mode used by the sensor.
    /// </summary>
    public string CommunicationMode { get; }

    /// <summary>
    /// Non-negative scalar representing the amount of observation noise.
    /// </summary>
    public double NoiseLevel { get; }

    /// <summary>
    /// Human-readable description of the expected latency behaviour.
    /// </summary>
    public string LatencyProfile { get; }

    /// <summary>
    /// Human-readable description of the expected failure behaviour.
    /// </summary>
    public string FailureProfile { get; }

    /// <summary>
    /// Creates a new SensorProfile instance.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the profile.
    /// </param>
    /// <param name="samplingInterval">
    /// Interval between consecutive sensor observations.
    /// </param>
    /// <param name="communicationMode">
    /// Communication mode description, for example MQTT or LoRaWAN.
    /// </param>
    /// <param name="noiseLevel">
    /// Non-negative scalar representing injected or expected noise.
    /// </param>
    /// <param name="latencyProfile">
    /// Human-readable latency profile.
    /// </param>
    /// <param name="failureProfile">
    /// Human-readable failure profile.
    /// </param>
    public SensorProfile(
        Guid id,
        TimeSpan samplingInterval,
        string communicationMode,
        double noiseLevel,
        string latencyProfile,
        string failureProfile)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Sensor profile identifier must not be an empty GUID.",
                nameof(id));
        }

        if (samplingInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(samplingInterval),
                samplingInterval,
                "Sampling interval must be strictly greater than zero.");
        }

        if (double.IsNaN(noiseLevel) || double.IsInfinity(noiseLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(noiseLevel),
                noiseLevel,
                "Noise level must be a finite number.");
        }

        if (noiseLevel < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(noiseLevel),
                noiseLevel,
                "Noise level must be greater than or equal to zero.");
        }

        if (string.IsNullOrWhiteSpace(communicationMode))
        {
            throw new ArgumentException(
                "Communication mode must not be null or whitespace.",
                nameof(communicationMode));
        }

        if (string.IsNullOrWhiteSpace(latencyProfile))
        {
            throw new ArgumentException(
                "Latency profile must not be null or whitespace.",
                nameof(latencyProfile));
        }

        if (string.IsNullOrWhiteSpace(failureProfile))
        {
            throw new ArgumentException(
                "Failure profile must not be null or whitespace.",
                nameof(failureProfile));
        }

        Id = id;
        SamplingInterval = samplingInterval;
        CommunicationMode = communicationMode.Trim();
        NoiseLevel = noiseLevel;
        LatencyProfile = latencyProfile.Trim();
        FailureProfile = failureProfile.Trim();
    }

    /// <summary>
    /// Returns a new SensorProfile instance with a different sampling interval.
    /// </summary>
    /// <param name="samplingInterval">
    /// New strictly positive sampling interval.
    /// </param>
    public SensorProfile WithSamplingInterval(TimeSpan samplingInterval)
    {
        return new SensorProfile(
            id: Id,
            samplingInterval: samplingInterval,
            communicationMode: CommunicationMode,
            noiseLevel: NoiseLevel,
            latencyProfile: LatencyProfile,
            failureProfile: FailureProfile);
    }

    /// <summary>
    /// Returns a new SensorProfile instance with updated communication behaviour.
    /// </summary>
    /// <param name="communicationMode">
    /// New communication mode.
    /// </param>
    /// <param name="latencyProfile">
    /// New latency profile.
    /// </param>
    /// <param name="failureProfile">
    /// New failure profile.
    /// </param>
    public SensorProfile WithCommunication(
        string communicationMode,
        string latencyProfile,
        string failureProfile)
    {
        return new SensorProfile(
            id: Id,
            samplingInterval: SamplingInterval,
            communicationMode: communicationMode,
            noiseLevel: NoiseLevel,
            latencyProfile: latencyProfile,
            failureProfile: failureProfile);
    }

    /// <summary>
    /// Returns a new SensorProfile instance with an updated noise level.
    /// </summary>
    /// <param name="noiseLevel">
    /// New non-negative noise level.
    /// </param>
    public SensorProfile WithNoiseLevel(double noiseLevel)
    {
        return new SensorProfile(
            id: Id,
            samplingInterval: SamplingInterval,
            communicationMode: CommunicationMode,
            noiseLevel: noiseLevel,
            latencyProfile: LatencyProfile,
            failureProfile: FailureProfile);
    }
}