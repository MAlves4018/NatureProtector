using NatureProtector.IntegrationTests.TestInfrastructure;

namespace NatureProtector.IntegrationTests.Flow;

[Collection(DockerIntegrationCollection.Name)]
public sealed class DockerPostgresTemporaryDatabaseCleanupTests
{
    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task TemporaryPostgresDatabase_DropsDatabase_WhenSetupFailsAfterCreation()
    {
        string? databaseName = null;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TemporaryPostgresDatabase.CreateAsync((database, _) =>
            {
                databaseName = database.DatabaseName;
                throw new InvalidOperationException("Forced setup failure after database creation.");
            }));

        Assert.Equal("Forced setup failure after database creation.", exception.Message);
        Assert.NotNull(databaseName);
        Assert.False(await TemporaryPostgresDatabase.DatabaseExistsAsync(databaseName));
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task TemporaryPostgresDatabase_DropsDatabase_WhenSetupIsCanceledAfterCreation()
    {
        using var cancellation = new CancellationTokenSource();
        string? databaseName = null;

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            TemporaryPostgresDatabase.CreateAsync(
                (database, cancellationToken) =>
                {
                    databaseName = database.DatabaseName;
                    cancellation.Cancel();
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                },
                cancellation.Token));

        Assert.NotNull(databaseName);
        Assert.False(await TemporaryPostgresDatabase.DatabaseExistsAsync(databaseName));
    }
}
