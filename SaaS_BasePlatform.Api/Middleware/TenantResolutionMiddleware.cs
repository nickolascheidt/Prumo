using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Common;

namespace SaaS_BasePlatform.Api.Middleware
{
    public class TenantResolutionMiddleware
    {
        public const string TenantHeader = "X-Tenant-Id";
        private const string TenantIdClaim = "tenant_id";

        private readonly RequestDelegate _next;

        public TenantResolutionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ITenantService tenantService)
        {
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var tenantClaim = context.User.FindFirst(TenantIdClaim)?.Value;
                if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out var claimTenantId))
                {
                    tenantContext.SetTenant(claimTenantId);
                }
                else if (context.Request.Headers.TryGetValue(TenantHeader, out var headerVal)
                         && Guid.TryParse(headerVal.ToString(), out var headerTenantId))
                {
                    var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (Guid.TryParse(userIdClaim, out var userId))
                    {
                        var isMember = await tenantService.IsMemberAsync(headerTenantId, userId);
                        if (isMember) tenantContext.SetTenant(headerTenantId);
                    }
                }
            }

            await _next(context);
        }
    }
}
