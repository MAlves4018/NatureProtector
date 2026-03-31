/*
 * This service resolves the pseudo-random seed used by the simulator host.
 *
 * Rationale:
 * - A simulation that cannot be reproduced is harder to debug and validate.
 * - Centralizing seed resolution ensures that the whole host uses a single,
 *   explicit deterministic source of pseudo-randomness.
 *
 * Design considerations:
 * - If a seed is configured, it is used as-is.
 * - If no seed is configured, a new one is generated once at startup.
 * - The service is intentionally small because its responsibility is purely
 *   deterministic seed selection, not random number generation itself.
 */

namespace NatureProtector.Simulator.Host.Services;

public sealed class SeedProvider
{
    /// <summary>
    /// Resolves the effective seed to use for the current simulator execution.
    /// </summary>
    /// <param name="configuredSeed">
    /// Optional seed supplied through configuration.
    /// </param>
    /// <returns>
    /// A non-zero integer seed suitable for constructing Random.
    /// </returns>
    public int ResolveSeed(int? configuredSeed)
    {
        if (configuredSeed.HasValue)
        {
            return configuredSeed.Value;
        }

        var generatedSeed = Random.Shared.Next(1, int.MaxValue);

        return generatedSeed;
    }

    /// <summary>
    /// Creates a Random instance using the resolved seed.
    /// </summary>
    /// <param name="seed">
    /// Seed previously resolved for the current execution.
    /// </param>
    /// <returns>
    /// Deterministic Random instance.
    /// </returns>
    public Random CreateRandom(int seed)
    {
        return new Random(seed);
    }
}