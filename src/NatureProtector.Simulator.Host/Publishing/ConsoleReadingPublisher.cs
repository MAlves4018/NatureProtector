using System.Text;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

/*
 * Este publisher escreve os envelopes gerados nos logs e na consola.
 *
 * Rationale:
 * - É a forma mais simples de validar localmente que o simulador está a gerar
 *   eventos estruturados plausíveis.
 * - Permite observar o payload sem depender logo do broker.
 *
 * Design considerations:
 * - O publisher escreve um resumo compacto nos logs e também o JSON completo na
 *   consola.
 * - A implementação é deliberadamente simples e sem efeitos laterais além da
 *   saída textual.
 */

namespace NatureProtector.Simulator.Host.Publishing;

public sealed class ConsoleReadingPublisher(
    ILogger<ConsoleReadingPublisher> logger) : IReadingPublisher
{
    /// <summary>
    /// Publica um envelope de leitura escrevendo um resumo e o respetivo JSON na
    /// consola.
    /// </summary>
    /// <param name="envelope">
    /// Envelope a publicar.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento para encerramento cooperativo.
    /// </param>
    public Task PublishAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        cancellationToken.ThrowIfCancellationRequested();

        var json = Encoding.UTF8.GetString(
            JsonEventSerializer.SerializeToUtf8Bytes(envelope));

        logger.LogInformation(
            "Publishing reading to console | EventId={EventId} | SensorId={SensorId} | SensorName={SensorName} | MetricType={MetricType} | Value={Value} | State={State}",
            envelope.EventId,
            envelope.Payload.SensorId,
            envelope.Payload.SensorName,
            envelope.Payload.MetricType,
            envelope.Payload.Value,
            envelope.Payload.OperationalState);

        Console.WriteLine(json);

        return Task.CompletedTask;
    }
}
