using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Prumo.Application.Services;
using Prumo.Domain.Common;
using Prumo.Domain.Enums;
using System.Security.Claims;

namespace Prumo.Api.Attributes
{
    /// <summary>
    /// The single gate for tenant-routed controllers. Proves membership against the
    /// ROUTE's tenantId, rejects a mismatch with the claim, fills the TenantContext from
    /// the route and only then checks the resource permission — in that order, because
    /// the resource check resolves roles using the TenantContext.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class TenantModuleAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public const string TenantRoleItemKey = "Prumo.TenantRole";

        private const string TenantIdClaim = "tenant_id";
        private const string TenantIdRouteKey = "tenantId";

        public string ResourceCode { get; }
        private readonly PermissionLevel? _explicitLevel;

        public TenantModuleAttribute(string resourceCode)
        {
            ResourceCode = resourceCode;
        }

        public TenantModuleAttribute(string resourceCode, PermissionLevel level)
        {
            ResourceCode = resourceCode;
            _explicitLevel = level;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var http = context.HttpContext;

            // 1. Who the user is — the only trusted data, because it comes from the signed token.
            var userIdRaw = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdRaw, out var userId))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "User not authenticated" });
                return;
            }

            // 2. Which tenant is being requested. Untrusted input: the thing to prove.
            if (!context.RouteData.Values.TryGetValue(TenantIdRouteKey, out var routeRaw)
                || !Guid.TryParse(routeRaw?.ToString(), out var routeTenantId))
            {
                context.Result = new ForbidResult();
                return;
            }

            // 3. The tenant selected in the token must exist and match the route.
            var claimRaw = http.User.FindFirst(TenantIdClaim)?.Value;
            if (!Guid.TryParse(claimRaw, out var claimTenantId) || claimTenantId != routeTenantId)
            {
                context.Result = new ForbidResult();
                return;
            }

            // 4. Membership proof against the database.
            var tenantService = http.RequestServices.GetRequiredService<ITenantService>();
            var role = await tenantService.GetUserRoleAsync(routeTenantId, userId, http.RequestAborted);
            if (role is null)
            {
                context.Result = new ForbidResult();
                return;
            }

            http.Items[TenantRoleItemKey] = role.Value;

            // 5. The TenantContext now comes from the route. From here on context and route
            //    are equal by construction — that is what makes step 6 correct.
            http.RequestServices.GetRequiredService<ITenantContext>().SetTenant(routeTenantId);

            // 6. Resource permission, resolved in the right tenant.
            var level = _explicitLevel ?? LevelForMethod(http.Request.Method);
            var permissions = http.RequestServices.GetRequiredService<IResourcePermissionService>();
            if (!await permissions.UserHasAccessAsync(userId, ResourceCode, level))
            {
                context.Result = new ForbidResult();
            }
        }

        internal static PermissionLevel LevelForMethod(string method) =>
            method.ToUpperInvariant() switch
            {
                "GET" or "HEAD" or "OPTIONS" => PermissionLevel.Read,
                "POST" or "PUT" or "PATCH" => PermissionLevel.Write,
                _ => PermissionLevel.Full
            };
    }
}
