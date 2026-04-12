namespace NatureProtector.Backoffice.Api.Configuration;

public sealed class BackofficeApiOptions
{
    public const string SectionName = "BackofficeApi";

    public bool ControlPlaneEnabled { get; set; } = true;
}
