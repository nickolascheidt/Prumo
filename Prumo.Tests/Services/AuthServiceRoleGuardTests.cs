using Prumo.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Prumo.Application.Services;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Authorization;

namespace Prumo.Tests.Services;

public class AuthServiceRoleGuardTests
{
    private static UserManager<ApplicationUser> MakeUserManager()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        return Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
    }

    private static AuthService MakeSut(UserManager<ApplicationUser> userManager)
    {
        var signInManager = Substitute.For<SignInManager<ApplicationUser>>(
            userManager,
            Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null, null, null, null);

        return new AuthService(
            userManager,
            signInManager,
            Substitute.For<IConfiguration>(),
            Substitute.For<ITenantService>(),
            Substitute.For<ITenantRoleService>(),
            Substitute.For<INotificationPublisher>());
    }

    [Fact]
    public async Task AssignRoleToUserAsync_WhenRoleIsNotMaster_ThrowsInvalidOperation()
    {
        var userManager = MakeUserManager();
        var sut = MakeSut(userManager);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.AssignRoleToUserAsync(Guid.NewGuid(), "HR"));
    }

    [Fact]
    public async Task AssignRoleToUserAsync_WhenRoleIsMaster_PassesGuardAndThrowsNotFound()
    {
        var userManager = MakeUserManager();
        userManager.FindByIdAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        var sut = MakeSut(userManager);

        // Master role passes the guard and proceeds to FindByIdAsync, which returns null.
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.AssignRoleToUserAsync(Guid.NewGuid(), "Administrator"));
    }
}
