using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Backoffice.Api.Bootstrap;
using NatureProtector.Backoffice.Api.Tests.TestInfrastructure;
using NatureProtector.Infrastructure.Postgres.Users;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class BackofficeAdminBootstrapperTests
{
    [Fact]
    public async Task EnsureAdminUserAsync_RecreatesMissingAdminRoleBeforeAssigningUserRole()
    {
        await using var scope = new SqliteControlDbContextScope();
        await using (var dbContext = scope.CreateDbContext())
        {
            dbContext.Roles.RemoveRange(dbContext.Roles);
            await dbContext.SaveChangesAsync();
        }

        await using (var dbContext = scope.CreateDbContext())
        {
            await BackofficeAdminBootstrapper.EnsureAdminUserAsync(
                dbContext,
                new PasswordHasher<UserRecord>(),
                "admin123");
        }

        await using var verificationContext = scope.CreateDbContext();
        var adminUserId = Guid.Parse(UserRecord.AdminIdString);

        Assert.True(await verificationContext.Roles
            .AnyAsync(entity => entity.Id == RoleRecord.AdminId && entity.Name == RoleRecord.Admin));
        Assert.True(await verificationContext.Users
            .AnyAsync(entity => entity.Id == adminUserId && entity.Username == UserRecord.AdminUsername));
        Assert.True(await verificationContext.UserRoles
            .AnyAsync(entity => entity.UserId == adminUserId && entity.RoleId == RoleRecord.AdminId));
    }

    [Fact]
    public async Task EnsureAdminUserAsync_IsIdempotentForAdminUserAndRole()
    {
        await using var scope = new SqliteControlDbContextScope();

        await using (var dbContext = scope.CreateDbContext())
        {
            await BackofficeAdminBootstrapper.EnsureAdminUserAsync(
                dbContext,
                new PasswordHasher<UserRecord>(),
                "admin123");
        }

        await using (var dbContext = scope.CreateDbContext())
        {
            await BackofficeAdminBootstrapper.EnsureAdminUserAsync(
                dbContext,
                new PasswordHasher<UserRecord>(),
                "admin123");
        }

        await using var verificationContext = scope.CreateDbContext();
        var adminUserId = Guid.Parse(UserRecord.AdminIdString);

        Assert.Equal(1, await verificationContext.Roles.CountAsync(entity => entity.Id == RoleRecord.AdminId));
        Assert.Equal(1, await verificationContext.Users.CountAsync(entity => entity.Id == adminUserId));
        Assert.Equal(1, await verificationContext.UserRoles.CountAsync(
            entity => entity.UserId == adminUserId && entity.RoleId == RoleRecord.AdminId));
    }
}
