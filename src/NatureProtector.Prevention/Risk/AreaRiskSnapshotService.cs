using NatureProtector.Core.Risk;

namespace NatureProtector.Prevention.Risk;

/*
 * Este serviço agrega avaliações individuais num snapshot de risco por área.
 *
 * Rationale:
 * - A pipeline precisa de uma visão resumida do estado da área para alimentar
 *   projeções, alertas e persistência operacional.
 * - A agregação fica isolada da persistência para poder evoluir de forma
 *   independente.
 *
 * Design considerations:
 * - Se não existirem avaliações aceites, o snapshot reflete explicitamente essa
 *   ausência de sinal.
 * - Quando existem avaliações, a lógica de agregação é delegada para o domínio
 *   Core.
 */

public sealed class AreaRiskSnapshotService : IAreaRiskSnapshotService
{
    /// <summary>
    /// Constrói um snapshot agregado para uma área num instante lógico.
    /// </summary>
    public AreaRiskSnapshot BuildSnapshot(
        IEnumerable<RiskAssessment> assessments,
        DateTimeOffset snapshotTime)
    {
        ArgumentNullException.ThrowIfNull(assessments);

        var items = assessments.ToList();

        if (items.Count == 0)
        {
            throw new ArgumentException(
                "At least one eligible risk assessment is required; area risk is unavailable.",
                nameof(assessments));
        }

        return AreaRiskSnapshot.CreateFromAssessments(
            id: Guid.NewGuid(),
            timestamp: snapshotTime,
            assessments: items);
    }
}
