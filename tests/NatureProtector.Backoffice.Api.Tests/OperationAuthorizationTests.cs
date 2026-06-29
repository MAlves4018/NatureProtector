using System.Security.Claims;
using NatureProtector.Backoffice.Api.Operations.Authorization;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class OperationAuthorizationTests
{
    [Fact]
    public void Admin_DoesNotImplicitlyReceiveProductionOrDestroyCapabilities()
    {
        var principal = Principal("Admin");

        Assert.True(OperationRoleCatalog.HasCapability(principal, OperationCapabilities.UsersManage));
        Assert.False(OperationRoleCatalog.HasCapability(principal, OperationCapabilities.DeploymentDeployProduction));
        Assert.False(OperationRoleCatalog.HasCapability(principal, OperationCapabilities.CloudDestroy));
    }

    [Fact]
    public void ReleaseApprover_ReceivesApprovalProductionAndDestroyCapabilities()
    {
        var principal = Principal("ReleaseApprover");

        Assert.True(OperationRoleCatalog.HasCapability(principal, OperationCapabilities.ApprovalReview));
        Assert.True(OperationRoleCatalog.HasCapability(principal, OperationCapabilities.DeploymentDeployProduction));
        Assert.True(OperationRoleCatalog.HasCapability(principal, OperationCapabilities.CloudDestroy));
        Assert.False(OperationRoleCatalog.HasCapability(principal, OperationCapabilities.UsersManage));
    }

    [Fact]
    public void Qa_CanExecuteQualityAndEvidenceButCannotMutateCloud()
    {
        var principal = Principal("QA");

        Assert.True(OperationRoleCatalog.HasCapability(principal, OperationCapabilities.QualityExecuteFull));
        Assert.True(OperationRoleCatalog.HasCapability(principal, OperationCapabilities.EvidenceExecuteCampaign));
        Assert.False(OperationRoleCatalog.HasCapability(principal, OperationCapabilities.CloudOperateStaging));
    }

    private static ClaimsPrincipal Principal(params string[] roles)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "tests"));
    }
}
