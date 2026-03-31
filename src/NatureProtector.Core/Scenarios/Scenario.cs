/*
 * This class represents a scenario definition used by the Nature Protector platform.
 *
 * Rationale:
 * - A Scenario describes a named preventive situation that can later be executed
 *   through one or more SimulationRun instances.
 * - It keeps together the semantic classification of the scenario and the numeric
 *   parameters that influence generated observations and execution behaviour.
 *
 * Design considerations:
 * - Scenario is treated as a stable definition object rather than an execution object.
 * - Runtime lifecycle information does not belong here anymore; it now belongs to
 *   SimulationRun.
 * - Parameters are encapsulated in ScenarioParameters to keep the scenario identity
 *   clean and to make configuration changes explicit.
 */

namespace NatureProtector.Core.Scenarios;

public sealed class Scenario
{
    /// <summary>
    /// Globally unique identifier of the scenario.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Human-readable name of the scenario.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Optional textual description of the scenario.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// High-level category of the scenario.
    /// </summary>
    public ScenarioCategory Category { get; }

    /// <summary>
    /// Configurable numeric parameters associated with this scenario.
    /// </summary>
    public ScenarioParameters Parameters { get; }

    /// <summary>
    /// Creates a new Scenario instance.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the scenario.
    /// </param>
    /// <param name="name">
    /// Human-readable scenario name.
    /// </param>
    /// <param name="category">
    /// High-level semantic category of the scenario.
    /// </param>
    /// <param name="parameters">
    /// Numeric parameters controlling the scenario behaviour.
    /// </param>
    /// <param name="description">
    /// Optional textual description of the scenario.
    /// </param>
    public Scenario(
        Guid id,
        string name,
        ScenarioCategory category,
        ScenarioParameters parameters,
        string? description = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Scenario identifier must not be an empty GUID.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Scenario name must not be null or whitespace.",
                nameof(name));
        }

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "Invalid scenario category value.");
        }

        Id = id;
        Name = name.Trim();
        Category = category;
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    /// <summary>
    /// Creates a new SimulationRun originating from this scenario.
    /// </summary>
    /// <returns>
    /// A new simulation run in the Defined state.
    /// </returns>
    public SimulationRun CreateRun()
    {
        return new SimulationRun(
            id: Guid.NewGuid());
    }

    /// <summary>
    /// Creates a new SimulationRun originating from this scenario, using
    /// a deterministic execution seed when reproducibility is needed.
    /// </summary>
    /// <param name="executionSeed">
    /// Optional deterministic seed controlling pseudo-random execution.
    /// </param>
    /// <returns>
    /// A new simulation run in the Defined state.
    /// </returns>
    public SimulationRun CreateRun(int executionSeed)
    {
        return new SimulationRun(
            id: Guid.NewGuid(),
            executionSeed: executionSeed);
    }

    /// <summary>
    /// Returns a short textual description of the scenario for logging and UI purposes.
    /// </summary>
    public string Describe()
    {
        return $"{Category} scenario '{Name}' (Id={Id})";
    }

    /// <summary>
    /// Returns a new Scenario instance with updated parameters.
    /// </summary>
    /// <param name="parameters">
    /// New scenario parameters.
    /// </param>
    /// <returns>
    /// A new Scenario instance preserving the current identity and metadata.
    /// </returns>
    public Scenario WithParameters(ScenarioParameters parameters)
    {
        return new Scenario(
            id: Id,
            name: Name,
            category: Category,
            parameters: parameters ?? throw new ArgumentNullException(nameof(parameters)),
            description: Description);
    }

    /// <summary>
    /// Returns a new Scenario instance with an updated category.
    /// </summary>
    /// <param name="category">
    /// New scenario category.
    /// </param>
    /// <returns>
    /// A new Scenario instance preserving the current identity and parameters.
    /// </returns>
    public Scenario WithCategory(ScenarioCategory category)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "Invalid scenario category value.");
        }

        return new Scenario(
            id: Id,
            name: Name,
            category: category,
            parameters: Parameters,
            description: Description);
    }
}