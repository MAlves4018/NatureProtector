using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Infrastructure.Influx.Services;

public sealed class NoOpInfluxWriteService : IInfluxWriteService
{
    private readonly ILogger<NoOpInfluxWriteService> _logger;

    public NoOpInfluxWriteService(
        IOptions<InfluxDbOptions> options,
        ILogger<NoOpInfluxWriteService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        var value = options.Value;
        var writes = value.Writes;

        _logger.LogInformation(
            "InfluxDB is disabled. Prevention pipeline will continue without writing telemetry to InfluxDB.");
        _logger.LogInformation("InfluxDB writes enabled: {Enabled}", value.Enabled);
        _logger.LogInformation("Fail pipeline on InfluxDB write error: {FailPipelineOnWriteError}", value.FailPipelineOnWriteError);
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

    public Task WriteBatchAsync(
        InfluxTelemetryBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug(
            "Skipped InfluxDB batch because InfluxDB is disabled. points={PointCount} | accepted_readings={AcceptedReadings} | risk_assessments={RiskAssessments} | area_risk_snapshots={AreaRiskSnapshots}",
            batch.PointCount,
            batch.AcceptedReadingCount,
            batch.RiskAssessmentCount,
            batch.AreaRiskSnapshotCount);
        return Task.CompletedTask;
    }
}
