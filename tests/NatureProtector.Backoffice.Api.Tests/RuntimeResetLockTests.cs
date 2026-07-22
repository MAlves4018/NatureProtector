using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Backoffice.Api.Tests.TestInfrastructure;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RuntimeResetLockTests
{
    [Fact]
    public async Task RuntimeMaintenanceLock_FallbackProviderSerializesConcurrentAcquisition()
    {
        await using var scope = new SqliteControlDbContextScope();
        await using var firstContext = scope.CreateDbContext();
        await using var secondContext = scope.CreateDbContext();
        var first = await RuntimeMaintenanceLock.AcquireAsync(firstContext, CancellationToken.None);

        var pending = RuntimeMaintenanceLock.AcquireAsync(secondContext, CancellationToken.None);

        Assert.NotSame(pending, await Task.WhenAny(pending, Task.Delay(50)));

        await first.DisposeAsync();
        await using var second = await pending.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RuntimeMaintenanceLock_FallbackProviderHonorsCancellationWhileWaiting()
    {
        await using var scope = new SqliteControlDbContextScope();
        await using var firstContext = scope.CreateDbContext();
        await using var secondContext = scope.CreateDbContext();
        await using var first = await RuntimeMaintenanceLock.AcquireAsync(firstContext, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => RuntimeMaintenanceLock.AcquireAsync(secondContext, cancellation.Token));
    }

    [Fact]
    public async Task RuntimeOperationStateLock_FallbackProviderSerializesConcurrentAcquisition()
    {
        await using var scope = new SqliteControlDbContextScope();
        await using var firstContext = scope.CreateDbContext();
        await using var secondContext = scope.CreateDbContext();
        var first = await RuntimeOperationStateLock.AcquireAsync(firstContext, CancellationToken.None);

        var pending = RuntimeOperationStateLock.AcquireAsync(secondContext, CancellationToken.None);

        Assert.NotSame(pending, await Task.WhenAny(pending, Task.Delay(50)));

        await first.DisposeAsync();
        await using var second = await pending.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RuntimeOperationStateLock_FallbackProviderHonorsCancellationWhileWaiting()
    {
        await using var scope = new SqliteControlDbContextScope();
        await using var firstContext = scope.CreateDbContext();
        await using var secondContext = scope.CreateDbContext();
        await using var first = await RuntimeOperationStateLock.AcquireAsync(firstContext, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => RuntimeOperationStateLock.AcquireAsync(secondContext, cancellation.Token));
    }
}
