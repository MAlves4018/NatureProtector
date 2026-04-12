/*
 * Este serviço resolve a seed pseudoaleatória usada pelo simulador.
 *
 * Rationale:
 * - Uma simulação que não possa ser reproduzida é mais difícil de validar,
 *   depurar e comparar.
 * - Centralizar a resolução da seed garante que todo o host usa a mesma origem
 *   determinística de pseudoaleatoriedade.
 *
 * Design considerations:
 * - Se existir seed configurada, ela é usada tal como foi definida.
 * - Se não existir seed configurada, é gerada uma única vez no arranque.
 * - O serviço é deliberadamente pequeno porque a sua responsabilidade é apenas
 *   escolher a seed, não gerar leituras.
 */

namespace NatureProtector.Simulator.Host.Services;

public sealed class SeedProvider
{
    /// <summary>
    /// Resolve a seed efetiva a usar na execução atual do simulador.
    /// </summary>
    /// <param name="configuredSeed">
    /// Seed opcional fornecida por configuração.
    /// </param>
    /// <returns>
    /// Seed inteira não nula adequada para construir <see cref="Random" />.
    /// </returns>
    public int ResolveSeed(int? configuredSeed)
    {
        if (configuredSeed.HasValue)
        {
            return configuredSeed.Value;
        }

        var generatedSeed = Random.Shared.Next(1, int.MaxValue);

        return generatedSeed;
    }

    /// <summary>
    /// Cria uma instância de <see cref="Random" /> com a seed já resolvida.
    /// </summary>
    /// <param name="seed">
    /// Seed anteriormente resolvida para a execução atual.
    /// </param>
    /// <returns>
    /// Instância determinística de <see cref="Random" />.
    /// </returns>
    public Random CreateRandom(int seed)
    {
        return new Random(seed);
    }
}
