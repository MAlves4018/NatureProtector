using NatureProtector.Core.Scenarios;
using Xunit;

namespace NatureProtector.Core.Tests.Scenarios;

/// <summary>
/// Unit tests for the Scenario definition object.
/// These tests cover constructor validation, run creation helpers
/// and immutable update helpers aligned with the current model.
/// </summary>
public class ScenarioTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var parameters = CreateParameters();

        // Act
        var scenario = new Scenario(
            id: id,
            name: "  High Risk Summer Scenario  ",
            category: ScenarioCategory.HighRisk,
            parameters: parameters,
            description: "  Main preventive configuration  ");

        // Assert
        Assert.Equal(id, scenario.Id);
        Assert.Equal("High Risk Summer Scenario", scenario.Name);
        Assert.Equal(ScenarioCategory.HighRisk, scenario.Category);
        Assert.Same(parameters, scenario.Parameters);
        Assert.Equal("Main preventive configuration", scenario.Description);
    }

    [Fact]
    public void Ctor_NormalizesWhitespaceDescription_ToNull()
    {
        // Arrange
        var parameters = CreateParameters();

        // Act
        var scenario = new Scenario(
            id: Guid.NewGuid(),
            name: "Scenario A",
            category: ScenarioCategory.Base,
            parameters: parameters,
            description: "   ");

        // Assert
        Assert.Null(scenario.Description);
    }

    [Fact]
    public void Ctor_Throws_WhenIdIsEmpty()
    {
        // Arrange
        var parameters = CreateParameters();

        // Act
        var ex = Assert.Throws<ArgumentException>(() => new Scenario(
            id: Guid.Empty,
            name: "Scenario A",
            category: ScenarioCategory.Base,
            parameters: parameters));

        // Assert
        Assert.Equal("id", ex.ParamName);
        Assert.Contains("must not be an empty GUID", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_Throws_WhenNameIsNullOrWhitespace(string? rawName)
    {
        // Arrange
        var parameters = CreateParameters();

        // Act
        var ex = Assert.Throws<ArgumentException>(() => new Scenario(
            id: Guid.NewGuid(),
            name: rawName!,
            category: ScenarioCategory.Base,
            parameters: parameters));

        // Assert
        Assert.Equal("name", ex.ParamName);
        Assert.Contains("must not be null or whitespace", ex.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenCategoryIsInvalid()
    {
        // Arrange
        var parameters = CreateParameters();
        var invalidCategory = (ScenarioCategory)999;

        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Scenario(
            id: Guid.NewGuid(),
            name: "Scenario A",
            category: invalidCategory,
            parameters: parameters));

        // Assert
        Assert.Equal("category", ex.ParamName);
        Assert.Contains("Invalid scenario category value", ex.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenParametersIsNull()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => new Scenario(
            id: Guid.NewGuid(),
            name: "Scenario A",
            category: ScenarioCategory.Base,
            parameters: null!));

        // Assert
        Assert.Equal("parameters", ex.ParamName);
    }

    [Fact]
    public void CreateRun_CreatesDefinedRun_WithoutSeed()
    {
        // Arrange
        var scenario = new Scenario(
            id: Guid.NewGuid(),
            name: "Scenario A",
            category: ScenarioCategory.Base,
            parameters: CreateParameters());

        // Act
        var run = scenario.CreateRun();

        // Assert
        Assert.NotEqual(Guid.Empty, run.Id);
        Assert.Equal(SimulationRunStatus.Defined, run.Status);
        Assert.Null(run.StartedAt);
        Assert.Null(run.EndedAt);
        Assert.Null(run.ExecutionSeed);
    }

    [Fact]
    public void CreateRun_WithSeed_CreatesDefinedRun_WithProvidedSeed()
    {
        // Arrange
        var scenario = new Scenario(
            id: Guid.NewGuid(),
            name: "Scenario A",
            category: ScenarioCategory.Exercise,
            parameters: CreateParameters());

        // Act
        var run = scenario.CreateRun(12345);

        // Assert
        Assert.NotEqual(Guid.Empty, run.Id);
        Assert.Equal(SimulationRunStatus.Defined, run.Status);
        Assert.Equal(12345, run.ExecutionSeed);
        Assert.Null(run.StartedAt);
        Assert.Null(run.EndedAt);
    }

    [Fact]
    public void Describe_ReturnsUsefulSummary()
    {
        // Arrange
        var id = Guid.NewGuid();
        var scenario = new Scenario(
            id: id,
            name: "Scenario A",
            category: ScenarioCategory.Failure,
            parameters: CreateParameters());

        // Act
        var text = scenario.Describe();

        // Assert
        Assert.Contains("Failure", text);
        Assert.Contains("Scenario A", text);
        Assert.Contains(id.ToString(), text);
    }

    [Fact]
    public void WithParameters_ReturnsNewScenario_WithUpdatedParameters()
    {
        // Arrange
        var originalParameters = CreateParameters();
        var newParameters = new ScenarioParameters(
            baseTemperature: 35.0,
            baseHumidity: 20.0,
            baseWindSpeed: 12.0,
            failureRate: 0.10,
            noiseLevel: 0.20,
            timeAcceleration: 4.0);

        var scenario = new Scenario(
            id: Guid.NewGuid(),
            name: "Scenario A",
            category: ScenarioCategory.HighRisk,
            parameters: originalParameters,
            description: "Description");

        // Act
        var updated = scenario.WithParameters(newParameters);

        // Assert
        Assert.NotSame(scenario, updated);
        Assert.Equal(scenario.Id, updated.Id);
        Assert.Equal(scenario.Name, updated.Name);
        Assert.Equal(scenario.Category, updated.Category);
        Assert.Equal(scenario.Description, updated.Description);
        Assert.Same(newParameters, updated.Parameters);

        // Original remains unchanged
        Assert.Same(originalParameters, scenario.Parameters);
    }

    [Fact]
    public void WithParameters_Throws_WhenParametersIsNull()
    {
        // Arrange
        var scenario = new Scenario(
            id: Guid.NewGuid(),
            name: "Scenario A",
            category: ScenarioCategory.Base,
            parameters: CreateParameters());

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => scenario.WithParameters(null!));

        // Assert
        Assert.Equal("parameters", ex.ParamName);
    }

    [Fact]
    public void WithCategory_ReturnsNewScenario_WithUpdatedCategory()
    {
        // Arrange
        var scenario = new Scenario(
            id: Guid.NewGuid(),
            name: "Scenario A",
            category: ScenarioCategory.Base,
            parameters: CreateParameters(),
            description: "Description");

        // Act
        var updated = scenario.WithCategory(ScenarioCategory.Exercise);

        // Assert
        Assert.NotSame(scenario, updated);
        Assert.Equal(scenario.Id, updated.Id);
        Assert.Equal(scenario.Name, updated.Name);
        Assert.Equal("Description", updated.Description);
        Assert.Same(scenario.Parameters, updated.Parameters);
        Assert.Equal(ScenarioCategory.Exercise, updated.Category);
    }

    [Fact]
    public void WithCategory_Throws_WhenCategoryIsInvalid()
    {
        // Arrange
        var scenario = new Scenario(
            id: Guid.NewGuid(),
            name: "Scenario A",
            category: ScenarioCategory.Base,
            parameters: CreateParameters());

        var invalidCategory = (ScenarioCategory)999;

        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => scenario.WithCategory(invalidCategory));

        // Assert
        Assert.Equal("category", ex.ParamName);
        Assert.Contains("Invalid scenario category value", ex.Message);
    }

    private static ScenarioParameters CreateParameters()
    {
        return new ScenarioParameters(
            baseTemperature: 28.0,
            baseHumidity: 35.0,
            baseWindSpeed: 6.0,
            failureRate: 0.05,
            noiseLevel: 0.10,
            timeAcceleration: 2.0);
    }
}