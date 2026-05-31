using Microsoft.AspNetCore.Identity;

namespace NatureProtector.Infrastructure.Postgres.Users;

public sealed class UserRecord
{

    public const string AdminIdString = "00000000-0000-0000-0000-000000000001";
    public const string AdminUsername = "admin";
    public const string AdminEmail = "aaa@aaa.aaa";
    public const string AdminOrganization = "NatureProtector";

    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public List<UserRoleRecord> UserRoles { get; set; } = [];
}

public sealed class RoleRecord
{
    public const string Admin = "Admin";
    internal const string Sim = "Sim";
    internal const string Pipeline = "Pipeline";
    public const short AdminId = 1;
    internal const short SimId = 2;
    internal const short PipelineId = 3;

    public short Id { get; init; }
    public string Name { get; set; } = string.Empty;
}

public sealed class UserRoleRecord
{
    public Guid UserId { get; set; }
    public short RoleId { get; set; }
    public UserRecord? User { get; set; }
}