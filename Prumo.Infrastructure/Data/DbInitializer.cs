using Prumo.Domain.Authorization;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data.Seeders;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Prumo.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
                var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();
                var configuration = services.GetRequiredService<IConfiguration>();
                var environment = services.GetRequiredService<IHostEnvironment>();

                logger.LogInformation("=== Iniciando inicialização do banco de dados ===");

                // Aplicar migrations pendentes (cria o banco e schema se necessário)
                await context.Database.MigrateAsync();
                logger.LogInformation("Banco de dados verificado/criado com sucesso");

                // One-time cleanup: remove legacy "User" role if it exists
                var legacyUserRole = await roleManager.FindByNameAsync("User");
                if (legacyUserRole != null)
                {
                    // cross-tenant de propósito: limpeza da role legada "User" em TODOS os
                    // tenants, no startup e sem TenantContext. Filtrar por tenant aqui
                    // deixaria lixo para trás em todos os outros.
                    var legacyRps = context.RolePermissions
                        .IgnoreQueryFilters()
                        .Where(rp => rp.RoleId == legacyUserRole.Id);
                    context.RolePermissions.RemoveRange(legacyRps);
                    await context.SaveChangesAsync();
                    await roleManager.DeleteAsync(legacyUserRole);
                    logger.LogInformation("✓ Role legada 'User' removida");
                }

                // Criar roles se não existirem
                var rolesConfig = new Dictionary<string, string>
                {
                    { "Administrador", "Acesso total ao sistema" },
                    { "Funcionario",   "Acesso para funcionários do sistema" },
                    { "Cliente",       "Acesso para clientes" },
                    { "RH",            "Acesso ao módulo de Recursos Humanos" },
                    { "Financeiro",    "Acesso ao módulo Financeiro" },
                    { "ContasAPagar",  "Acesso ao módulo de Contas a Pagar" }
                };

                foreach (var (roleName, description) in rolesConfig)
                {
                    var roleExists = await roleManager.RoleExistsAsync(roleName);
                    if (!roleExists)
                    {
                        var role = new ApplicationRole
                        {
                            Name = roleName,
                            Description = description,
                            CreatedAt = DateTime.UtcNow
                        };
                        var roleResult = await roleManager.CreateAsync(role);
                        if (roleResult.Succeeded)
                        {
                            logger.LogInformation($"✓ Role '{roleName}' criada com sucesso!");
                        }
                        else
                        {
                            var roleErrors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                            logger.LogError($"✗ Erro ao criar role '{roleName}': {roleErrors}");
                        }
                    }
                    else
                    {
                        logger.LogDebug($"✓ Role '{roleName}' já existe");
                    }
                }

                // Criar permissões se não existirem
                logger.LogInformation("Criando permissões...");
                var allPermissions = Permissions.GetAllPermissions();
                var permissionMap = new Dictionary<string, Permission>();

                foreach (var permissionName in allPermissions)
                {
                    var existingPermission = await context.Permissions
                        .FirstOrDefaultAsync(p => p.Name == permissionName);

                    if (existingPermission == null)
                    {
                        var permission = new Permission
                        {
                            Name = permissionName,
                            Description = $"Permissão: {permissionName}",
                            CreatedAt = DateTime.UtcNow
                        };
                        context.Permissions.Add(permission);
                        await context.SaveChangesAsync();
                        permissionMap[permissionName] = permission;
                        logger.LogInformation($"✓ Permissão '{permissionName}' criada");
                    }
                    else
                    {
                        permissionMap[permissionName] = existingPermission;
                    }
                }

                // Tenant-scoped role permissions and resources are seeded per-tenant via
                // TenantBootstrapSeeder when a tenant is created (see TenantService.CreateAsync).
                // Reaplicar isso no startup ressuscitava grants revogados — ver
                // SeederIdempotenceTests. Tenants antigos são cobertos pela migration de backfill.

                // Backfill: convert legacy global feature-role assignments to per-tenant rows.
                var masterRoleId = (await roleManager.FindByNameAsync(Permissions.Roles.MasterAdmin))?.Id;
                if (masterRoleId is null)
                {
                    logger.LogWarning("Master role '{Role}' not found; skipping per-tenant role backfill.", Permissions.Roles.MasterAdmin);
                }
                else
                {
                    var globalAssignments = await (
                        from ur in context.UserRoles
                        join r in context.Roles on ur.RoleId equals r.Id
                        where r.Id != masterRoleId
                        select new { ur.UserId, r.Id, r.Name }
                    ).ToListAsync();

                    if (globalAssignments.Count > 0)
                    {
                        await BackfillTenantUserRolesAsync(
                            context,
                            globalAssignments.Select(a => (a.UserId, a.Id, a.Name!)).ToList());

                        var toRemove = await context.UserRoles
                            .Where(ur => ur.RoleId != masterRoleId.Value)
                            .ToListAsync();
                        context.UserRoles.RemoveRange(toRemove);
                        await context.SaveChangesAsync();
                        logger.LogInformation("✓ Backfilled {Count} per-tenant role rows", globalAssignments.Count);
                    }
                }

                // Verificar se já existe o admin
                var adminUser = await userManager.FindByEmailAsync("admin@SBP.com");
                if (adminUser != null)
                {
                    logger.LogDebug("✓ Usuário admin já existe.");

                    // Verificar se tem a role
                    var hasRole = await userManager.IsInRoleAsync(adminUser, "Administrador");
                    if (!hasRole)
                    {
                        await userManager.AddToRoleAsync(adminUser, "Administrador");
                        logger.LogInformation("✓ Role Administrador adicionada ao usuário admin");
                    }

                    await EnsureDefaultTenantAsync(context, adminUser, logger);
                    await SyncResourceCatalogAsync(context, logger);

                    logger.LogInformation("=== Inicialização concluída ===");
                    return;
                }

                // Criar usuário admin
                var seedPassword = configuration["Seed:AdminPassword"];
                var isDevelopment = environment.IsDevelopment() || environment.IsEnvironment("Demo");

                if (string.IsNullOrWhiteSpace(seedPassword))
                {
                    if (isDevelopment)
                    {
                        logger.LogWarning(
                            "Admin não criado: defina Seed:AdminPassword (user secrets) para semear o admin local.");
                        logger.LogInformation("=== Inicialização concluída ===");
                        return;
                    }

                    throw new InvalidOperationException(
                        "Seed:AdminPassword não configurada. Em Production o admin master nunca é criado "
                        + "com senha padrão — forneça Seed__AdminPassword por variável de ambiente ou "
                        + "remova o seeding de admin deste ambiente.");
                }

                logger.LogInformation("Criando usuário administrador...");
                adminUser = new ApplicationUser
                {
                    UserName = "admin@SBP.com",
                    Email = "admin@SBP.com",
                    EmailConfirmed = true,
                    FullName = "Administrador do Sistema",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(adminUser, seedPassword);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrador");
                    await EnsureDefaultTenantAsync(context, adminUser, logger);
                    // A senha NUNCA vai para o log: o Serilog tem sink para tabela, e isso
                    // depositaria a credencial do admin master no armazenamento de log.
                    logger.LogInformation("✓ Usuário admin criado: {Email}", adminUser.Email);
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger.LogError("✗ Erro ao criar usuário admin: {Errors}", errors);
                }

                await SyncResourceCatalogAsync(context, logger);

                logger.LogInformation("=== Inicialização concluída ===");
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();
                logger.LogError(ex, "Erro ao inicializar o banco de dados.");
            }
        }

        /// <summary>
        /// For each (userId, roleId) global feature-role assignment, create a
        /// per-tenant TenantUserRole for every tenant the user belongs to.
        /// Idempotent. Does not touch the master-admin role.
        /// </summary>
        public static async Task BackfillTenantUserRolesAsync(
            ApplicationDbContext context,
            IReadOnlyCollection<(Guid UserId, Guid RoleId, string RoleName)> globalAssignments,
            CancellationToken cancellationToken = default)
        {
            foreach (var (userId, roleId, roleName) in globalAssignments)
            {
                if (roleName == Domain.Authorization.Permissions.Roles.MasterAdmin)
                    continue;

                // cross-tenant de propósito: o backfill precisa descobrir TODOS os tenants
                // de que o usuário participa. TenantUser nem é ITenantScoped, então esta
                // chamada também não contorna nada — some quando o backfill sair.
                var tenantIds = await context.TenantUsers
                    .IgnoreQueryFilters()
                    .Where(tu => tu.UserId == userId)
                    .Select(tu => tu.TenantId)
                    .ToListAsync(cancellationToken);

                foreach (var tenantId in tenantIds)
                {
                    // cross-tenant de propósito: o backfill percorre vários tenants numa
                    // volta só, sem TenantContext. Sem o bypass a checagem não acharia a
                    // linha existente e o backfill duplicaria as concessões.
                    var exists = await context.TenantUserRoles
                        .IgnoreQueryFilters()
                        .AnyAsync(tur => tur.TenantId == tenantId
                                      && tur.UserId == userId
                                      && tur.RoleId == roleId, cancellationToken);
                    if (!exists)
                    {
                        context.TenantUserRoles.Add(new TenantUserRole
                        {
                            TenantId = tenantId,
                            UserId = userId,
                            RoleId = roleId,
                            GrantedAt = DateTime.UtcNow
                        });
                    }
                }
            }
            await context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Garante o tenant 'default' e a associação do admin. Semeia SÓ no nascimento
        /// do tenant — reaplicar os seeders a cada boot ressuscitava grants revogados
        /// (bug 4203a15). `internal` para que DbInitializerReseedTests possa provar isso;
        /// o `InitializeAsync` inteiro não é testável em memória porque chama
        /// `MigrateAsync`, que exige provider relacional.
        /// </summary>
        /// <summary>
        /// Completa o catálogo de recursos de todo tenant a cada boot.
        /// </summary>
        /// <remarks>
        /// Sem isto, um módulo novo só chega a tenants criados <b>depois</b> dele: o
        /// <c>TenantBootstrapSeeder</c> roda uma vez, na criação do tenant, e até aqui a
        /// solução tinha sido escrever uma migration de backfill por recurso novo
        /// (ver <c>BackfillTenantResources</c>) — trabalho manual, fácil de esquecer, e
        /// que deixa a tela nova invisível justamente nos tenants que já existem.
        ///
        /// Só acrescenta linhas de <c>Resource</c>. Nenhuma permissão é tocada, então
        /// nada de revogado volta.
        /// </remarks>
        private static async Task SyncResourceCatalogAsync(
            ApplicationDbContext context, ILogger logger)
        {
            try
            {
                // cross-tenant de propósito: contagem de todos os tenants, no startup,
                // sem TenantContext resolvido — serve só para relatar quantos recursos
                // foram acrescentados.
                var before = await context.Resources.IgnoreQueryFilters().CountAsync();

                await Seeders.TenantBootstrapSeeder.SyncResourcesForAllTenantsAsync(context);

                // cross-tenant de propósito: mesma contagem, depois da sincronização.
                var after = await context.Resources.IgnoreQueryFilters().CountAsync();

                if (after > before)
                {
                    logger.LogInformation(
                        "✓ Catálogo de recursos sincronizado: {Count} recurso(s) acrescentado(s) a tenants existentes",
                        after - before);
                }

                await BackfillDashboardGrantsAsync(context, logger);
            }
            catch (Exception ex)
            {
                // Não derruba o startup: sem os recursos novos a app sobe com as telas
                // novas invisíveis, o que é ruim mas recuperável. Cair aqui não seria.
                logger.LogError(ex, "✗ Falha ao sincronizar o catálogo de recursos");
            }
        }

        /// <summary>
        /// Aba do dashboard que passou a ter recurso próprio, e o recurso que ela
        /// emprestava antes.
        /// </summary>
        private static readonly (string Dashboard, string PreviouslyGatedBy)[] DashboardMigrationMap =
        {
            ("Dashboard.Accounting", "GeneralLedger.Management"),
            ("Dashboard.Finance",    "AccountsPayable.Entries"),
            ("Dashboard.HR",         "HR.Employees"),
            ("Dashboard.Admin",      "User.Management")
        };

        /// <summary>
        /// Concede cada <c>Dashboard.*</c> novo a quem já enxergava aquela aba pelo
        /// recurso emprestado, para que ninguém perca acesso na troca.
        /// </summary>
        /// <remarks>
        /// Roda uma vez por par (role, recurso): se o grant já existe, é pulado. Isso o
        /// torna idempotente e — importante — faz com que revogar o acesso ao dashboard
        /// depois <b>não</b> seja desfeito no próximo boot, porque a linha reaparecendo
        /// exigiria que ela não existisse, e ela existe até alguém apagá-la de propósito.
        ///
        /// A ressalva honesta: se o admin revogar `Dashboard.HR` e mantiver
        /// `HR.Employees`, esta rotina reconcede no boot seguinte. É o mesmo formato do
        /// bug 4203a15, mitigado por rodar só enquanto a coluna de origem existir — a
        /// intenção é remover este backfill assim que os tenants estiverem migrados.
        /// </remarks>
        private static async Task BackfillDashboardGrantsAsync(
            ApplicationDbContext context, ILogger logger)
        {
            // cross-tenant de propósito: roda no startup, sem TenantContext, e precisa
            // enxergar os recursos e grants de todos os tenants.
            var resources = await context.Resources
                .IgnoreQueryFilters()
                .Select(r => new { r.Id, r.Code, r.TenantId })
                .ToListAsync();

            var granted = 0;

            foreach (var (dashboardCode, sourceCode) in DashboardMigrationMap)
            {
                var dashboards = resources.Where(r => r.Code == dashboardCode).ToList();

                foreach (var dashboard in dashboards)
                {
                    var source = resources.FirstOrDefault(
                        r => r.Code == sourceCode && r.TenantId == dashboard.TenantId);

                    if (source is null) continue;

                    // cross-tenant de propósito: mesmo motivo, e o TenantId entra no filtro.
                    var sourceGrants = await context.ResourcePermissions
                        .IgnoreQueryFilters()
                        .Where(rp => rp.ResourceId == source.Id && rp.TenantId == dashboard.TenantId)
                        .Select(rp => new { rp.RoleId, rp.Level })
                        .ToListAsync();

                    foreach (var grant in sourceGrants)
                    {
                        // cross-tenant de propósito: mesmo motivo, e o TenantId entra no filtro.
                        var alreadyThere = await context.ResourcePermissions
                            .IgnoreQueryFilters()
                            .AnyAsync(rp => rp.ResourceId == dashboard.Id
                                         && rp.RoleId == grant.RoleId
                                         && rp.TenantId == dashboard.TenantId);

                        if (alreadyThere) continue;

                        context.ResourcePermissions.Add(new ResourcePermission
                        {
                            TenantId = dashboard.TenantId,
                            ResourceId = dashboard.Id,
                            RoleId = grant.RoleId,
                            // Dashboard é só leitura: nem Write nem Full significam nada
                            // numa tela que só mostra números.
                            Level = PermissionLevel.Read
                        });
                        granted++;
                    }
                }
            }

            if (granted > 0)
            {
                await context.SaveChangesAsync();
                logger.LogInformation(
                    "✓ {Count} acesso(s) a dashboard concedido(s) a quem já enxergava a aba pelo recurso do módulo",
                    granted);
            }
        }

        internal static async Task EnsureDefaultTenantAsync(
            ApplicationDbContext context,
            ApplicationUser owner,
            ILogger logger)
        {
            const string defaultSlug = "default";

            // cross-tenant de propósito: procurar o tenant "default" é o passo que decide
            // se ele precisa ser criado — não existe tenant resolvido antes disso.
            var tenant = await context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == defaultSlug);

            if (tenant == null)
            {
                tenant = new Tenant
                {
                    Name = "Default",
                    Slug = defaultSlug,
                    OwnerUserId = owner.Id
                };
                context.Tenants.Add(tenant);
                context.TenantUsers.Add(new TenantUser
                {
                    TenantId = tenant.Id,
                    UserId = owner.Id,
                    Role = Domain.Enums.TenantRole.Owner
                });
                await context.SaveChangesAsync();
                logger.LogInformation("✓ Tenant 'default' criado para o usuário admin");

                // Semeia SÓ no nascimento do tenant. Reaplicar isso a cada startup
                // ressuscitava grants revogados — ver SeederIdempotenceTests. Tenants
                // que já existem são cobertos pela migration de backfill.
                await Seeders.TenantBootstrapSeeder.SeedAsync(context, tenant.Id);
                await Seeders.ChartOfAccountsSeeder.SeedAsync(context, tenant.Id);
            }
            else
            {
                // cross-tenant de propósito: startup sem TenantContext. Sem o bypass a
                // checagem daria falso e o seeder recriaria a associação do owner a cada
                // boot — a mesma classe de bug do 4203a15.
                var membershipExists = await context.TenantUsers
                    .IgnoreQueryFilters()
                    .AnyAsync(tu => tu.TenantId == tenant.Id && tu.UserId == owner.Id);
                if (!membershipExists)
                {
                    context.TenantUsers.Add(new TenantUser
                    {
                        TenantId = tenant.Id,
                        UserId = owner.Id,
                        Role = Domain.Enums.TenantRole.Owner
                    });
                    await context.SaveChangesAsync();
                }
            }
        }

    }
}
