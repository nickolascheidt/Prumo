using Prumo.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Prumo.Api.Authorization
{
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            var userPermissions = context.User.FindAll("permission")
                .Select(c => c.Value)
                .ToList();

            if (Permissions.HasPermission(userPermissions, requirement.Permission))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
