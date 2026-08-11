using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Prumo.Application.Services;
using Prumo.Domain.Authorization;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data;
using Xunit;

namespace Prumo.Tests.Services
{
    public class ResourcePermissionTenantTests
    {
        private static ApplicationDbContext NewDb(ITenantContext ctx) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, ctx);

        private static ApplicationDbContext NewDb(ITenantContext ctx, string dbName) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName).Options, ctx);

        private static UserManager<ApplicationUser> MockUserManager(Guid userId)
        {
            var store = Substitute.For<IUserStore<ApplicationUser>>();
            var um = Substitute.For<UserManager<ApplicationUser>>(
                store, null, null, null, null, null, null, null, null);

            var user = new ApplicationUser { Id = userId, UserName = "test@example.com", Email = "test@example.com" };
            um.FindByIdAsync(userId.ToString()).Returns(user);
            um.GetRolesAsync(Arg.Is<ApplicationUser>(u => u.Id == userId))
              .Returns(new List<string>());

            return um;
        }

        [Fact]
        public async Task Tenant_owner_has_full_access_within_tenant()
        {
            var tenant = Guid.NewGuid();
            var ctx = Substitute.For<ITenantContext>();
            ctx.TenantId.Returns(tenant);
            ctx.HasTenant.Returns(true);
            using var db = NewDb(ctx);

            var user = Guid.NewGuid();
            db.Resources.Add(new Resource { Id = Guid.NewGuid(), TenantId = tenant, Code = "HR.Employees", Name = "x", Module = "RH", IsActive = true });
            db.TenantUsers.Add(new TenantUser { TenantId = tenant, UserId = user, Role = TenantRole.Owner });
            await db.SaveChangesAsync();

            var sut = new ResourcePermissionService(db, MockUserManager(user), ctx);
            var level = await sut.GetUserPermissionForResourceAsync(user, "HR.Employees");

            Assert.Equal(PermissionLevel.Full, level);
        }

        [Fact]
        public async Task NonAdmin_per_tenant_role_grants_correct_level_and_no_cross_tenant_leak()
        {
            // Arrange — shared in-memory database so both context instances see the same data
            var dbName = Guid.NewGuid().ToString();
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var ctxA = Substitute.For<ITenantContext>();
            ctxA.TenantId.Returns(tenantA);
            ctxA.HasTenant.Returns(true);

            using var dbA = NewDb(ctxA, dbName);

            // Role "RH" scoped to tenantA
            var rhRoleId = Guid.NewGuid();
            dbA.Roles.Add(new ApplicationRole { Id = rhRoleId, Name = "RH", NormalizedName = "RH" });

            // Resource scoped to tenantA
            var resourceId = Guid.NewGuid();
            dbA.Resources.Add(new Resource
            {
                Id = resourceId,
                TenantId = tenantA,
                Code = "HR.Employees",
                Name = "Employees",
                Module = "RH",
                IsActive = true
            });

            // ResourcePermission: tenantA, RH role -> HR.Employees, Level=Write
            dbA.ResourcePermissions.Add(new ResourcePermission
            {
                TenantId = tenantA,
                RoleId = rhRoleId,
                ResourceId = resourceId,
                Level = PermissionLevel.Write
            });

            // TenantUserRole: assign user to RH role within tenantA
            dbA.TenantUserRoles.Add(new TenantUserRole
            {
                TenantId = tenantA,
                UserId = userId,
                RoleId = rhRoleId
            });

            await dbA.SaveChangesAsync();

            // UserManager returns NO global roles (user has only a per-tenant role)
            var um = MockUserManager(userId);

            // Act — query as tenantA context: expect Write
            var sutA = new ResourcePermissionService(dbA, um, ctxA);
            var levelA = await sutA.GetUserPermissionForResourceAsync(userId, "HR.Employees");
            Assert.Equal(PermissionLevel.Write, levelA);

            // Act — query as tenantB context over same DB: expect None (no cross-tenant leak)
            var ctxB = Substitute.For<ITenantContext>();
            ctxB.TenantId.Returns(tenantB);
            ctxB.HasTenant.Returns(true);

            using var dbB = NewDb(ctxB, dbName);
            var sutB = new ResourcePermissionService(dbB, um, ctxB);
            var levelB = await sutB.GetUserPermissionForResourceAsync(userId, "HR.Employees");
            Assert.Equal(PermissionLevel.None, levelB);
        }

        [Fact]
        public async Task TenantAdmin_BuildUserPermissionsDto_returns_full_for_active_resource()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var ctx = Substitute.For<ITenantContext>();
            ctx.TenantId.Returns(tenantId);
            ctx.HasTenant.Returns(true);

            using var db = NewDb(ctx);

            // Seed an active resource in this tenant
            var resourceId = Guid.NewGuid();
            db.Resources.Add(new Resource
            {
                Id = resourceId,
                TenantId = tenantId,
                Code = "HR.Employees",
                Name = "Employees",
                Module = "RH",
                IsActive = true
            });

            // Register user as tenant Owner (tenant admin)
            db.TenantUsers.Add(new TenantUser
            {
                TenantId = tenantId,
                UserId = userId,
                Role = TenantRole.Owner
            });

            await db.SaveChangesAsync();

            // UserManager: user "Owner" has no global roles
            var store = Substitute.For<IUserStore<ApplicationUser>>();
            var um = Substitute.For<UserManager<ApplicationUser>>(
                store, null, null, null, null, null, null, null, null);
            var appUser = new ApplicationUser
            {
                Id = userId,
                UserName = "owner@example.com",
                Email = "owner@example.com"
            };
            um.FindByIdAsync(userId.ToString()).Returns(appUser);
            um.GetRolesAsync(Arg.Is<ApplicationUser>(u => u.Id == userId))
              .Returns(new List<string>());

            // Act
            var sut = new ResourcePermissionService(db, um, ctx);
            var dto = await sut.GetUserPermissionsAsync(userId);

            // Assert
            Assert.NotNull(dto);
            Assert.True(dto!.ResourcePermissions.ContainsKey("HR.Employees"),
                "Expected HR.Employees in ResourcePermissions");
            Assert.Equal(PermissionLevel.Full, dto.ResourcePermissions["HR.Employees"]);
            Assert.Contains(dto.AllowedResources, r => r.Code == "HR.Employees" && r.UserPermissionLevel == PermissionLevel.Full);
        }

        [Fact]
        public async Task MasterAdmin_without_tenant_returns_empty_instead_of_throwing_on_duplicate_codes()
        {
            // Arrange — no ambient tenant, so the Resources query filter is disabled and
            // both tenants' copies of the seeded catalog are visible. Building a
            // code-keyed dictionary over that set used to throw (duplicate key) and
            // surfaced as a 500 on GET /api/resources/my-permissions.
            var ctx = Substitute.For<ITenantContext>();
            ctx.TenantId.Returns((Guid?)null);
            ctx.HasTenant.Returns(false);
            using var db = NewDb(ctx);

            var userId = Guid.NewGuid();
            foreach (var tenant in new[] { Guid.NewGuid(), Guid.NewGuid() })
            {
                db.Resources.Add(new Resource
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant,
                    Code = "HR.Employees",
                    Name = "Funcionários",
                    Module = "RH",
                    IsActive = true
                });
            }
            await db.SaveChangesAsync();

            var store = Substitute.For<IUserStore<ApplicationUser>>();
            var um = Substitute.For<UserManager<ApplicationUser>>(
                store, null, null, null, null, null, null, null, null);
            var appUser = new ApplicationUser { Id = userId, UserName = "admin@SBP.com", Email = "admin@SBP.com" };
            um.FindByIdAsync(userId.ToString()).Returns(appUser);
            um.GetRolesAsync(Arg.Is<ApplicationUser>(u => u.Id == userId))
              .Returns(new List<string> { Permissions.Roles.MasterAdmin });

            // Act
            var sut = new ResourcePermissionService(db, um, ctx);
            var dto = await sut.GetUserPermissionsAsync(userId);

            // Assert
            Assert.NotNull(dto);
            Assert.Empty(dto!.AllowedResources);
            Assert.Empty(dto.ResourcePermissions);
            Assert.Contains(Permissions.Roles.MasterAdmin, dto.Roles);
        }
    }
}
