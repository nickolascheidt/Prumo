using BiomePampa.Domain.Authorization;
using BiomePampa.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BiomePampa.Infrastructure.Data
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

                // Garantir que o banco existe
                await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Banco de dados verificado/criado com sucesso");

                // Criar roles se não existirem
                var rolesConfig = new Dictionary<string, string>
                {
                    { "Administrador", "Acesso total ao sistema" },
                    { "Funcionario", "Acesso para funcionários do sistema" },
                    { "Cliente", "Acesso para clientes" }
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
                        logger.LogInformation($"✓ Role '{roleName}' já existe");
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

                // Atribuir permissões às roles
                logger.LogInformation("Atribuindo permissões às roles...");

                var rolePermissionsConfig = new Dictionary<string, IReadOnlyCollection<string>>
                {
                    { "Administrador", Permissions.DefaultRolePermissions.Admin },
                    { "Funcionario", Permissions.DefaultRolePermissions.Funcionario },
                    { "Cliente", Permissions.DefaultRolePermissions.Cliente }
                };

                foreach (var (roleName, permissions) in rolePermissionsConfig)
                {
                    var role = await roleManager.FindByNameAsync(roleName);
                    if (role == null)
                    {
                        logger.LogWarning($"Role '{roleName}' não encontrada. Pulando atribuição de permissões.");
                        continue;
                    }

                    foreach (var permissionName in permissions)
                    {
                        if (!permissionMap.TryGetValue(permissionName, out var permission))
                        {
                            logger.LogWarning($"Permissão '{permissionName}' não encontrada no mapa. Pulando.");
                            continue;
                        }

                        var existingRolePermission = await context.RolePermissions
                            .FirstOrDefaultAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id);

                        if (existingRolePermission == null)
                        {
                            var rolePermission = new RolePermission
                            {
                                RoleId = role.Id,
                                PermissionId = permission.Id,
                                GrantedAt = DateTime.UtcNow
                            };
                            context.RolePermissions.Add(rolePermission);
                        }
                    }
                }

                await context.SaveChangesAsync();
                logger.LogInformation($"✓ Permissões atribuídas às roles com sucesso!");

                // Verificar se já existe o admin
                var adminUser = await userManager.FindByEmailAsync("admin@biomepampa.com");
                if (adminUser != null)
                {
                    logger.LogInformation("✓ Usuário admin já existe.");

                    // Verificar se tem a role
                    var hasRole = await userManager.IsInRoleAsync(adminUser, "Administrador");
                    if (!hasRole)
                    {
                        await userManager.AddToRoleAsync(adminUser, "Administrador");
                        logger.LogInformation("✓ Role Administrador adicionada ao usuário admin");
                    }

                    logger.LogInformation("=== Inicialização concluída ===");
                    return;
                }

                // Criar usuário admin
                logger.LogInformation("Criando usuário administrador...");
                adminUser = new ApplicationUser
                {
                    UserName = "admin@biomepampa.com",
                    Email = "admin@biomepampa.com",
                    EmailConfirmed = true,
                    FullName = "Administrador do Sistema",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(adminUser, "Admin@123");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrador");
                    logger.LogInformation("✓✓✓ Usuário admin criado com sucesso! ✓✓✓");
                    logger.LogInformation("═══════════════════════════════════════");
                    logger.LogInformation("  Email: admin@biomepampa.com");
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
    }
}
