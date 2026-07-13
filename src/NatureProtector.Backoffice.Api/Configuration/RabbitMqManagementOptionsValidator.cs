using Microsoft.Extensions.Options;
using NatureProtector.Shared.Configuration;

namespace NatureProtector.Backoffice.Api.Configuration;

/// <summary>
/// Validates only the RabbitMQ Management HTTP surface. AMQP transport
/// validation remains owned by the publisher and consumer components.
/// </summary>
public sealed class RabbitMqManagementOptionsValidator : IValidateOptions<RabbitMqOptions>
{
    public ValidateOptionsResult Validate(string? name, RabbitMqOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        var scheme = options.ManagementScheme?.Trim().ToLowerInvariant();

        if (scheme is not ("http" or "https"))
        {
            failures.Add("RabbitMq:ManagementScheme must be either 'http' or 'https'.");
        }

        if (string.IsNullOrWhiteSpace(options.GetEffectiveManagementHost()))
        {
            failures.Add("RabbitMq management host must not be empty.");
        }

        if (options.ManagementPort is < 1 or > 65535)
        {
            failures.Add("RabbitMq:ManagementPort must be between 1 and 65535.");
        }

        if (options.ManagementTimeoutSeconds is < 1 or > 30)
        {
            failures.Add("RabbitMq:ManagementTimeoutSeconds must be between 1 and 30 seconds.");
        }

        if (string.IsNullOrWhiteSpace(options.GetEffectiveManagementUserName()))
        {
            failures.Add("RabbitMq management username must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.GetEffectiveManagementPassword()))
        {
            failures.Add("RabbitMq management password must not be empty.");
        }

        if (scheme == "http")
        {
            if (!options.ManagementAllowInsecureHttp)
            {
                failures.Add(
                    "RabbitMq Management HTTP requires RabbitMq:ManagementAllowInsecureHttp=true. " +
                    "Use HTTPS outside explicitly isolated local or Compose environments.");
            }

            if (!string.IsNullOrWhiteSpace(options.ManagementCertificateAuthorityPath))
            {
                failures.Add(
                    "RabbitMq:ManagementCertificateAuthorityPath can only be used with ManagementScheme=https.");
            }
        }

        if (scheme == "https" &&
            !string.IsNullOrWhiteSpace(options.ManagementCertificateAuthorityPath))
        {
            try
            {
                _ = PrivateCertificateAuthorityValidator.Create(
                    options.ManagementCertificateAuthorityPath,
                    options.ManagementCheckCertificateRevocation);
            }
            catch (Exception exception) when (
                exception is FileNotFoundException or
                UnauthorizedAccessException or
                System.Security.Cryptography.CryptographicException or
                ArgumentException)
            {
                failures.Add(
                    "RabbitMq management private CA could not be loaded or validated. " +
                    "Check the mounted certificate path and file contents.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
