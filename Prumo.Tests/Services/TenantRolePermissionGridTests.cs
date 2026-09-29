using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Prumo.Application.Services;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Services
{
    /// <summary>
    /// The level grid on the Roles screen, as a tenant Owner/Admin uses it. The endpoints
    /// used to be master-only, so a tenant could create a role but never give it access.
    /// Now they run under the tenant route, and these tests hold the tenant boundary: a
    /// role or a resource that is not visible in the tenant is treated as not found.
    /// </summary>
    public class TenantRolePermissionGridTests
    {
        private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

        private static ApplicationDbContext NewDb(Guid tenantId, string dbName)
        {
            var ctx = new TenantContext();
            ctx.SetTenant(tenantId);
            return new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(dbName).Options, ctx);
        }

        private static ResourcePermissionService ServiceOver(ApplicationDbContext db) =>
            new(db,
                Substitute.For<UserManager<ApplicationUser>>(
                    Substitute.For<IUserStore<ApplicationUser>>(),
                    null, null, null, null, null, null, null, null),
                new TenantContext());

        private static ApplicationRole Role(string name, Guid? tenantId) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            TenantId = tenantId
        };

        private static Resource Resource(Guid tenantId, string code) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = code,
            Module = "HR",
            IsActive = true
        };

        [Fact]
        public async Task A_tenant_sets_a_level_on_its_own_role_and_reads_it_back()
        {
            using var db = NewDb(TenantA, nameof(A_tenant_sets_a_level_on_its_own_role_and_reads_it_back));
            var role = Role("Viewer", TenantA);
            var resource = Resource(TenantA, "HR.Employees");
            db.Roles.Add(role);
            db.Resources.Add(resource);
            await db.SaveChangesAsync();
            var service = ServiceOver(db);

            var done = await service.SetTenantRolePermissionAsync(
                TenantA, role.Id, resource.Id, PermissionLevel.Read, "owner@x.com", Guid.NewGuid());
            var levels = await service.GetTenantRolePermissionsAsync(TenantA, role.Id);

            Assert.True(done);
            var row = Assert.Single(levels!);
            Assert.Equal(resource.Id, row.ResourceId);
            Assert.Equal(PermissionLevel.Read, row.Level);
        }

        [Fact]
        public async Task A_tenant_can_set_levels_on_a_canonical_role()
        {
            using var db = NewDb(TenantA, nameof(A_tenant_can_set_levels_on_a_canonical_role));
            var role = Role("HR", tenantId: null);
            var resource = Resource(TenantA, "HR.Employees");
            db.Roles.Add(role);
            db.Resources.Add(resource);
            await db.SaveChangesAsync();

            var done = await ServiceOver(db).SetTenantRolePermissionAsync(
                TenantA, role.Id, resource.Id, PermissionLevel.Write);

            Assert.True(done);
            var permission = Assert.Single(await db.ResourcePermissions.ToListAsync());
            Assert.Equal(TenantA, permission.TenantId);
        }

        [Fact]
        public async Task Another_tenants_role_is_not_found()
        {
            var dbName = nameof(Another_tenants_role_is_not_found);
            using var db = NewDb(TenantA, dbName);
            var foreignRole = Role("Viewer", TenantB);
            var resource = Resource(TenantA, "HR.Employees");
            db.Roles.Add(foreignRole);
            db.Resources.Add(resource);
            await db.SaveChangesAsync();
            var service = ServiceOver(db);

            var done = await service.SetTenantRolePermissionAsync(
                TenantA, foreignRole.Id, resource.Id, PermissionLevel.Full);
            var levels = await service.GetTenantRolePermissionsAsync(TenantA, foreignRole.Id);

            Assert.False(done);
            Assert.Null(levels);
            Assert.Empty(await db.ResourcePermissions.IgnoreQueryFilters().ToListAsync());
        }

        [Fact]
        public async Task Another_tenants_resource_is_not_found()
        {
            var dbName = nameof(Another_tenants_resource_is_not_found);
            Resource foreignResource;
            using (var dbB = NewDb(TenantB, dbName))
            {
                foreignResource = Resource(TenantB, "HR.Employees");
                dbB.Resources.Add(foreignResource);
                await dbB.SaveChangesAsync();
            }

            using var db = NewDb(TenantA, dbName);
            var role = Role("Viewer", TenantA);
            db.Roles.Add(role);
            await db.SaveChangesAsync();

            var done = await ServiceOver(db).SetTenantRolePermissionAsync(
                TenantA, role.Id, foreignResource.Id, PermissionLevel.Full);

            Assert.False(done);
            Assert.Empty(await db.ResourcePermissions.IgnoreQueryFilters().ToListAsync());
        }

        [Fact]
        public async Task None_revokes_and_the_audit_log_records_it()
        {
            using var db = NewDb(TenantA, nameof(None_revokes_and_the_audit_log_records_it));
            var role = Role("Viewer", TenantA);
            var resource = Resource(TenantA, "HR.Employees");
            db.Roles.Add(role);
            db.Resources.Add(resource);
            await db.SaveChangesAsync();
            var service = ServiceOver(db);

            await service.SetTenantRolePermissionAsync(TenantA, role.Id, resource.Id, PermissionLevel.Write);
            var done = await service.SetTenantRolePermissionAsync(TenantA, role.Id, resource.Id, PermissionLevel.None);

            Assert.True(done);
            Assert.Empty(await db.ResourcePermissions.ToListAsync());
            var last = (await db.ResourcePermissionAuditLogs.ToListAsync())
                .OrderBy(l => l.PerformedAt).Last();
            Assert.Equal(PermissionLevel.Write, last.PreviousLevel);
            Assert.Equal(PermissionLevel.None, last.NewLevel);
        }

        [Fact]
        public async Task The_resource_list_only_has_the_tenants_own_resources()
        {
            var dbName = nameof(The_resource_list_only_has_the_tenants_own_resources);
            using (var dbB = NewDb(TenantB, dbName))
            {
                dbB.Resources.Add(Resource(TenantB, "HR.Payments"));
                await dbB.SaveChangesAsync();
            }

            using var db = NewDb(TenantA, dbName);
            db.Resources.Add(Resource(TenantA, "HR.Employees"));
            await db.SaveChangesAsync();

            var resources = await ServiceOver(db).GetTenantResourcesAsync(TenantA);

            var only = Assert.Single(resources);
            Assert.Equal("HR.Employees", only.Code);
        }
    }
}
