using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace NatureProtector.Backoffice.Api.Operations.Authorization;

public static class OperationCapabilities
{
    public const string DemoRead = "demo.read";
    public const string AreaRead = "area.read";
    public const string RiskRead = "risk.read";
    public const string PipelineRead = "pipeline.read";
    public const string RunRead = "run.read";
    public const string ScenarioRead = "scenario.read";
    public const string SimulationRead = "simulation.read";
    public const string SimulationExecute = "simulation.execute";
    public const string QualityRead = "quality.read";
    public const string QualityExecuteStatic = "quality.execute.static";
    public const string QualityExecuteFull = "quality.execute.full";
    public const string EvidenceRead = "evidence.read";
    public const string EvidenceDownload = "evidence.download";
    public const string EvidenceExecuteCampaign = "evidence.execute.campaign";
    public const string EvidenceCompare = "evidence.compare";
    public const string DeploymentRead = "deployment.read";
    public const string DeploymentPlan = "deployment.plan";
    public const string DeploymentDeployStaging = "deployment.deploy.staging";
    public const string DeploymentDeployProduction = "deployment.deploy.production";
    public const string DeploymentRollback = "deployment.rollback";
    public const string CloudRead = "cloud.read";
    public const string CloudOperateStaging = "cloud.operate.staging";
    public const string CloudOperateProduction = "cloud.operate.production";
    public const string CloudDestroy = "cloud.destroy";
    public const string ApprovalReview = "approval.review";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string AdminRead = "admin.read";
    public const string AdminExecute = "admin.execute";
    public const string P3Read = "p3.read";
    public const string DataContextRead = "data_context.read";
    public const string DbRead = "db.read";
    public const string HelpRead = "help.read";
    public const string DBRead = "db.read";

    public static readonly IReadOnlyList<string> All =
    [
        DemoRead, AreaRead, RiskRead, PipelineRead, RunRead, ScenarioRead, SimulationRead,
        SimulationExecute, QualityRead, QualityExecuteStatic, QualityExecuteFull, EvidenceRead,
        EvidenceDownload, EvidenceExecuteCampaign, EvidenceCompare, DeploymentRead, DeploymentPlan,
        DeploymentDeployStaging, DeploymentDeployProduction, DeploymentRollback, CloudRead,
        CloudOperateStaging, CloudOperateProduction, CloudDestroy, ApprovalReview, UsersManage,
        RolesManage, AdminRead, AdminExecute, DbRead, P3Read, DataContextRead, HelpRead
    ];
}

