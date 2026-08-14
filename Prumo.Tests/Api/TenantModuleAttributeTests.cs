using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Prumo.Api.Attributes;
using Prumo.Application.Services;
using Prumo.Domain.Common;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Multitenancy;
using System.Security.Claims;

namespace Prumo.Tests.Api
{
    public class TenantModuleAttributeTests
    {
        private const string Code = "HR.Employees";

        private static AuthorizationFilterContext BuildContext(
            Guid? userId,
            Guid? claimTenantId,
            Guid? routeTenantId,
            string method,
            ITenantService tenantService,
            IResourcePermissionService permissions,
            ITenantContext tenantContext)
        {
            var claims = new List<Claim>();
            if (userId.HasValue)
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            if (claimTenantId.HasValue)
                claims.Add(new Claim("tenant_id", claimTenantId.Value.ToString()));

            var services = new ServiceCollection();
            services.AddSingleton(tenantService);
            services.AddSingleton(permissions);
            services.AddSingleton(tenantContext);

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
                RequestServices = services.BuildServiceProvider()
            };
            httpContext.Request.Method = method;

            var routeData = new RouteData();
            if (routeTenantId.HasValue)
                routeData.Values["tenantId"] = routeTenantId.Value.ToString();

            var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
            return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        }

        private static (ITenantService, IResourcePermissionService) Allowing(
            Guid tenantId, Guid userId, TenantRole role = TenantRole.Member)
        {
            var tenants = Substitute.For<ITenantService>();
            tenants.GetUserRoleAsync(tenantId, userId, Arg.Any<CancellationToken>())
                   .Returns(Task.FromResult<TenantRole?>(role));

            var permissions = Substitute.For<IResourcePermissionService>();
            permissions.UserHasAccessAsync(userId, Code, Arg.Any<PermissionLevel>())
                       .Returns(Task.FromResult(true));

            return (tenants, permissions);
        }

        [Fact]
        public async Task Member_with_matching_route_and_claim_is_allowed()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var (tenants, permissions) = Allowing(tenantId, userId);
            var tenantContext = new TenantContext();

            var ctx = BuildContext(userId, tenantId, tenantId, "GET", tenants, permissions, tenantContext);
            await new TenantModuleAttribute(Code).OnAuthorizationAsync(ctx);

            Assert.Null(ctx.Result);
            Assert.Equal(tenantId, tenantContext.TenantId);
            Assert.Equal(TenantRole.Member, ctx.HttpContext.Items[TenantModuleAttribute.TenantRoleItemKey]);
        }

        [Fact]
        public async Task Claim_tenant_different_from_route_is_forbidden()
        {
            var routeTenant = Guid.NewGuid();
            var claimTenant = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var (tenants, permissions) = Allowing(routeTenant, userId);

            var ctx = BuildContext(userId, claimTenant, routeTenant, "GET", tenants, permissions, new TenantContext());
            await new TenantModuleAttribute(Code).OnAuthorizationAsync(ctx);

            Assert.IsType<ForbidResult>(ctx.Result);
        }

        [Fact]
        public async Task Missing_tenant_claim_is_forbidden()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var (tenants, permissions) = Allowing(tenantId, userId);

            var ctx = BuildContext(userId, null, tenantId, "GET", tenants, permissions, new TenantContext());
            await new TenantModuleAttribute(Code).OnAuthorizationAsync(ctx);

            Assert.IsType<ForbidResult>(ctx.Result);
        }

        [Fact]
        public async Task Non_member_is_forbidden()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var tenants = Substitute.For<ITenantService>();
            tenants.GetUserRoleAsync(tenantId, userId, Arg.Any<CancellationToken>())
                   .Returns(Task.FromResult<TenantRole?>(null));
            var permissions = Substitute.For<IResourcePermissionService>();
            permissions.UserHasAccessAsync(userId, Code, Arg.Any<PermissionLevel>())
                       .Returns(Task.FromResult(true));

            var ctx = BuildContext(userId, tenantId, tenantId, "GET", tenants, permissions, new TenantContext());
            await new TenantModuleAttribute(Code).OnAuthorizationAsync(ctx);

            Assert.IsType<ForbidResult>(ctx.Result);
        }

        [Fact]
        public async Task Member_without_resource_permission_is_forbidden()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var tenants = Substitute.For<ITenantService>();
            tenants.GetUserRoleAsync(tenantId, userId, Arg.Any<CancellationToken>())
                   .Returns(Task.FromResult<TenantRole?>(TenantRole.Member));
            var permissions = Substitute.For<IResourcePermissionService>();
            permissions.UserHasAccessAsync(userId, Code, Arg.Any<PermissionLevel>())
                       .Returns(Task.FromResult(false));

            var ctx = BuildContext(userId, tenantId, tenantId, "GET", tenants, permissions, new TenantContext());
            await new TenantModuleAttribute(Code).OnAuthorizationAsync(ctx);

            Assert.IsType<ForbidResult>(ctx.Result);
        }

        [Fact]
        public async Task Unauthenticated_user_is_unauthorized()
        {
            var tenantId = Guid.NewGuid();
            var tenants = Substitute.For<ITenantService>();
            var permissions = Substitute.For<IResourcePermissionService>();

            var ctx = BuildContext(null, tenantId, tenantId, "GET", tenants, permissions, new TenantContext());
            await new TenantModuleAttribute(Code).OnAuthorizationAsync(ctx);

            Assert.IsType<UnauthorizedObjectResult>(ctx.Result);
        }

        [Theory]
        [InlineData("GET", PermissionLevel.Read)]
        [InlineData("POST", PermissionLevel.Write)]
        [InlineData("PUT", PermissionLevel.Write)]
        [InlineData("PATCH", PermissionLevel.Write)]
        [InlineData("DELETE", PermissionLevel.Full)]
        public async Task Required_level_is_derived_from_the_http_verb(string method, PermissionLevel expected)
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var (tenants, permissions) = Allowing(tenantId, userId);

            var ctx = BuildContext(userId, tenantId, tenantId, method, tenants, permissions, new TenantContext());
            await new TenantModuleAttribute(Code).OnAuthorizationAsync(ctx);

            await permissions.Received(1).UserHasAccessAsync(userId, Code, expected);
        }

        [Fact]
        public async Task Explicit_level_overrides_the_verb()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var (tenants, permissions) = Allowing(tenantId, userId);

            var ctx = BuildContext(userId, tenantId, tenantId, "GET", tenants, permissions, new TenantContext());
            await new TenantModuleAttribute(Code, PermissionLevel.Full).OnAuthorizationAsync(ctx);

            await permissions.Received(1).UserHasAccessAsync(userId, Code, PermissionLevel.Full);
        }
    }
}
