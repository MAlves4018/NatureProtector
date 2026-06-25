using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Users;

namespace NatureProtector.Infrastructure.Postgres.Bootstrap;

/// <summary>
/// Materializa o utilizador administrativo inicial numa operação explícita de bootstrap.
/// Nunca deve ser chamado pelo startup de uma réplica da API.
/// </summary>
public static class AdminUserBootstrapper
{
    public static async Task EnsureAdminUserAsync(
        NatureProtectorControlDbContext dbContext,
        IPasswordHasher<UserRecord> passwordHasher,
        string adminPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(passwordHasher);

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException("NP_BOOTSTRAP_ADMIN_PASSWORD is required by the explicit bootstrap job.");
        }

        await EnsureAdminRoleAsync(dbContext, cancellationToken);

        var adminUser = await dbContext.Users
            .SingleOrDefaultAsync(
                entity => entity.Username == UserRecord.AdminUsername ||
                          entity.Email == UserRecord.AdminEmail,
                cancellationToken);

        if (adminUser is null)
        {
            adminUser = new UserRecord
            {
                Id = Guid.Parse(UserRecord.AdminIdString),
                Username = UserRecord.AdminUsername,
                Email = UserRecord.AdminEmail,
                Organization = UserRecord.AdminOrganization,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Users.Add(adminUser);
        }

        adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, adminPassword);

        var hasAdminRole = await dbContext.UserRoles
            .AnyAsync(entity => entity.UserId == adminUser.Id && entity.RoleId == RoleRecord.AdminId, cancellationToken);
        if (!hasAdminRole)
        {
            dbContext.UserRoles.Add(new UserRoleRecord
            {
                UserId = adminUser.Id,
                RoleId = RoleRecord.AdminId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureAdminRoleAsync(
        NatureProtectorControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var adminRole = await dbContext.Roles
            .SingleOrDefaultAsync(entity => entity.Id == RoleRecord.AdminId, cancellationToken);

        if (adminRole is null)
        {
            dbContext.Roles.Add(new RoleRecord
            {
                Id = RoleRecord.AdminId,
                Name = RoleRecord.Admin
            });
        }
        else if (!string.Equals(adminRole.Name, RoleRecord.Admin, StringComparison.Ordinal))
        {
            adminRole.Name = RoleRecord.Admin;
        }
    }
}
