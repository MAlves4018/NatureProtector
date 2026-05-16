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

        _client = new InfluxDBClient(_options.Url, _options.Token);
    }

    public async Task WriteBatchAsync(
        InfluxTelemetryBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);

        cancellationToken.ThrowIfCancellationRequested();

        if (batch.IsEmpty)
        {
            _logger.LogDebug("Skipped InfluxDB batch write because the batch was empty.");
            return;
        }

        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.influx.write.batch");
        activity?.SetTag(TelemetryTags.Outcome, "stored");
        activity?.SetTag(TelemetryTags.HasAcceptedReadings, batch.AcceptedReadingCount > 0);
        activity?.SetTag(TelemetryTags.HasRiskAssessments, batch.RiskAssessmentCount > 0);
        activity?.SetTag(TelemetryTags.HasAreaRiskSnapshots, batch.AreaRiskSnapshotCount > 0);

        var points = BuildPoints(batch);
        var tags = CreateBatchTelemetryTags(batch, "stored");
        var stopwatch = Stopwatch.StartNew();

        await _client
            .GetWriteApiAsync()
            .WritePointsAsync(points, _options.Bucket, _options.Organization, cancellationToken)
            .ConfigureAwait(false);

        stopwatch.Stop();

        PreventionHostTelemetry.InfluxBatchWriteDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
        PreventionHostTelemetry.InfluxBatchPoints.Record(points.Count, tags);

        _logger.LogInformation(
            "influx_batch_write_ms={ElapsedMs} | points={PointCount} | accepted_readings={AcceptedReadings} | risk_assessments={RiskAssessments} | area_risk_snapshots={AreaRiskSnapshots}",
            stopwatch.ElapsedMilliseconds,
            points.Count,
            batch.AcceptedReadingCount,
            batch.RiskAssessmentCount,
            batch.AreaRiskSnapshotCount);
    }

    public async Task WriteAcceptedReadingAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        await WriteBatchAsync(
            new InfluxTelemetryBatch().AddAcceptedReading(envelope),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteRiskAssessmentAsync(
        Guid areaId,
        Guid sensorId,
        RiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        await WriteBatchAsync(
            new InfluxTelemetryBatch().AddRiskAssessment(areaId, sensorId, assessment),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAreaRiskSnapshotAsync(
        Guid areaId,
        int assessmentCount,
        AreaRiskSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await WriteBatchAsync(
            new InfluxTelemetryBatch().AddAreaRiskSnapshot(areaId, assessmentCount, snapshot),
            cancellationToken).ConfigureAwait(false);
    }

    private static List<PointData> BuildPoints(InfluxTelemetryBatch batch)
    {
        var points = new List<PointData>(batch.PointCount);

        foreach (var envelope in batch.AcceptedReadings)
        {
            points.Add(PointData
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
                .Timestamp(envelope.EventTime.UtcDateTime, WritePrecision.Ns));
        }

        foreach (var write in batch.RiskAssessments)
        {
            var point = PointData
                .Measurement("risk_assessments")
                .Tag("area_id", write.AreaId.ToString())
                .Tag("sensor_id", write.SensorId.ToString())
                .Tag("risk_level", write.Assessment.RiskLevel.ToString())
                .Field("risk_score", write.Assessment.RiskScore)
                .Timestamp(write.Assessment.Timestamp.UtcDateTime, WritePrecision.Ns);

            if (!string.IsNullOrWhiteSpace(write.Assessment.ExplanationSummary))
            {
                point = point.Field("has_explanation", 1);
            }

            points.Add(point);
        }

        foreach (var write in batch.AreaRiskSnapshots)
        {
            var severity = SeverityExtensions.FromRiskLevel(write.Snapshot.AggregateRiskLevel);

            points.Add(PointData
                .Measurement("area_risk_snapshots")
                .Tag("area_id", write.AreaId.ToString())
                .Tag("aggregate_risk_level", write.Snapshot.AggregateRiskLevel.ToString())
                .Tag("severity", severity.ToString())
                .Field("aggregate_risk_score", write.Snapshot.AggregateRiskScore)
                .Field("assessment_count", write.AssessmentCount)
                .Timestamp(write.Snapshot.Timestamp.UtcDateTime, WritePrecision.Ns));
        }

        return points;
    }

    private static TagList CreateBatchTelemetryTags(InfluxTelemetryBatch batch, string outcome)
    {
        return new TagList
        {
            { TelemetryTags.Outcome, outcome },
            { TelemetryTags.HasAcceptedReadings, batch.AcceptedReadingCount > 0 ? "true" : "false" },
            { TelemetryTags.HasRiskAssessments, batch.RiskAssessmentCount > 0 ? "true" : "false" },
            { TelemetryTags.HasAreaRiskSnapshots, batch.AreaRiskSnapshotCount > 0 ? "true" : "false" }
        };
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
