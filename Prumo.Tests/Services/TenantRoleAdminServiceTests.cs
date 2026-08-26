using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Prumo.Application.DTOs.Roles;
using Prumo.Application.Services;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Services
{
    /// <summary>
    /// Uma role criada por um tenant não pode aparecer para outro. É a mesma classe de
    /// vazamento que originou o rework de RBAC — roles do Identity eram globais e o
    /// acesso vazava entre tenants. Backlog item 3A.
    /// </summary>
    public class TenantRoleAdminServiceTests
    {
        private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static ApplicationDbContext NewDb(Guid tenantId, string dbName)
        {
            var ctx = new TenantContext();
            ctx.SetTenant(tenantId);
            return new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(dbName).Options, ctx);
        }

        /// <summary>
        /// RoleManager fino sobre o próprio DbContext: só precisamos de NormalizeKey,
        /// CreateAsync e DeleteAsync, e queremos que escrevam no mesmo banco em memória
        /// que o service consulta.
        /// </summary>
        private static RoleManager<ApplicationRole> RoleManagerOver(ApplicationDbContext db)
        {
            var manager = Substitute.For<RoleManager<ApplicationRole>>(
                Substitute.For<IRoleStore<ApplicationRole>>(),
                null, null, null, null);

            manager.NormalizeKey(Arg.Any<string>())
                   .Returns(call => call.Arg<string>()?.ToUpperInvariant());

            manager.CreateAsync(Arg.Any<ApplicationRole>())
                   .Returns(call =>
                   {
                       var role = call.Arg<ApplicationRole>();
                       role.NormalizedName = role.Name?.ToUpperInvariant();
                       db.Roles.Add(role);
                       db.SaveChanges();
                       return Task.FromResult(IdentityResult.Success);
                   });

            manager.DeleteAsync(Arg.Any<ApplicationRole>())
                   .Returns(call =>
                   {
                       db.Roles.Remove(call.Arg<ApplicationRole>());
                       db.SaveChanges();
                       return Task.FromResult(IdentityResult.Success);
                   });

            return manager;
        }

        private static void SeedCanonicalRole(ApplicationDbContext db, string name)
        {
            db.Roles.Add(new ApplicationRole
            {
                Id = Guid.NewGuid(),
                Name = name,
                NormalizedName = name.ToUpperInvariant(),
                TenantId = null
            });
            db.SaveChanges();
        }

        private static TenantRoleAdminService ServiceOver(ApplicationDbContext db) =>
            new(db, RoleManagerOver(db));

        [Fact]
        public async Task Role_criada_por_um_tenant_aparece_na_lista_dele()
        {
            using var db = NewDb(TenantA, nameof(Role_criada_por_um_tenant_aparece_na_lista_dele));
            var service = ServiceOver(db);

            await service.CreateAsync(TenantA, new CreateTenantRoleDto("Leitura", null));

            var visiveis = await service.GetVisibleRolesAsync(TenantA);

            Assert.Contains(visiveis, r => r.Name == "Leitura" && !r.IsCanonical);
        }

        [Fact]
        public async Task Role_criada_por_um_tenant_NAO_aparece_para_outro()
        {
            using var db = NewDb(TenantA, nameof(Role_criada_por_um_tenant_NAO_aparece_para_outro));
            var service = ServiceOver(db);

            await service.CreateAsync(TenantA, new CreateTenantRoleDto("Leitura", null));

            var visiveisEmB = await service.GetVisibleRolesAsync(TenantB);

            Assert.DoesNotContain(visiveisEmB, r => r.Name == "Leitura");
        }

        [Fact]
        public async Task As_roles_canonicas_continuam_visiveis_em_qualquer_tenant()
        {
            using var db = NewDb(TenantA, nameof(As_roles_canonicas_continuam_visiveis_em_qualquer_tenant));
            SeedCanonicalRole(db, "RH");
            var service = ServiceOver(db);

            var visiveisEmB = await service.GetVisibleRolesAsync(TenantB);

            Assert.Contains(visiveisEmB, r => r.Name == "RH" && r.IsCanonical);
        }

        [Fact]
        public async Task Role_do_tenant_vira_chave_atribuivel_so_no_tenant_dono()
        {
            using var db = NewDb(TenantA, nameof(Role_do_tenant_vira_chave_atribuivel_so_no_tenant_dono));
            var service = ServiceOver(db);

            await service.CreateAsync(TenantA, new CreateTenantRoleDto("Leitura", null));

            var atribuiveisEmA = await service.GetAssignableRoleNamesAsync(TenantA);
            var atribuiveisEmB = await service.GetAssignableRoleNamesAsync(TenantB);

            Assert.Contains("Leitura", atribuiveisEmA);
            Assert.DoesNotContain("Leitura", atribuiveisEmB);

            // As canônicas seguem atribuíveis nos dois, e o master continua fora.
            Assert.Contains("RH", atribuiveisEmA);
            Assert.Contains("RH", atribuiveisEmB);
            Assert.DoesNotContain("Administrador", atribuiveisEmA);
        }

        [Fact]
        public async Task Uma_role_criada_pelo_tenant_pode_ser_concedida_a_um_membro()
        {
            using var db = NewDb(TenantA, nameof(Uma_role_criada_pelo_tenant_pode_ser_concedida_a_um_membro));
            var admin = ServiceOver(db);
            var roles = new TenantRoleService(db);

            await admin.CreateAsync(TenantA, new CreateTenantRoleDto("Leitura", null));

            var userId = Guid.NewGuid();
            await roles.AssignFeatureRoleAsync(TenantA, userId, "Leitura");

            var concedidas = await roles.GetTenantRoleNamesAsync(userId, TenantA);
            Assert.Contains("Leitura", concedidas);
        }

        [Fact]
        public async Task Nao_deixa_conceder_a_role_de_outro_tenant()
        {
            using var db = NewDb(TenantA, nameof(Nao_deixa_conceder_a_role_de_outro_tenant));
            var admin = ServiceOver(db);
            var roles = new TenantRoleService(db);

            // A role existe, mas pertence ao tenant A.
            await admin.CreateAsync(TenantA, new CreateTenantRoleDto("Leitura", null));

            // O tenant B não pode concedê-la só por saber o nome.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => roles.AssignFeatureRoleAsync(TenantB, Guid.NewGuid(), "Leitura"));
        }

        [Fact]
        public async Task Nao_deixa_recriar_uma_role_do_sistema()
        {
            using var db = NewDb(TenantA, nameof(Nao_deixa_recriar_uma_role_do_sistema));
            SeedCanonicalRole(db, "RH");
            var service = ServiceOver(db);

            var erro = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateAsync(TenantA, new CreateTenantRoleDto("rh", null)));

            Assert.Contains("role do sistema", erro.Message);
        }

        [Fact]
        public async Task Nao_deixa_um_tenant_excluir_a_role_de_outro()
        {
            using var db = NewDb(TenantA, nameof(Nao_deixa_um_tenant_excluir_a_role_de_outro));
            var service = ServiceOver(db);

            var criada = await service.CreateAsync(TenantA, new CreateTenantRoleDto("Leitura", null));

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => service.DeleteAsync(TenantB, criada.Id));
        }

        [Fact]
        public async Task Nao_deixa_excluir_role_ainda_atribuida_a_um_membro()
        {
            using var db = NewDb(TenantA, nameof(Nao_deixa_excluir_role_ainda_atribuida_a_um_membro));
            var service = ServiceOver(db);

            var criada = await service.CreateAsync(TenantA, new CreateTenantRoleDto("Leitura", null));

            db.TenantUserRoles.Add(new TenantUserRole
            {
                TenantId = TenantA,
                UserId = Guid.NewGuid(),
                RoleId = criada.Id
            });
            await db.SaveChangesAsync();

            var erro = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.DeleteAsync(TenantA, criada.Id));

            Assert.Contains("atribuída a membros", erro.Message);
        }
    }
}
