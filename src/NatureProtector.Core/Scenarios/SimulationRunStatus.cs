/*
 * This enumeration represents the lifecycle state of a simulation run.
 *
 * Rationale:
 * - A SimulationRun evolves through a small and explicit state machine.
 * - The status values provide a compact way to express execution progress
 *   in logs, dashboards and orchestration logic.
 *
 * Design considerations:
 * - The values are aligned with the current target model.
 * - The enum is intentionally small and focused on baseline execution flow.
 */

namespace NatureProtector.Core.Scenarios;

public enum SimulationRunStatus
{
    /// <summary>
    /// The run has been defined but is not yet ready for execution.
    /// </summary>
    Defined = 0,

    /// <summary>
    /// The run has been prepared and is ready to start.
    /// </summary>
    Ready = 1,

    /// <summary>
    /// The run is currently executing.
    /// </summary>
    Running = 2,

    /// <summary>
    /// The run completed successfully.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// The run ended unsuccessfully.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// The run was cancelled before normal completion.
    /// </summary>
    Cancelled = 5
}