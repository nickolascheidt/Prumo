using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prumo.Application.DTOs.Auth;
using Prumo.Application.DTOs.Tenants;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Data.Seeders;

namespace Prumo.Application.Services
{
    public class TenantService : ITenantService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public TenantService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<TenantDto> CreateAsync(Guid ownerUserId, CreateTenantRequestDto request, CancellationToken cancellationToken = default)
        {
            var slug = request.Slug.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(slug))
                throw new ArgumentException("Slug is required");

            var slugTaken = await _db.Tenants
                .AnyAsync(t => t.Slug == slug, cancellationToken);
            if (slugTaken)
                throw new InvalidOperationException($"Slug '{slug}' is already in use");

            var tenant = new Tenant
            {
                Name = request.Name,
                Slug = slug,
                OwnerUserId = ownerUserId
            };

            _db.Tenants.Add(tenant);

            _db.TenantUsers.Add(new TenantUser
            {
                TenantId = tenant.Id,
                UserId = ownerUserId,
                Role = TenantRole.Owner
            });

            await _db.SaveChangesAsync(cancellationToken);

            await TenantBootstrapSeeder.SeedAsync(_db, tenant.Id, cancellationToken);

            return new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.OwnerUserId, tenant.CreatedAt);
        }

        public async Task<IReadOnlyList<TenantMembershipDto>> GetUserMembershipsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.TenantUsers
                .Where(tu => tu.UserId == userId)
                .Select(tu => new TenantMembershipDto(
                    tu.TenantId,
                    tu.Tenant.Name,
                    tu.Tenant.Slug,
                    tu.Role,
                    tu.JoinedAt))
                .ToListAsync(cancellationToken);
        }

        public async Task<TenantDto?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
            return t == null ? null : new TenantDto(t.Id, t.Name, t.Slug, t.OwnerUserId, t.CreatedAt);
        }

        public async Task<TenantDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        {
            slug = slug.Trim().ToLowerInvariant();
            var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);
            return t == null ? null : new TenantDto(t.Id, t.Name, t.Slug, t.OwnerUserId, t.CreatedAt);
        }

        public async Task<IReadOnlyList<TenantMemberDto>> GetMembersAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await _db.TenantUsers
                .Where(tu => tu.TenantId == tenantId)
                .Select(tu => new TenantMemberDto(
                    tu.UserId,
                    tu.User.Email!,
                    tu.User.FullName,
                    tu.Role,
                    tu.JoinedAt))
                .ToListAsync(cancellationToken);
        }

        public async Task AddMemberAsync(Guid tenantId, Guid userId, TenantRole role, CancellationToken cancellationToken = default)
        {
            var exists = await _db.TenantUsers
                .AnyAsync(tu => tu.TenantId == tenantId && tu.UserId == userId, cancellationToken);
            if (exists)
                throw new InvalidOperationException("User is already a member of this tenant");

            _db.TenantUsers.Add(new TenantUser { TenantId = tenantId, UserId = userId, Role = role });
            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Insere o master admin como membro de um tenant que ele não criou, para suporte.
        /// Não existe bypass da checagem de associação: um bypass reintroduziria o vazamento
        /// cross-tenant que o trabalho de RBAC removeu, e seria um caminho que o teste de
        /// arquitetura não consegue ver. Aqui o acesso vira uma linha no banco, auditada.
        /// </summary>
        public async Task<bool> GrantSupportAccessAsync(
            Guid tenantId, Guid masterAdminUserId, CancellationToken ct = default)
        {
            var tenantExists = await _db.Tenants
                .AnyAsync(t => t.Id == tenantId, ct);

            if (!tenantExists) return false;

            var alreadyMember = await _db.TenantUsers
                .AnyAsync(tu => tu.TenantId == tenantId && tu.UserId == masterAdminUserId, ct);

            if (alreadyMember) return true;

            _db.TenantUsers.Add(new TenantUser
            {
                TenantId = tenantId,
                UserId = masterAdminUserId,
                Role = TenantRole.Admin
            });

            // PermissionAuditLog é modelado para grants de role/permissão, então os campos
            // de role e permissão recebem sentinelas legíveis em vez de ids inventados —
            // o que importa registrar aqui é quem entrou, em qual tenant e quando.
            var admin = await _userManager.FindByIdAsync(masterAdminUserId.ToString());
            _db.PermissionAuditLogs.Add(new PermissionAuditLog
            {
                TenantId = tenantId,
                RoleId = Guid.Empty,
                RoleName = "TenantRole.Admin",
                PermissionId = Guid.Empty,
                PermissionName = "SupportAccess",
                Action = "SUPPORT_ACCESS_GRANTED",
                PerformedByUserId = masterAdminUserId,
                PerformedByUserEmail = admin?.Email ?? masterAdminUserId.ToString(),
                PerformedAt = DateTime.UtcNow,
                Reason = $"Master admin {masterAdminUserId} inseriu-se como Admin do tenant {tenantId} para suporte."
            });

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task RemoveMemberAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        {
            var membership = await _db.TenantUsers
                .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId, cancellationToken);
            if (membership == null) return;

            if (membership.Role == TenantRole.Owner)
                throw new InvalidOperationException("Cannot remove the tenant owner");

            _db.TenantUsers.Remove(membership);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> IsMemberAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.TenantUsers
                .AnyAsync(tu => tu.TenantId == tenantId && tu.UserId == userId, cancellationToken);
        }

        public async Task<TenantRole?> GetUserRoleAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        {
            var membership = await _db.TenantUsers
                .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId, cancellationToken);
            return membership?.Role;
        }

        public async Task<UserLookupDto?> LookupUserByEmailAsync(string email, CancellationToken ct = default)
        {
            var user = await _userManager.FindByEmailAsync(email.Trim().ToLowerInvariant());
            if (user == null || !user.IsActive)
                return null;
            return new UserLookupDto(user.Id, user.Email!, user.FullName);
        }

        public async Task<TenantMemberDto> CreateAndAddMemberAsync(
            Guid tenantId, CreateTenantUserDto dto, CancellationToken ct = default)
        {
            var existing = await _userManager.FindByEmailAsync(dto.Email.Trim().ToLowerInvariant());
            if (existing != null)
                throw new InvalidOperationException($"A user with email '{dto.Email}' already exists.");

            var user = new ApplicationUser
            {
                UserName = dto.Email.Trim().ToLowerInvariant(),
                Email = dto.Email.Trim().ToLowerInvariant(),
                FullName = dto.FullName,
                PhoneNumber = dto.Phone,
                IsActive = true
            };

            var createResult = await _userManager.CreateAsync(user, dto.Password);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(e => e.Description)));

            _db.TenantUsers.Add(new TenantUser { TenantId = tenantId, UserId = user.Id, Role = dto.Role });
            await _db.SaveChangesAsync(ct);

            return new TenantMemberDto(user.Id, user.Email!, user.FullName, dto.Role, DateTime.UtcNow);
        }

        public async Task UpdateMemberRoleAsync(
            Guid tenantId, Guid userId, TenantRole newRole, CancellationToken ct = default)
        {
            var member = await _db.TenantUsers
                .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId, ct)
                ?? throw new KeyNotFoundException("Member not found.");

            if (member.Role == TenantRole.Owner)
                throw new InvalidOperationException("Cannot change the Owner's role.");

            if (newRole == TenantRole.Owner)
                throw new InvalidOperationException("Cannot promote a member to Owner.");

            member.Role = newRole;
            await _db.SaveChangesAsync(ct);
        }
    }
}
