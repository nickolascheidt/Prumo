using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;
using SaaS_BasePlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SaaS_BasePlatform.Infrastructure.Data.Seeders
{
    public static class ResourceSeeder
    {
        public static async Task SeedResourcesAndPermissionsAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Definir recursos do sistema
            var resources = new[]
            {
                // Módulo Administrativo
                new Resource
                {
                    Code = "User.Management",
                    Name = "Gestão de Usuários",
                    Description = "Cadastro e gerenciamento de usuários",
                    Module = "Administrativo",
                    FrontendRoute = "/admin/usuarios",
                    Icon = "manage_accounts",
                    DisplayOrder = 20
                },
                new Resource
                {
                    Code = "Role.Management",
                    Name = "Gestão de Roles",
                    Description = "Cadastro e gerenciamento de roles",
                    Module = "Administrativo",
                    FrontendRoute = "/admin/roles",
                    Icon = "admin_panel_settings",
                    DisplayOrder = 21
                },
                new Resource
                {
                    Code = "Permission.Management",
                    Name = "Gestão de Permissões",
                    Description = "Configuração de permissões e acessos",
                    Module = "Administrativo",
                    FrontendRoute = "/admin/permissoes",
                    Icon = "security",
                    DisplayOrder = 22
                },
                new Resource
                {
                    Code = "System.Configuration",
                    Name = "Configurações do Sistema",
                    Description = "Configurações gerais da aplicação",
                    Module = "Administrativo",
                    FrontendRoute = "/admin/configuracoes",
                    Icon = "settings",
                    DisplayOrder = 23
                },

                // Dashboard
                new Resource
                {
                    Code = "Dashboard.Main",
                    Name = "Dashboard Principal",
                    Description = "Dashboard com visão geral",
                    Module = "Dashboard",
                    FrontendRoute = "/dashboard",
                    Icon = "dashboard",
                    DisplayOrder = 0
                }
            };

            // Inserir recursos apenas se não existirem
            foreach (var resource in resources)
            {
                var exists = await context.Resources.AnyAsync(r => r.Code == resource.Code);
                if (!exists)
                {
                    context.Resources.Add(resource);
                }
            }

            await context.SaveChangesAsync();

            // Configurar permissões padrão para roles existentes
            await ConfigureDefaultPermissionsAsync(context);
        }

        private static async Task ConfigureDefaultPermissionsAsync(ApplicationDbContext context)
        {
            // Buscar role de administrador
            var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrador");

            if (adminRole == null) return;

            // Admin tem acesso total a tudo
            var allResources = await context.Resources.ToListAsync();
            foreach (var resource in allResources)
            {
                var exists = await context.ResourcePermissions
                    .AnyAsync(rp => rp.RoleId == adminRole.Id && rp.ResourceId == resource.Id);

                if (!exists)
                {
                    context.ResourcePermissions.Add(new ResourcePermission
                    {
                        RoleId = adminRole.Id,
                        ResourceId = resource.Id,
                        Level = PermissionLevel.Full
                    });
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