public static class OperationRoleCatalog
{
    private static readonly IReadOnlyDictionary<string, string[]> CapabilitiesByRole =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pipeline"] =
            [
                OperationCapabilities.DemoRead, OperationCapabilities.AreaRead, OperationCapabilities.RiskRead,
                OperationCapabilities.PipelineRead, OperationCapabilities.RunRead, OperationCapabilities.QualityRead,
                OperationCapabilities.EvidenceRead, OperationCapabilities.EvidenceDownload,
                OperationCapabilities.EvidenceCompare, OperationCapabilities.DataContextRead,
                OperationCapabilities.HelpRead
            ],
            ["Sim"] =
            [
                OperationCapabilities.DemoRead, OperationCapabilities.AreaRead, OperationCapabilities.RiskRead,
                OperationCapabilities.RunRead, OperationCapabilities.ScenarioRead, OperationCapabilities.SimulationRead,
                OperationCapabilities.SimulationExecute, OperationCapabilities.EvidenceRead,
                OperationCapabilities.DataContextRead, OperationCapabilities.HelpRead
            ],
            ["QA"] =
            [
                OperationCapabilities.DemoRead, OperationCapabilities.AreaRead, OperationCapabilities.RiskRead,
                OperationCapabilities.RunRead, OperationCapabilities.ScenarioRead, OperationCapabilities.SimulationRead,
                OperationCapabilities.SimulationExecute, OperationCapabilities.EvidenceRead, OperationCapabilities.QualityRead,
                OperationCapabilities.QualityExecuteStatic, OperationCapabilities.QualityExecuteFull,
                OperationCapabilities.EvidenceRead, OperationCapabilities.EvidenceDownload,
                OperationCapabilities.EvidenceExecuteCampaign, OperationCapabilities.EvidenceCompare,
                OperationCapabilities.DataContextRead, OperationCapabilities.HelpRead
            ],
            ["Operations"] =
            [
                OperationCapabilities.DemoRead, OperationCapabilities.AreaRead, OperationCapabilities.RiskRead,
                OperationCapabilities.PipelineRead, OperationCapabilities.RunRead, OperationCapabilities.QualityRead,
                OperationCapabilities.EvidenceRead, OperationCapabilities.EvidenceDownload,
                OperationCapabilities.EvidenceCompare, OperationCapabilities.DeploymentRead,
                OperationCapabilities.DeploymentPlan, OperationCapabilities.DeploymentDeployStaging,
                OperationCapabilities.DeploymentRollback, OperationCapabilities.CloudRead,
                OperationCapabilities.CloudOperateStaging, OperationCapabilities.DataContextRead,
                OperationCapabilities.HelpRead, OperationCapabilities.DBRead
            ],
            ["ReleaseApprover"] =
            [
                OperationCapabilities.DemoRead, OperationCapabilities.QualityRead,
                OperationCapabilities.EvidenceRead, OperationCapabilities.EvidenceDownload,
                OperationCapabilities.EvidenceCompare, OperationCapabilities.DeploymentRead,
                OperationCapabilities.DeploymentPlan, OperationCapabilities.DeploymentDeployProduction,
                OperationCapabilities.DeploymentRollback, OperationCapabilities.CloudRead,
                OperationCapabilities.CloudOperateProduction, OperationCapabilities.CloudDestroy,
                OperationCapabilities.ApprovalReview, OperationCapabilities.DataContextRead,
                OperationCapabilities.HelpRead
            ],
            ["Admin"] =
            [
                OperationCapabilities.DemoRead, OperationCapabilities.AreaRead, OperationCapabilities.RiskRead,
                OperationCapabilities.PipelineRead, OperationCapabilities.RunRead, OperationCapabilities.ScenarioRead,
                OperationCapabilities.SimulationRead, OperationCapabilities.SimulationExecute, OperationCapabilities.QualityRead,
                OperationCapabilities.EvidenceRead, OperationCapabilities.EvidenceDownload,
                OperationCapabilities.EvidenceCompare, OperationCapabilities.DeploymentRead,
                OperationCapabilities.CloudRead, OperationCapabilities.UsersManage, OperationCapabilities.RolesManage,
                OperationCapabilities.AdminRead, OperationCapabilities.AdminExecute, OperationCapabilities.P3Read,
                OperationCapabilities.DataContextRead, OperationCapabilities.HelpRead
            ]
        };

    public static IReadOnlyList<string> GetCapabilities(IEnumerable<string> roles) => roles
        .Where(role => !string.IsNullOrWhiteSpace(role))
        .SelectMany(role => CapabilitiesByRole.TryGetValue(role, out var capabilities) ? capabilities : [])
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(capability => capability, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static IReadOnlyList<string> GetRoles(ClaimsPrincipal principal) => principal
        .FindAll(ClaimTypes.Role)
        .Select(claim => claim.Value)
        .Where(role => !string.IsNullOrWhiteSpace(role))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static IReadOnlyList<string> GetCapabilities(ClaimsPrincipal principal) =>
        GetCapabilities(GetRoles(principal));

    public static bool HasCapability(ClaimsPrincipal principal, string capability) =>
        GetCapabilities(principal).Contains(capability, StringComparer.OrdinalIgnoreCase);
}

public sealed record OperationCapabilityRequirement(string Capability) : IAuthorizationRequirement;

public sealed class OperationCapabilityAuthorizationHandler
    : AuthorizationHandler<OperationCapabilityRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationCapabilityRequirement requirement)
    {
        if (OperationRoleCatalog.HasCapability(context.User, requirement.Capability))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public static class OperationAuthorization
{
    public static void Configure(AuthorizationOptions options)
    {
        foreach (var capability in OperationCapabilities.All)
        {
            options.AddPolicy(
                capability,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new OperationCapabilityRequirement(capability)));
        }
    }
}
