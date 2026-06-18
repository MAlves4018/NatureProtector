using NatureProtector.Backoffice.Api.UserPlane.Contracts;
using NatureProtector.Backoffice.Api.UserPlane.Services;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class UnavailableUserRolePlaneServiceTests
{
    [Fact]
    public async Task UnavailableService_ReportsUnavailability_AndReturnsNeutralResults()
    {
        var service = new UnavailableUserRolePlaneService("User plane disabled for tests.");
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        const short roleId = 7;
        var userRequest = new UserRequest(
            "operator",
            "not-used",
            "operator@example.test",
            "NatureProtector",
            ["Admin"]);
        var loginRequest = new LoginRequest("operator", "not-used");

        Assert.False(service.IsAvailable);
        Assert.Equal("User plane disabled for tests.", service.AvailabilityMessage);
        Assert.Null(await service.CreateUserAsync(userRequest, CancellationToken.None));
        Assert.Null(await service.UpdateUserAsync(userId, userRequest, CancellationToken.None));
        Assert.False(await service.DeleteUserAsync(userId, CancellationToken.None));
        Assert.Null(await service.GetUserAsync(userId, CancellationToken.None));
        Assert.Null(await service.CreateRoleAsync("Admin", CancellationToken.None));
        Assert.Null(await service.UpdateRoleAsync(roleId, "Admin", CancellationToken.None));
        Assert.False(await service.DeleteRoleAsync(roleId, CancellationToken.None));
        Assert.Null(await service.GetRoleAsync(roleId, CancellationToken.None));
        Assert.Null(await service.AddRoleToUserAsync(userId, roleId, CancellationToken.None));
        Assert.Null(await service.RemoveRoleFromUserAsync(userId, roleId, CancellationToken.None));
        Assert.Empty(await service.GetUsersInRoleAsync(roleId, CancellationToken.None));
        Assert.False(await service.CheckUserRoleAsync(userId, roleId, CancellationToken.None));
        Assert.Empty(await service.GetRolesForUserAsync(userId, CancellationToken.None));
        Assert.Null(await service.LoginAsync(loginRequest, CancellationToken.None));
        Assert.False(await service.LogoutAsync(CancellationToken.None));
        Assert.Null(await service.GetCurrentUserAsync("Bearer test", CancellationToken.None));
    }
}
