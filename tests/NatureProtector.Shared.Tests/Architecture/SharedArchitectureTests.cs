using NatureProtector.Shared.Messaging;

namespace NatureProtector.Shared.Tests.Architecture;

public sealed class SharedArchitectureTests
{
    [Fact]
    public void SharedAssembly_DoesNotReferenceFeatureInfrastructureApiOrPersistenceAssemblies()
    {
        var offenders = typeof(EventEnvelope<>).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(IsForbiddenReference)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static bool IsForbiddenReference(string assemblyName)
    {
        return assemblyName.Equals("NatureProtector.Core", StringComparison.Ordinal) ||
               assemblyName.StartsWith("NatureProtector.Prevention", StringComparison.Ordinal) ||
               assemblyName.StartsWith("NatureProtector.Infrastructure.", StringComparison.Ordinal) ||
               assemblyName.Equals("NatureProtector.Backoffice.Api", StringComparison.Ordinal) ||
               assemblyName.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
               assemblyName.StartsWith("Npgsql", StringComparison.OrdinalIgnoreCase);
    }
}
