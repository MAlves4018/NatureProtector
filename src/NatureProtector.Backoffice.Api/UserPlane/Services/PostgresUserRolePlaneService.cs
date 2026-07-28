using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NatureProtector.Backoffice.Api.Configuration;
using NatureProtector.Backoffice.Api.UserPlane.Contracts;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Users;

namespace NatureProtector.Backoffice.Api.UserPlane.Services;

public sealed class PostgresUserRolePlaneService : IUserRolePlaneService
{
    private readonly IDbContextFactory<NatureProtectorControlDbContext> _dbContextFactory;
    private readonly IPasswordHasher<UserRecord> _passwordHasher;
    private readonly JwtAuthenticationOptions _jwtOptions;

    public PostgresUserRolePlaneService(
        IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory,
        IPasswordHasher<UserRecord> passwordHasher,
        IOptions<JwtAuthenticationOptions> jwtOptions)
    {
        _dbContextFactory = dbContextFactory;
        _passwordHasher = passwordHasher;
        _jwtOptions = jwtOptions.Value;
    }

    /// <summary>
    /// Indica que a implementação PostgreSQL do userRole plane está disponível.
    /// </summary>
    public bool IsAvailable => !string.IsNullOrWhiteSpace(_jwtOptions.SigningKey);

    /// <summary>
    /// Mensagem curta de disponibilidade exposta pelos endpoints da API.
    /// </summary>
    public string AvailabilityMessage => IsAvailable
        ? "PostgreSQL-backed userRole plane is available."
        : "JWT signing key is not configured. Set Jwt:SigningKey to enable user plane operations.";

    public async Task<UserRoleResponse?> AddRoleToUserAsync(Guid userId, short roleId, CancellationToken cancellationToken)
    {
        if (!IsAvailable || userId == Guid.Empty)
        {
            return null;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(entity => entity.Id == userId, cancellationToken);
        if (!userExists)
        {
            return null;
        }

        var role = await dbContext.Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == roleId, cancellationToken);
        if (role is null)
        {
            return null;
        }

        var exists = await dbContext.UserRoles
            .AnyAsync(entity => entity.UserId == userId && entity.RoleId == roleId, cancellationToken);
        if (!exists)
        {
            dbContext.UserRoles.Add(new UserRoleRecord
            {
                UserId = userId,
                RoleId = roleId
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new UserRoleResponse(
            role.Id,
            role.Name,
            userId);
    }

    public async Task<bool> CheckUserRoleAsync(Guid userId, short roleId, CancellationToken cancellationToken)
    {
        if (!IsAvailable || userId == Guid.Empty)
        {
            return false;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.UserRoles
            .AsNoTracking()
            .AnyAsync(entity => entity.UserId == userId && entity.RoleId == roleId, cancellationToken);
    }

    public async Task<RoleResponse?> CreateRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(roleName))
        {
            return null;
        }

        var normalizedName = roleName.Trim();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await dbContext.Roles
            .AsNoTracking()
            .AnyAsync(entity => entity.Name == normalizedName, cancellationToken);
        if (exists)
        {
            return null;
        }

        var lastId = await dbContext.Roles
            .AsNoTracking()
            .Select(entity => (int?)entity.Id)
            .MaxAsync(cancellationToken);
        var nextId = (lastId ?? 0) + 1;
        if (nextId > short.MaxValue)
        {
            return null;
        }

        var role = new RoleRecord
        {
            Id = (short)nextId,
            Name = normalizedName
        };

        await dbContext.Roles.AddAsync(role, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RoleResponse(role.Id, role.Name);
    }

    public async Task<UserResponse?> CreateUserAsync(UserRequest request, CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.Email))
        {
            return null;
        }

        var normalizedEmail = request.Email.Trim();
        var roleNames = request.Roles is null ? null : NormalizeRoleNames(request.Roles);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                entity => entity.Username == request.Username || entity.Email == normalizedEmail,
                cancellationToken);
        if (userExists)
        {
            return null;
        }

        var user = new UserRecord
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = normalizedEmail,
            Organization = request.Organization?.Trim() ?? string.Empty,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var roles = roleNames is not null
            ? await LoadRolesAsync(dbContext, roleNames, cancellationToken)
            : [];
        if (roleNames is not null && roles.Count != roleNames.Count)
        {
            return null;
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        dbContext.UserRoles.AddRange(roles.Select(role => new UserRoleRecord
        {
            UserId = user.Id,
            RoleId = role.Id
        }));

        await dbContext.SaveChangesAsync(cancellationToken);

        return new UserResponse(
            user.Id,
            user.Username,
            user.Email,
            roles.Select(role => role.Name).ToArray());
    }

    public async Task<bool> DeleteRoleAsync(short roleId, CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            return false;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var role = await dbContext.Roles
            .SingleOrDefaultAsync(entity => entity.Id == roleId, cancellationToken);
        if (role is null)
        {
            return false;
        }

        dbContext.Roles.Remove(role);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!IsAvailable || userId == Guid.Empty)
        {
            return false;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await dbContext.Users
            .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<RoleResponse?> GetRoleAsync(short roleId, CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            return null;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var role = await dbContext.Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == roleId, cancellationToken);
        if (role is null)
        {
            return null;
        }

        return new RoleResponse(role.Id, role.Name);
    }

    public async Task<UserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!IsAvailable || userId == Guid.Empty)
        {
            return null;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return await BuildUserResponseAsync(dbContext, user, cancellationToken);
    }

