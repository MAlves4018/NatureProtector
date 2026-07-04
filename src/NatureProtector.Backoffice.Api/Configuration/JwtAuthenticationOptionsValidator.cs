using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NatureProtector.Backoffice.Api.Configuration;

/*
 * Valida a configuração JWT antes de a API aceitar pedidos.
 *
 * Rationale:
 * - Uma chave vazia, curta ou de desenvolvimento não pode ser aceite por
 *   staging/production quando um override de secret falha.
 * - A validação no arranque produz uma falha explícita em vez de permitir uma
 *   configuração fraca ou um erro tardio durante a autenticação.
 */

public sealed class JwtAuthenticationOptionsValidator : IValidateOptions<JwtAuthenticationOptions>
{
    public const string DevelopmentSigningKey = "dev-only-change-me-please-32-bytes!!";

    private static readonly string[] NonProductionMarkers =
    [
        "dev-only",
        "change-me",
        "changeme",
        "replace-with",
        "replace_with",
        "local-test",
        "example",
        "<",
        ">"
    ];

    private readonly IHostEnvironment _environment;

    public JwtAuthenticationOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, JwtAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            failures.Add("Jwt:SigningKey is required and must be supplied through environment-specific configuration or a secret provider.");
        }
        else
        {
            if (options.SigningKey.Length < 32)
            {
                failures.Add("Jwt:SigningKey must contain at least 32 characters.");
            }

            if (!_environment.IsDevelopment() && IsDevelopmentOrPlaceholderValue(options.SigningKey))
            {
                failures.Add("Jwt:SigningKey contains a development or placeholder value that is not allowed outside Development.");
            }
        }

        if (options.TokenLifetimeMinutes is < 1 or > 24 * 60)
        {
            failures.Add("Jwt:TokenLifetimeMinutes must be between 1 and 1440 minutes.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsDevelopmentOrPlaceholderValue(string value)
    {
        if (string.Equals(value, DevelopmentSigningKey, StringComparison.Ordinal))
        {
            return true;
        }

        return NonProductionMarkers.Any(marker =>
            value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
