using NatureProtector.Backoffice.Api.Operations.Authorization;
using NatureProtector.Backoffice.Api.Operations.Contracts;

namespace NatureProtector.Backoffice.Api.Operations.Services;

public sealed record OperationInputDefinition(
    string Name,
    string Description,
    bool Required = false,
    string? DefaultValue = null);

public sealed record OperationDefinition(
    string OperationId,
    string Category,
    string DisplayName,
    string Description,
    string RequiredCapability,
    string RiskLevel,
    bool RequiresConfirmation,
    bool RequiresApproval,
    string[] Environments,
    OperationInputDefinition[] Inputs,
    string Workflow,
    string ConfirmationPhrase,
    string Availability = "implemented",
    string EvidenceLevel = "IMPLEMENTED_NOT_PROVED",
    string? Limitation = null)
{
    public OperationDefinitionResponse ToResponse(bool authorized) => new(
        OperationId,
        Category,
        DisplayName,
        Description,
        RequiredCapability,
        RiskLevel,
        RequiresConfirmation,
        RequiresApproval,
        Environments,
        Inputs.Select(input => new OperationInputDefinitionResponse(
            input.Name,
            input.Description,
            input.Required,
            input.DefaultValue)).ToArray(),
        Workflow,
        ConfirmationPhrase,
        authorized,
        Availability,
        EvidenceLevel,
        Limitation);
}

public interface IOperationCatalog
{
    IReadOnlyList<OperationDefinition> All { get; }
    OperationDefinition? Find(string operationId);
}

public sealed class OperationCatalog : IOperationCatalog
{
    private static readonly OperationInputDefinition RefInput =
        new("ref", "Git commit, branch or immutable release reference.", false, "master");
    private static readonly OperationInputDefinition ManifestInput =
        new("manifest", "Release manifest path or artifact identifier.");
    private static readonly OperationInputDefinition PlanHashInput =
        new("planHash", "SHA-256 of the exact approved plan.");

    public IReadOnlyList<OperationDefinition> All { get; } =
    [
        Quality("frontend-fast", "Frontend fast", OperationCapabilities.QualityExecuteStatic),
        Quality("frontend-full", "Frontend full", OperationCapabilities.QualityExecuteFull),
        Quality("backend-unit", "Backend unit", OperationCapabilities.QualityExecuteStatic),
        Quality("backend-integration", "Backend integration", OperationCapabilities.QualityExecuteFull),
        Quality("architecture", "Architecture", OperationCapabilities.QualityExecuteStatic),
        Quality("security", "Security", OperationCapabilities.QualityExecuteFull),
        Quality(
            "playwright-fixture",
            "Playwright fixture",
            OperationCapabilities.QualityExecuteStatic,
            availability: "blocked-workflow-suite-not-mapped",
            evidenceLevel: "NOT_PROVED",
            limitation: "The authoritative quality wrapper does not map this suite ID; dispatch would deterministically fail as an unknown suite."),
        Quality(
            "playwright-full-stack",
            "Playwright full stack",
            OperationCapabilities.QualityExecuteFull,
            availability: "blocked-workflow-suite-not-mapped",
            evidenceLevel: "NOT_PROVED",
            limitation: "The authoritative quality wrapper does not map this suite ID; dispatch would deterministically fail as an unknown suite."),
        Quality("accessibility", "Accessibility", OperationCapabilities.QualityExecuteStatic),
        Quality("mutation", "Mutation", OperationCapabilities.QualityExecuteFull),
        Quality("terraform-static", "Terraform static", OperationCapabilities.QualityExecuteStatic),
        Quality("cloud-static", "Cloud static", OperationCapabilities.QualityExecuteStatic),
        Quality("quality-all", "All quality gates", OperationCapabilities.QualityExecuteFull),

        Evidence("evidence-static", "Static evidence campaign", "static", false),
        Evidence("evidence-quality", "Quality evidence campaign", "quality", false),
        Evidence("evidence-full-plan", "Full evidence plan", "full-plan", true),
        Evidence("evidence-full-execute", "Full evidence execution", "full", true, true),

        Deployment("staging-plan", "Plan staging", OperationCapabilities.DeploymentPlan, "staging", "medium", true, false, "PLAN staging"),
        Deployment(
            "staging-deploy", "Deploy staging", OperationCapabilities.DeploymentDeployStaging, "staging", "high", true, false, "DEPLOY staging",
            [RefInput, new OperationInputDefinition("releaseRunId", "Successful immutable release workflow run ID.", true), new OperationInputDefinition("deploymentMode", "verified or services-only-bootstrap.", false, "verified"), new OperationInputDefinition("edgeBootstrapConfirmation", "Required only for services-only-bootstrap.")]),
        Deployment(
            "staging-rollback", "Rollback staging", OperationCapabilities.DeploymentRollback, "staging", "high", true, false, "ROLLBACK staging",
            [RefInput, new OperationInputDefinition("releaseId", "Cloud Deploy release ID to restore.", true)]),
        Deployment(
            "production-plan", "Plan production", OperationCapabilities.DeploymentPlan, "production", "high", true, true, "PLAN production",
            availability: "blocked-no-authoritative-workflow", evidenceLevel: "NOT_PROVED", limitation: "The current repository has no standalone immutable production plan workflow exposed for dispatch."),
        Deployment(
            "production-deploy", "Deploy production", OperationCapabilities.DeploymentDeployProduction, "production", "critical", true, true, "PROMOTE VERIFIED RELEASE TO PRODUCTION",
            [RefInput, new OperationInputDefinition("stagingRunId", "Successful staging workflow run ID.", true), new OperationInputDefinition("releaseName", "Exact Cloud Deploy release name proved in staging.", true), new OperationInputDefinition("firstReleaseConfirmation", "Required only for the first production release."), new OperationInputDefinition("deploymentMode", "verified or services-only-bootstrap.", false, "verified"), new OperationInputDefinition("edgeBootstrapConfirmation", "Required only for services-only-bootstrap.")]),
        Deployment(
            "production-rollback", "Rollback production", OperationCapabilities.DeploymentRollback, "production", "critical", true, true, "ROLLBACK production",
            availability: "blocked-no-authoritative-workflow", evidenceLevel: "NOT_PROVED", limitation: "No dedicated production rollback workflow is present in the current repository."),

        Cloud("cloud-inventory", "Collect cloud inventory", OperationCapabilities.CloudRead, ["staging"], "low", false, false, string.Empty, availability: "blocked-missing-qualified-owner-input", evidenceLevel: "NOT_PROVED", limitation: "The read-only inventory script requires a validated owner input contract that is not stored in the repository or accepted from the browser."),
        Cloud("cloud-costs", "Collect cloud costs", OperationCapabilities.CloudRead, ["staging", "production"], "low", false, false, string.Empty, availability: "blocked-no-authoritative-workflow", evidenceLevel: "NOT_PROVED", limitation: "A bounded cost export workflow has not yet been established."),
        Cloud("cloud-smoke", "Run cloud smoke", OperationCapabilities.CloudOperateStaging, ["staging"], "medium", true, false, "SMOKE staging", availability: "blocked-missing-qualified-input-contract", evidenceLevel: "NOT_PROVED", limitation: "The current runtime probe requires a larger qualified input contract than this safe UI surface currently exposes."),
        Cloud("cloud-open-staging", "Open staging", OperationCapabilities.CloudOperateStaging, ["staging"], "high", true, false, "OPEN staging", [new OperationInputDefinition("ttlHours", "Staging time-to-live in hours.", true, "4")]),
        Cloud("cloud-close-staging", "Close staging", OperationCapabilities.CloudOperateStaging, ["staging"], "high", true, false, "CLOSE staging"),
        Cloud("cloud-destroy-plan", "Prepare destroy plan", OperationCapabilities.CloudDestroy, ["staging", "production"], "critical", true, true, "PREPARE DESTROY {environment}", [PlanHashInput], "blocked-no-destroy-plan-workflow", "NOT_PROVED", "A destroy-specific immutable plan workflow must exist before this operation can be enabled."),
        Cloud("cloud-destroy-execute", "Execute approved destroy plan", OperationCapabilities.CloudDestroy, ["staging", "production"], "critical", true, true, "DESTROY {environment} PLAN {planHash}", [PlanHashInput], "blocked-until-approved-plan", "NOT_PROVED", "Execution remains blocked unless an immutable destroy plan workflow, exact hash and separate approval are all present."),
    ];

