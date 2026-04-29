using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Infrastructure.Influx.Services;

public sealed class SafeInfluxWriteService : IInfluxWriteService
{
    private readonly Func<IInfluxWriteService> _innerFactory;
    private readonly InfluxDbOptions _options;
    private readonly ILogger<SafeInfluxWriteService> _logger;

    public SafeInfluxWriteService(
        Func<IInfluxWriteService> innerFactory,
        IOptions<InfluxDbOptions> options,
        ILogger<SafeInfluxWriteService> logger)
    {
        ArgumentNullException.ThrowIfNull(innerFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _innerFactory = innerFactory;
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        var writes = _options.Writes;

        _logger.LogInformation("InfluxDB writes enabled: {Enabled}", _options.Enabled);
        _logger.LogInformation("Fail pipeline on InfluxDB write error: {FailPipelineOnWriteError}", _options.FailPipelineOnWriteError);
        _logger.LogInformation("Write accepted_readings: {AcceptedReadings}", writes.AcceptedReadings);
        _logger.LogInformation("Write risk_assessments: {RiskAssessments}", writes.RiskAssessments);
        _logger.LogInformation("Write area_risk_snapshots: {AreaRiskSnapshots}", writes.AreaRiskSnapshots);
    }

    public Task WriteAcceptedReadingAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return WriteBatchAsync(
            new InfluxTelemetryBatch().AddAcceptedReading(envelope),
            cancellationToken);
    }

    public Task WriteRiskAssessmentAsync(
        Guid areaId,
        Guid sensorId,
        RiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        return WriteBatchAsync(
            new InfluxTelemetryBatch().AddRiskAssessment(areaId, sensorId, assessment),
            cancellationToken);
    }

    public Task WriteAreaRiskSnapshotAsync(
        Guid areaId,
        int assessmentCount,
        AreaRiskSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return WriteBatchAsync(
            new InfluxTelemetryBatch().AddAreaRiskSnapshot(areaId, assessmentCount, snapshot),
            cancellationToken);
    }

    public async Task WriteBatchAsync(
        InfluxTelemetryBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var filteredBatch = batch.CloneFiltered(
            includeAcceptedReadings: _options.Writes.AcceptedReadings,
            includeRiskAssessments: _options.Writes.RiskAssessments,
            includeAreaRiskSnapshots: _options.Writes.AreaRiskSnapshots);

        await ExecuteAsync(
            filteredBatch,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteAsync(
        InfluxTelemetryBatch batch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (batch.IsEmpty)
        {
            _logger.LogDebug("Skipped InfluxDB batch write because all measurements in the batch are disabled or absent.");
            return;
        }

        try
        {
            await _innerFactory().WriteBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!_options.FailPipelineOnWriteError && ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to write InfluxDB batch. Continuing pipeline because observability writes are configured as non-critical.");
        }
    }
}
