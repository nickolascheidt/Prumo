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
using Prumo.Notifications.Contracts;
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
            // As feature roles vêm na mesma query, como subconsulta correlacionada. Buscá-las
            // por membro seria N+1 — e é a razão de a tela de roles nunca as ter mostrado.
            var masterAdminRoleId = await _db.Roles
                .Where(r => r.Name == Permissions.Roles.MasterAdmin)
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            // cross-tenant: o TenantsController não usa [TenantModule], então o
            // TenantContext vem do claim e pode ser outro tenant do chamador. A subconsulta
            // de TenantUserRoles (ITenantScoped) fecharia nele e a lista viria sem roles.
            // O tenant desta leitura é o da rota, filtrado nos dois Where.
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
        /// Owner só nasce com o tenant (CreateAsync). Inserir membro como Owner faria um
        /// Admin fabricar um segundo dono que RemoveMemberAsync e UpdateMemberRoleAsync
        /// se recusam a tocar — escalonamento com persistência (auditoria de 2026-09-15).
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

            // Entidade própria desde o item 3B. Antes isto ia no PermissionAuditLog com
            // sentinelas (RoleId vazio, PermissionName "SupportAccess"), porque aquela
            // tabela era modelada para grants de permissão e não para isto.
            var admin = await _userManager.FindByIdAsync(masterAdminUserId.ToString());
            _db.SupportAccessLogs.Add(new SupportAccessLog
            {
                TenantId = tenantId,
                MasterAdminUserId = masterAdminUserId,
                MasterAdminEmail = admin?.Email ?? masterAdminUserId.ToString(),
                GrantedAt = DateTime.UtcNow,
                Reason = $"Master admin inseriu-se como Admin do tenant {tenantId} para suporte."
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

            // As feature roles saem junto. Nada as apaga em cascata (TenantUserRoles não
            // tem FK para TenantUsers), e deixá-las órfãs faz quem for readmitido voltar
            // com os módulos que tinha, sem ninguém conceder de novo.
            // cross-tenant: o TenantsController não usa [TenantModule], então o
            // TenantContext vem do claim e pode ser outro tenant do chamador. O tenant
            // desta remoção é o da rota, filtrado abaixo.
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
        /// O admin digita um e-mail. Se já existe conta, a pessoa entra na hora; se não,
        /// fica um convite pendente que o cadastro resolve sozinho.
        ///
        /// Substituiu a criação de conta com senha digitada pelo admin, que fazia a senha
        /// inicial de todo mundo passar por ele.
        /// </summary>
        public async Task<InviteMemberResultDto> InviteMemberAsync(
            Guid tenantId, InviteMemberRequestDto dto, Guid invitedByUserId, CancellationToken ct = default)
        {
            RejectOwner(dto.Role);

            var email = dto.Email.Trim();
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("E-mail é obrigatório.", nameof(dto));

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
                    throw new InvalidOperationException($"'{email}' já é membro desta empresa.");

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

            // cross-tenant: o TenantsController é isento de [TenantModule] (exigir
            // associação provada em quem gerencia associação seria circular), então aqui o
            // TenantContext viria do claim, que pode divergir da rota. O tenant desta
            // consulta é o da rota, o mesmo que o guard do controller já provou.
            var pending = await _db.TenantInvitations
                .IgnoreQueryFilters()
                .AnyAsync(i => i.TenantId == tenantId && i.NormalizedEmail == normalized && i.AcceptedAt == null, ct);
            if (pending)
                throw new InvalidOperationException($"Já existe um convite pendente para '{email}'.");

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
            // cross-tenant: mesma razão do InviteMemberAsync — o tenant vem da rota, já
            // provada pelo guard do controller, e não do claim que o filtro global usaria.
            return await _db.TenantInvitations
                .IgnoreQueryFilters()
                .Where(i => i.TenantId == tenantId && i.AcceptedAt == null)
                .OrderBy(i => i.CreatedAt)
                .Select(i => new TenantInvitationDto(i.Id, i.Email, i.Role, i.CreatedAt))
                .ToListAsync(ct);
        }

        public async Task CancelInvitationAsync(Guid tenantId, Guid invitationId, CancellationToken ct = default)
        {
            // cross-tenant: mesma razão do InviteMemberAsync. O `i.TenantId == tenantId` é
            // o que impede cancelar convite de outra empresa, e vem da rota.
            var invitation = await _db.TenantInvitations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Id == invitationId && i.TenantId == tenantId && i.AcceptedAt == null, ct)
                ?? throw new KeyNotFoundException("Convite não encontrado.");

            _db.TenantInvitations.Remove(invitation);
            await _db.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Chamado no cadastro: admite quem acabou de criar a conta nos tenants que já
        /// tinham convidado aquele endereço.
        /// </summary>
        public async Task<int> AcceptPendingInvitationsAsync(
            Guid userId, string email, CancellationToken ct = default)
        {
            var normalized = email.Trim().ToUpperInvariant();

            // cross-tenant de propósito: quem acabou de se cadastrar não pertence a tenant
            // nenhum, então não há TenantContext para o filtro global resolver. A consulta
            // é fechada pelo e-mail, que a pessoa acabou de provar ser dela.
            var invitations = await _db.TenantInvitations
                .IgnoreQueryFilters()
                .Where(i => i.NormalizedEmail == normalized && i.AcceptedAt == null)
                .ToListAsync(ct);

            if (invitations.Count == 0)
                return 0;

            foreach (var invitation in invitations)
            {
                // cross-tenant: mesma razão da consulta acima — no cadastro não há tenant
                // selecionado, e o vínculo é o e-mail que a pessoa acabou de provar.
                var alreadyMember = await _db.TenantUsers
                    .IgnoreQueryFilters()
                    .AnyAsync(tu => tu.TenantId == invitation.TenantId && tu.UserId == userId, ct);

                if (!alreadyMember)
                {
                    _db.TenantUsers.Add(new TenantUser
                    {
                        TenantId = invitation.TenantId,
                        UserId = userId,
                        // Convite com Owner só pode ser anterior à regra de RejectOwner.
                        // Honrá-lo recriaria o furo; recusá-lo deixaria a pessoa fora de
                        // um tenant que a esperava. Entra como Member.
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
                        ["invitedBy"] = invitedBy?.FullName ?? invitedBy?.Email ?? "Um administrador",
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
