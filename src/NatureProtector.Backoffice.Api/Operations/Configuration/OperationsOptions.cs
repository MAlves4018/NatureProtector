namespace NatureProtector.Backoffice.Api.Operations.Configuration;

public sealed class OperationsOptions
{
    public const string SectionName = "Operations";

    public string Mode { get; init; } = "Disabled";
    public string StoreRoot { get; init; } = "../NatureProtector-Operations-Data-local";
    public string GitHubApiBaseUrl { get; init; } = "https://api.github.com";
    public string GitHubRepository { get; init; } = string.Empty;
    public string GitHubToken { get; init; } = string.Empty;
    public string DefaultRef { get; init; } = "master";
    public string CallbackSecret { get; init; } = string.Empty;
    public bool AllowSelfApproval { get; init; }
}
