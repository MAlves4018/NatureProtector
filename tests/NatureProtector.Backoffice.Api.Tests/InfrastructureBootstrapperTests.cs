using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Backoffice.Api.Tests.TestInfrastructure;
using NatureProtector.Infrastructure.Postgres.Bootstrap;
using NatureProtector.Infrastructure.Postgres.Users;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class InfrastructureBootstrapperTests
{
    [Fact]
    public async Task AdminUserBootstrapper_RequiresExplicitPassword()
    {
        await using var scope = new SqliteControlDbContextScope();
        await using var dbContext = scope.CreateDbContext();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AdminUserBootstrapper.EnsureAdminUserAsync(
                dbContext,
                new PasswordHasher<UserRecord>(),
                " "));

        Assert.Contains("NP_BOOTSTRAP_ADMIN_PASSWORD", exception.Message);
    }

    [Fact]
    public async Task AdminUserBootstrapper_RepairsRoleAndIsIdempotent()
    {
        await using var scope = new SqliteControlDbContextScope();
        await using (var dbContext = scope.CreateDbContext())
        {
            var role = await dbContext.Roles.SingleAsync(entity => entity.Id == RoleRecord.AdminId);
            role.Name = "LegacyAdmin";
            await dbContext.SaveChangesAsync();
        }

        await using (var dbContext = scope.CreateDbContext())
        {
            await AdminUserBootstrapper.EnsureAdminUserAsync(
                dbContext,
                new PasswordHasher<UserRecord>(),
                "admin123");
        }

        await using (var dbContext = scope.CreateDbContext())
        {
            await AdminUserBootstrapper.EnsureAdminUserAsync(
                dbContext,
                new PasswordHasher<UserRecord>(),
                "admin456");
        }

        await using var verification = scope.CreateDbContext();
        var adminId = Guid.Parse(UserRecord.AdminIdString);
        Assert.Equal(RoleRecord.Admin, (await verification.Roles.SingleAsync(entity => entity.Id == RoleRecord.AdminId)).Name);
        Assert.Equal(1, await verification.Users.CountAsync(entity => entity.Id == adminId));
        Assert.Equal(1, await verification.UserRoles.CountAsync(entity => entity.UserId == adminId && entity.RoleId == RoleRecord.AdminId));
        Assert.Equal("admin", (await verification.Users.SingleAsync(entity => entity.Id == adminId)).Username);
    }

    [Fact]
    public async Task ControlPlaneBootstrapper_BootstrapsPilotAreaFromVersionedArtifacts_AndIsIdempotent()
    {
        await using var scope = new SqliteControlDbContextScope();
        await using var dbContext = scope.CreateDbContext();
        var repoRoot = ResolveRepoRoot();
        var bootstrapper = new ControlPlaneBootstrapper(dbContext, repoRoot, skipSchemaMigration: true);

        var first = await bootstrapper.BootstrapPilotAreaAsync(CancellationToken.None);
        var second = await bootstrapper.BootstrapPilotAreaAsync(CancellationToken.None);

        Assert.Equal(1, first.ConfigurationVersionNumber);
        Assert.Equal("proenca-a-nova", first.AreaCode);
        Assert.Equal(first, second);
        Assert.True(first.GridCellCount > 0);
        Assert.Equal(3, first.SensorProfileCount);
        Assert.Equal(6, first.SensorNodeCount);
        Assert.Equal(3, first.ScenarioCount);
        Assert.True(first.DatasetArtifactCount >= first.ScenarioCount);
        Assert.True(first.ScenarioDatasetBindingCount >= first.ScenarioCount);
        Assert.Equal(1, await dbContext.ConfigurationVersions.CountAsync());
        Assert.Equal(1, await dbContext.Areas.CountAsync());
        Assert.Equal(first.GridCellCount, await dbContext.GridCells.CountAsync());
        Assert.Equal(first.SensorProfileCount, await dbContext.SensorProfiles.CountAsync());
        Assert.Equal(first.SensorNodeCount, await dbContext.SensorNodes.CountAsync(entity => entity.IsActive));
        Assert.Equal(first.ScenarioCount, await dbContext.ScenarioDefinitions.CountAsync());
    }

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NatureProtector.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved for bootstrapper test.");
    }
}