    public OperationDefinition? Find(string operationId) => All.FirstOrDefault(
        definition => string.Equals(definition.OperationId, operationId, StringComparison.OrdinalIgnoreCase));

    private static OperationDefinition Quality(
        string id,
        string name,
        string capability,
        string availability = "implemented",
        string evidenceLevel = "IMPLEMENTED_NOT_PROVED",
        string? limitation = null) => new(
        id, "quality", name, $"Execute the closed '{id}' quality suite through the authoritative CI workflows.",
        capability, "low", false, false, ["ci"], [RefInput], "_quality-operation.yml", string.Empty,
        availability, evidenceLevel, limitation);

    private static OperationDefinition Evidence(
        string id,
        string name,
        string profile,
        bool requiresConfirmation,
        bool requiresApproval = false) => new(
        id, "evidence", name, $"Execute the '{profile}' evidence profile and index returned artifacts.",
        OperationCapabilities.EvidenceExecuteCampaign, requiresApproval ? "high" : "medium",
        requiresConfirmation, requiresApproval, ["ci", "staging"],
        [RefInput, new OperationInputDefinition("profile", "Closed evidence profile.", true, profile)],
        "_evidence-campaign.yml", requiresConfirmation ? $"EXECUTE {profile} EVIDENCE" : string.Empty);

    private static OperationDefinition Deployment(
        string id,
        string name,
        string capability,
        string environment,
        string risk,
        bool confirmation,
        bool approval,
        string phrase,
        OperationInputDefinition[]? inputs = null,
        string availability = "implemented",
        string evidenceLevel = "IMPLEMENTED_NOT_PROVED",
        string? limitation = null) => new(
        id, "deployment", name, $"Request '{id}' through the current deployment workflows without exposing cloud credentials to the browser.",
        capability, risk, confirmation, approval, [environment], inputs ?? [RefInput, ManifestInput],
        "_deployment-operation.yml", phrase, availability, evidenceLevel, limitation);

    private static OperationDefinition Cloud(
        string id,
        string name,
        string capability,
        string[] environments,
        string risk,
        bool confirmation,
        bool approval,
        string phrase,
        OperationInputDefinition[]? inputs = null,
        string availability = "implemented",
        string evidenceLevel = "IMPLEMENTED_NOT_PROVED",
        string? limitation = null) => new(
        id, "cloud", name, $"Execute the closed cloud operation '{id}'. Arbitrary gcloud or Terraform commands are never accepted.",
        capability, risk, confirmation, approval, environments, inputs ?? [RefInput],
        "_cloud-operation.yml", phrase, availability, evidenceLevel, limitation);
}
