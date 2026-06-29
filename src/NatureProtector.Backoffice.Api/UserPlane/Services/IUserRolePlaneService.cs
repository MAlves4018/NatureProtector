using NatureProtector.Backoffice.Api.UserPlane.Contracts;

namespace NatureProtector.Backoffice.Api.UserPlane.Services;

public interface IUserRolePlaneService
{
    bool IsAvailable { get; }
    string AvailabilityMessage { get; }

    Task<UserResponse?> CreateUserAsync(UserRequest request, CancellationToken cancellationToken);
    Task<UserResponse?> UpdateUserAsync(Guid userId, UserRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserResponse>> ListUsersAsync(CancellationToken cancellationToken);

    Task<RoleResponse?> CreateRoleAsync(string roleName, CancellationToken cancellationToken);
    Task<RoleResponse?> UpdateRoleAsync(short roleId, string newRoleName, CancellationToken cancellationToken);
    Task<bool> DeleteRoleAsync(short roleId, CancellationToken cancellationToken);
    Task<RoleResponse?> GetRoleAsync(short roleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RoleResponse>> ListRolesAsync(CancellationToken cancellationToken);

    Task<UserRoleResponse?> AddRoleToUserAsync(Guid userId, short roleId, CancellationToken cancellationToken);
    Task<UserResponse?> RemoveRoleFromUserAsync(Guid userId, short roleId, CancellationToken cancellationToken);
    Task<IEnumerable<UserResponse>> GetUsersInRoleAsync(short roleId, CancellationToken cancellationToken);
    Task<bool> CheckUserRoleAsync(Guid userId, short roleId, CancellationToken cancellationToken);

    Task<IEnumerable<RoleResponse>> GetRolesForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<bool> LogoutAsync(CancellationToken cancellationToken);

    Task<UserResponse?> GetCurrentUserAsync(string? authorizationHeader, CancellationToken cancellationToken);

    
}