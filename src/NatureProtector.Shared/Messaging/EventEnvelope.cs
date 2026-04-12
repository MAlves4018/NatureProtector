/*
 * Este contrato representa o envelope canónico usado para transportar eventos
 * entre o simulador, o fluxo operacional e os mecanismos de observabilidade.
 *
 * Rationale:
 * - O sistema precisa de metadados estáveis para correlação, rastreabilidade e
 *   persistência, independentemente do payload concreto.
 * - Um envelope comum evita que cada produtor ou consumidor reinvente a forma
 *   de transportar identificadores, timestamps e proveniência.
 *
 * Design considerations:
 * - O payload é genérico para permitir reutilização do mesmo envelope em vários
 *   tipos de eventos.
 * - EventId e CorrelationId servem propósitos diferentes: deduplicação do
 *   evento individual e encadeamento lógico da execução.
 * - IngestTime é opcional porque nem todos os produtores conhecem o instante em
 *   que o evento entra realmente no fluxo operacional.
 */

namespace NatureProtector.Shared.Messaging;

/// <summary>
/// Envelope canónico de evento usado pelos produtores e consumidores da
/// plataforma NatureProtector.
/// </summary>
public sealed record EventEnvelope<TPayload>(
    string SchemaVersion,
    Guid EventId,
    string CorrelationId,
    string Producer,
    string EventType,
    Guid AreaId,
    DateTimeOffset EventTime,
    DateTimeOffset? IngestTime,
    TPayload Payload);
