using System.Reflection;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class ApiContractReflectionTests
{
    [Fact]
    public void PublicApiContractTypes_DoNotExposePersistenceOrEfTypes()
    {
        var contractTypes = typeof(RuntimeSummaryResponse).Assembly
            .GetTypes()
            .Where(type => type.IsPublic && IsApiContractNamespace(type.Namespace))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        var offenders = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var contractType in contractTypes)
        {
            foreach (var constructor in contractType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    AddForbiddenTypes(
                        parameter.ParameterType,
                        $"{contractType.FullName} constructor parameter {parameter.Name}",
                        offenders);
                }
            }

            foreach (var property in contractType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                AddForbiddenTypes(
                    property.PropertyType,
                    $"{contractType.FullName}.{property.Name}",
                    offenders);
            }
        }

        Assert.Empty(offenders);
    }

    private static bool IsApiContractNamespace(string? namespaceName)
    {
        return namespaceName is not null &&
               namespaceName.StartsWith("NatureProtector.Backoffice.Api.", StringComparison.Ordinal) &&
               namespaceName.Contains(".Contracts", StringComparison.Ordinal);
    }

    private static void AddForbiddenTypes(Type type, string context, ISet<string> offenders)
    {
        foreach (var candidate in FlattenExposedTypes(type))
        {
            if (IsForbiddenContractType(candidate))
            {
                _ = offenders.Add($"{context} exposes {candidate.FullName}");
            }
        }
    }

    private static IEnumerable<Type> FlattenExposedTypes(Type type)
    {
        if (type.IsArray)
        {
            foreach (var nestedType in FlattenExposedTypes(type.GetElementType()!))
            {
                yield return nestedType;
            }

            yield break;
        }

        if (type.IsGenericType)
        {
            yield return type.GetGenericTypeDefinition();

            foreach (var genericArgument in type.GetGenericArguments())
            {
                foreach (var nestedType in FlattenExposedTypes(genericArgument))
                {
                    yield return nestedType;
                }
            }

            yield break;
        }

        yield return Nullable.GetUnderlyingType(type) ?? type;
    }

    private static bool IsForbiddenContractType(Type type)
    {
        var namespaceName = type.Namespace ?? string.Empty;
        var fullName = type.FullName ?? type.Name;

        return namespaceName.StartsWith("NatureProtector.Infrastructure.", StringComparison.Ordinal) ||
               namespaceName.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
               namespaceName.StartsWith("Npgsql", StringComparison.OrdinalIgnoreCase) ||
               fullName.Contains("DbContext", StringComparison.Ordinal);
    }
}
