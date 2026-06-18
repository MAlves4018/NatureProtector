using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NatureProtector.Backoffice.Api.Configuration;
using NatureProtector.Backoffice.Api.UserPlane.Contracts;
using NatureProtector.Backoffice.Api.UserPlane.Services;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Users;
using NatureProtector.IntegrationTests.TestInfrastructure;

namespace NatureProtector.IntegrationTests.UserPlane;

[Collection(DockerIntegrationCollection.Name)]
public sealed class PostgresUserRolePlaneServiceTests
{
    private const string Issuer = "NatureProtector.IntegrationTests";
    private const string Audience = "NatureProtector.Backoffice.IntegrationTests";
    private const string SigningKey = "nature-protector-integration-signing-key-32";

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task CreateUser_WithRoles_PersistsHashRolesLoginAndCurrentUser_OnRealPostgres()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        var service = CreateService(database);

        var created = await service.CreateUserAsync(
            new UserRequest(
                "owner-admin",
                "correct-password",
                "owner-admin@example.test",
                "NatureProtector",
                ["Admin", "Pipeline"]),
            CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(["Admin", "Pipeline"], created.Roles.OrderBy(role => role));

        await using (var dbContext = database.CreateDbContext())
        {
            var storedUser = await dbContext.Users.SingleAsync(user => user.Id == created.Id);
            Assert.NotEqual("correct-password", storedUser.PasswordHash);
            Assert.StartsWith("AQAAAA", storedUser.PasswordHash, StringComparison.Ordinal);

            var verification = new PasswordHasher<UserRecord>().VerifyHashedPassword(
                storedUser,
                storedUser.PasswordHash,
                "correct-password");
            Assert.NotEqual(PasswordVerificationResult.Failed, verification);

            var storedRoles = await dbContext.UserRoles
                .Where(userRole => userRole.UserId == created.Id)
                .Join(
                    dbContext.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (_, role) => role.Name)
                .OrderBy(role => role)
                .ToListAsync();
            Assert.Equal(["Admin", "Pipeline"], storedRoles);
        }

        var login = await service.LoginAsync(
            new LoginRequest("owner-admin@example.test", "correct-password"),
            CancellationToken.None);

        Assert.NotNull(login);
        Assert.Equal(created.Id, login.UserId);
        Assert.Equal(["Admin", "Pipeline"], login.Roles.OrderBy(role => role));
        Assert.False(string.IsNullOrWhiteSpace(login.Token));

        var current = await service.GetCurrentUserAsync($"Bearer {login.Token}", CancellationToken.None);
        Assert.NotNull(current);
        Assert.Equal(created.Id, current.Id);
        Assert.Equal(["Admin", "Pipeline"], current.Roles.OrderBy(role => role));

        Assert.Null(await service.LoginAsync(
            new LoginRequest("owner-admin", "wrong-password"),
            CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task CreateUser_RejectsDuplicatesInvalidInputAndMissingRoles_WithoutPartialRows()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        var service = CreateService(database);

        var created = await service.CreateUserAsync(
            NewUser("unique-user", "unique@example.test", ["Sim"]),
            CancellationToken.None);
        Assert.NotNull(created);

        Assert.Null(await service.CreateUserAsync(
            NewUser("unique-user", "other@example.test", ["Sim"]),
            CancellationToken.None));
        Assert.Null(await service.CreateUserAsync(
            NewUser("other-user", "unique@example.test", ["Sim"]),
            CancellationToken.None));
        Assert.Null(await service.CreateUserAsync(
            NewUser("missing-role-user", "missing-role@example.test", ["NoSuchRole"]),
            CancellationToken.None));
        Assert.Null(await service.CreateUserAsync(
            new UserRequest("", "password", "blank@example.test", "NatureProtector", ["Sim"]),
            CancellationToken.None));

        await using var dbContext = database.CreateDbContext();
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email.EndsWith("@example.test")));
        Assert.False(await dbContext.Users.AnyAsync(user => user.Username == "missing-role-user"));
        Assert.False(await dbContext.UserRoles.AnyAsync(userRole => userRole.UserId != created.Id));
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task RoleAssignmentRemovalAndUsersByRole_AreIdempotentAndRejectMissingEntities()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        var service = CreateService(database);
        var roleIds = await LoadSeededRoleIdsAsync(database);
        var user = await service.CreateUserAsync(
            NewUser("role-target", "role-target@example.test", null),
            CancellationToken.None);
        Assert.NotNull(user);

        var simAssignment = await service.AddRoleToUserAsync(user.Id, roleIds["Sim"], CancellationToken.None);
        var duplicateSimAssignment = await service.AddRoleToUserAsync(user.Id, roleIds["Sim"], CancellationToken.None);
        var pipelineAssignment = await service.AddRoleToUserAsync(user.Id, roleIds["Pipeline"], CancellationToken.None);

        Assert.NotNull(simAssignment);
        Assert.NotNull(duplicateSimAssignment);
        Assert.NotNull(pipelineAssignment);
        Assert.True(await service.CheckUserRoleAsync(user.Id, roleIds["Sim"], CancellationToken.None));
        Assert.True(await service.CheckUserRoleAsync(user.Id, roleIds["Pipeline"], CancellationToken.None));

        var roles = await service.GetRolesForUserAsync(user.Id, CancellationToken.None);
        Assert.Equal(["Pipeline", "Sim"], roles.Select(role => role.Name).OrderBy(role => role));

        var simUsers = await service.GetUsersInRoleAsync(roleIds["Sim"], CancellationToken.None);
        Assert.Contains(simUsers, candidate => candidate.Id == user.Id);

        await using (var dbContext = database.CreateDbContext())
        {
            Assert.Equal(1, await dbContext.UserRoles.CountAsync(entity => entity.UserId == user.Id && entity.RoleId == roleIds["Sim"]));
        }

        var afterRemoval = await service.RemoveRoleFromUserAsync(user.Id, roleIds["Sim"], CancellationToken.None);
        Assert.NotNull(afterRemoval);
        Assert.Equal(["Pipeline"], afterRemoval.Roles);
        Assert.False(await service.CheckUserRoleAsync(user.Id, roleIds["Sim"], CancellationToken.None));

        Assert.Null(await service.AddRoleToUserAsync(Guid.NewGuid(), roleIds["Sim"], CancellationToken.None));
        Assert.Null(await service.AddRoleToUserAsync(user.Id, short.MaxValue, CancellationToken.None));
        Assert.Null(await service.RemoveRoleFromUserAsync(Guid.NewGuid(), roleIds["Pipeline"], CancellationToken.None));
        Assert.Empty(await service.GetRolesForUserAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Empty(await service.GetUsersInRoleAsync(short.MaxValue, CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task UpdateUser_InvalidRolesRollbackAndValidRolesReplaceWithoutPartialData()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        var service = CreateService(database);
        var user = await service.CreateUserAsync(
            NewUser("rollback-user", "rollback@example.test", ["Sim"]),
            CancellationToken.None);
        Assert.NotNull(user);

        var rejected = await service.UpdateUserAsync(
            user.Id,
            new UserRequest(
                "rollback-mutated",
                "new-password",
                "rollback-mutated@example.test",
                "Changed",
                ["NoSuchRole"]),
            CancellationToken.None);

        Assert.Null(rejected);
        var unchanged = await service.GetUserAsync(user.Id, CancellationToken.None);
        Assert.NotNull(unchanged);
        Assert.Equal("rollback-user", unchanged.Username);
        Assert.Equal("rollback@example.test", unchanged.Email);
        Assert.Equal(["Sim"], unchanged.Roles);

        var updated = await service.UpdateUserAsync(
            user.Id,
            new UserRequest(
                "rollback-user",
                "new-password",
                "rollback@example.test",
                "NatureProtector",
                ["Admin", "Pipeline"]),
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(["Admin", "Pipeline"], updated.Roles.OrderBy(role => role));
        Assert.NotNull(await service.LoginAsync(
            new LoginRequest("rollback-user", "new-password"),
            CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task ApiLoginJwtAndProtectedEndpoint_UseRealPostgresUserPlane()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        var service = CreateService(database);
        var admin = await service.CreateUserAsync(
            NewUser("api-admin", "api-admin@example.test", ["Admin"]),
            CancellationToken.None);
        var sim = await service.CreateUserAsync(
            NewUser("api-sim", "api-sim@example.test", ["Sim"]),
            CancellationToken.None);
        var pipeline = await service.CreateUserAsync(
            NewUser("api-pipeline", "api-pipeline@example.test", ["Pipeline"]),
            CancellationToken.None);
        var roleless = await service.CreateUserAsync(
            NewUser("api-roleless", "api-roleless@example.test", null),
            CancellationToken.None);
        Assert.NotNull(admin);
        Assert.NotNull(sim);
        Assert.NotNull(pipeline);
        Assert.NotNull(roleless);

        await using var factory = new RealPostgresApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        using (var anonymous = await client.GetAsync($"/api/users-roles/users/{admin.Id}"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        var adminLogin = await LoginAsync(client, "api-admin", "correct-password");
        client.SetBearerToken(adminLogin.Token);
        using (var adminResponse = await client.GetAsync($"/api/users-roles/users/{admin.Id}"))
        {
            Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
            var body = await adminResponse.Content.ReadFromJsonAsync<UserResponse>();
            Assert.Equal(admin.Id, body?.Id);
        }

        var simLogin = await LoginAsync(client, "api-sim", "correct-password");
        client.SetBearerToken(simLogin.Token);
        using (var simResponse = await client.GetAsync($"/api/users-roles/users/{admin.Id}"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, simResponse.StatusCode);
        }

        var pipelineLogin = await LoginAsync(client, "api-pipeline", "correct-password");
        client.SetBearerToken(pipelineLogin.Token);
        using (var pipelineResponse = await client.GetAsync("/api/users-roles/me"))
        {
            Assert.Equal(HttpStatusCode.OK, pipelineResponse.StatusCode);
            var body = await pipelineResponse.Content.ReadFromJsonAsync<UserResponse>();
            Assert.Equal(["Pipeline"], body?.Roles);
        }

        var rolelessLogin = await LoginAsync(client, "api-roleless", "correct-password");
        client.SetBearerToken(rolelessLogin.Token);
        using (var rolelessResponse = await client.GetAsync($"/api/users-roles/users/{admin.Id}"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, rolelessResponse.StatusCode);
        }

        foreach (var invalidToken in new[]
        {
            CreateToken(admin.Id, "api-admin", "api-admin@example.test", ["Admin"], expires: DateTime.UtcNow.AddMinutes(-1)),
            CreateToken(admin.Id, "api-admin", "api-admin@example.test", ["Admin"], signingKey: "wrong-nature-protector-integration-key-32"),
            CreateToken(admin.Id, "api-admin", "api-admin@example.test", ["Admin"], issuer: "WrongIssuer"),
            CreateToken(admin.Id, "api-admin", "api-admin@example.test", ["Admin"], audience: "WrongAudience")
        })
        {
            client.SetBearerToken(invalidToken);
            using var invalidResponse = await client.GetAsync($"/api/users-roles/users/{admin.Id}");
            Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
        }
    }

    private static PostgresUserRolePlaneService CreateService(TemporaryPostgresDatabase database)
    {
        return new PostgresUserRolePlaneService(
            database.CreateFactory(),
            new PasswordHasher<UserRecord>(),
            Options.Create(new JwtAuthenticationOptions
            {
                Issuer = Issuer,
                Audience = Audience,
                SigningKey = SigningKey,
                TokenLifetimeMinutes = 30
            }));
    }

    private static UserRequest NewUser(string username, string email, string[]? roles)
    {
        return new UserRequest(
            username,
            "correct-password",
            email,
            "NatureProtector",
            roles);
    }

    private static async Task<Dictionary<string, short>> LoadSeededRoleIdsAsync(TemporaryPostgresDatabase database)
    {
        await using var dbContext = database.CreateDbContext();
        return await dbContext.Roles.ToDictionaryAsync(role => role.Name, role => role.Id);
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client, string username, string password)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/users-roles/login",
            new LoginRequest(username, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        return login;
    }

    private static string CreateToken(
        Guid userId,
        string username,
        string email,
        IReadOnlyList<string> roles,
        string issuer = Issuer,
        string audience = Audience,
        string signingKey = SigningKey,
        DateTime? expires = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Email, email)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now.AddMinutes(-5),
            expires: expires ?? now.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class RealPostgresApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BackofficeApi:ControlPlaneEnabled"] = "true",
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:SigningKey"] = SigningKey,
                    ["Jwt:TokenLifetimeMinutes"] = "30"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextFactory<NatureProtectorControlDbContext>>();
                services.AddDbContextFactory<NatureProtectorControlDbContext>(
                    options => options.UseNpgsql(connectionString));
                services.Configure<JwtAuthenticationOptions>(options =>
                {
                    options.Issuer = Issuer;
                    options.Audience = Audience;
                    options.SigningKey = SigningKey;
                    options.TokenLifetimeMinutes = 30;
                });
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = Issuer,
                        ValidateAudience = true,
                        ValidAudience = Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                });
            });
        }
    }
}

internal static class OwnerAuditHttpClientExtensions
{
    public static void SetBearerToken(this HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
