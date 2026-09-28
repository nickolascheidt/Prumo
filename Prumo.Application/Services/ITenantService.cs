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
        /// <summary>
        /// Admits by e-mail: an existing account joins right away, an e-mail without an
        /// account becomes a pending invitation.
        /// </summary>
        Task<InviteMemberResultDto> InviteMemberAsync(Guid tenantId, InviteMemberRequestDto dto, Guid invitedByUserId, CancellationToken ct = default);

        Task<IReadOnlyList<TenantInvitationDto>> GetPendingInvitationsAsync(Guid tenantId, CancellationToken ct = default);

        Task CancelInvitationAsync(Guid tenantId, Guid invitationId, CancellationToken ct = default);

        /// <summary>
        /// Consumes the pending invitations for a newly registered address. Returns how many
        /// tenants the person just joined.
        /// </summary>
        Task<int> AcceptPendingInvitationsAsync(Guid userId, string email, CancellationToken ct = default);
        Task UpdateMemberRoleAsync(Guid tenantId, Guid userId, TenantRole newRole, CancellationToken ct = default);
        Task<bool> GrantSupportAccessAsync(Guid tenantId, Guid masterAdminUserId, CancellationToken ct = default);
    }
}
