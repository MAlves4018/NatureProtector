/*
 * This enumeration represents the high-level category of a scenario used
 * by the Nature Protector platform.
 *
 * Rationale:
 * - ScenarioCategory provides a lightweight semantic classification for
 *   scenario configuration and execution.
 * - The values are aligned with the current target model, where scenarios
 *   are grouped according to their preventive purpose rather than by
 *   technical execution mode.
 *
 * Design considerations:
 * - The enum is intentionally small and focused on the current baseline.
 * - Additional categories can be introduced later without changing the
 *   core meaning of the existing values.
 */

namespace NatureProtector.Core.Scenarios;

public enum ScenarioCategory
{
    /// <summary>
    /// Baseline scenario representing ordinary expected operating conditions.
    /// </summary>
    Base = 0,

    /// <summary>
    /// Scenario representing conditions expected to yield higher preventive risk.
    /// </summary>
    HighRisk = 1,

    /// <summary>
    /// Scenario representing degraded or faulty behaviour in sensors or flow.
    /// </summary>
    Failure = 2,

    /// <summary>
    /// Scenario used for controlled drills or demonstrations.
    /// </summary>
    Exercise = 3
}