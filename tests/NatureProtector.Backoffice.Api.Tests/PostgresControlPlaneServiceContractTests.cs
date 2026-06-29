using Microsoft.EntityFrameworkCore;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Infrastructure.Postgres.Persistence;
using System.Reflection;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class PostgresControlPlaneServiceContractTests
{
    [Fact]
    public void PublicMethodContract_MatchesInterfaceExactly()
    {
        var expected = typeof(IControlPlaneService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => !method.IsSpecialName)
            .Select(Describe)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var actual = typeof(PostgresControlPlaneService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(Describe)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AvailabilityPropertyContract_MatchesInterfaceExactly()
    {
        var expected = typeof(IControlPlaneService)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(Describe)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var actual = typeof(PostgresControlPlaneService)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(Describe)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ConstructorContract_RemainsStable()
    {
        var constructor = Assert.Single(typeof(PostgresControlPlaneService).GetConstructors());
        var parameters = constructor.GetParameters();

        Assert.Equal(3, parameters.Length);
        Assert.Equal(typeof(IDbContextFactory<NatureProtectorControlDbContext>), parameters[0].ParameterType);
        Assert.Equal(typeof(string), parameters[1].ParameterType);
        Assert.Equal(typeof(bool), parameters[2].ParameterType);
        Assert.False(parameters[0].HasDefaultValue);
        Assert.Null(parameters[1].DefaultValue);
        Assert.Equal(false, parameters[2].DefaultValue);
    }

    private static string Describe(MethodInfo method)
        => $"{method.Name}({string.Join(',', method.GetParameters().Select(parameter => parameter.ParameterType))}):{method.ReturnType}";

    private static string Describe(PropertyInfo property)
        => $"{property.Name}:{property.PropertyType}";
}
