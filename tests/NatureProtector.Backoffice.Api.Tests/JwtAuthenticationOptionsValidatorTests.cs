using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NatureProtector.Backoffice.Api.Configuration;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class JwtAuthenticationOptionsValidatorTests
{
    [Fact]
    public void Validate_Development_AllowsDocumentedLocalKey()
    {
        var validator = new JwtAuthenticationOptionsValidator(Environment("Development"));
        var options = ValidOptions(JwtAuthenticationOptionsValidator.DevelopmentSigningKey);

        var result = validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_Staging_RejectsDocumentedLocalKey()
    {
        var validator = new JwtAuthenticationOptionsValidator(Environment("Staging"));
        var options = ValidOptions(JwtAuthenticationOptionsValidator.DevelopmentSigningKey);

        var result = validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "Jwt:SigningKey contains a development or placeholder value that is not allowed outside Development.",
            result.Failures);
    }

    [Fact]
    public void Validate_Production_AcceptsStrongEnvironmentKey()
    {
        var validator = new JwtAuthenticationOptionsValidator(Environment("Production"));
        var options = ValidOptions("8d21f4c7-98b2-45ad-a6cf-0d147cc3e855");

        var result = validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null, "Jwt:SigningKey is required and must be supplied through environment-specific configuration or a secret provider.")]
    [InlineData("short-key", "Jwt:SigningKey must contain at least 32 characters.")]
    [InlineData("replace-with-production-secret-value-123456", "Jwt:SigningKey contains a development or placeholder value that is not allowed outside Development.")]
    public void Validate_Production_RejectsMissingShortOrPlaceholderKeys(string? signingKey, string expectedFailure)
    {
        var validator = new JwtAuthenticationOptionsValidator(Environment("Production"));
        var options = ValidOptions(signingKey ?? string.Empty);

        var result = validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains(expectedFailure, result.Failures);
    }

    [Fact]
    public void Validate_RejectsInvalidLifetimeAndMissingIdentityFields()
    {
        var validator = new JwtAuthenticationOptionsValidator(Environment("Development"));
        var options = ValidOptions(JwtAuthenticationOptionsValidator.DevelopmentSigningKey);
        options.Issuer = " ";
        options.Audience = string.Empty;
        options.TokenLifetimeMinutes = 0;

        var result = validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains("Jwt:Issuer is required.", result.Failures);
        Assert.Contains("Jwt:Audience is required.", result.Failures);
        Assert.Contains("Jwt:TokenLifetimeMinutes must be between 1 and 1440 minutes.", result.Failures);
    }

    private static JwtAuthenticationOptions ValidOptions(string signingKey) => new()
    {
        Issuer = "NatureProtector",
        Audience = "NatureProtector.Backoffice",
        SigningKey = signingKey,
        TokenLifetimeMinutes = 60
    };

    private static IHostEnvironment Environment(string name) => new TestHostEnvironment
    {
        EnvironmentName = name
    };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "NatureProtector.Backoffice.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
