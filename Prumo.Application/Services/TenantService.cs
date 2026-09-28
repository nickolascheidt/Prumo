using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prumo.Application.DTOs.Auth;
using Prumo.Application.DTOs.Tenants;
using Prumo.Domain.Authorization;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Data.Seeders;
using Prumo.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace Prumo.Application.Services
{
    public class TenantService : ITenantService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationPublisher _notifications;
        private readonly IConfiguration _configuration;

        public TenantService(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            INotificationPublisher notifications,
            IConfiguration configuration)
        {
            _db = db;
            _userManager = userManager;
            _notifications = notifications;
            _configuration = configuration;
        }

        private string AppBaseUrl =>
            _configuration["App:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:4200";

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
            // Feature roles come in the same query, as a correlated subquery. Fetching them
            // per member would be N+1.
            var masterAdminRoleId = await _db.Roles
                .Where(r => r.Name == Permissions.Roles.MasterAdmin)
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            // Cross-tenant: TenantsController does not use [TenantModule], so the
            // TenantContext comes from the claim and may be another of the caller's tenants.
            // The TenantUserRoles subquery (ITenantScoped) would close over it and the list
            // would come back without roles. This read's tenant is the route's, filtered in
            // both Where clauses.
            return await _db.TenantUsers
                .IgnoreQueryFilters()
                .Where(tu => tu.TenantId == tenantId)
                .Select(tu => new TenantMemberDto(
                    tu.UserId,
                    tu.User.Email!,
                    tu.User.FullName,
                    tu.Role,
                    tu.JoinedAt,
                    _db.TenantUserRoles
                        .Where(tur => tur.TenantId == tenantId && tur.UserId == tu.UserId)
                        .Select(tur => tur.Role.Name!)
                        .ToList(),
                    masterAdminRoleId != null
                        && _db.UserRoles.Any(ur => ur.UserId == tu.UserId && ur.RoleId == masterAdminRoleId)))
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// An Owner is only born with the tenant (CreateAsync). Adding a member as Owner would
        /// let an Admin manufacture a second owner that RemoveMemberAsync and
        /// UpdateMemberRoleAsync refuse to touch — a privilege escalation that sticks.
        /// </summary>
        private static void RejectOwner(TenantRole role)
        {
            if (role == TenantRole.Owner)
                throw new InvalidOperationException("Cannot add a member as Owner.");
        }

        public async Task AddMemberAsync(Guid tenantId, Guid userId, TenantRole role, CancellationToken cancellationToken = default)
        {
            RejectOwner(role);

            var exists = await _db.TenantUsers
                .AnyAsync(tu => tu.TenantId == tenantId && tu.UserId == userId, cancellationToken);
            if (exists)
                throw new InvalidOperationException("User is already a member of this tenant");

            _db.TenantUsers.Add(new TenantUser { TenantId = tenantId, UserId = userId, Role = role });
            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Adds the master admin as a member of a tenant they did not create, for support.
        /// There is no bypass of the membership check: a bypass would reintroduce the
        /// cross-tenant leak that per-tenant RBAC removed, and would be a path the
        /// architecture test cannot see. Here access becomes an audited row in the database.
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

            // Support access has its own audit table, separate from the permission audit
            // log: that one is modeled for role × resource level changes, not for this.
            var admin = await _userManager.FindByIdAsync(masterAdminUserId.ToString());
            _db.SupportAccessLogs.Add(new SupportAccessLog
            {
                TenantId = tenantId,
                MasterAdminUserId = masterAdminUserId,
                MasterAdminEmail = admin?.Email ?? masterAdminUserId.ToString(),
                GrantedAt = DateTime.UtcNow,
                Reason = $"Master admin added themselves as Admin of tenant {tenantId} for support."
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

            // Feature roles go too. Nothing deletes them in cascade (TenantUserRoles has no
            // FK to TenantUsers), and leaving them orphaned would let someone who is
            // re-admitted come back with the modules they had, without anyone granting
            // them again.
            // Cross-tenant: TenantsController does not use [TenantModule], so the
            // TenantContext comes from the claim and may be another of the caller's tenants.
            // This removal's tenant is the route's, filtered below.
            var featureRoles = await _db.TenantUserRoles
                .IgnoreQueryFilters()
                .Where(tur => tur.TenantId == tenantId && tur.UserId == userId)
                .ToListAsync(cancellationToken);

            _db.TenantUserRoles.RemoveRange(featureRoles);
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

        /// <summary>
        /// The admin types an e-mail. If an account already exists, the person joins right
        /// away; if not, a pending invitation is left for sign-up to resolve.
        ///
        /// Replaced creating accounts with a password typed by the admin, which made
        /// everyone's initial password pass through them.
        /// </summary>
        public async Task<InviteMemberResultDto> InviteMemberAsync(
            Guid tenantId, InviteMemberRequestDto dto, Guid invitedByUserId, CancellationToken ct = default)
        {
            RejectOwner(dto.Role);

            var email = dto.Email.Trim();
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("E-mail is required.", nameof(dto));

            var normalized = email.ToUpperInvariant();

            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
                ?? throw new KeyNotFoundException("Tenant not found.");

            var invitedBy = await _userManager.FindByIdAsync(invitedByUserId.ToString());
            var existingUser = await _userManager.FindByEmailAsync(email);

            if (existingUser is not null)
            {
                var alreadyMember = await _db.TenantUsers
                    .AnyAsync(tu => tu.TenantId == tenantId && tu.UserId == existingUser.Id, ct);
                if (alreadyMember)
                    throw new InvalidOperationException($"'{email}' is already a member of this company.");

                _db.TenantUsers.Add(new TenantUser
                {
                    TenantId = tenantId,
                    UserId = existingUser.Id,
                    Role = dto.Role
                });
                await _db.SaveChangesAsync(ct);

                await PublishInvitationNoticeAsync(email, tenant.Name, invitedBy, ct);

                return new InviteMemberResultDto(true, existingUser.Id, email);
            }

            // Cross-tenant: TenantsController is exempt from [TenantModule] (requiring proven
            // membership from whoever manages membership would be circular), so the
            // TenantContext here would come from the claim, which may differ from the route.
            // This query's tenant is the route's, the same one the controller guard proved.
            var pending = await _db.TenantInvitations
                .IgnoreQueryFilters()
                .AnyAsync(i => i.TenantId == tenantId && i.NormalizedEmail == normalized && i.AcceptedAt == null, ct);
            if (pending)
                throw new InvalidOperationException($"There is already a pending invitation for '{email}'.");

            _db.TenantInvitations.Add(new TenantInvitation
            {
                TenantId = tenantId,
                Email = email,
                NormalizedEmail = normalized,
                Role = dto.Role,
                InvitedByUserId = invitedByUserId
            });
            await _db.SaveChangesAsync(ct);

            await PublishInvitationNoticeAsync(email, tenant.Name, invitedBy, ct);

            return new InviteMemberResultDto(false, null, email);
        }

        public async Task<IReadOnlyList<TenantInvitationDto>> GetPendingInvitationsAsync(
            Guid tenantId, CancellationToken ct = default)
        {
            // Cross-tenant: same reason as InviteMemberAsync — the tenant comes from the route,
            // already proven by the controller guard, not from the claim the global filter uses.
            return await _db.TenantInvitations
                .IgnoreQueryFilters()
                .Where(i => i.TenantId == tenantId && i.AcceptedAt == null)
                .OrderBy(i => i.CreatedAt)
                .Select(i => new TenantInvitationDto(i.Id, i.Email, i.Role, i.CreatedAt))
                .ToListAsync(ct);
        }

        public async Task CancelInvitationAsync(Guid tenantId, Guid invitationId, CancellationToken ct = default)
        {
            // Cross-tenant: same reason as InviteMemberAsync. `i.TenantId == tenantId` is what
            // prevents cancelling another company's invitation, and it comes from the route.
            var invitation = await _db.TenantInvitations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Id == invitationId && i.TenantId == tenantId && i.AcceptedAt == null, ct)
                ?? throw new KeyNotFoundException("Invitation not found.");

            _db.TenantInvitations.Remove(invitation);
            await _db.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Called at sign-up: admits the person who just created an account into the tenants
        /// that had invited that address.
        /// </summary>
        public async Task<int> AcceptPendingInvitationsAsync(
            Guid userId, string email, CancellationToken ct = default)
        {
            var normalized = email.Trim().ToUpperInvariant();

            // Cross-tenant on purpose: someone who just signed up belongs to no tenant, so
            // there is no TenantContext for the global filter to resolve. The query is
            // closed by the e-mail, which the person just proved is theirs.
            var invitations = await _db.TenantInvitations
                .IgnoreQueryFilters()
                .Where(i => i.NormalizedEmail == normalized && i.AcceptedAt == null)
                .ToListAsync(ct);

            if (invitations.Count == 0)
                return 0;

            foreach (var invitation in invitations)
            {
                // Cross-tenant: same reason as the query above — there is no selected tenant
                // at sign-up, and the link is the e-mail the person just proved.
                var alreadyMember = await _db.TenantUsers
                    .IgnoreQueryFilters()
                    .AnyAsync(tu => tu.TenantId == invitation.TenantId && tu.UserId == userId, ct);

                if (!alreadyMember)
                {
                    _db.TenantUsers.Add(new TenantUser
                    {
                        TenantId = invitation.TenantId,
                        UserId = userId,
                        // An invitation with Owner can only predate the reject-Owner rule.
                        // Honoring it would reopen the hole; refusing it would leave the person
                        // out of a tenant that expected them. They join as Member.
                        Role = invitation.Role == TenantRole.Owner ? TenantRole.Member : invitation.Role
                    });
                }

                invitation.AcceptedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
            return invitations.Count;
        }

        private async Task PublishInvitationNoticeAsync(
            string email, string tenantName, ApplicationUser? invitedBy, CancellationToken ct)
        {
            await _notifications.PublishAsync(
                new NotificationMessage
                {
                    Type = NotificationTypes.TenantInvitation,
                    To = email,
                    CorrelationId = Guid.NewGuid(),
                    Data = new Dictionary<string, string>
                    {
                        ["tenantName"] = tenantName,
                        ["invitedBy"] = invitedBy?.FullName ?? invitedBy?.Email ?? "An administrator",
                        ["link"] = AppBaseUrl
                    }
                },
                ct);
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
