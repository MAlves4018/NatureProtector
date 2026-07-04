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
    public const string Sim = "Sim";
    public const string Pipeline = "Pipeline";
    public const string QA = "QA";
    public const string Operations = "Operations";
    public const string ReleaseApprover = "ReleaseApprover";
    public const short AdminId = 1;
    public const short SimId = 2;
    public const short PipelineId = 3;
    public const short QAId = 4;
    public const short OperationsId = 5;
    public const short ReleaseApproverId = 6;

    public short Id { get; init; }
    public string Name { get; set; } = string.Empty;
}

public sealed class UserRoleRecord
{
    public Guid UserId { get; set; }
    public short RoleId { get; set; }
    public UserRecord? User { get; set; }
}