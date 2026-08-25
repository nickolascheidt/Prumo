using Microsoft.EntityFrameworkCore;
using Prumo.Application.Services;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Services
{
    /// <summary>
    /// GetTenantRoleNamesAsync tem um requisito que o tipo da entidade esconde: ele roda
    /// durante a emissão do token, quando ainda NÃO existe tenant resolvido.
    ///
    /// O TenantResolutionMiddleware preenche o TenantContext a partir do claim `tenant_id`,
    /// e em POST /api/tenants/select o token de ENTRADA ainda não tem esse claim — é
    /// justamente o token que está sendo emitido que vai passar a ter. No login o usuário
    /// nem autenticado está quando o token é gerado.
    ///
    /// TenantUserRole é ITenantScoped, então o filtro global fail-closed se aplica a ele.
    /// Sem o IgnoreQueryFilters este método devolveria lista vazia nesse momento, e o
    /// usuário receberia um token sem nenhuma feature role — menu vazio, o bug 73dc458.
    ///
    /// Por isso aquele IgnoreQueryFilters é cross-tenant legítimo e NÃO saiu na fase 2.
    /// Este teste existe para que remover a chamada quebre o build em vez de quebrar o login.
    /// </summary>
    public class TenantRoleServiceTenantlessTests
    {
        [Fact]
        public async Task Tenant_roles_are_readable_while_no_tenant_is_resolved()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var roleId = Guid.NewGuid();

            var seedCtx = new TenantContext();
            seedCtx.SetTenant(tenantId);
            await using (var seed = NewDb(seedCtx, dbName))
            {
                seed.Roles.Add(new ApplicationRole { Id = roleId, Name = "RH", NormalizedName = "RH" });
                seed.TenantUserRoles.Add(new TenantUserRole
                {
                    TenantId = tenantId,
                    UserId = userId,
                    RoleId = roleId
                });
                await seed.SaveChangesAsync();
            }

            // O estado real do POST /tenants/select: autenticado, mas sem tenant resolvido.
            await using var db = NewDb(new TenantContext(), dbName);
            var service = new TenantRoleService(db);

            var roles = await service.GetTenantRoleNamesAsync(userId, tenantId);

            Assert.Equal(new[] { "RH" }, roles);
        }

        private static ApplicationDbContext NewDb(ITenantContext ctx, string dbName) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName).Options, ctx);
    }
}
