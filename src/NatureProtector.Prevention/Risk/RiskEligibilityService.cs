using NatureProtector.Prevention.Readings;

namespace NatureProtector.Prevention.Risk;

/*
 * Esta fronteira separa a leitura já aceite da decisão de elegibilidade para
 * o motor de risco.
 *
 * Design note:
 * - A baseline atual continua permissiva por compatibilidade: todas as leituras
 *   normalizadas que chegam aqui são consideradas elegíveis.
 * - O objetivo desta camada é criar um ponto explícito para futuras regras de
 *   suporte de métricas, unidades, janelas temporais e requisitos de dados,
 *   sem acoplar essas decisões ao envelope ou ao scoring service.
 */
public sealed class RiskEligibilityService : IRiskEligibilityService
{
    public Task<RiskEligibilityResult> EvaluateAsync(
        NormalizedReading reading,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(RiskEligibilityResult.Eligible);
    }
}
