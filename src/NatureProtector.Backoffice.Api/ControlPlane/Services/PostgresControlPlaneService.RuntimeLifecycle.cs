using Microsoft.EntityFrameworkCore;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Backoffice.Api.RuntimeOrchestration;
using NatureProtector.Core.Scenarios;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Pipeline;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

public sealed partial class PostgresControlPlaneService
{
    // <phase5-slice id="runtime-lifecycle">
    public Task<RuntimeOperationResponse?> GetRuntimeOperationAsync(Guid operationId, CancellationToken cancellationToken)
        => GetAndReconcileRuntimeOperationAsync(entity => entity.OperationId == operationId, cancellationToken);

    public Task<RuntimeOperationResponse?> GetRuntimeOperationByRunAsync(Guid runId, CancellationToken cancellationToken)
        => GetAndReconcileRuntimeOperationAsync(entity => entity.SimulationRunId == runId, cancellationToken);

    public async Task<RuntimeOperationResponse?> GetCurrentRuntimeOperationAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var operationId = await dbContext.RuntimeOperations.AsNoTracking()
            .OrderByDescending(entity => entity.AcceptedAt)
            .Select(entity => (Guid?)entity.OperationId)
            .FirstOrDefaultAsync(cancellationToken);
        return operationId.HasValue ? await GetRuntimeOperationAsync(operationId.Value, cancellationToken) : null;
    }

    public Task<RuntimeOperationResponse?> GetRuntimeOperationByRequestAsync(Guid requestId, CancellationToken cancellationToken)
        => GetAndReconcileRuntimeOperationAsync(entity => entity.RequestId == requestId, cancellationToken);

    public async Task<RuntimeOperationResponse?> ReconcileRuntimeOperationWithProviderAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        var snapshot = await _runtimeRunOrchestrator.GetAsync(new RuntimeExecutionId(operationId), cancellationToken);
        if (snapshot is not null)
        {
            await ApplyRuntimeExecutionSnapshotAsync(operationId, snapshot, cancellationToken);
        }

        return await GetRuntimeOperationAsync(operationId, cancellationToken);
    }

    public async Task EnsureRuntimeEvidenceAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var operation = await GetRuntimeOperationAsync(operationId, cancellationToken);
        if (operation?.TerminalOutcome is null || operation.SimulationRunId is not Guid runId ||
            string.IsNullOrWhiteSpace(operation.EvidenceLocation))
        {
            return;
        }

        var marker = Path.Combine(operation.EvidenceLocation, "run-scoped-evidence-complete.json");
        if (File.Exists(marker))
        {
            return;
        }

        Directory.CreateDirectory(operation.EvidenceLocation);
        var run = await GetSimulationRunAsync(runId, cancellationToken);
        var audit = await GetRuntimeRunAuditAsync(runId, cancellationToken);
        var timings = await GetRuntimeRunTimingsAsync(runId, cancellationToken);
        await WriteJsonEvidenceAsync(operation.EvidenceLocation, $"run-{runId:D}.json", (object?)run ?? new { error = "run_not_found" }, cancellationToken);
        await WriteJsonEvidenceAsync(operation.EvidenceLocation, $"run-{runId:D}-audit.json", (object?)audit ?? new { error = "audit_not_found" }, cancellationToken);
        await WriteJsonEvidenceAsync(operation.EvidenceLocation, $"run-{runId:D}-timings.json", (object?)timings ?? new { error = "timings_not_found" }, cancellationToken);
        await WriteJsonEvidenceAsync(operation.EvidenceLocation, "run-scoped-evidence-complete.json", new
        {
            operationId,
            simulationRunId = runId,
            completedAtUtc = DateTimeOffset.UtcNow,
            terminalOutcome = operation.TerminalOutcome
        }, cancellationToken);
    }

    private async Task<RuntimeOperationRecord?> ReserveRuntimeOperationAsync(
        Guid requestId,
        string correlationId,
        RuntimeRunStartRequest request,
        string provider,
        RuntimeEvidenceReference? evidence,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var maintenanceLock = await RuntimeMaintenanceLock.AcquireAsync(dbContext, cancellationToken);
        var operation = new RuntimeOperationRecord
        {
            OperationId = Guid.NewGuid(),
            RequestId = requestId,
            IdempotencyKey = requestId.ToString("D"),
            Provider = provider,
            CorrelationId = correlationId,
            RequestedState = "Requested",
            ProviderState = RuntimeExecutionState.Starting.ToString(),
            RunState = "Pending",
            ProcessingState = "Pending",
            State = RuntimeExecutionState.Starting.ToString(),
            IsOperational = true,
            AcceptedAt = now,
            UpdatedAt = now,
            DeadlineAt = now.Add(CalculateRuntimeOperationDeadline(request)),
            EvidenceId = evidence?.EvidenceId,
            EvidenceLocation = evidence?.Location
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

    private async Task ApplyRuntimeLaunchReceiptAsync(
        Guid operationId,
        RuntimeLaunchReceipt receipt,
        CancellationToken cancellationToken)
        => await UpdateRuntimeProviderStateAsync(
            operationId,
            receipt.State,
            receipt.ProviderReference,
            receipt.RejectionCode,
            receipt.Message,
            cancellationToken);

    private async Task ApplyRuntimeExecutionSnapshotAsync(
        Guid operationId,
        RuntimeExecutionSnapshot snapshot,
        CancellationToken cancellationToken)
        => await UpdateRuntimeProviderStateAsync(
            operationId,
            snapshot.State,
            providerReference: null,
            snapshot.FailureCode,
            snapshot.FailureMessage,
            cancellationToken);

    private async Task UpdateRuntimeProviderStateAsync(
        Guid operationId,
        RuntimeExecutionState providerState,
        string? providerReference,
        string? failureCode,
        string? failureMessage,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var stateLock = await RuntimeOperationStateLock.AcquireAsync(dbContext, cancellationToken);
        var operation = await dbContext.RuntimeOperations
            .SingleOrDefaultAsync(entity => entity.OperationId == operationId, cancellationToken);
        if (operation is null || operation.TerminalOutcome is not null)
        {
            return;
        }

        operation.Provider = _runtimeRunOrchestrator.Provider;
        operation.ProviderState = providerState.ToString();
        operation.UpdatedAt = now;
        operation.StartedAt ??= providerState is RuntimeExecutionState.Starting or RuntimeExecutionState.Running or RuntimeExecutionState.Succeeded
            ? now
            : null;
        if (!string.IsNullOrWhiteSpace(providerReference))
        {
            operation.ProviderOperationName ??= providerReference;
        }

        switch (providerState)
        {
            case RuntimeExecutionState.Unknown:
                // Unknown is provider telemetry only. It must never regress system lifecycle state.
                break;
            case RuntimeExecutionState.Accepted:
            case RuntimeExecutionState.Starting:
            case RuntimeExecutionState.Running:
                if (RuntimeOperationStateOrder(operation.State) < RuntimeOperationStateOrder("LaunchAccepted"))
                {
                    operation.State = "LaunchAccepted";
                }
                break;
            case RuntimeExecutionState.Succeeded:
                if (RuntimeOperationStateOrder(operation.State) < RuntimeOperationStateOrder("ProducerCompleted"))
                {
                    operation.State = "ProducerCompleted";
                }
                operation.ProducerCompletedAt ??= now;
                break;
            case RuntimeExecutionState.Rejected:
            case RuntimeExecutionState.Failed:
            case RuntimeExecutionState.TimedOut:
            case RuntimeExecutionState.Cancelled:
                operation.State = providerState.ToString();
                operation.TerminalOutcome = providerState.ToString();
                operation.IsOperational = false;
                operation.FinishedAt = now;
                operation.FailureCode = failureCode ?? $"provider_{providerState.ToString().ToLowerInvariant()}";
                operation.FailureMessage = string.IsNullOrWhiteSpace(failureMessage)
                    ? $"Runtime provider entered terminal state {providerState}."
                    : CloudRunGatewayErrorPolicy.ExtractSafeProviderSummary(failureMessage);
                break;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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
        await using var stateLock = await RuntimeOperationStateLock.AcquireAsync(dbContext, cancellationToken);
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
            var run = await dbContext.SimulationRuns.AsNoTracking()
                .SingleOrDefaultAsync(candidate =>
                    candidate.OrchestratorCorrelationId == operation.CorrelationId,
                    cancellationToken);
            if (run is not null)
            {
                operation.SimulationRunId = run.Id;
                operation.RunState = "RunObserved";
                operation.State = "RunObserved";
            }
            else if (DateTimeOffset.UtcNow >= operation.DeadlineAt)
            {
                operation.State = "Orphaned";
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
        var expectedSensors = await ResolveExpectedSensorCountAsync(dbContext, run, cancellationToken);
        var expected = checked(run.NumberOfCycles * expectedSensors);
        var counts = await dbContext.InboxEvents.AsNoTracking()
            .Where(entity => entity.SimulationRunId == runId)
            .GroupBy(entity => entity.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);
        int Count(InboxEventStatus status) => counts.GetValueOrDefault(status);
        var accepted = counts.Values.Sum();
        var pending = Count(InboxEventStatus.Pending);
        var processing = Count(InboxEventStatus.Processing);
        var retry = Count(InboxEventStatus.RetryPending);
        var processed = Count(InboxEventStatus.Processed);
        var quarantined = Count(InboxEventStatus.Quarantined);
        var finalizedCycles = await dbContext.CycleSettlements.AsNoTracking()
            .CountAsync(entity => entity.SimulationRunId == runId && entity.FinalizedAt != null, cancellationToken);
        var temporalSettled = finalizedCycles == run.NumberOfCycles;
        var missingReadingsExpected = SimulationRunMetadata.ReadDegradationProfiles(run.MetadataJson)
            .Contains("missing-readings");
        var missingCoverageSettled = missingReadingsExpected && accepted <= expected;
        var expectedCoverageSettled = accepted >= expected || (temporalSettled && missingReadingsExpected) || missingCoverageSettled;
        var settled = run.Status == SimulationRunStatus.Completed && expectedCoverageSettled &&
            pending == 0 && processing == 0 && retry == 0 && processed + quarantined == accepted;
        return new RuntimeOperationAccountingResponse(expected, accepted, pending, processing, retry, processed, quarantined, settled);
    }


    private static async Task<int> ResolveExpectedSensorCountAsync(
        NatureProtector.Infrastructure.Postgres.Persistence.NatureProtectorControlDbContext dbContext,
        SimulationRunRecord run,
        CancellationToken cancellationToken)
    {
        var explicitMembership = SimulationRunMetadata.ReadExpectedSensorIds(run.MetadataJson);
        if (explicitMembership.Count > 0)
        {
            return explicitMembership.Count;
        }

        var settlementMembership = await dbContext.CycleSettlements.AsNoTracking()
            .Where(entity => entity.SimulationRunId == run.Id)
            .OrderBy(entity => entity.CycleIndex)
            .Select(entity => entity.ExpectedSensorIdsJson)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(settlementMembership))
        {
            try
            {
                var settlementCount = System.Text.Json.JsonSerializer.Deserialize<Guid[]>(settlementMembership)?
                    .Distinct()
                    .Count() ?? 0;
                if (settlementCount > 0)
                {
                    return settlementCount;
                }
            }
            catch (System.Text.Json.JsonException)
            {
            }
        }

        var metadataCount = SimulationRunMetadata.ReadExpectedSensorCount(run.MetadataJson);
        if (metadataCount.HasValue)
        {
            return metadataCount.Value;
        }

        var legacyActiveCount = await dbContext.SensorNodes.AsNoTracking()
            .CountAsync(entity => entity.AreaId == run.AreaId && entity.IsActive, cancellationToken);
        if (legacyActiveCount <= 0)
        {
            throw new InvalidOperationException(
                $"Simulation run '{run.Id:D}' has no resolvable expected sensor membership for accounting.");
        }

        return legacyActiveCount;
    }

    private static int RuntimeOperationStateOrder(string? state)
        => state switch
        {
            "Requested" or "Starting" => 0,
            "LaunchAccepted" => 1,
            "RunObserved" => 2,
            "ProducerCompleted" => 3,
            "PipelineSettling" => 4,
            "SystemCompleted" => 5,
            _ => 0
        };

    private static RuntimeOperationResponse ToRuntimeOperationResponse(
        RuntimeOperationRecord operation,
        RuntimeOperationAccountingResponse accounting)
        => new(operation.OperationId, operation.RequestId, operation.CorrelationId, operation.SimulationRunId,
            operation.RequestedState, operation.ProviderState, operation.RunState, operation.ProcessingState,
            operation.State, operation.TerminalOutcome, operation.AcceptedAt, operation.UpdatedAt,
            operation.StartedAt, operation.ProducerCompletedAt, operation.SystemCompletedAt, operation.FinishedAt,
            operation.FailureCode, operation.FailureMessage, operation.EvidenceId, operation.EvidenceLocation, accounting);
    // </phase5-slice>
}
