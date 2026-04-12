using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

/*
 * Este serviço gera leituras simuladas plausíveis para o ciclo atual.
 *
 * Rationale:
 * - A geração de leituras não deve ficar misturada com a orquestração nem com
 *   a publicação.
 * - Este isolamento torna a pipeline de simulação mais testável e mais simples
 *   de evoluir.
 *
 * Design considerations:
 * - Os valores são gerados a partir da baseline do cenário e de ruído
 *   pseudoaleatório limitado.
 * - O tipo de sensor determina tanto a métrica emitida como a unidade.
 * - A implementação está alinhada com os contratos de leitura hoje existentes,
 *   sem assumir métricas que ainda não estão modeladas.
 * - Nesta fase, apenas Temperature, Humidity e Wind são publicados como
 *   leituras; sensores compostos continuam fora de âmbito.
 */

namespace NatureProtector.Simulator.Host.Services;

public sealed class ReadingGenerationService
{
    private const string ProducerName = "NatureProtector.Simulator.Host";
    private const string SchemaVersion = "1.0";

    /// <summary>
    /// Gera um lote de leituras, uma por sensor configurado no contexto.
    /// </summary>
    /// <param name="context">
    /// Contexto de simulação em memória da execução atual.
    /// </param>
    /// <param name="simulationRunId">
    /// Identificador da execução de simulação atual.
    /// </param>
    /// <param name="cycleIndex">
    /// Índice do ciclo usado para introduzir evolução temporal nos valores.
    /// </param>
    /// <param name="eventTime">
    /// Timestamp lógico associado a este ciclo.
    /// </param>
    /// <param name="random">
    /// Gerador pseudoaleatório criado a partir da seed resolvida.
    /// </param>
    /// <returns>
    /// Coleção de envelopes prontos a publicar.
    /// </returns>
    public IReadOnlyCollection<EventEnvelope<SensorReadingProducedPayload>> GenerateBatch(
        SimulationContext context,
        Guid simulationRunId,
        int cycleIndex,
        DateTimeOffset eventTime,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(random);

        var envelopes = new List<EventEnvelope<SensorReadingProducedPayload>>();

        foreach (var sensor in context.Sensors)
        {
            var envelope = GenerateReading(
                context,
                simulationRunId,
                sensor,
                cycleIndex,
                eventTime,
                random);

            envelopes.Add(envelope);
        }

        return envelopes;
    }

    /// <summary>
    /// Gera um envelope de leitura simulada para um sensor concreto.
    /// </summary>
    /// <param name="context">
    /// Contexto de simulação em memória.
    /// </param>
    /// <param name="simulationRunId">
    /// Identificador da execução de simulação.
    /// </param>
    /// <param name="sensor">
    /// Sensor para o qual a leitura é gerada.
    /// </param>
    /// <param name="cycleIndex">
    /// Índice do ciclo atual.
    /// </param>
    /// <param name="eventTime">
    /// Timestamp lógico associado à leitura.
    /// </param>
    /// <param name="random">
    /// Gerador pseudoaleatório determinístico.
    /// </param>
    /// <returns>
    /// Envelope de evento totalmente construído e pronto a publicar.
    /// </returns>
    public EventEnvelope<SensorReadingProducedPayload> GenerateReading(
        SimulationContext context,
        Guid simulationRunId,
        Sensor sensor,
        int cycleIndex,
        DateTimeOffset eventTime,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sensor);
        ArgumentNullException.ThrowIfNull(random);

        var failureRate = context.Scenario.Parameters.FailureRate;
        var isAvailable = sensor.IsActive && random.NextDouble() >= failureRate;

        var metricType = ResolveMetricType(sensor.Type);
        var unit = ResolveMeasurementUnit(sensor.Type);

        double value;

        if (!isAvailable)
        {
            // Na implementação atual, sensores indisponíveis continuam a emitir
            // um evento marcado como inválido para exercitar a pipeline.
            value = 0.0;
        }
        else
        {
            value = GenerateMetricValue(
                context,
                sensor,
                cycleIndex,
                random);
        }

        var payload = new SensorReadingProducedPayload(
            SimulationRunId: simulationRunId,
            SensorId: sensor.Id,
            SensorName: sensor.Name,
            MetricType: metricType,
            Unit: unit,
            Value: value,
            Latitude: sensor.Location.Latitude,
            Longitude: sensor.Location.Longitude,
            OperationalState: isAvailable
                ? SensorOperationalState.Nominal
                : SensorOperationalState.Invalid);

