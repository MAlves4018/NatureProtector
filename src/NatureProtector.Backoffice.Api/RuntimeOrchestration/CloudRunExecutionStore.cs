using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Infrastructure.Postgres.Persistence;

namespace NatureProtector.Backoffice.Api.RuntimeOrchestration;

internal sealed record CloudRunExecutionRecord(
    RuntimeExecutionId ExecutionId,
    string IdempotencyKey,
    string? ProviderOperationName,
    string? ProviderExecutionName,
    RuntimeExecutionState State,
    DateTimeOffset AcceptedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    string? FailureCode,
    string? FailureMessage,
    string LogCorrelation,
    RuntimeEvidenceReference? Evidence,
    Guid? LaunchLeaseToken,
    DateTimeOffset? LaunchLeaseUntilUtc);

internal sealed record CloudRunExecutionReservation(
    CloudRunExecutionRecord Record,
    Guid LeaseToken,
    bool OwnsLaunch,
    bool ReusedExistingExecution);

internal interface ICloudRunExecutionStore
{
    Task<CloudRunExecutionReservation> ReserveAsync(RuntimeLaunchRequest request, TimeSpan leaseDuration, CancellationToken cancellationToken);
    Task<bool> AttachOperationAsync(RuntimeExecutionId executionId, Guid leaseToken, string operationName, CancellationToken cancellationToken);
    Task<CloudRunExecutionRecord?> GetAsync(RuntimeExecutionId executionId, CancellationToken cancellationToken);
    Task UpdateAsync(CloudRunExecutionRecord record, CancellationToken cancellationToken);
}

