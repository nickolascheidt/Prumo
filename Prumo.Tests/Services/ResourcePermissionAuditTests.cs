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
    /// Conceder e revogar nível de recurso é o que de fato dá e tira acesso, e até o
    /// item 3B isso não deixava rastro nenhum: a linha guardava quem criou, mas revogar
    /// apagava a linha e o histórico junto.
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
                Name = "Leitura",
                NormalizedName = "LEITURA",
                TenantId = TenantId
            };
            var resource = new Resource
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "HR.Employees",
                Name = "Funcionários",
                Module = "RH"
            };

            db.Roles.Add(role);
            db.Resources.Add(resource);
            await db.SaveChangesAsync();

            return (role.Id, resource.Id);
        }

        private static ResourcePermissionService ServiceOver(ApplicationDbContext db) =>
            new(db, MockUserManager(), new TenantContext());

        [Fact]
        public async Task Conceder_nivel_registra_quem_deu_o_que_e_para_quem()
        {
            await using var db = NewDb(nameof(Conceder_nivel_registra_quem_deu_o_que_e_para_quem));
            var (roleId, resourceId) = await SeedAsync(db);
            var service = ServiceOver(db);

            await service.AssignPermissionAsync(
                new AssignResourcePermissionDto { RoleId = roleId, ResourceId = resourceId, Level = PermissionLevel.Read },
                "admin@x.com", PerformerId);

            var log = await db.ResourcePermissionAuditLogs.IgnoreQueryFilters().SingleAsync();

            Assert.Equal("Leitura", log.RoleName);
            Assert.Equal("HR.Employees", log.ResourceCode);
            Assert.Equal(PermissionLevel.None, log.PreviousLevel);
            Assert.Equal(PermissionLevel.Read, log.NewLevel);
            Assert.Equal(PerformerId, log.PerformedByUserId);
            Assert.Equal("admin@x.com", log.PerformedByUserEmail);
        }

        [Fact]
        public async Task Revogar_registra_a_volta_para_None()
        {
            await using var db = NewDb(nameof(Revogar_registra_a_volta_para_None));
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
        public async Task Regravar_o_mesmo_nivel_nao_polui_o_historico()
        {
            await using var db = NewDb(nameof(Regravar_o_mesmo_nivel_nao_polui_o_historico));
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
        public async Task O_nome_da_role_sobrevive_a_exclusao_dela()
        {
            await using var db = NewDb(nameof(O_nome_da_role_sobrevive_a_exclusao_dela));
            var (roleId, resourceId) = await SeedAsync(db);
            var service = ServiceOver(db);

            await service.AssignPermissionAsync(
                new AssignResourcePermissionDto { RoleId = roleId, ResourceId = resourceId, Level = PermissionLevel.Read },
                "admin@x.com", PerformerId);

            // A role some; o histórico não pode virar uma lista de GUIDs órfãos.
            db.Roles.Remove(await db.Roles.SingleAsync(r => r.Id == roleId));
            await db.SaveChangesAsync();

            var log = await db.ResourcePermissionAuditLogs.IgnoreQueryFilters().SingleAsync();
            Assert.Equal("Leitura", log.RoleName);
        }
    }
}
