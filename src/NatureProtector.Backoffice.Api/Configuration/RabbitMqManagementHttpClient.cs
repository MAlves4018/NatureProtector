using Microsoft.Extensions.Options;
using NatureProtector.Shared.Configuration;

namespace NatureProtector.Backoffice.Api.Configuration;

public static class RabbitMqManagementHttpClient
{
    public const string ClientName = "RabbitMqManagement";

    public static IServiceCollection AddRabbitMqManagementHttpClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<RabbitMqOptions>, RabbitMqManagementOptionsValidator>();
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient(ClientName, (serviceProvider, client) =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<RabbitMqOptions>>()
                    .Value;
                client.Timeout = TimeSpan.FromSeconds(options.ManagementTimeoutSeconds);
            })
            .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<RabbitMqOptions>>()
                    .Value;
                return CreatePrimaryHandler(options);
            })
            // A mounted CA is loaded when a handler is created. Limiting handler
            // lifetime allows certificate rotation to be observed without an
            // application restart, while preserving connection pooling.
            .SetHandlerLifetime(TimeSpan.FromMinutes(2));

        return services;
    }

    public static Uri BuildQueuesUri(RabbitMqOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var scheme = options.ManagementScheme?.Trim().ToLowerInvariant();
        if (scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "RabbitMQ ManagementScheme must be validated before building the Management API URI.");
        }

        return new UriBuilder(
            scheme,
            options.GetEffectiveManagementHost(),
            options.ManagementPort,
            "/api/queues")
            .Uri;
    }

    public static HttpMessageHandler CreatePrimaryHandler(RabbitMqOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            CheckCertificateRevocationList = options.ManagementCheckCertificateRevocation
        };

        if (!string.Equals(options.ManagementScheme, "https", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(options.ManagementCertificateAuthorityPath))
        {
            return handler;
        }

        var validator = PrivateCertificateAuthorityValidator.Create(
            options.ManagementCertificateAuthorityPath,
            options.ManagementCheckCertificateRevocation)
            ?? throw new InvalidOperationException(
                "A RabbitMQ management private CA path was configured but no validator was created.");

        handler.ServerCertificateCustomValidationCallback =
            (request, certificate, chain, errors) =>
                validator.Validate(request, certificate, chain, errors);

        return handler;
    }
}
