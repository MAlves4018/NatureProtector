/*
 * This class represents a concrete execution instance created from a Scenario.
 *
 * Rationale:
 * - Scenario describes what is being modelled.
 * - SimulationRun describes one concrete execution of that scenario, with its
 *   own status, timestamps and optional execution seed.
 *
 * Design considerations:
 * - The lifecycle is deliberately explicit through SimulationRunStatus.
 * - The class starts in the Defined state and can then move through a small
 *   controlled set of transitions.
 * - ExecutionSeed is optional because some runs may be deterministic while
 *   others may rely on runtime-generated randomness.
 */

namespace NatureProtector.Core.Scenarios;

public sealed class SimulationRun
{
    /// <summary>
    /// Globally unique identifier of the simulation run.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Actual start timestamp of the run, if it has already started.
    /// </summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>
    /// Actual end timestamp of the run, if it has already ended.
    /// </summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>
    /// Current lifecycle status of the run.
    /// </summary>
    public SimulationRunStatus Status { get; private set; }

    /// <summary>
    /// Optional seed controlling deterministic pseudo-random execution.
    /// </summary>
    public int? ExecutionSeed { get; }

    /// <summary>
    /// Creates a new SimulationRun instance in the Defined state.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the run.
    /// </param>
    /// <param name="executionSeed">
    /// Optional deterministic seed used during execution.
    /// </param>
    public SimulationRun(Guid id, int? executionSeed = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Simulation run identifier must not be an empty GUID.",
                nameof(id));
        }

        Id = id;
        ExecutionSeed = executionSeed;
        Status = SimulationRunStatus.Defined;
    }

    /// <summary>
    /// Marks the run as ready for execution.
    /// </summary>
    public void MarkReady()
    {
        if (Status != SimulationRunStatus.Defined)
        {
            throw new InvalidOperationException(
                $"Simulation run {Id} cannot be marked ready from status {Status}.");
        }

        Status = SimulationRunStatus.Ready;
    }

    /// <summary>
    /// Starts the run at the specified time.
    /// </summary>
    /// <param name="startedAt">
    /// Actual run start timestamp.
    /// </param>
    public void Start(DateTimeOffset startedAt)
    {
        if (startedAt == default)
        {
            throw new ArgumentException(
                "Start time must be a valid, non-default timestamp.",
                nameof(startedAt));
        }

        if (Status is not SimulationRunStatus.Defined and not SimulationRunStatus.Ready)
        {
            throw new InvalidOperationException(
                $"Simulation run {Id} cannot start from status {Status}.");
        }

        if (StartedAt.HasValue)
        {
            throw new InvalidOperationException(
                $"Simulation run {Id} has already been started at {StartedAt:O}.");
        }

        StartedAt = startedAt;
        Status = SimulationRunStatus.Running;
    }

    /// <summary>
    /// Completes the run successfully at the specified time.
    /// </summary>
    /// <param name="endedAt">
    /// Actual run end timestamp.
    /// </param>
    public void Complete(DateTimeOffset endedAt)
    {
        EnsureCanEnd(endedAt, expectedStatus: SimulationRunStatus.Running);
        EndedAt = endedAt;
        Status = SimulationRunStatus.Completed;
    }

    /// <summary>
    /// Marks the run as failed at the specified time.
    /// </summary>
    /// <param name="endedAt">
    /// Actual run end timestamp.
    /// </param>
    public void Fail(DateTimeOffset endedAt)
    {
        EnsureCanEnd(endedAt, expectedStatus: SimulationRunStatus.Running);
        EndedAt = endedAt;
        Status = SimulationRunStatus.Failed;
    }

    /// <summary>
    /// Cancels the run at the specified time.
    /// </summary>
    /// <param name="endedAt">
    /// Actual cancellation timestamp.
    /// </param>
    public void Cancel(DateTimeOffset endedAt)
    {
        if (endedAt == default)
        {
            throw new ArgumentException(
                "End time must be a valid, non-default timestamp.",
                nameof(endedAt));
        }

        if (Status is SimulationRunStatus.Completed or SimulationRunStatus.Failed or SimulationRunStatus.Cancelled)
        {
            throw new InvalidOperationException(
                $"Simulation run {Id} cannot be cancelled from status {Status}.");
        }

        if (StartedAt.HasValue && endedAt < StartedAt.Value)
        {
            throw new InvalidOperationException(
                "Simulation run end time cannot be earlier than the start time.");
        }

        EndedAt = endedAt;
        Status = SimulationRunStatus.Cancelled;
    }

    /// <summary>
    /// Validates common end-of-run rules for transitions that require a running state.
    /// </summary>
    /// <param name="endedAt">
    /// End timestamp being validated.
    /// </param>
    /// <param name="expectedStatus">
    /// Expected current status before ending.
    /// </param>
    private void EnsureCanEnd(DateTimeOffset endedAt, SimulationRunStatus expectedStatus)
    {
        if (endedAt == default)
        {
            throw new ArgumentException(
                "End time must be a valid, non-default timestamp.",
                nameof(endedAt));
        }

        if (Status != expectedStatus)
        {
            throw new InvalidOperationException(
                $"Simulation run {Id} cannot end from status {Status}. Expected {expectedStatus}.");
        }

        if (!StartedAt.HasValue)
        {
            throw new InvalidOperationException(
                $"Simulation run {Id} cannot end because it has not been started yet.");
        }

        if (EndedAt.HasValue)
        {
            throw new InvalidOperationException(
                $"Simulation run {Id} has already ended at {EndedAt:O}.");
        }

        if (endedAt < StartedAt.Value)
        {
            throw new InvalidOperationException(
                "Simulation run end time cannot be earlier than the start time.");
        }
    }
}