internal sealed class PostgresCloudRunExecutionStore(
    IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory) : ICloudRunExecutionStore
{
    private const string Provider = "cloud-run-job";

    public async Task<CloudRunExecutionReservation> ReserveAsync(
        RuntimeLaunchRequest request,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var executionId = new RuntimeExecutionId(Guid.NewGuid());
        var leaseToken = Guid.NewGuid();
        var leaseUntil = now.Add(leaseDuration);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO control.runtime_orchestrator_executions (
                    execution_id, request_id, idempotency_key, provider, state,
                    requested_state, provider_state, run_state, processing_state, is_operational, deadline_at,
                    accepted_at, updated_at,
                    log_correlation, evidence_id, evidence_location, launch_lease_token, launch_lease_until)
                VALUES (@execution_id, @request_id, @idempotency_key, @provider, @state,
                    'Requested', 'Launching', 'Pending', 'Pending', TRUE, @deadline_at,
                    @accepted_at, @updated_at,
                    @log_correlation, @evidence_id, @evidence_location, @lease_token, @lease_until)
                ON CONFLICT (idempotency_key) DO NOTHING;
                """;
            Add(insert, "execution_id", executionId.Value);
            Add(insert, "request_id", request.RequestId);
            Add(insert, "idempotency_key", request.IdempotencyKey);
            Add(insert, "provider", Provider);
            Add(insert, "state", RuntimeExecutionState.Starting.ToString());
            Add(insert, "accepted_at", now);
            Add(insert, "updated_at", now);
            Add(insert, "deadline_at", now.Add(request.Timeout));
            Add(insert, "log_correlation", request.Simulation.OrchestratorCorrelationId);
            Add(insert, "evidence_id", request.Evidence?.EvidenceId);
            Add(insert, "evidence_location", request.Evidence?.Location);
            Add(insert, "lease_token", leaseToken);
            Add(insert, "lease_until", leaseUntil);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        var record = await ReadByIdempotencyKeyAsync(connection, transaction, request.IdempotencyKey, cancellationToken)
            ?? throw new InvalidOperationException("Runtime execution reservation could not be read after insert.");

        var ownsLaunch = record.ExecutionId == executionId && record.LaunchLeaseToken == leaseToken;
        if (!ownsLaunch && string.IsNullOrWhiteSpace(record.ProviderOperationName) &&
            !IsTerminal(record.State) && record.LaunchLeaseUntilUtc <= now)
        {
            await using var claim = connection.CreateCommand();
            claim.Transaction = transaction;
            claim.CommandText = """
                UPDATE control.runtime_orchestrator_executions
                SET launch_lease_token = @lease_token,
                    launch_lease_until = @lease_until,
                    updated_at = @updated_at
                WHERE execution_id = @execution_id
                  AND provider_operation_name IS NULL
                  AND (launch_lease_until IS NULL OR launch_lease_until <= @updated_at)
                  AND state NOT IN ('Succeeded', 'Failed', 'TimedOut', 'Cancelled', 'Rejected');
                """;
            Add(claim, "lease_token", leaseToken);
            Add(claim, "lease_until", leaseUntil);
            Add(claim, "updated_at", now);
            Add(claim, "execution_id", record.ExecutionId.Value);
            ownsLaunch = await claim.ExecuteNonQueryAsync(cancellationToken) == 1;
            if (ownsLaunch)
            {
                record = record with { LaunchLeaseToken = leaseToken, LaunchLeaseUntilUtc = leaseUntil, UpdatedAtUtc = now };
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new CloudRunExecutionReservation(record, leaseToken, ownsLaunch, !ownsLaunch || record.ExecutionId != executionId);
    }

    public async Task<bool> AttachOperationAsync(
        RuntimeExecutionId executionId,
        Guid leaseToken,
        string operationName,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var updated = await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE control.runtime_orchestrator_executions
            SET provider_operation_name = {operationName}, state = {RuntimeExecutionState.Running.ToString()},
                provider_state = 'LaunchAccepted',
                started_at = COALESCE(started_at, {DateTimeOffset.UtcNow}), updated_at = {DateTimeOffset.UtcNow},
                launch_lease_token = NULL, launch_lease_until = NULL
            WHERE execution_id = {executionId.Value} AND launch_lease_token = {leaseToken};
            """, cancellationToken);
        return updated == 1;
    }

    public async Task<CloudRunExecutionRecord?> GetAsync(RuntimeExecutionId executionId, CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        return await ReadAsync(connection, null, "execution_id = @value", executionId.Value, cancellationToken);
    }

    public async Task UpdateAsync(CloudRunExecutionRecord record, CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE control.runtime_orchestrator_executions SET
                provider_execution_name = @provider_execution_name,
                state = @state,
                provider_state = @provider_state,
                terminal_outcome = @terminal_outcome,
                is_operational = @is_operational,
                updated_at = @updated_at,
                started_at = @started_at,
                finished_at = @finished_at,
                failure_code = @failure_code,
                failure_message = @failure_message,
                launch_lease_token = NULL,
                launch_lease_until = NULL
            WHERE execution_id = @execution_id;
            """;
        Add(command, "provider_execution_name", record.ProviderExecutionName);
        Add(command, "state", record.State.ToString());
        Add(command, "provider_state", record.State.ToString());
        Add(command, "terminal_outcome", IsTerminal(record.State) ? record.State.ToString() : null);
        Add(command, "is_operational", !IsTerminal(record.State));
        Add(command, "updated_at", record.UpdatedAtUtc);
        Add(command, "started_at", record.StartedAtUtc);
        Add(command, "finished_at", record.FinishedAtUtc);
        Add(command, "failure_code", record.FailureCode);
        Add(command, "failure_message", record.FailureMessage);
        Add(command, "execution_id", record.ExecutionId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<CloudRunExecutionRecord?> ReadByIdempotencyKeyAsync(
        DbConnection connection,
        DbTransaction transaction,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => await ReadAsync(connection, transaction, "idempotency_key = @value", idempotencyKey, cancellationToken);

    private static async Task<CloudRunExecutionRecord?> ReadAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string predicate,
        object value,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT execution_id, idempotency_key, provider_operation_name, provider_execution_name, state,
                   accepted_at, updated_at, started_at, finished_at, failure_code, failure_message,
                   log_correlation, evidence_id, evidence_location, launch_lease_token, launch_lease_until
            FROM control.runtime_orchestrator_executions WHERE {predicate};
            """;
        Add(command, "value", value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var stateText = reader.GetString(4);
        var state = Enum.TryParse<RuntimeExecutionState>(stateText, true, out var parsed)
            ? parsed
            : RuntimeExecutionState.Unknown;
        var evidence = reader.IsDBNull(12) || reader.IsDBNull(13)
            ? null
            : new RuntimeEvidenceReference(reader.GetString(12), reader.GetString(13));

        return new CloudRunExecutionRecord(
            new RuntimeExecutionId(reader.GetGuid(0)), reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), state,
            reader.GetFieldValue<DateTimeOffset>(5), reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
            reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.GetString(11), evidence,
            reader.IsDBNull(14) ? null : reader.GetGuid(14),
            reader.IsDBNull(15) ? null : reader.GetFieldValue<DateTimeOffset>(15));
    }

    private static void Add(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static bool IsTerminal(RuntimeExecutionState state) => state is RuntimeExecutionState.Succeeded
        or RuntimeExecutionState.Failed or RuntimeExecutionState.TimedOut
        or RuntimeExecutionState.Cancelled or RuntimeExecutionState.Rejected;
}
