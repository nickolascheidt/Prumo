using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prumo.Application.DTOs.Roles;
using Prumo.Domain.Authorization;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;

namespace Prumo.Application.Services
{
    public class TenantRoleAdminService : ITenantRoleAdminService
    {
        private const int MaxNameLength = 64;

        private readonly ApplicationDbContext _db;
        private readonly RoleManager<ApplicationRole> _roleManager;

        public TenantRoleAdminService(ApplicationDbContext db, RoleManager<ApplicationRole> roleManager)
        {
            _db = db;
            _roleManager = roleManager;
        }

        public async Task<IReadOnlyList<TenantRoleDto>> GetVisibleRolesAsync(
            Guid tenantId, CancellationToken ct = default)
        {
            var roles = await _db.Roles
                .Where(r => r.TenantId == null || r.TenantId == tenantId)
                .OrderBy(r => r.Name)
                .ToListAsync(ct);

            // cross-tenant: TenantUserRole é ITenantScoped e este service roda em
            // caminhos sem TenantContext resolvido; o Where abaixo é o filtro real.
            var counts = await _db.TenantUserRoles
                .IgnoreQueryFilters()
                .Where(tur => tur.TenantId == tenantId)
                .GroupBy(tur => tur.RoleId)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);

            return roles
                .Select(r => new TenantRoleDto(
                    r.Id,
                    r.Name!,
                    r.Description,
                    r.TenantId == null,
                    counts.TryGetValue(r.Id, out var c) ? c : 0))
                .ToList();
        }

        public async Task<IReadOnlyList<string>> GetAssignableRoleNamesAsync(
            Guid tenantId, CancellationToken ct = default)
        {
            var ownNames = await _db.Roles
                .Where(r => r.TenantId == tenantId)
                .Select(r => r.Name!)
                .ToListAsync(ct);

            // As canônicas vêm da lista do Domain, e não do banco, porque ela já exclui
            // o master admin de propósito — ele existe como role mas não é concedível
            // por um admin de tenant.
            return Permissions.Roles.AssignableFeatureRoles
                .Concat(ownNames)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public async Task<TenantRoleDto> CreateAsync(
            Guid tenantId, CreateTenantRoleDto dto, CancellationToken ct = default)
        {
            var name = (dto.Name ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("O nome da role é obrigatório.");
            }

            if (name.Length > MaxNameLength)
            {
                throw new ArgumentException($"O nome da role deve ter no máximo {MaxNameLength} caracteres.");
            }

            var normalized = _roleManager.NormalizeKey(name);

            // Nomes canônicos são reservados: deixar um tenant criar a própria "HR"
            // tornaria o nome ambíguo em toda tela, todo claim e todo log.
            if (await _db.Roles.AnyAsync(r => r.NormalizedName == normalized && r.TenantId == null, ct))
            {
                throw new InvalidOperationException($"'{name}' é uma role do sistema e não pode ser recriada.");
            }

            if (await _db.Roles.AnyAsync(r => r.NormalizedName == normalized && r.TenantId == tenantId, ct))
            {
                throw new InvalidOperationException($"Já existe uma role '{name}' neste tenant.");
            }

            var role = new ApplicationRole
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                TenantId = tenantId
            };

            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" | ", result.Errors.Select(e => e.Description)));
            }

            // Nasce vazia de propósito (decisão de 2026-08-18): zero ResourcePermission,
            // zero acesso. O admin concede depois, na grade.
            return new TenantRoleDto(role.Id, role.Name!, role.Description, false, 0);
        }

        public async Task DeleteAsync(Guid tenantId, Guid roleId, CancellationToken ct = default)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId, ct);

            if (role is null)
            {
                throw new KeyNotFoundException("Role não encontrada.");
            }

            if (role.TenantId is null)
            {
                throw new InvalidOperationException("Roles do sistema não podem ser excluídas.");
            }

            // O gate que impede um tenant de apagar a role de outro. 404 e não 403 de
            // propósito: quem não é dono não deve nem saber que ela existe.
            if (role.TenantId != tenantId)
            {
                throw new KeyNotFoundException("Role não encontrada.");
            }

            // cross-tenant: TenantUserRole é ITenantScoped e o Where restringe a este tenant.
            var inUse = await _db.TenantUserRoles
                .IgnoreQueryFilters()
                .AnyAsync(tur => tur.RoleId == roleId && tur.TenantId == tenantId, ct);

            if (inUse)
            {
                throw new InvalidOperationException(
                    "Esta role ainda está atribuída a membros. Remova-a deles antes de excluir.");
            }

            // As permissões de recurso morrem junto: deixá-las órfãs faria um Id
            // reaproveitado herdar acesso que ninguém concedeu.
            // cross-tenant: ResourcePermission é ITenantScoped e o Where restringe aqui.
            var grants = await _db.ResourcePermissions
                .IgnoreQueryFilters()
                .Where(rp => rp.RoleId == roleId && rp.TenantId == tenantId)
                .ToListAsync(ct);

            if (grants.Count > 0)
            {
                _db.ResourcePermissions.RemoveRange(grants);
                await _db.SaveChangesAsync(ct);
            }

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" | ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
