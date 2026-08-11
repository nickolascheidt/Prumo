using Prumo.Application.DTOs.Auth;
using Prumo.Application.DTOs.Tenants;
using Prumo.Domain.Enums;

namespace Prumo.Application.Services
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
        Task<UserLookupDto?> LookupUserByEmailAsync(string email, CancellationToken ct = default);
        Task<TenantMemberDto> CreateAndAddMemberAsync(Guid tenantId, CreateTenantUserDto dto, CancellationToken ct = default);
        Task UpdateMemberRoleAsync(Guid tenantId, Guid userId, TenantRole newRole, CancellationToken ct = default);
    }
}
