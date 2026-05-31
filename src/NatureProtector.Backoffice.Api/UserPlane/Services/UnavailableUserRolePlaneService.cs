using NatureProtector.Backoffice.Api.UserPlane.Contracts;

namespace NatureProtector.Backoffice.Api.UserPlane.Services;

public sealed class UnavailableUserRolePlaneService : IUserRolePlaneService
{
    public UnavailableUserRolePlaneService(string availabilityMessage)
    {
        AvailabilityMessage = availabilityMessage;
    }

    public bool IsAvailable => false;

    public string AvailabilityMessage { get; }

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

    public Task<UserResponse?> GetCurrentUserAsync(string? authorizationHeader, CancellationToken cancellationToken)
        => Task.FromResult<UserResponse?>(null);
}
