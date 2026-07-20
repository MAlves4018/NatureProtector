namespace NatureProtector.Prevention.Host.Projection;

public sealed class CycleSettlementWorker(
    PostgresCycleProjectionCoordinator coordinator,
    IAreaOperationalProjectionStore projectionStore,
    ILogger<CycleSettlementWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var finalizations = await coordinator.FinalizeCompletedRunsAsync(stoppingToken);
                foreach (var finalized in finalizations.Where(item => item.IsOperational))
                {
                    if (finalized.Snapshot is null)
                    {
                        await projectionStore.MarkUnavailableAsync(
                            finalized.AreaId,
                            DateTimeOffset.UtcNow,
                            finalized.AggregationReason ?? "NoEligibleAssessments",
                            stoppingToken,
                            finalized.SimulationRunId,
                            finalized.CycleIndex);
                        continue;
                    }

                    await projectionStore.SaveAsync(
                        finalized.AreaId, finalized.Snapshot, finalized.EligibleCount, stoppingToken,
                        finalized.SimulationRunId, finalized.CycleIndex);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Cycle settlement reconciliation failed.");
            }
        }
    }
}
