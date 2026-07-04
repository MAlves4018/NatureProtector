using NatureProtector.Backoffice.Api.Operations.Services;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class OperationCatalogTests
{
    private readonly OperationCatalog _catalog = new();

    [Fact]
    public void Catalog_ContainsOnlyUniqueClosedOperationIdentifiers()
    {
        Assert.NotEmpty(_catalog.All);
        Assert.Equal(
            _catalog.All.Count,
            _catalog.All.Select(operation => operation.OperationId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(_catalog.All, operation => operation.Inputs.Any(input => input.Name.Equals("command", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("production-plan")]
    [InlineData("production-rollback")]
    [InlineData("cloud-destroy-plan")]
    [InlineData("cloud-destroy-execute")]
    public void DangerousOperationWithoutQualifiedWorkflow_RemainsBlocked(string operationId)
    {
        var operation = Assert.IsType<OperationDefinition>(_catalog.Find(operationId));

        Assert.False(string.Equals("implemented", operation.Availability, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("NOT_PROVED", operation.EvidenceLevel, ignoreCase: true);
        Assert.True(operation.RequiresConfirmation);
        Assert.True(operation.RequiresApproval);
    }

    [Fact]
    public void StagingPlan_IsImplementedAsPlanOnlyOperation()
    {
        var operation = Assert.IsType<OperationDefinition>(_catalog.Find("staging-plan"));

        Assert.Equal("implemented", operation.Availability, ignoreCase: true);
        Assert.Equal("_deployment-operation.yml", operation.Workflow);
        Assert.Equal("PLAN staging", operation.ConfirmationPhrase);
        Assert.False(operation.RequiresApproval);
    }
}
