namespace NatureProtector.Backoffice.Api.Configuration;

public sealed class BackofficeApiOptions
{
    public const string SectionName = "BackofficeApi";

    public bool ControlPlaneEnabled { get; set; } = true;

    /// <summary>
    /// Allows the Backoffice to start local Simulator processes. This is a
    /// development/evidence-only mechanism and is rejected in hosted
    /// environments by <see cref="BackofficeApiOptionsValidator"/>.
    /// </summary>
    public bool LocalRuntimeProcessLaunchEnabled { get; set; }
}
