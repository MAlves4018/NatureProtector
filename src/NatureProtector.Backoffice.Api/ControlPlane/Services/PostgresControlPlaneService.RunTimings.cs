using Microsoft.EntityFrameworkCore;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Core.Scenarios;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Shared.Observability;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

// Feature slice: RunTimings. Public behavior remains exposed through IControlPlaneService.

public sealed partial class PostgresControlPlaneService : IControlPlaneService
{
    // <phase5-slice id="run-timings">
    public async Task<RuntimeRunTimingSummaryResponse?> GetRuntimeRunTimingsAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.SimulationRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        var inboxEvents = await dbContext.InboxEvents
            .Include(entity => entity.Attempts)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var runInboxEvents = inboxEvents
            .Where(entity =>
                TryGetSimulationRunId(entity.PayloadJson) == runId ||
                TryGetSimulationRunId(entity.EnvelopeJson) == runId)
            .ToArray();
        var attempts = runInboxEvents
            .SelectMany(entity => entity.Attempts)
            .OrderBy(entity => entity.StartedAt)
            .ToArray();

        var riskAssessments = await dbContext.RiskAssessmentLogs
            .AsNoTracking()
            .Where(entity => entity.SimulationRunId == runId)
            .ToListAsync(cancellationToken);

        var alerts = await dbContext.AlertStates
            .AsNoTracking()
            .Where(entity => entity.AreaOperationalState!.SimulationRunId == runId)
            .ToListAsync(cancellationToken);

        var limitations = new List<string>
        {
            "Logger stopwatch timings are emitted in logs but are not structurally associated with SimulationRunId yet."
        };
        if (runInboxEvents.Length == 0)
        {
            limitations.Add("No pipeline.event_inbox rows were associated with this SimulationRunId.");
        }

        if (attempts.Length == 0)
        {
            limitations.Add("No pipeline.processing_attempts rows were associated with this SimulationRunId.");
        }

        if (riskAssessments.Count == 0)
        {
            limitations.Add("No projection.risk_assessment_log rows were associated with this SimulationRunId.");
        }

        if (alerts.Count == 0)
        {
            limitations.Add("No projection.alert_state rows were associated with this SimulationRunId.");
        }

        DateTimeOffset? firstInboxReceivedAt = runInboxEvents.Length == 0 ? null : runInboxEvents.Min(entity => entity.ReceivedAt);
        DateTimeOffset? firstProcessingAttemptStartedAt = attempts.Length == 0 ? null : attempts.Min(entity => entity.StartedAt);
        var lastProcessingAttemptFinishedAt = MaxFinishedAt(attempts);
        DateTimeOffset? firstRiskAssessmentCreatedAt = riskAssessments.Count == 0 ? null : riskAssessments.Min(entity => entity.CreatedAt);
        DateTimeOffset? firstAlertTriggeredAt = alerts.Count == 0 ? null : alerts.Min(entity => entity.TriggeredAt);
        var attemptDurations = attempts
            .Select(CalculateDurationMilliseconds)
            .Where(duration => duration.HasValue)
            .Select(duration => duration!.Value)
            .ToArray();

        var stages = attempts
            .GroupBy(entity => new
            {
                Stage = string.IsNullOrWhiteSpace(entity.Stage) ? "Unknown" : entity.Stage,
                Outcome = entity.Outcome.ToString(),
                entity.ErrorCode
            })
            .OrderBy(group => group.Key.Stage, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Outcome, StringComparer.Ordinal)
            .ThenBy(group => group.Key.ErrorCode, StringComparer.Ordinal)
            .Select(group =>
            {
                var rows = group.ToArray();
                var durations = rows
                    .Select(CalculateDurationMilliseconds)
                    .Where(duration => duration.HasValue)
                    .Select(duration => duration!.Value)
                    .ToArray();

                return new RuntimeStageTimingSummaryResponse(
                    group.Key.Stage,
                    group.Key.Outcome,
                    group.Key.ErrorCode,
                    rows.Length,
                    rows.Min(entity => entity.StartedAt),
                    MaxFinishedAt(rows),
                    durations.Length == 0 ? null : durations.Min(),
                    durations.Length == 0 ? null : durations.Average(),
                    durations.Length == 0 ? null : durations.Max());
            })
            .ToArray();
        var timeline = BuildRunTimeline(
            run.Id,
            run.CreatedAt,
            run.StartedAt,
            run.EndedAt,
            firstInboxReceivedAt,
            firstProcessingAttemptStartedAt,
            lastProcessingAttemptFinishedAt,
            firstRiskAssessmentCreatedAt,
            firstAlertTriggeredAt);

        return new RuntimeRunTimingSummaryResponse(
            run.Id,
            CalculateDurationMilliseconds(run.StartedAt, run.EndedAt),
            run.StartedAt,
            run.EndedAt,
            firstInboxReceivedAt,
            firstProcessingAttemptStartedAt,
            lastProcessingAttemptFinishedAt,
            firstRiskAssessmentCreatedAt,
            firstAlertTriggeredAt,
            CalculateDurationMilliseconds(run.StartedAt, firstInboxReceivedAt),
            CalculateDurationMilliseconds(run.StartedAt, firstProcessingAttemptStartedAt),
            CalculateDurationMilliseconds(run.StartedAt, firstRiskAssessmentCreatedAt),
            CalculateDurationMilliseconds(run.StartedAt, firstAlertTriggeredAt),
            new RuntimeAttemptTimingSummaryResponse(
                attempts.Length,
                attempts.Count(entity => entity.Outcome == ProcessingAttemptOutcome.Succeeded),
                attempts.Count(entity =>
                    entity.Outcome == ProcessingAttemptOutcome.Failed ||
                    entity.Outcome == ProcessingAttemptOutcome.RetryScheduled),
                attempts.Count(entity => entity.Outcome == ProcessingAttemptOutcome.Quarantined),
                attemptDurations.Length == 0 ? null : attemptDurations.Min(),
                attemptDurations.Length == 0 ? null : attemptDurations.Average(),
                attemptDurations.Length == 0 ? null : attemptDurations.Max()),
            stages,
            limitations,
            BuildRunDataScope(
                runId,
                run.Id,
                run.Id,
                "PostgreSQL runtime tables",
                "simulation-run timings"),
            timeline);
    }
    // </phase5-slice>

}
