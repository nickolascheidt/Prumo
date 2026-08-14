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
    /// Gate único dos controllers roteados por tenant. Prova a associação contra o
    /// tenantId da ROTA, rejeita divergência com o claim, preenche o TenantContext a
    /// partir da rota e só então checa a permissão de recurso — nessa ordem, porque a
    /// checagem de recurso resolve as roles usando o TenantContext.
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

            // 1. Quem é o usuário — o único dado confiável, porque vem do token assinado.
            var userIdRaw = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdRaw, out var userId))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "User not authenticated" });
                return;
            }

            // 2. Qual tenant está sendo pedido. Entrada não confiável: alvo da prova.
            if (!context.RouteData.Values.TryGetValue(TenantIdRouteKey, out var routeRaw)
                || !Guid.TryParse(routeRaw?.ToString(), out var routeTenantId))
            {
                context.Result = new ForbidResult();
                return;
            }

            // 3. O tenant selecionado no token tem que existir e ser o mesmo da rota.
            var claimRaw = http.User.FindFirst(TenantIdClaim)?.Value;
            if (!Guid.TryParse(claimRaw, out var claimTenantId) || claimTenantId != routeTenantId)
            {
                context.Result = new ForbidResult();
                return;
            }

            // 4. Prova de associação contra o banco.
            var tenantService = http.RequestServices.GetRequiredService<ITenantService>();
            var role = await tenantService.GetUserRoleAsync(routeTenantId, userId, http.RequestAborted);
            if (role is null)
            {
                context.Result = new ForbidResult();
                return;
            }

            http.Items[TenantRoleItemKey] = role.Value;

            // 5. TenantContext passa a vir da rota. Daqui em diante contexto e rota são
            //    iguais por construção — é isso que torna o passo 6 correto.
            http.RequestServices.GetRequiredService<ITenantContext>().SetTenant(routeTenantId);

            // 6. Permissão de recurso, resolvida no tenant certo.
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
