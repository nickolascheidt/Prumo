using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Prumo.Api.Controllers;
using Prumo.Application.Services;
using System.Security.Claims;

namespace Prumo.Tests.Api
{
    /// <summary>
    /// O TenantsController é isento do [TenantModule] (gerencia associação, seria
    /// circular), então cada action com {tenantId} prova a associação à mão. Este teste
    /// existe porque uma delas não provava.
    /// </summary>
    public class TenantsControllerTests
    {
        private static TenantsController MakeController(Guid userId, ITenantService tenants) =>
            new(tenants, Substitute.For<IAuthService>(), Substitute.For<ITenantRoleService>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"))
                    }
                }
            };

        [Fact]
        public async Task Assignable_roles_of_a_tenant_the_caller_does_not_belong_to_is_forbidden()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var tenants = Substitute.For<ITenantService>();
            tenants.IsMemberAsync(tenantId, userId, Arg.Any<CancellationToken>()).Returns(false);
            var roleAdmin = Substitute.For<ITenantRoleAdminService>();

            var result = await MakeController(userId, tenants)
                .GetAssignableRoles(tenantId, roleAdmin, CancellationToken.None);

            Assert.IsType<ForbidResult>(result.Result);
            await roleAdmin.DidNotReceive().GetAssignableRoleNamesAsync(tenantId, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Assignable_roles_are_returned_to_a_member()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var tenants = Substitute.For<ITenantService>();
            tenants.IsMemberAsync(tenantId, userId, Arg.Any<CancellationToken>()).Returns(true);
            var roleAdmin = Substitute.For<ITenantRoleAdminService>();
            roleAdmin.GetAssignableRoleNamesAsync(tenantId, Arg.Any<CancellationToken>())
                .Returns(new[] { "HR" });

            var result = await MakeController(userId, tenants)
                .GetAssignableRoles(tenantId, roleAdmin, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(new[] { "HR" }, Assert.IsAssignableFrom<IReadOnlyList<string>>(ok.Value));
        }
    }
}
