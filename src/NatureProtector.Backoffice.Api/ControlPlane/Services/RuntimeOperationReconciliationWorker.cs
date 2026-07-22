using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NatureProtector.Infrastructure.Postgres.Persistence;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

/// <summary>
/// Reconciles non-terminal runtime operations independently of browser/API polling.
/// </summary>
public sealed class RuntimeOperationReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory,
    IOptions<RuntimeOperationReconciliationOptions> options,
    ILogger<RuntimeOperationReconciliationWorker> logger) : BackgroundService
{
    private readonly RuntimeOperationReconciliationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Runtime operation reconciliation worker is disabled.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Clamp(_options.IntervalSeconds, 1, 60));
        var batchSize = Math.Clamp(_options.BatchSize, 1, 500);
        logger.LogInformation(
            "Runtime operation reconciliation worker started. interval_seconds={IntervalSeconds} batch_size={BatchSize}",
            interval.TotalSeconds,
            batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileBatchAsync(batchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Runtime operation reconciliation batch failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task ReconcileBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var nonTerminalOperations = dbContext.RuntimeOperations
            .AsNoTracking()
            .Where(operation => operation.TerminalOutcome == null);

        var operationIds = string.Equals(dbContext.Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal)
            ? (await nonTerminalOperations
                .Select(operation => new { operation.OperationId, operation.UpdatedAt })
                .ToListAsync(cancellationToken))
                .OrderBy(operation => operation.UpdatedAt)
                .Take(batchSize)
                .Select(operation => operation.OperationId)
                .ToList()
            : await nonTerminalOperations
                .OrderBy(operation => operation.UpdatedAt)
                .Select(operation => operation.OperationId)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

        if (operationIds.Count == 0)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var controlPlane = scope.ServiceProvider.GetRequiredService<IControlPlaneService>();
        foreach (var operationId in operationIds)
        {
            try
            {
                _ = await controlPlane.ReconcileRuntimeOperationWithProviderAsync(operationId, cancellationToken);
                await controlPlane.EnsureRuntimeEvidenceAsync(operationId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Runtime operation reconciliation failed. operation_id={OperationId}",
                    operationId);
            }
        }
    }
}
