using Microsoft.EntityFrameworkCore;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Backoffice.Api.RuntimeOrchestration;
using NatureProtector.Core.Scenarios;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Pipeline;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

public sealed partial class PostgresControlPlaneService
{
    public Task<RuntimeOperationResponse?> GetRuntimeOperationAsync(Guid operationId, CancellationToken cancellationToken)
        => GetAndReconcileRuntimeOperationAsync(entity => entity.OperationId == operationId, cancellationToken);

    public Task<RuntimeOperationResponse?> GetRuntimeOperationByRequestAsync(Guid requestId, CancellationToken cancellationToken)
        => GetAndReconcileRuntimeOperationAsync(entity => entity.RequestId == requestId, cancellationToken);

    private async Task<RuntimeOperationRecord?> ReserveRuntimeOperationAsync(
        Guid requestId,
        string correlationId,
        RuntimeRunStartRequest request,
        string? evidenceLocation,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var operation = new RuntimeOperationRecord
        {
            OperationId = Guid.NewGuid(),
            RequestId = requestId,
            IdempotencyKey = requestId.ToString("D"),
            CorrelationId = correlationId,
            RequestedState = "Requested",
            ProviderState = "Launching",
            RunState = "Pending",
            ProcessingState = "Pending",
            State = "Launching",
            IsOperational = true,
            AcceptedAt = now,
            UpdatedAt = now,
            DeadlineAt = now.AddSeconds(Math.Clamp(request.TimeoutSeconds, 5, 3600)),
            EvidenceLocation = evidenceLocation
        };
        dbContext.RuntimeOperations.Add(operation);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return operation;
        }
        catch (DbUpdateException)
        {
            // The filtered unique index is the atomic global operational admission authority.
            return null;
        }
    }

    private async Task UpdateRuntimeOperationAsync(
        Guid operationId,
        string state,
        string providerState,
        string runState,
        string processingState,
        Guid? simulationRunId,
        string? terminalOutcome,
        string? failureCode,
        string? failureDetail,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.RuntimeOperations
            .Where(entity => entity.OperationId == operationId && entity.TerminalOutcome == null &&
                (entity.SimulationRunId == null || simulationRunId == null || entity.SimulationRunId == simulationRunId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entity => entity.State, state)
                .SetProperty(entity => entity.ProviderState, providerState)
                .SetProperty(entity => entity.RunState, runState)
                .SetProperty(entity => entity.ProcessingState, processingState)
                .SetProperty(entity => entity.SimulationRunId, entity => entity.SimulationRunId ?? simulationRunId)
                .SetProperty(entity => entity.UpdatedAt, now)
                .SetProperty(entity => entity.StartedAt, entity => entity.StartedAt ?? now)
                .SetProperty(entity => entity.TerminalOutcome, terminalOutcome)
                .SetProperty(entity => entity.FinishedAt, terminalOutcome == null ? null : now)
                .SetProperty(entity => entity.IsOperational, terminalOutcome == null)
                .SetProperty(entity => entity.FailureCode, failureCode)
                .SetProperty(entity => entity.FailureMessage,
                    failureDetail == null ? null : CloudRunGatewayErrorPolicy.ExtractSafeProviderSummary(failureDetail)),
                cancellationToken);
    }

    private async Task<RuntimeOperationResponse?> GetAndReconcileRuntimeOperationAsync(
        System.Linq.Expressions.Expression<Func<RuntimeOperationRecord, bool>> predicate,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var operation = await dbContext.RuntimeOperations.SingleOrDefaultAsync(predicate, cancellationToken);
        if (operation is null) return null;

        if (operation.TerminalOutcome is null)
        {
            operation = await ReconcileRuntimeOperationAsync(dbContext, operation, cancellationToken);
        }

        var accounting = await BuildRuntimeOperationAccountingAsync(dbContext, operation, cancellationToken);
        return ToRuntimeOperationResponse(operation, accounting);
    }

    private static async Task<RuntimeOperationRecord> ReconcileRuntimeOperationAsync(
        NatureProtector.Infrastructure.Postgres.Persistence.NatureProtectorControlDbContext dbContext,
        RuntimeOperationRecord operation,
        CancellationToken cancellationToken)
    {
        if (operation.SimulationRunId is null)
        {
            var candidates = await dbContext.SimulationRuns.AsNoTracking()
                .Where(run => run.MetadataJson != null)
                .ToListAsync(cancellationToken);
            var run = candidates.SingleOrDefault(candidate =>
                candidate.CreatedAt >= operation.AcceptedAt.AddSeconds(-5) &&
                candidate.MetadataJson!.Contains(operation.CorrelationId, StringComparison.OrdinalIgnoreCase));
            if (run is not null)
            {
                operation.SimulationRunId = run.Id;
                operation.RunState = "RunObserved";
                operation.State = "RunObserved";
            }
            else if (DateTimeOffset.UtcNow >= operation.DeadlineAt)
            {
                operation.State = "Orphaned";
                operation.ProviderState = "Orphaned";
                operation.RunState = "NotObserved";
                operation.ProcessingState = "NotStarted";
                operation.TerminalOutcome = "Orphaned";
                operation.FailureCode = "run_not_observed";
                operation.FailureMessage = "No SimulationRun matched the operation correlation before its deadline.";
                operation.FinishedAt = DateTimeOffset.UtcNow;
                operation.IsOperational = false;
            }
        }

        if (operation.SimulationRunId is Guid runId)
        {
            var run = await dbContext.SimulationRuns.AsNoTracking().SingleAsync(entity => entity.Id == runId, cancellationToken);
            operation.RunState = run.Status.ToString();
            if (run.Status == SimulationRunStatus.Failed)
            {
                operation.State = "Failed";
                operation.TerminalOutcome = "Failed";
                operation.FailureCode = "producer_failed";
                operation.FailureMessage = "The correlated producer run failed.";
                operation.FinishedAt = run.EndedAt ?? DateTimeOffset.UtcNow;
                operation.IsOperational = false;
            }
            else if (run.Status == SimulationRunStatus.Completed)
            {
                operation.ProducerCompletedAt ??= run.EndedAt ?? DateTimeOffset.UtcNow;
                operation.ProviderState = "ProducerCompleted";
                operation.State = "PipelineSettling";
                operation.ProcessingState = "PipelineSettling";
                var accounting = await BuildRuntimeOperationAccountingAsync(dbContext, operation, cancellationToken);
                if (accounting.Settled)
                {
                    operation.State = "SystemCompleted";
                    operation.ProcessingState = "SystemCompleted";
                    operation.TerminalOutcome = "SystemCompleted";
                    operation.SystemCompletedAt = DateTimeOffset.UtcNow;
                    operation.FinishedAt = operation.SystemCompletedAt;
                    operation.IsOperational = false;
                }
                else if (DateTimeOffset.UtcNow >= operation.DeadlineAt)
                {
                    operation.State = "TimedOut";
                    operation.TerminalOutcome = "TimedOut";
                    operation.FailureCode = "pipeline_not_settled";
                    operation.FailureMessage = "Run-scoped pipeline accounting did not settle before the operation deadline.";
                    operation.FinishedAt = DateTimeOffset.UtcNow;
                    operation.IsOperational = false;
                }
            }
        }

        operation.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return operation;
    }

    private static async Task<RuntimeOperationAccountingResponse> BuildRuntimeOperationAccountingAsync(
        NatureProtector.Infrastructure.Postgres.Persistence.NatureProtectorControlDbContext dbContext,
        RuntimeOperationRecord operation,
        CancellationToken cancellationToken)
    {
        if (operation.SimulationRunId is not Guid runId)
            return new RuntimeOperationAccountingResponse(0, 0, 0, 0, 0, 0, 0, false);

        var run = await dbContext.SimulationRuns.AsNoTracking().SingleAsync(entity => entity.Id == runId, cancellationToken);
        var expectedSensors = await dbContext.SensorNodes.AsNoTracking()
            .CountAsync(entity => entity.AreaId == run.AreaId && entity.IsActive, cancellationToken);
        var expected = checked(run.NumberOfCycles * expectedSensors);
        var inbox = await dbContext.InboxEvents.AsNoTracking().ToListAsync(cancellationToken);
        var scoped = inbox.Where(entity => TryGetSimulationRunId(entity.PayloadJson) == runId).ToArray();
        int Count(InboxEventStatus status) => scoped.Count(entity => entity.Status == status);
        var accepted = scoped.Length;
        var pending = Count(InboxEventStatus.Pending);
        var processing = Count(InboxEventStatus.Processing);
        var retry = Count(InboxEventStatus.RetryPending);
        var processed = Count(InboxEventStatus.Processed);
        var quarantined = Count(InboxEventStatus.Quarantined);
        var settled = run.Status == SimulationRunStatus.Completed && accepted >= expected &&
            pending == 0 && processing == 0 && retry == 0 && processed + quarantined == accepted;
        return new RuntimeOperationAccountingResponse(expected, accepted, pending, processing, retry, processed, quarantined, settled);
    }

    private static RuntimeOperationResponse ToRuntimeOperationResponse(
        RuntimeOperationRecord operation,
        RuntimeOperationAccountingResponse accounting)
        => new(operation.OperationId, operation.RequestId, operation.CorrelationId, operation.SimulationRunId,
            operation.RequestedState, operation.ProviderState, operation.RunState, operation.ProcessingState,
            operation.State, operation.TerminalOutcome, operation.AcceptedAt, operation.UpdatedAt,
            operation.StartedAt, operation.ProducerCompletedAt, operation.SystemCompletedAt, operation.FinishedAt,
            operation.FailureCode, operation.FailureMessage, operation.EvidenceId, operation.EvidenceLocation, accounting);
}