    public async Task<IEnumerable<UserResponse>> GetUsersInRoleAsync(short roleId, CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            return [];
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var users = await dbContext.UserRoles
            .AsNoTracking()
            .Where(entity => entity.RoleId == roleId)
            .Join(
                dbContext.Users.AsNoTracking(),
                userRole => userRole.UserId,
                user => user.Id,
                (userRole, user) => user)
            .OrderBy(user => user.Username)
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            return [];
        }

        var results = new List<UserResponse>(users.Count);
        foreach (var user in users)
        {
            results.Add(await BuildUserResponseAsync(dbContext, user, cancellationToken));
        }

        return results;
    }

    public async Task<IReadOnlyList<UserResponse>> ListUsersAsync(CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            return [];
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(entity => entity.Username)
            .ToListAsync(cancellationToken);
        var responses = new List<UserResponse>(users.Count);
        foreach (var user in users)
        {
            responses.Add(await BuildUserResponseAsync(dbContext, user, cancellationToken));
        }
        return responses;
    }

    public async Task<IReadOnlyList<RoleResponse>> ListRolesAsync(CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            return [];
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Roles
            .AsNoTracking()
            .OrderBy(entity => entity.Id)
            .Select(entity => new RoleResponse(entity.Id, entity.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var identifier = request.UsernameOrEmail.Trim();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entity => entity.Username == identifier || entity.Email == identifier,
                cancellationToken);
        if (user is null)
        {
            return null;
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var roleNames = await LoadRoleNamesAsync(dbContext, user.Id, cancellationToken);
        var token = BuildToken(user, roleNames);

        return new LoginResponse(
            user.Id,
            user.Username,
            user.Email,
            roleNames,
            token);
    }

    public async Task<UserResponse?> GetCurrentUserAsync(
        string? authorizationHeader,
        CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
        {
            return null;
        }

        var token = authorizationHeader.Substring("Bearer ".Length).Trim();

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return null;
            }

            return await GetUserAsync(userId, cancellationToken);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    public Task<bool> LogoutAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public async Task<UserResponse?> RemoveRoleFromUserAsync(Guid userId, short roleId, CancellationToken cancellationToken)
    {
        if (!IsAvailable || userId == Guid.Empty)
        {
            return null;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var role = await dbContext.Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == roleId, cancellationToken);
        if (role is null)
        {
            return null;
        }

        var mapping = await dbContext.UserRoles
            .SingleOrDefaultAsync(
                entity => entity.UserId == userId && entity.RoleId == roleId,
                cancellationToken);
        if (mapping is not null)
        {
            dbContext.UserRoles.Remove(mapping);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return await BuildUserResponseAsync(dbContext, user, cancellationToken);
    }

    public async Task<RoleResponse?> UpdateRoleAsync(short roleId, string newRoleName, CancellationToken cancellationToken)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(newRoleName))
        {
            return null;
        }

        var normalizedName = newRoleName.Trim();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var role = await dbContext.Roles
            .SingleOrDefaultAsync(entity => entity.Id == roleId, cancellationToken);
        if (role is null)
        {
            return null;
        }

        var exists = await dbContext.Roles
            .AsNoTracking()
            .AnyAsync(entity => entity.Id != roleId && entity.Name == normalizedName, cancellationToken);
        if (exists)
        {
            return null;
        }

        role.Name = normalizedName;

        dbContext.Update(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RoleResponse(role.Id, role.Name);
    }

    public async Task<UserResponse?> UpdateUserAsync(Guid userId, UserRequest request, CancellationToken cancellationToken)
    {
        if (!IsAvailable || userId == Guid.Empty)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email))
        {
            return null;
        }

        var normalizedEmail = request.Email.Trim();
        var roleNames = request.Roles is null ? null : NormalizeRoleNames(request.Roles);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await dbContext.Users
            .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var duplicate = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                entity => entity.Id != userId &&
                          (entity.Username == request.Username || entity.Email == normalizedEmail),
                cancellationToken);
        if (duplicate)
        {
            return null;
        }

        user.Username = request.Username;
        user.Email = normalizedEmail;
        user.Organization = request.Organization?.Trim() ?? user.Organization;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        }

        if (roleNames is not null)
        {
            var roles = await LoadRolesAsync(dbContext, roleNames, cancellationToken);
            if (roles.Count != roleNames.Count)
            {
                return null;
            }

            var desiredRoleIds = roles.Select(role => role.Id).ToHashSet();
            var existingUserRoles = await dbContext.UserRoles
                .Where(entity => entity.UserId == user.Id)
                .ToListAsync(cancellationToken);

            var rolesToRemove = existingUserRoles
                .Where(entity => !desiredRoleIds.Contains(entity.RoleId))
                .ToList();
            if (rolesToRemove.Count > 0)
            {
                dbContext.UserRoles.RemoveRange(rolesToRemove);
            }

            var existingRoleIds = existingUserRoles.Select(entity => entity.RoleId).ToHashSet();
            var rolesToAdd = roles
                .Where(role => !existingRoleIds.Contains(role.Id))
                .Select(role => new UserRoleRecord
                {
                    UserId = user.Id,
                    RoleId = role.Id
                })
                .ToArray();

            if (rolesToAdd.Length > 0)
            {
                dbContext.UserRoles.AddRange(rolesToAdd);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildUserResponseAsync(dbContext, user, cancellationToken);
    }

    public async Task<IEnumerable<RoleResponse>> GetRolesForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!IsAvailable || userId == Guid.Empty)
        {
            return [];
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var roles = await dbContext.UserRoles
            .AsNoTracking()
            .Where(entity => entity.UserId == userId)
            .Join(
                dbContext.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => role)
            .OrderBy(role => role.Name)
            .Select(role => new RoleResponse(role.Id, role.Name))
            .ToListAsync(cancellationToken);

        return roles;
    }

    private string BuildToken(UserRecord user, IReadOnlyList<string> roleNames)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email)
        };

        foreach (var role in roleNames)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.TokenLifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static List<string> NormalizeRoleNames(IReadOnlyList<string>? roles)
    {
        if (roles is null || roles.Count == 0)
        {
            return [];
        }

        return roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<List<RoleRecord>> LoadRolesAsync(
        NatureProtectorControlDbContext dbContext,
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken)
    {
        if (roleNames.Count == 0)
        {
            return [];
        }

        var allRoles = await dbContext.Roles
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return allRoles
            .Where(role => roleNames.Contains(role.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private static async Task<IReadOnlyList<string>> LoadRoleNamesAsync(
        NatureProtectorControlDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.UserRoles
            .AsNoTracking()
            .Where(entity => entity.UserId == userId)
            .Join(
                dbContext.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => role.Name)
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }

    private static async Task<UserResponse> BuildUserResponseAsync(
        NatureProtectorControlDbContext dbContext,
        UserRecord user,
        CancellationToken cancellationToken)
    {
        var roleNames = await LoadRoleNamesAsync(dbContext, user.Id, cancellationToken);
        return new UserResponse(
            user.Id,
            user.Username,
            user.Email,
            roleNames);
    }

    private static async Task<UsersWithRoleResponse> BuildUsersWithRoleResponseAsync(
        NatureProtectorControlDbContext dbContext,
        RoleRecord role,
        CancellationToken cancellationToken)
    {
        var users = await dbContext.UserRoles
            .AsNoTracking()
            .Where(entity => entity.RoleId == role.Id)
            .Join(
                dbContext.Users.AsNoTracking(),
                userRole => userRole.UserId,
                user => user.Id,
                (userRole, user) => new UserSummaryResponse(
                    user.Id,
                    user.Username,
                    user.Email))
            .OrderBy(user => user.Username)
            .ToListAsync(cancellationToken);

        return new UsersWithRoleResponse(role.Id, role.Name, users);
    }
}
