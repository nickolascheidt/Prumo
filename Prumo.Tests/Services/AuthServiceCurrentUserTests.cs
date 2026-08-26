using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Prumo.Application.Services;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Authorization;
using Xunit;

namespace Prumo.Tests.Services
{
    /// <summary>
    /// Covers the read model behind <c>GET /api/auth/me</c>. The JWT path resolves roles
    /// per tenant (global ∪ per-tenant feature roles); this endpoint must agree with it,
    /// otherwise the SPA bootstrap (step 2 of login) sees no roles for a tenant member.
    /// </summary>
    public class AuthServiceCurrentUserTests
    {
        private static UserManager<ApplicationUser> MakeUserManager()
        {
            var store = Substitute.For<IUserStore<ApplicationUser>>();
            return Substitute.For<UserManager<ApplicationUser>>(
                store, null, null, null, null, null, null, null, null);
        }

        private static SignInManager<ApplicationUser> MakeSignInManager(
            UserManager<ApplicationUser> userManager)
        {
            var contextAccessor = Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var claimsFactory = Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>();
            return Substitute.For<SignInManager<ApplicationUser>>(
                userManager, contextAccessor, claimsFactory, null, null, null, null);
        }

        private sealed class Fixture
        {
            public required AuthService Service { get; init; }
            public required ApplicationUser User { get; init; }
        }

        private static Fixture MakeFixture(
            IReadOnlyList<string> globalRoles,
            IReadOnlyList<string> tenantFeatureRoles,
            Guid tenantId)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = "member@test.com",
                UserName = "member@test.com",
                FullName = "Test Member",
                IsActive = true
            };

            var userManager = MakeUserManager();
            userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
            userManager.GetRolesAsync(user).Returns(globalRoles.ToList() as IList<string>);

            var tenantRoles = Substitute.For<ITenantRoleService>();
            tenantRoles.GetEffectiveRoleNamesAsync(
                    user.Id, tenantId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(globalRoles.Concat(tenantFeatureRoles).Distinct().ToList());

            var service = new AuthService(
                userManager,
                MakeSignInManager(userManager),
                Substitute.For<IConfiguration>(),
                Substitute.For<ITenantService>(),
                tenantRoles);

            return new Fixture { Service = service, User = user };
        }

        [Fact]
        public async Task GetUserByIdAsync_WithTenant_IncludesPerTenantFeatureRoles()
        {
            var tenantId = Guid.NewGuid();
            var f = MakeFixture(globalRoles: Array.Empty<string>(),
                                tenantFeatureRoles: new[] { "RH" },
                                tenantId: tenantId);

            var dto = await f.Service.GetUserByIdAsync(f.User.Id, tenantId);

            Assert.NotNull(dto);
            Assert.Contains("RH", dto!.Roles);
        }

        [Fact]
        public async Task GetUserByIdAsync_WithDifferentTenant_ExcludesRolesGrantedElsewhere()
        {
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();

            // Fixture grants RH only for tenantA; tenantB resolves to no feature roles.
            var f = MakeFixture(globalRoles: Array.Empty<string>(),
                                tenantFeatureRoles: new[] { "RH" },
                                tenantId: tenantA);

            var dto = await f.Service.GetUserByIdAsync(f.User.Id, tenantB);

            Assert.NotNull(dto);
            Assert.DoesNotContain("RH", dto!.Roles);
        }

        [Fact]
        public async Task GetUserByIdAsync_WithoutTenant_ReturnsGlobalRolesOnly()
        {
            var tenantId = Guid.NewGuid();
            var f = MakeFixture(globalRoles: new[] { "Administrador" },
                                tenantFeatureRoles: new[] { "RH" },
                                tenantId: tenantId);

            var dto = await f.Service.GetUserByIdAsync(f.User.Id, tenantId: null);

            Assert.NotNull(dto);
            Assert.Contains("Administrador", dto!.Roles);
            Assert.DoesNotContain("RH", dto.Roles);
        }
    }
}
