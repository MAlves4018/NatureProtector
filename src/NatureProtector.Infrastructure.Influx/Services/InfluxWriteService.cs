using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Options;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Infrastructure.Influx.Services;

/*
 * Este serviço escreve em InfluxDB as medições de observabilidade produzidas
 * pela pipeline de prevenção.
 *
 * Rationale:
 * - O projeto precisa de séries temporais prontas para consulta em dashboards e
 *   exploração rápida.
 * - A pipeline não deve conhecer o detalhe do cliente Influx nem o esquema das
 *   medições.
 *
 * Design considerations:
 * - Leituras aceites, avaliações de risco e snapshots agregados são escritos em
 *   medições separadas.
 * - Tags e fields foram escolhidos para suportar filtros por área, sensor e
 *   severidade sem reprocessamento.
 */

public sealed class InfluxWriteService : IInfluxWriteService, IDisposable
{
    private readonly InfluxDBClient _client;
    private readonly InfluxDbOptions _options;

    /// <summary>
    /// Inicializa o cliente de escrita em InfluxDB com a configuração resolvida.
    /// </summary>
    public InfluxWriteService(IOptions<InfluxDbOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

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

    /// <summary>
    /// Escreve em InfluxDB uma leitura aceite pela pipeline.
    /// </summary>
    public async Task WriteAcceptedReadingAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

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

        await _client
            .GetWriteApiAsync()
            .WritePointAsync(point, _options.Bucket, _options.Organization, cancellationToken);
    }

    /// <summary>
    /// Escreve em InfluxDB uma avaliação de risco individual.
    /// </summary>
    public async Task WriteRiskAssessmentAsync(
        Guid areaId,
        Guid sensorId,
        RiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assessment);

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

        await _client
            .GetWriteApiAsync()
            .WritePointAsync(point, _options.Bucket, _options.Organization, cancellationToken);
    }

    /// <summary>
    /// Escreve em InfluxDB um snapshot agregado de risco por área.
    /// </summary>
    public async Task WriteAreaRiskSnapshotAsync(
        Guid areaId,
        int assessmentCount,
        AreaRiskSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var severity = SeverityExtensions.FromRiskLevel(snapshot.AggregateRiskLevel);

        var point = PointData
            .Measurement("area_risk_snapshots")
            .Tag("area_id", areaId.ToString())
            .Tag("aggregate_risk_level", snapshot.AggregateRiskLevel.ToString())
            .Tag("severity", severity.ToString())
            .Field("aggregate_risk_score", snapshot.AggregateRiskScore)
            .Field("assessment_count", assessmentCount)
            .Timestamp(snapshot.Timestamp.UtcDateTime, WritePrecision.Ns);

        await _client
            .GetWriteApiAsync()
            .WritePointAsync(point, _options.Bucket, _options.Organization, cancellationToken);
    }

    /// <summary>
    /// Liberta o cliente InfluxDB mantido por este serviço.
    /// </summary>
    public void Dispose()
    {
        _client.Dispose();
    }
}
