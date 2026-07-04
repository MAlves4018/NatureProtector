using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Users;

namespace NatureProtector.Backoffice.Api.Bootstrap;

public static class BackofficeAdminBootstrapper
{
    public static async Task EnsureAdminUserAsync(
        NatureProtectorControlDbContext dbContext,
        IPasswordHasher<UserRecord> passwordHasher,
        string adminPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(passwordHasher);

        await EnsureCanonicalRolesAsync(dbContext, cancellationToken);

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

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

    private static async Task EnsureCanonicalRolesAsync(
        NatureProtectorControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var canonicalRoles = new (short Id, string Name)[]
        {
            (RoleRecord.AdminId, RoleRecord.Admin),
            (RoleRecord.SimId, RoleRecord.Sim),
            (RoleRecord.PipelineId, RoleRecord.Pipeline),
            (RoleRecord.QAId, RoleRecord.QA),
            (RoleRecord.OperationsId, RoleRecord.Operations),
            (RoleRecord.ReleaseApproverId, RoleRecord.ReleaseApprover)
        };

        foreach (var canonical in canonicalRoles)
        {
            var role = await dbContext.Roles
                .SingleOrDefaultAsync(entity => entity.Id == canonical.Id, cancellationToken);
            if (role is null)
            {
                dbContext.Roles.Add(new RoleRecord
                {
                    Id = canonical.Id,
                    Name = canonical.Name
                });
            }
            else if (!string.Equals(role.Name, canonical.Name, StringComparison.Ordinal))
            {
                role.Name = canonical.Name;
            }
        }
    }

}
