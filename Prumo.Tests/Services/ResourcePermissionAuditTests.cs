using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Prumo.Application.DTOs;
using Prumo.Application.Services;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Services
{
    /// <summary>
    /// Granting and revoking a resource level is what actually gives and takes access, so
    /// it has to leave a trail: revoking deletes the permission row, and the history must
    /// not go with it.
    /// </summary>
    public class ResourcePermissionAuditTests
    {
        private static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid PerformerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        private static ApplicationDbContext NewDb(string dbName)
        {
            var ctx = new TenantContext();
            ctx.SetTenant(TenantId);
            return new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(dbName).Options, ctx);
        }

        private static UserManager<ApplicationUser> MockUserManager() =>
            Substitute.For<UserManager<ApplicationUser>>(
                Substitute.For<IUserStore<ApplicationUser>>(),
                null, null, null, null, null, null, null, null);

        private static async Task<(Guid roleId, Guid resourceId)> SeedAsync(ApplicationDbContext db)
        {
            var role = new ApplicationRole
            {
                Id = Guid.NewGuid(),
                Name = "Viewer",
                NormalizedName = "VIEWER",
                TenantId = TenantId
            };
            var resource = new Resource
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "HR.Employees",
                Name = "Employees",
                Module = "HR"
            };

            db.Roles.Add(role);
            db.Resources.Add(resource);
            await db.SaveChangesAsync();

            return (role.Id, resource.Id);
        }

        private static ResourcePermissionService ServiceOver(ApplicationDbContext db) =>
            new(db, MockUserManager(), new TenantContext());

        [Fact]
        public async Task Granting_a_level_records_who_gave_what_to_whom()
        {
            await using var db = NewDb(nameof(Granting_a_level_records_who_gave_what_to_whom));
            var (roleId, resourceId) = await SeedAsync(db);
            var service = ServiceOver(db);

            await service.AssignPermissionAsync(
                new AssignResourcePermissionDto { RoleId = roleId, ResourceId = resourceId, Level = PermissionLevel.Read },
                "admin@x.com", PerformerId);

            var log = await db.ResourcePermissionAuditLogs.IgnoreQueryFilters().SingleAsync();

            Assert.Equal("Viewer", log.RoleName);
            Assert.Equal("HR.Employees", log.ResourceCode);
            Assert.Equal(PermissionLevel.None, log.PreviousLevel);
            Assert.Equal(PermissionLevel.Read, log.NewLevel);
            Assert.Equal(PerformerId, log.PerformedByUserId);
            Assert.Equal("admin@x.com", log.PerformedByUserEmail);
        }

        [Fact]
        public async Task Revoking_records_the_return_to_None()
        {
            await using var db = NewDb(nameof(Revoking_records_the_return_to_None));
            var (roleId, resourceId) = await SeedAsync(db);
            var service = ServiceOver(db);

            await service.AssignPermissionAsync(
                new AssignResourcePermissionDto { RoleId = roleId, ResourceId = resourceId, Level = PermissionLevel.Write },
                "admin@x.com", PerformerId);

            await service.RemovePermissionAsync(roleId, resourceId, "admin@x.com", PerformerId);

            var logs = await db.ResourcePermissionAuditLogs
                .IgnoreQueryFilters()
                .OrderBy(l => l.PerformedAt)
                .ToListAsync();

            Assert.Equal(2, logs.Count);
            Assert.Equal(PermissionLevel.Write, logs[1].PreviousLevel);
            Assert.Equal(PermissionLevel.None, logs[1].NewLevel);
        }

        [Fact]
        public async Task Rewriting_the_same_level_does_not_pollute_the_history()
        {
            await using var db = NewDb(nameof(Rewriting_the_same_level_does_not_pollute_the_history));
            var (roleId, resourceId) = await SeedAsync(db);
            var service = ServiceOver(db);

            var dto = new AssignResourcePermissionDto
            {
                RoleId = roleId,
                ResourceId = resourceId,
                Level = PermissionLevel.Read
            };

            await service.AssignPermissionAsync(dto, "admin@x.com", PerformerId);
            await service.AssignPermissionAsync(dto, "admin@x.com", PerformerId);

            var logs = await db.ResourcePermissionAuditLogs.IgnoreQueryFilters().ToListAsync();

            Assert.Single(logs);
        }

        [Fact]
        public async Task The_role_name_survives_its_deletion()
        {
            await using var db = NewDb(nameof(The_role_name_survives_its_deletion));
            var (roleId, resourceId) = await SeedAsync(db);
            var service = ServiceOver(db);

            await service.AssignPermissionAsync(
                new AssignResourcePermissionDto { RoleId = roleId, ResourceId = resourceId, Level = PermissionLevel.Read },
                "admin@x.com", PerformerId);

            // The role goes away; the history must not turn into a list of orphaned GUIDs.
            db.Roles.Remove(await db.Roles.SingleAsync(r => r.Id == roleId));
            await db.SaveChangesAsync();

            var log = await db.ResourcePermissionAuditLogs.IgnoreQueryFilters().SingleAsync();
            Assert.Equal("Viewer", log.RoleName);
        }
    }
}
