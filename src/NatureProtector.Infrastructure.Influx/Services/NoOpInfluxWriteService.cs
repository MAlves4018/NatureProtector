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
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Skipped InfluxDB write for measurement {Measurement} because InfluxDB is disabled.", "accepted_readings");
        return Task.CompletedTask;
    }

    public Task WriteRiskAssessmentAsync(
        Guid areaId,
        Guid sensorId,
        RiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Skipped InfluxDB write for measurement {Measurement} because InfluxDB is disabled.", "risk_assessments");
        return Task.CompletedTask;
    }

    public Task WriteAreaRiskSnapshotAsync(
        Guid areaId,
        int assessmentCount,
        AreaRiskSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Skipped InfluxDB write for measurement {Measurement} because InfluxDB is disabled.", "area_risk_snapshots");
        return Task.CompletedTask;
    }
}
