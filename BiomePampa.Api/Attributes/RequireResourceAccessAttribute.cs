using BiomePampa.Application.Services;
using BiomePampa.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace BiomePampa.Api.Attributes
{
    /// <summary>
    /// Atributo para autorizar acesso baseado em recursos e níveis de permissão
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class RequireResourceAccessAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public string ResourceCode { get; }
        public PermissionLevel MinimumLevel { get; }

        public RequireResourceAccessAttribute(string resourceCode, PermissionLevel minimumLevel = PermissionLevel.Read)
        {
            ResourceCode = resourceCode;
            MinimumLevel = minimumLevel;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
            
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "User not authenticated" });
                return;
            }

            var permissionService = context.HttpContext.RequestServices
                .GetService(typeof(IResourcePermissionService)) as IResourcePermissionService;

            if (permissionService == null)
            {
                context.Result = new StatusCodeResult(500);
                return;
            }

            var hasAccess = await permissionService.UserHasAccessAsync(userId, ResourceCode, MinimumLevel);

            if (!hasAccess)
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
