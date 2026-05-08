using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaaS_BasePlatform.Application.DTOs.Tenants;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;
using SaaS_BasePlatform.Infrastructure.Data;
using SaaS_BasePlatform.Infrastructure.Data.Seeders;

namespace SaaS_BasePlatform.Application.Services
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
                .IgnoreQueryFilters()
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
                .IgnoreQueryFilters()
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
            var t = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
            return t == null ? null : new TenantDto(t.Id, t.Name, t.Slug, t.OwnerUserId, t.CreatedAt);
        }

        public async Task<TenantDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        {
            slug = slug.Trim().ToLowerInvariant();
            var t = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);
            return t == null ? null : new TenantDto(t.Id, t.Name, t.Slug, t.OwnerUserId, t.CreatedAt);
        }

        public async Task<IReadOnlyList<TenantMemberDto>> GetMembersAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await _db.TenantUsers
                .IgnoreQueryFilters()
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
                .IgnoreQueryFilters()
                .AnyAsync(tu => tu.TenantId == tenantId && tu.UserId == userId, cancellationToken);
            if (exists)
                throw new InvalidOperationException("User is already a member of this tenant");

            _db.TenantUsers.Add(new TenantUser { TenantId = tenantId, UserId = userId, Role = role });
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task RemoveMemberAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        {
            var membership = await _db.TenantUsers
                .IgnoreQueryFilters()
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
                .IgnoreQueryFilters()
                .AnyAsync(tu => tu.TenantId == tenantId && tu.UserId == userId, cancellationToken);
        }

        public async Task<TenantRole?> GetUserRoleAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        {
            var membership = await _db.TenantUsers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId, cancellationToken);
            return membership?.Role;
        }

        public async Task<TenantMemberDto> CreateAndAddMemberAsync(
            Guid tenantId, CreateTenantUserDto dto, CancellationToken cancellationToken = default)
        {
            var existing = await _userManager.FindByEmailAsync(dto.Email);
            if (existing != null)
                throw new InvalidOperationException($"A user with email '{dto.Email}' already exists.");

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, dto.Password);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "Usuario");
            await AddMemberAsync(tenantId, user.Id, dto.Role, cancellationToken);

            return new TenantMemberDto(user.Id, user.Email!, user.FullName, dto.Role, DateTime.UtcNow);
        }
    }
}
