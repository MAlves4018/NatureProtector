using System.Diagnostics;
using System.Diagnostics.Metrics;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Shared.Observability;

namespace NatureProtector.Infrastructure.Influx.Services;

public sealed class InfluxWriteService : IInfluxWriteService, IDisposable
{
    private readonly InfluxDBClient _client;
    private readonly InfluxDbOptions _options;
    private readonly ILogger<InfluxWriteService> _logger;

    public InfluxWriteService(
        IOptions<InfluxDbOptions> options,
        ILogger<InfluxWriteService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.Url))
            throw new InvalidOperationException("InfluxDb:Url is required.");

        if (string.IsNullOrWhiteSpace(_options.Token))
            throw new InvalidOperationException("InfluxDb:Token is required.");

        if (string.IsNullOrWhiteSpace(_options.Organization))
            throw new InvalidOperationException("InfluxDb:Organization is required.");

        if (string.IsNullOrWhiteSpace(_options.Bucket))
            throw new InvalidOperationException("InfluxDb:Bucket is required.");

        _client = InfluxDBClientFactory.Create(_options.Url, _options.Token);
    }

    public async Task WriteAcceptedReadingAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.influx.write.accepted_reading");

        var point = PointData
            .Measurement("accepted_readings")
            .Tag("area_id", envelope.AreaId.ToString())
            .Tag("sensor_id", envelope.Payload.SensorId.ToString())
            .Tag("sensor_name", envelope.Payload.SensorName)
            .Tag("metric_type", envelope.Payload.MetricType.ToString())
            .Tag("unit", envelope.Payload.Unit.ToString())
            .Tag("operational_state", envelope.Payload.OperationalState.ToString())
            .Field("value", envelope.Payload.Value)
            .Field("latitude", envelope.Payload.Latitude)
            .Field("longitude", envelope.Payload.Longitude)
            .Timestamp(envelope.EventTime.UtcDateTime, WritePrecision.Ns);

        var stopwatch = Stopwatch.StartNew();

        await _client
            .GetWriteApiAsync()
            .WritePointAsync(point, _options.Bucket, _options.Organization, cancellationToken);

        stopwatch.Stop();
        PreventionHostTelemetry.InfluxWriteDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
        {
            { TelemetryTags.Measurement, "accepted_readings" },
            { TelemetryTags.Outcome, "stored" }
        });

        _logger.LogInformation(
            "influx_write_ms={ElapsedMs} | Measurement={Measurement} | AreaId={AreaId} | SensorId={SensorId}",
            stopwatch.ElapsedMilliseconds,
            "accepted_readings",
            envelope.AreaId,
            envelope.Payload.SensorId);
    }

    public async Task WriteRiskAssessmentAsync(
        Guid areaId,
        Guid sensorId,
        RiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.influx.write.risk_assessment");

        var point = PointData
            .Measurement("risk_assessments")
            .Tag("area_id", areaId.ToString())
            .Tag("sensor_id", sensorId.ToString())
            .Tag("risk_level", assessment.RiskLevel.ToString())
            .Field("risk_score", assessment.RiskScore)
            .Timestamp(assessment.Timestamp.UtcDateTime, WritePrecision.Ns);

        if (!string.IsNullOrWhiteSpace(assessment.ExplanationSummary))
        {
            point = point.Field("has_explanation", 1);
        }

        var stopwatch = Stopwatch.StartNew();

        await _client
            .GetWriteApiAsync()
            .WritePointAsync(point, _options.Bucket, _options.Organization, cancellationToken);

        stopwatch.Stop();
        PreventionHostTelemetry.InfluxWriteDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
        {
            { TelemetryTags.Measurement, "risk_assessments" },
            { TelemetryTags.Outcome, "stored" }
        });

        _logger.LogInformation(
            "influx_write_ms={ElapsedMs} | Measurement={Measurement} | AreaId={AreaId} | SensorId={SensorId}",
            stopwatch.ElapsedMilliseconds,
            "risk_assessments",
            areaId,
            sensorId);
    }

    public async Task WriteAreaRiskSnapshotAsync(
        Guid areaId,
        int assessmentCount,
        AreaRiskSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.influx.write.area_risk_snapshot");

        var severity = SeverityExtensions.FromRiskLevel(snapshot.AggregateRiskLevel);

        var point = PointData
            .Measurement("area_risk_snapshots")
            .Tag("area_id", areaId.ToString())
            .Tag("aggregate_risk_level", snapshot.AggregateRiskLevel.ToString())
            .Tag("severity", severity.ToString())
            .Field("aggregate_risk_score", snapshot.AggregateRiskScore)
            .Field("assessment_count", assessmentCount)
            .Timestamp(snapshot.Timestamp.UtcDateTime, WritePrecision.Ns);

        var stopwatch = Stopwatch.StartNew();

        await _client
            .GetWriteApiAsync()
            .WritePointAsync(point, _options.Bucket, _options.Organization, cancellationToken);

        stopwatch.Stop();
        PreventionHostTelemetry.InfluxWriteDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
        {
            { TelemetryTags.Measurement, "area_risk_snapshots" },
            { TelemetryTags.Outcome, "stored" }
        });

        _logger.LogInformation(
            "influx_write_ms={ElapsedMs} | Measurement={Measurement} | AreaId={AreaId} | AssessmentCount={AssessmentCount}",
            stopwatch.ElapsedMilliseconds,
            "area_risk_snapshots",
            areaId,
            assessmentCount);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
