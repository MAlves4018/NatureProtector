using System.Diagnostics;
using Microsoft.Extensions.Options;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Simulator.Host.Publishing;
using NatureProtector.Simulator.Host.Services;

namespace NatureProtector.Simulator.Host.TemporalLoad;

public sealed class TemporalLoadRunner(
    ILogger<TemporalLoadRunner> logger,
    IOptions<TemporalLoadOptions> temporalOptions,
    SeedProvider seedProvider,
    ISimulationContextSource simulationContextSource,
    ReadingGenerationService readingGenerationService,
    ISimulationRunStore simulationRunStore,
    IReadingPublisher readingPublisher,
    ISimulatorProcessExitCode processExitCode,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    private readonly TemporalLoadOptions _options = temporalOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ExecuteTemporalLoadAsync(stoppingToken);
        }
        finally
        {
            logger.LogInformation("Temporal load runner finished. Stopping Simulator.Host process.");
            applicationLifetime.StopApplication();
        }
    }

    private async Task ExecuteTemporalLoadAsync(CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.PublisherTimeoutSeconds)));

        var workload = TemporalWorkloadLoader.Load(_options.WorkloadPath!, _options.WorkloadId!);
        var schedule = TemporalLoadScheduler.Build(workload);
        var context = await simulationContextSource.CreateAsync(timeout.Token);
        var seed = seedProvider.ResolveSeed(_options.Seed ?? workload.Seed ?? context.PreferredSeed);
        var random = seedProvider.CreateRandom(seed);
        var sensors = context.Sensors.OrderBy(sensor => sensor.Name, StringComparer.Ordinal).ToArray();
        var run = context.Scenario.CreateRun(seed);
        run.MarkReady();
        await simulationRunStore.UpsertAsync(context, run, timeout.Token);
        run.Start(DateTimeOffset.UtcNow);
        await simulationRunStore.UpsertAsync(context, run, timeout.Token);

        var runRoot = ResolveRunRoot(workload.Id, run.Id);
        Directory.CreateDirectory(runRoot);
        TemporalWorkloadLoader.WriteJson(Path.Combine(runRoot, "identity.json"), new
        {
            schemaVersion = 1,
            simulationRunId = run.Id,
            workloadId = workload.Id,
            _options.RunLabel,
            _options.Topology,
            _options.Repetition,
            seed,
            startedAtUtc = run.StartedAt
        });
        TemporalWorkloadLoader.WriteJson(Path.Combine(runRoot, "workload.json"), workload);
        TemporalWorkloadLoader.WriteJson(Path.Combine(runRoot, "configuration.json"), new
        {
            schemaVersion = 1,
            context.AreaId,
            context.ScenarioCode,
            context.Scenario.Name,
            context.ConfigurationVersionId,
            sensorCount = sensors.Length,
            context.StartTimestamp,
            maxCatchUpBurst = _options.MaxCatchUpBurst
        });

        var eventRows = new List<TemporalPublishedEventRow>();
        var stopwatch = Stopwatch.StartNew();
        var publishWindow = Stopwatch.StartNew();
        DateTimeOffset? firstConfirmed = null;
        DateTimeOffset? lastConfirmed = null;
        var consecutiveCatchUp = 0;
        var nominalRegenerationCount = 0;

        try
        {
            foreach (var entry in schedule.Entries)
            {
                var due = entry.DueOffset;
                var remaining = due - stopwatch.Elapsed;
                if (remaining > TimeSpan.Zero)
                {
                    consecutiveCatchUp = 0;
                    await Task.Delay(remaining, timeout.Token);
                }
                else
                {
                    consecutiveCatchUp++;
                    if (consecutiveCatchUp > _options.MaxCatchUpBurst)
                    {
                        var pause = TimeSpan.FromMilliseconds(Math.Min(100, Math.Abs(remaining.TotalMilliseconds)));
                        await Task.Delay(pause, timeout.Token);
                        consecutiveCatchUp = 0;
                    }
                }

                var sensor = sensors[entry.EventIndex % sensors.Length];
                var scheduledAt = context.StartTimestamp + entry.DueOffset;
                var publishStartedAt = DateTimeOffset.UtcNow;
                var before = stopwatch.Elapsed;
                var generated = GenerateTemporalReading(
                    readingGenerationService,
                    context,
                    run.Id,
                    sensor,
                    entry.EventIndex,
                    scheduledAt,
                    random);
                nominalRegenerationCount += generated.RegenerationCount;
                var envelope = generated.Envelope;

                await readingPublisher.PublishAsync(envelope, timeout.Token);
                var confirmedAt = DateTimeOffset.UtcNow;
                firstConfirmed ??= confirmedAt;
                lastConfirmed = confirmedAt;

                eventRows.Add(new TemporalPublishedEventRow(
                    event_index: entry.EventIndex,
                    simulation_run_id: run.Id,
                    event_id: envelope.EventId,
                    segment_id: entry.SegmentId,
                    segment_kind: entry.SegmentKind,
                    sensor_id: sensor.Id,
                    grid_cell_id: sensor.Location.CellId ?? string.Empty,
                    cycle_index: envelope.Payload.CycleIndex ?? entry.EventIndex,
                    requested_rate: entry.RequestedRate,
                    due_offset_ms: entry.DueOffset.TotalMilliseconds,
                    scheduler_elapsed_ms: before.TotalMilliseconds,
                    schedule_delay_ms: Math.Max(0, (before - due).TotalMilliseconds),
                    event_time_utc: envelope.EventTime.UtcDateTime.ToString("O"),
                    publish_started_utc: publishStartedAt.UtcDateTime.ToString("O"),
                    confirmed_utc: confirmedAt.UtcDateTime.ToString("O")));
            }

            publishWindow.Stop();
            run.Complete(DateTimeOffset.UtcNow);
            await simulationRunStore.UpsertAsync(context, run, CancellationToken.None);
            WriteRunArtifacts(runRoot, workload, schedule, eventRows, publishWindow.Elapsed, firstConfirmed, lastConfirmed, "completed", nominalRegenerationCount);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            run.Cancel(DateTimeOffset.UtcNow);
            await simulationRunStore.UpsertAsync(context, run, CancellationToken.None);
            WriteRunArtifacts(runRoot, workload, schedule, eventRows, publishWindow.Elapsed, firstConfirmed, lastConfirmed, "cancelled", nominalRegenerationCount);
            throw;
        }
        catch (Exception)
        {
            processExitCode.MarkFailure();
            if (run.Status == SimulationRunStatus.Running)
            {
                run.Fail(DateTimeOffset.UtcNow);
                await simulationRunStore.UpsertAsync(context, run, CancellationToken.None);
            }

            WriteRunArtifacts(runRoot, workload, schedule, eventRows, publishWindow.Elapsed, firstConfirmed, lastConfirmed, "failed", nominalRegenerationCount);
            throw;
        }
    }

    private TemporalGeneratedReading GenerateTemporalReading(
        ReadingGenerationService readingGenerationService,
        SimulationContext context,
        Guid simulationRunId,
        Sensor sensor,
        int eventIndex,
        DateTimeOffset scheduledAt,
        Random random)
    {
        for (var attempt = 1; attempt <= _options.MaxNominalGenerationAttempts; attempt++)
        {
            var envelope = readingGenerationService.GenerateReading(
                context,
                simulationRunId,
                sensor,
                eventIndex,
                scheduledAt,
                random);

            if (!_options.RequireNominalEvents ||
                envelope.Payload.OperationalState == SensorOperationalState.Nominal)
            {
                return new TemporalGeneratedReading(envelope, attempt - 1);
            }
        }

        throw new InvalidOperationException(
            $"Temporal load could not generate a nominal reading for event index {eventIndex} " +
            $"after {_options.MaxNominalGenerationAttempts} attempt(s).");
    }

    private void WriteRunArtifacts(
        string runRoot,
        TemporalWorkloadDefinition workload,
        TemporalLoadSchedule schedule,
        IReadOnlyList<TemporalPublishedEventRow> eventRows,
        TimeSpan publishWindow,
        DateTimeOffset? firstConfirmed,
        DateTimeOffset? lastConfirmed,
        string terminalState,
        int nominalRegenerationCount)
    {
        WriteCsv(Path.Combine(runRoot, "events.csv"), eventRows);
        var requestedRate = schedule.Entries.Count / Math.Max(0.001, schedule.ActiveDuration.TotalSeconds);
        var intervals = eventRows
            .Zip(eventRows.Skip(1), (left, right) =>
                (DateTimeOffset.Parse(right.confirmed_utc) - DateTimeOffset.Parse(left.confirmed_utc)).TotalMilliseconds)
            .ToArray();
        var delays = eventRows.Select(row => row.schedule_delay_ms).ToArray();
        var confirmedWindow = firstConfirmed.HasValue && lastConfirmed.HasValue
            ? lastConfirmed.Value - firstConfirmed.Value
            : publishWindow;
        var precision = TemporalLoadScheduler.CalculatePrecision(
            requestedRate,
            schedule.Entries.Count,
            eventRows.Count,
            schedule.ActiveDuration,
            intervals,
            delays);
        var summary = new
        {
            schemaVersion = 1,
            workloadId = workload.Id,
            terminalState,
            requestedRateEventsPerSecond = precision.RequestedRate,
            scheduledEventCount = precision.ScheduledCount,
            actualPublishedCount = eventRows.Count,
            publisherConfirmedCount = precision.ConfirmedCount,
            actualPublishRateEventsPerSecond = precision.ActualPublishRate,
            rateAbsoluteErrorEventsPerSecond = precision.AbsoluteError,
            ratePercentError = precision.PercentError,
            rateWithinFivePercent = precision.WithinFivePercent,
            jitterMs = precision.JitterMs,
            accumulatedDelayMs = precision.AccumulatedDelayMs,
            unableToKeepUpSamples = eventRows.Count(row => row.schedule_delay_ms > 0),
            nominalRegenerationCount,
            activeDurationSeconds = schedule.ActiveDuration.TotalSeconds,
            publishWindowSeconds = schedule.ActiveDuration.TotalSeconds,
            confirmationWindowSeconds = confirmedWindow.TotalSeconds,
            completedAtUtc = DateTimeOffset.UtcNow
        };
        TemporalWorkloadLoader.WriteJson(Path.Combine(runRoot, "summary.json"), summary);
        TemporalWorkloadLoader.WriteJson(Path.Combine(runRoot, "receipt.json"), new
        {
            schemaVersion = 1,
            status = terminalState == "completed" ? "PASS" : "FAIL",
            terminalState,
            rawEvents = "events.csv",
            summary = "summary.json"
        });
    }

    private string ResolveRunRoot(string workloadId, Guid simulationRunId)
    {
        var safeWorkload = string.Join("-", workloadId.Split(Path.GetInvalidFileNameChars()));
        var safeLabel = string.Join("-", _options.RunLabel.Split(Path.GetInvalidFileNameChars()));
        return Path.GetFullPath(Path.Combine(
            _options.OutputRoot,
            safeWorkload,
            _options.Topology,
            $"r{_options.Repetition}",
            $"{safeLabel}-{simulationRunId:N}"));
    }

    private static void WriteCsv<T>(string path, IReadOnlyList<T> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var properties = typeof(T).GetProperties();
        using var writer = new StreamWriter(path);
        writer.WriteLine(string.Join(",", properties.Select(property => property.Name)));
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(",", properties.Select(property =>
                EscapeCsv(Convert.ToString(property.GetValue(row), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty))));
        }
    }

    private static string EscapeCsv(string value)
    {
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}

public sealed record TemporalPublishedEventRow(
    int event_index,
    Guid simulation_run_id,
    Guid event_id,
    string segment_id,
    string segment_kind,
    Guid sensor_id,
    string grid_cell_id,
    int cycle_index,
    double requested_rate,
    double due_offset_ms,
    double scheduler_elapsed_ms,
    double schedule_delay_ms,
    string event_time_utc,
    string publish_started_utc,
    string confirmed_utc);

public sealed record TemporalGeneratedReading(
    EventEnvelope<SensorReadingProducedPayload> Envelope,
    int RegenerationCount);
