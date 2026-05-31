namespace NatureProtector.Backoffice.Api.UserPlane.Contracts;

// Requests ----------------------------------------------
public sealed record UserRequest(
    string Username,
    string Password,
    string FullName,
    string Email,
    string Organization,
    string[]? Roles);

public sealed record LoginRequest(
    string UsernameOrEmail,
    string Password);

public sealed record RoleRequest(
    string Name);

//Responses ----------------------------------------------
public sealed record UserResponse(
    Guid Id,
    string Username,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles);

public sealed record LoginResponse(
    Guid UserId,
    string Username,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles,
    string Token);

public sealed record RoleResponse(
    short Id,
    string Name);

public sealed record UserSummaryResponse(
    Guid Id,
    string Username,
    string FullName,
    string Email);

public sealed record UserRoleResponse(
    short Id,
    string Name,
    Guid UserId);

public sealed record UsersWithRoleResponse(
    short RoleId,
    string RoleName,
    IReadOnlyList<UserSummaryResponse> Users);