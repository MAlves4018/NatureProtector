namespace NatureProtector.Backoffice.Api.Configuration;

public sealed class JwtAuthenticationOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "NatureProtector";
    public string Audience { get; set; } = "NatureProtector.Backoffice";
    public string SigningKey { get; set; } = string.Empty;
    public int TokenLifetimeMinutes { get; set; } = 60;
}
