/*
 * Esta classe concentra os nomes estáveis da topologia RabbitMQ usada pelos
 * componentes de runtime do projeto.
 *
 * Rationale:
 * - Produtores e consumidores têm de declarar exatamente os mesmos recursos de
 *   transporte assíncrono para que o fluxo end-to-end seja previsível.
 * - Centralizar estes nomes evita divergências difíceis de detetar entre hosts.
 *
 * Design considerations:
 * - O exchange é único e as filas são especializadas por responsabilidade.
 * - As bindings atuais são mínimas e refletem apenas o que o runtime suporta
 *   hoje.
 */

namespace NatureProtector.Shared.Messaging;

public static class NatureProtectorRabbitMqTopology
{
    /// <summary>
    /// Exchange principal onde os eventos operacionais são publicados.
    /// </summary>
    public const string ExchangeName = "np.events";
    public const string ExchangeType = "topic";

    /// <summary>
    /// Fila consumida pelo fluxo de prevenção.
    /// </summary>
    public const string IngestionReadingsQueue = "np.ingestion.readings";

    /// <summary>
    /// Fila reservada para inspeção e observabilidade de eventos brutos.
    /// </summary>
    public const string ObservabilityRawQueue = "np.observability.raw";

    /// <summary>
    /// Catálogo histórico das ligações conhecidas pela plataforma. A topologia
    /// efetiva e os papéis operacionais devem ser obtidos através de
    /// RabbitMqOptions.GetQueueDefinitions() e RabbitMqOptions.GetBindings(),
    /// porque a fila auxiliar de observabilidade é opcional.
    /// </summary>
    public static readonly (string QueueName, string RoutingKey)[] Bindings =
    [
        (IngestionReadingsQueue, RoutingKeys.SensorReadingProduced),
        (ObservabilityRawQueue, RoutingKeys.SensorReadingProduced)
    ];
}
