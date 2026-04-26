using SaaS_BasePlatform.Application.DTOs.Tenants;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Application.Services
{
    public interface ITenantService
    {
        Task<TenantDto> CreateAsync(Guid ownerUserId, CreateTenantRequestDto request, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TenantMembershipDto>> GetUserMembershipsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<TenantDto?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<TenantDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TenantMemberDto>> GetMembersAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task AddMemberAsync(Guid tenantId, Guid userId, TenantRole role, CancellationToken cancellationToken = default);
        Task RemoveMemberAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
        Task<bool> IsMemberAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
        Task<TenantRole?> GetUserRoleAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    }
}
