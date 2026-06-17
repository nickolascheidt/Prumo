using SaaS_BasePlatform.Domain.Authorization;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Infrastructure.Data.Seeders;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SaaS_BasePlatform.Infrastructure.Data
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

                logger.LogInformation("=== Iniciando inicialização do banco de dados ===");

                // Aplicar migrations pendentes (cria o banco e schema se necessário)
                await context.Database.MigrateAsync();
                logger.LogInformation("Banco de dados verificado/criado com sucesso");

                // One-time cleanup: remove legacy "User" role if it exists
                var legacyUserRole = await roleManager.FindByNameAsync("User");
                if (legacyUserRole != null)
                {
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

                // Tenant-scoped role permissions and resources are now seeded per-tenant
                // via TenantBootstrapSeeder when a tenant is created (see TenantService.CreateAsync).
                await EnsureTenantBootstrapAsync(context, logger);

                // Backfill: convert legacy global feature-role assignments to per-tenant rows.
                var masterRoleId = (await roleManager.FindByNameAsync(Permissions.Roles.MasterAdmin))?.Id;
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

                    // Remove the now-migrated global feature-role assignments.
                    var toRemove = await context.UserRoles
                        .Where(ur => ur.RoleId != masterRoleId)
                        .ToListAsync();
                    context.UserRoles.RemoveRange(toRemove);
                    await context.SaveChangesAsync();
                    logger.LogInformation("✓ Backfilled {Count} per-tenant role rows", globalAssignments.Count);
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

                    logger.LogInformation("=== Inicialização concluída ===");
                    return;
                }

                // Criar usuário admin
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

                var result = await userManager.CreateAsync(adminUser, "Admin@123");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrador");
                    await EnsureDefaultTenantAsync(context, adminUser, logger);
                    logger.LogInformation("✓✓✓ Usuário admin criado com sucesso! ✓✓✓");
                    logger.LogInformation("═══════════════════════════════════════");
                    logger.LogInformation("  Email: admin@SBP.com");
                    logger.LogInformation("  Senha: Admin@123");
                    logger.LogInformation("═══════════════════════════════════════");
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger.LogError($"✗✗✗ Erro ao criar usuário admin: {errors}");
                }

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

                var tenantIds = await context.TenantUsers
                    .IgnoreQueryFilters()
                    .Where(tu => tu.UserId == userId)
                    .Select(tu => tu.TenantId)
                    .ToListAsync(cancellationToken);

                foreach (var tenantId in tenantIds)
                {
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

        private static async Task EnsureDefaultTenantAsync(
            ApplicationDbContext context,
            ApplicationUser owner,
            ILogger logger)
        {
            const string defaultSlug = "default";

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
            }
            else
            {
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

            await Seeders.TenantBootstrapSeeder.SeedAsync(context, tenant.Id);
            await Seeders.ChartOfAccountsSeeder.SeedAsync(context, tenant.Id);
        }

        private static async Task EnsureTenantBootstrapAsync(
            ApplicationDbContext context,
            ILogger logger)
        {
            var tenantIds = await context.Tenants
                .IgnoreQueryFilters()
                .Select(t => t.Id)
                .ToListAsync();

            foreach (var tenantId in tenantIds)
            {
                await Seeders.TenantBootstrapSeeder.SeedAsync(context, tenantId);
            }

            logger.LogInformation("✓ Bootstrap de recursos/permissoes reaplicado para {Count} tenant(s)", tenantIds.Count);
        }
    }
}