        var correlationId = $"{simulationRunId:N}-{cycleIndex:D4}-{sensor.Id:N}";

        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: SchemaVersion,
            EventId: Guid.NewGuid(),
            CorrelationId: correlationId,
            Producer: ProducerName,
            EventType: EventTypes.SensorReadingProduced,
            AreaId: context.AreaId,
            EventTime: eventTime,
            IngestTime: null,
            Payload: payload);
    }

    /// <summary>
    /// Gera um valor plausível a partir da baseline do cenário e do tipo de
    /// sensor.
    /// </summary>
    /// <param name="context">
    /// Contexto de simulação com os parâmetros base do cenário.
    /// </param>
    /// <param name="sensor">
    /// Sensor cujo tipo determina a lógica de geração.
    /// </param>
    /// <param name="cycleIndex">
    /// Índice do ciclo usado para introduzir variação temporal suave.
    /// </param>
    /// <param name="random">
    /// Gerador pseudoaleatório determinístico.
    /// </param>
    /// <returns>
    /// Valor numérico gerado para a métrica do sensor.
    /// </returns>
    private static double GenerateMetricValue(
        SimulationContext context,
        Sensor sensor,
        int cycleIndex,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sensor);
        ArgumentNullException.ThrowIfNull(random);

        var parameters = context.Scenario.Parameters;

        var baseTemperature = RequireValue(
            parameters.BaseTemperature,
            nameof(parameters.BaseTemperature));

        var baseHumidity = RequireValue(
            parameters.BaseHumidity,
            nameof(parameters.BaseHumidity));

        var baseWindSpeed = RequireValue(
            parameters.BaseWindSpeed,
            nameof(parameters.BaseWindSpeed));

        var temporalWave = Math.Sin(cycleIndex / 3.0);
        var profileNoise = sensor.Profile.NoiseLevel;
        var scenarioNoise = parameters.NoiseLevel;
        var totalNoise = profileNoise + scenarioNoise;

        return sensor.Type switch
        {
            SensorType.Temperature => Clamp(
                baseTemperature
                + (temporalWave * 1.5)
                + NextCenteredNoise(random, totalNoise, amplitude: 2.0),
                min: -20.0,
                max: 60.0),

            SensorType.Humidity => Clamp(
                baseHumidity
                - (temporalWave * 4.0)
                + NextCenteredNoise(random, totalNoise, amplitude: 5.0),
                min: 0.0,
                max: 100.0),

            SensorType.Wind => Clamp(
                baseWindSpeed
                + Math.Abs(temporalWave * 1.8)
                + NextCenteredNoise(random, totalNoise, amplitude: 1.5),
                min: 0.0,
                max: 35.0),

            SensorType.Composite => throw new InvalidOperationException(
                "Composite sensors are not yet supported by the current shared reading contracts. " +
                "Use Temperature, Humidity or Wind sensors in the simulator configuration for Day 4."),

            _ => throw new InvalidOperationException(
                $"Sensor type '{sensor.Type}' is not supported by the simulator.")
        };
    }

    /// <summary>
    /// Mapeia um <see cref="SensorType" /> para a métrica do contrato de
    /// eventos.
    /// </summary>
    /// <param name="sensorType">
    /// Tipo de sensor proveniente do domínio Core.
    /// </param>
    /// <returns>
    /// Tipo de métrica usado no payload publicado.
    /// </returns>
    private static SensorMetricType ResolveMetricType(SensorType sensorType)
    {
        return sensorType switch
        {
            SensorType.Temperature => SensorMetricType.Temperature,
            SensorType.Humidity => SensorMetricType.Humidity,
            SensorType.Wind => SensorMetricType.WindSpeed,
            SensorType.Composite => throw new InvalidOperationException(
                "Composite sensors do not yet have a matching metric type in the shared contracts."),
            _ => throw new InvalidOperationException(
                $"Sensor type '{sensorType}' cannot be mapped to a metric type.")
        };
    }

    /// <summary>
    /// Mapeia um <see cref="SensorType" /> para a unidade de medição
    /// correspondente.
    /// </summary>
    /// <param name="sensorType">
    /// Tipo de sensor proveniente do domínio Core.
    /// </param>
    /// <returns>
    /// Unidade usada no payload publicado.
    /// </returns>
    private static MeasurementUnit ResolveMeasurementUnit(SensorType sensorType)
    {
        return sensorType switch
        {
            SensorType.Temperature => MeasurementUnit.Celsius,
            SensorType.Humidity => MeasurementUnit.Percent,
            SensorType.Wind => MeasurementUnit.MetersPerSecond,
            SensorType.Composite => throw new InvalidOperationException(
                "Composite sensors do not yet have a matching measurement unit in the shared contracts."),
            _ => throw new InvalidOperationException(
                $"Sensor type '{sensorType}' cannot be mapped to a measurement unit.")
        };
    }

    /// <summary>
    /// Garante que um parâmetro numérico opcional do cenário está presente.
    /// </summary>
    /// <param name="value">
    /// Valor opcional a validar.
    /// </param>
    /// <param name="name">
    /// Nome do parâmetro usado na mensagem de erro.
    /// </param>
    /// <returns>
    /// Valor numérico não nulo.
    /// </returns>
    private static double RequireValue(double? value, string name)
    {
        return value ?? throw new InvalidOperationException(
            $"Scenario parameter '{name}' must have a value for simulation reading generation.");
    }

    /// <summary>
    /// Gera ruído limitado centrado em zero.
    /// </summary>
    /// <param name="random">
    /// Gerador pseudoaleatório.
    /// </param>
    /// <param name="noiseLevel">
    /// Fator de intensidade do ruído.
    /// </param>
    /// <param name="amplitude">
    /// Impacto absoluto máximo do ruído.
    /// </param>
    /// <returns>
    /// Valor aleatório assinado e limitado.
    /// </returns>
    private static double NextCenteredNoise(Random random, double noiseLevel, double amplitude)
    {
        var raw = (random.NextDouble() * 2.0) - 1.0;
        return raw * amplitude * noiseLevel;
    }

    /// <summary>
    /// Limita um valor numérico a um intervalo fechado.
    /// </summary>
    /// <param name="value">
    /// Valor a limitar.
    /// </param>
    /// <param name="min">
    /// Valor mínimo permitido.
    /// </param>
    /// <param name="max">
    /// Valor máximo permitido.
    /// </param>
    /// <returns>
    /// Valor já limitado ao intervalo.
    /// </returns>
    private static double Clamp(double value, double min, double max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}
