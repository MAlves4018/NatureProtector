using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NatureProtector.Infrastructure.Postgres.Persistence;

namespace NatureProtector.Prevention.Host.Tests.TestInfrastructure;

internal sealed class DuplicateInsertOnSaveInterceptor(
    DbContextOptions<NatureProtectorControlDbContext> sidecarOptions,
    Func<NatureProtectorControlDbContext, bool> shouldInject,
    Func<NatureProtectorControlDbContext, NatureProtectorControlDbContext, CancellationToken, Task> injectDuplicateAsync)
    : SaveChangesInterceptor
{
    private int _hasInjected;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not NatureProtectorControlDbContext dbContext ||
            Interlocked.Exchange(ref _hasInjected, 1) == 1 ||
            !shouldInject(dbContext))
        {
            return result;
        }

        await using var sidecarContext = new NatureProtectorControlDbContext(sidecarOptions);
        await injectDuplicateAsync(sidecarContext, dbContext, cancellationToken);
        await sidecarContext.SaveChangesAsync(cancellationToken);

        return result;
    }
}

internal sealed class ThrowingSaveChangesInterceptor(Exception exception) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        throw exception;
    }
}
