using BiomePampa.Domain.Entities;
using Microsoft.AspNetCore.Identity;
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
                string[] roles = { "Administrador", "Funcionario", "Cliente" };
                foreach (var roleName in roles)
                {
                    var roleExists = await roleManager.RoleExistsAsync(roleName);
                    if (!roleExists)
                    {
                        var role = new ApplicationRole
                        {
                            Name = roleName,
                            Description = $"Role {roleName}"
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
