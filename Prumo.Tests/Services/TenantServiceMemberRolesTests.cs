using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Prumo.Application.Services;
using Prumo.Domain.Authorization;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Services
{
    /// <summary>
    /// GET /tenants/{id}/members has to carry the feature roles, otherwise the screen
    /// that lists them can only count roles by asking once per member (N+1) — which is
    /// why the count sat at 0. Backlog item 4.
    /// </summary>
    public class TenantServiceMemberRolesTests
    {
        private static ApplicationDbContext NewDb(Guid tenantId, string dbName)
        {
            var ctx = new TenantContext();
            ctx.SetTenant(tenantId);
            return new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(dbName).Options, ctx);
        }

        private static UserManager<ApplicationUser> MockUserManager() =>
            Substitute.For<UserManager<ApplicationUser>>(
                Substitute.For<IUserStore<ApplicationUser>>(),
                null, null, null, null, null, null, null, null);

        private static void AddMember(ApplicationDbContext db, Guid tenantId, Guid userId, string email)
        {
            db.Users.Add(new ApplicationUser
            {
                Id = userId,
                Email = email,
                UserName = email,
                FullName = email.Split('@')[0],
                IsActive = true
            });
            db.TenantUsers.Add(new TenantUser
            {
                TenantId = tenantId,
                UserId = userId,
                Role = TenantRole.Member,
                JoinedAt = DateTime.UtcNow
            });
        }

        private static void GrantFeatureRole(
            ApplicationDbContext db, Guid tenantId, Guid userId, Guid roleId, string roleName)
        {
            if (!db.Roles.Local.Any(r => r.Id == roleId))
                db.Roles.Add(new ApplicationRole { Id = roleId, Name = roleName, NormalizedName = roleName.ToUpperInvariant() });

            db.TenantUserRoles.Add(new TenantUserRole
            {
                TenantId = tenantId,
                UserId = userId,
                RoleId = roleId,
                GrantedAt = DateTime.UtcNow
            });
        }

        [Fact]
        public async Task Members_carry_the_feature_roles_granted_in_this_tenant()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var rhRoleId = Guid.NewGuid();

            await using var db = NewDb(tenantId, dbName);
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t", OwnerUserId = Guid.NewGuid() });
            AddMember(db, tenantId, userId, "maria@example.com");
            GrantFeatureRole(db, tenantId, userId, rhRoleId, "RH");
            await db.SaveChangesAsync();

            var members = await new TenantService(db, MockUserManager()).GetMembersAsync(tenantId);

            var maria = Assert.Single(members);
            Assert.Equal(new[] { "RH" }, maria.Roles);
        }

        [Fact]
        public async Task A_member_with_no_feature_role_comes_back_with_an_empty_list()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantId = Guid.NewGuid();

            await using var db = NewDb(tenantId, dbName);
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t", OwnerUserId = Guid.NewGuid() });
            AddMember(db, tenantId, Guid.NewGuid(), "sem-chave@example.com");
            await db.SaveChangesAsync();

            var members = await new TenantService(db, MockUserManager()).GetMembersAsync(tenantId);

            Assert.Empty(Assert.Single(members).Roles);
        }

        [Fact]
        public async Task A_role_granted_in_another_tenant_does_not_show_up_here()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var rhRoleId = Guid.NewGuid();

            await using (var seed = NewDb(tenantB, dbName))
            {
                seed.Tenants.Add(new Tenant { Id = tenantB, Name = "B", Slug = "b", OwnerUserId = Guid.NewGuid() });
                GrantFeatureRole(seed, tenantB, userId, rhRoleId, "RH");
                await seed.SaveChangesAsync();
            }

            await using var db = NewDb(tenantA, dbName);
            db.Tenants.Add(new Tenant { Id = tenantA, Name = "A", Slug = "a", OwnerUserId = Guid.NewGuid() });
            AddMember(db, tenantA, userId, "viajante@example.com");
            await db.SaveChangesAsync();

            var members = await new TenantService(db, MockUserManager()).GetMembersAsync(tenantA);

            Assert.Empty(Assert.Single(members).Roles);
        }

        [Fact]
        public async Task The_master_admin_is_flagged_rather_than_listed_as_a_feature_role()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var masterRoleId = Guid.NewGuid();

            await using var db = NewDb(tenantId, dbName);
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t", OwnerUserId = adminId });
            AddMember(db, tenantId, adminId, "admin@SBP.com");
            db.Roles.Add(new ApplicationRole
            {
                Id = masterRoleId,
                Name = Permissions.Roles.MasterAdmin,
                NormalizedName = Permissions.Roles.MasterAdmin.ToUpperInvariant()
            });
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = adminId, RoleId = masterRoleId });
            await db.SaveChangesAsync();

            var admin = Assert.Single(await new TenantService(db, MockUserManager()).GetMembersAsync(tenantId));

            Assert.True(admin.IsMasterAdmin);
            Assert.Empty(admin.Roles);
        }

        [Fact]
        public async Task An_ordinary_member_is_not_flagged_as_master_admin()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantId = Guid.NewGuid();

            await using var db = NewDb(tenantId, dbName);
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t", OwnerUserId = Guid.NewGuid() });
            AddMember(db, tenantId, Guid.NewGuid(), "maria@example.com");
            await db.SaveChangesAsync();

            var members = await new TenantService(db, MockUserManager()).GetMembersAsync(tenantId);

            Assert.False(Assert.Single(members).IsMasterAdmin);
        }
    }
}
