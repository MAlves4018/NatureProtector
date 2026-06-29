using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NatureProtector.Backoffice.Api.Configuration;

/*
 * Separa explicitamente a orquestração local da configuração de ambientes
 * alojados.
 *
 * Rationale:
 * - O lançamento por ProcessStartInfo assume SDK, source tree e filesystem
 *   local, premissas que não são válidas para containers imutáveis ou Cloud Run.
 * - Staging e production devem usar uma implementação de orquestração própria
 *   ou manter a criação de processos locais desativada.
 */

public sealed class BackofficeApiOptionsValidator : IValidateOptions<BackofficeApiOptions>
{
    private readonly IHostEnvironment _environment;

    public BackofficeApiOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, BackofficeApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!_environment.IsDevelopment() && options.LocalRuntimeProcessLaunchEnabled)
        {
            return ValidateOptionsResult.Fail(
                "BackofficeApi:LocalRuntimeProcessLaunchEnabled cannot be true outside Development. " +
                "Use an environment-specific distributed orchestrator or keep local process launch disabled.");
        }

        return ValidateOptionsResult.Success;
    }
}
