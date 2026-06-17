using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Tests.Architecture;

public sealed class PreventionArchitectureTests
{
    [Fact]
    public void PreventionAssembly_DoesNotReferenceInfrastructureApiOrPersistencePackages()
    {
        var offenders = typeof(SimpleRiskScoringService).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(IsForbiddenReference)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static bool IsForbiddenReference(string assemblyName)
    {
        return assemblyName.StartsWith("NatureProtector.Infrastructure.", StringComparison.Ordinal) ||
               assemblyName.Equals("NatureProtector.Backoffice.Api", StringComparison.Ordinal) ||
               assemblyName.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
               assemblyName.StartsWith("Npgsql", StringComparison.OrdinalIgnoreCase);
    }
}
