namespace NatureProtector.IntegrationTests.TestInfrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DockerIntegrationCollection
{
    public const string Name = "DockerIntegration";
}
