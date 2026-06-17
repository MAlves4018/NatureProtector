using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using NatureProtector.Backoffice.Api.UserPlane.Contracts;
using NatureProtector.Backoffice.Api.UserPlane.Services;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class JwtAuthenticationTests
{
    private const string Issuer = "NatureProtector.Tests";
    private const string Audience = "NatureProtector.Backoffice.Tests";
    private const string SigningKey = "nature-protector-tests-signing-key-32";
    private const string OtherSigningKey = "nature-protector-tests-other-sign-key!";
    private static readonly Guid FirstUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondUserId = Guid.Parse("10000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task ProtectedEndpoint_ReturnsUnauthorized_WithoutToken()
    {
        await using var factory = new JwtTestFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/control/runtime/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_AcceptsValidJwtWithRequiredRole()
    {
        await using var factory = new JwtTestFactory();
        using var client = factory.CreateClient();
        client.SetBearerToken(CreateToken(FirstUserId, "sim.user", "sim@example.local", ["Sim"]));

        using var response = await client.GetAsync("/api/control/runtime/summary");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_ReturnsUnauthorized_ForExpiredJwt()
    {
        await using var factory = new JwtTestFactory();
        using var client = factory.CreateClient();
        client.SetBearerToken(CreateToken(
            FirstUserId,
            "sim.user",
            "sim@example.local",
            ["Sim"],
            notBefore: DateTime.UtcNow.AddMinutes(-30),
            expires: DateTime.UtcNow.AddMinutes(-10)));

        using var response = await client.GetAsync("/api/control/runtime/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("invalid-signature")]
    [InlineData("invalid-issuer")]
    [InlineData("invalid-audience")]
    public async Task ProtectedEndpoint_ReturnsUnauthorized_ForInvalidJwt(string invalidCase)
    {
        await using var factory = new JwtTestFactory();
        using var client = factory.CreateClient();
        var token = invalidCase switch
        {
            "invalid-signature" => CreateToken(
                FirstUserId,
                "sim.user",
                "sim@example.local",
                ["Sim"],
                signingKey: OtherSigningKey),
            "invalid-issuer" => CreateToken(
                FirstUserId,
                "sim.user",
                "sim@example.local",
                ["Sim"],
                issuer: "NatureProtector.WrongIssuer"),
            "invalid-audience" => CreateToken(
                FirstUserId,
                "sim.user",
                "sim@example.local",
                ["Sim"],
                audience: "NatureProtector.WrongAudience"),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidCase), invalidCase, null)
        };
        client.SetBearerToken(token);

        using var response = await client.GetAsync("/api/control/runtime/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RoleProtectedEndpoint_ReturnsForbidden_WhenRoleIsMissing()
    {
        await using var factory = new JwtTestFactory();
        using var client = factory.CreateClient();
        client.SetBearerToken(CreateToken(FirstUserId, "reader.user", "reader@example.local", []));

        using var response = await client.GetAsync("/api/control/runtime/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RoleProtectedEndpoint_ReturnsForbidden_WhenRoleIsDifferent()
    {
        await using var factory = new JwtTestFactory();
        using var client = factory.CreateClient();
        client.SetBearerToken(CreateToken(FirstUserId, "reviewer.user", "reviewer@example.local", ["Reviewer"]));

        using var response = await client.GetAsync("/api/control/runtime/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RoleProtectedEndpoint_AcceptsJwtWithMultipleRoles_WhenOneRoleMatches()
    {
        await using var factory = new JwtTestFactory();
        using var client = factory.CreateClient();
        client.SetBearerToken(CreateToken(FirstUserId, "pipeline.user", "pipeline@example.local", ["Reviewer", "Pipeline"]));

        using var response = await client.GetAsync("/api/control/runtime/summary");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_UsesDistinctJwtUsers()
    {
        await using var factory = new JwtTestFactory();
        using var client = factory.CreateClient();

        client.SetBearerToken(CreateToken(FirstUserId, "first.user", "first@example.local", ["Admin"]));
        using var firstResponse = await client.GetAsync("/api/users-roles/me");
        var firstUser = await firstResponse.Content.ReadFromJsonAsync<UserResponse>();

        client.SetBearerToken(CreateToken(SecondUserId, "second.user", "second@example.local", ["Admin"]));
        using var secondResponse = await client.GetAsync("/api/users-roles/me");
        var secondUser = await secondResponse.Content.ReadFromJsonAsync<UserResponse>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(firstUser);
        Assert.NotNull(secondUser);
        Assert.Equal(FirstUserId, firstUser.Id);
        Assert.Equal(SecondUserId, secondUser.Id);
        Assert.Equal("first.user", firstUser.Username);
        Assert.Equal("second.user", secondUser.Username);
    }

    private static string CreateToken(
        Guid userId,
        string username,
        string email,
        IReadOnlyList<string> roles,
        string issuer = Issuer,
        string audience = Audience,
        string signingKey = SigningKey,
        DateTime? notBefore = null,
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
            notBefore: notBefore ?? now.AddMinutes(-1),
            expires: expires ?? now.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class JwtTestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BackofficeApi:ControlPlaneEnabled", "false");
            builder.UseSetting("Jwt:Issuer", Issuer);
            builder.UseSetting("Jwt:Audience", Audience);
            builder.UseSetting("Jwt:SigningKey", SigningKey);
            builder.UseSetting("Jwt:TokenLifetimeMinutes", "30");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BackofficeApi:ControlPlaneEnabled"] = "false",
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:SigningKey"] = SigningKey,
                    ["Jwt:TokenLifetimeMinutes"] = "30"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRolePlaneService>();
                services.AddSingleton<IUserRolePlaneService, JwtEchoUserRolePlaneService>();
            });
        }
    }

    private sealed class JwtEchoUserRolePlaneService : IUserRolePlaneService
    {
        public bool IsAvailable => true;

        public string AvailabilityMessage => "JWT echo user plane available for authentication tests.";

        public Task<UserResponse?> GetCurrentUserAsync(string? authorizationHeader, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(authorizationHeader) ||
                !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<UserResponse?>(null);
            }

            var token = authorizationHeader["Bearer ".Length..].Trim();
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var userId = ReadGuidClaim(jwt, ClaimTypes.NameIdentifier, JwtRegisteredClaimNames.Sub, "nameid");
            var username = ReadClaim(jwt, ClaimTypes.Name, "unique_name", "name") ?? "unknown";
            var email = ReadClaim(jwt, ClaimTypes.Email, JwtRegisteredClaimNames.Email, "email") ?? "unknown@example.local";
            var roles = jwt.Claims
                .Where(claim => claim.Type is ClaimTypes.Role or "role" or "roles")
                .Select(claim => claim.Value)
                .Order(StringComparer.Ordinal)
                .ToArray();

            return Task.FromResult<UserResponse?>(new UserResponse(userId, username, email, roles));
        }

        public Task<UserResponse?> CreateUserAsync(UserRequest request, CancellationToken cancellationToken)
            => Task.FromResult<UserResponse?>(null);

        public Task<UserResponse?> UpdateUserAsync(Guid userId, UserRequest request, CancellationToken cancellationToken)
            => Task.FromResult<UserResponse?>(null);

        public Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task<UserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<UserResponse?>(null);

        public Task<RoleResponse?> CreateRoleAsync(string roleName, CancellationToken cancellationToken)
            => Task.FromResult<RoleResponse?>(null);

        public Task<RoleResponse?> UpdateRoleAsync(short roleId, string newRoleName, CancellationToken cancellationToken)
            => Task.FromResult<RoleResponse?>(null);

        public Task<bool> DeleteRoleAsync(short roleId, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task<RoleResponse?> GetRoleAsync(short roleId, CancellationToken cancellationToken)
            => Task.FromResult<RoleResponse?>(null);

        public Task<UserRoleResponse?> AddRoleToUserAsync(Guid userId, short roleId, CancellationToken cancellationToken)
            => Task.FromResult<UserRoleResponse?>(null);

        public Task<UserResponse?> RemoveRoleFromUserAsync(Guid userId, short roleId, CancellationToken cancellationToken)
            => Task.FromResult<UserResponse?>(null);

        public Task<IEnumerable<UserResponse>> GetUsersInRoleAsync(short roleId, CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<UserResponse>>([]);

        public Task<bool> CheckUserRoleAsync(Guid userId, short roleId, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
            => Task.FromResult<LoginResponse?>(null);

        public Task<bool> LogoutAsync(CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task<IEnumerable<RoleResponse>> GetRolesForUserAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<RoleResponse>>([]);

        private static Guid ReadGuidClaim(JwtSecurityToken jwt, params string[] claimTypes)
        {
            var value = ReadClaim(jwt, claimTypes);
            return Guid.TryParse(value, out var userId)
                ? userId
                : Guid.Empty;
        }

        private static string? ReadClaim(JwtSecurityToken jwt, params string[] claimTypes)
            => jwt.Claims.FirstOrDefault(claim => claimTypes.Contains(claim.Type, StringComparer.Ordinal))?.Value;
    }
}

internal static class JwtAuthenticationTestHttpClientExtensions
{
    public static void SetBearerToken(this HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
