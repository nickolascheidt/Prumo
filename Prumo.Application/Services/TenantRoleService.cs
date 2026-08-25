using Microsoft.EntityFrameworkCore;
using Prumo.Domain.Authorization;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;

namespace Prumo.Application.Services
{
    public class TenantRoleService : ITenantRoleService
    {
        private readonly ApplicationDbContext _db;

        public TenantRoleService(ApplicationDbContext db) => _db = db;

        public async Task<IReadOnlyList<string>> GetTenantRoleNamesAsync(
            Guid userId, Guid tenantId, CancellationToken ct = default)
        {
            // Este método roda durante a emissão do token, em POST /tenants/select e no
            // login, quando o TenantContext ainda está vazio: o middleware o preenche pelo
            // claim `tenant_id`, que é justamente o que o token sendo emitido ainda não
            // tem. TenantUserRole é ITenantScoped, então sem o bypass o filtro fail-closed
            // devolveria lista vazia e o usuário receberia um token sem feature role
            // nenhuma — o bug 73dc458, menu vazio.
            //
            // Logo: cross-tenant de propósito. O tenantId vem do parâmetro e é filtrado no
            // Where abaixo. Coberto por TenantRoleServiceTenantlessTests.
            return await _db.TenantUserRoles
                .IgnoreQueryFilters()
                .Where(tur => tur.TenantId == tenantId && tur.UserId == userId)
                .Select(tur => tur.Role.Name!)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<string>> GetEffectiveRoleNamesAsync(
            Guid userId, Guid tenantId, IReadOnlyCollection<string> globalRoleNames,
            CancellationToken ct = default)
        {
            var tenantRoles = await GetTenantRoleNamesAsync(userId, tenantId, ct);
            return globalRoleNames.Concat(tenantRoles).Distinct().ToList();
        }

        public async Task AssignFeatureRoleAsync(
            Guid tenantId, Guid userId, string roleName,
            Guid? grantedByUserId = null, CancellationToken ct = default)
        {
            if (!Permissions.Roles.AssignableFeatureRoles.Contains(roleName))
                throw new InvalidOperationException(
                    $"'{roleName}' is not an assignable tenant feature role.");

            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct)
                ?? throw new InvalidOperationException($"Role '{roleName}' does not exist.");

            // Cross-tenant de propósito: o TenantsController não usa [TenantModule] — ele
            // gateia por GetUserRoleAsync(tenantId da ROTA), enquanto o TenantContext vem
            // do claim. Um Owner de B agindo com claim de A faria esta checagem procurar
            // em A, não achar nada e inserir linha duplicada em B. O tenantId da rota é
            // filtrado no AnyAsync abaixo.
            var exists = await _db.TenantUserRoles
                .IgnoreQueryFilters()
                .AnyAsync(t => t.TenantId == tenantId && t.UserId == userId && t.RoleId == role.Id, ct);
            if (exists) return;

            _db.TenantUserRoles.Add(new TenantUserRole
            {
                TenantId = tenantId,
                UserId = userId,
                RoleId = role.Id,
                GrantedByUserId = grantedByUserId,
                GrantedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }

        public async Task RevokeFeatureRoleAsync(
            Guid tenantId, Guid userId, string roleName, CancellationToken ct = default)
        {
            // Cross-tenant de propósito, mesmo motivo do AssignFeatureRoleAsync: aqui a
            // consequência seria pior — não achar a linha faz o revoke virar no-op
            // silencioso, e a role continua concedida sem nenhum erro.
            var row = await _db.TenantUserRoles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId
                                       && t.UserId == userId
                                       && t.Role.Name == roleName, ct);
            if (row == null) return;
            _db.TenantUserRoles.Remove(row);
            await _db.SaveChangesAsync(ct);
        }
    }
}
