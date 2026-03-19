using BiomePampa.Domain.Entities;
using BiomePampa.Domain.Enums;
using BiomePampa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BiomePampa.Infrastructure.Data.Seeders
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
                // Módulo RH
                new Resource
                {
                    Code = "WorkLog.Management",
                    Name = "Gestão de Ponto",
                    Description = "Controle de ponto dos funcionários",
                    Module = "RH",
                    FrontendRoute = "/rh/controle-ponto",
                    Icon = "schedule",
                    DisplayOrder = 1
                },
                new Resource
                {
                    Code = "Employee.Management",
                    Name = "Gestão de Funcionários",
                    Description = "Cadastro e gerenciamento de funcionários",
                    Module = "RH",
                    FrontendRoute = "/rh/funcionarios",
                    Icon = "people",
                    DisplayOrder = 2
                },
                new Resource
                {
                    Code = "Employee.Reports",
                    Name = "Relatórios de RH",
                    Description = "Relatórios e dashboards de RH",
                    Module = "RH",
                    FrontendRoute = "/rh/relatorios",
                    Icon = "assessment",
                    DisplayOrder = 3
                },

                // Módulo Financeiro/Contabilidade
                new Resource
                {
                    Code = "Payment.Management",
                    Name = "Gestão de Pagamentos",
                    Description = "Controle de pagamentos e folha",
                    Module = "Financeiro",
                    FrontendRoute = "/financeiro/pagamentos",
                    Icon = "payments",
                    DisplayOrder = 10
                },
                new Resource
                {
                    Code = "Payment.Reports",
                    Name = "Relatórios Financeiros",
                    Description = "Relatórios e balanços financeiros",
                    Module = "Financeiro",
                    FrontendRoute = "/financeiro/relatorios",
                    Icon = "bar_chart",
                    DisplayOrder = 11
                },
                new Resource
                {
                    Code = "PaymentPeriod.Management",
                    Name = "Períodos de Pagamento",
                    Description = "Gestão de períodos de pagamento",
                    Module = "Financeiro",
                    FrontendRoute = "/financeiro/periodos",
                    Icon = "date_range",
                    DisplayOrder = 12
                },

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
            // Buscar roles
            var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrador");
            var rhRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "RH");
            var contabilidadeRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Contabilidade");

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

            // RH tem acesso ao módulo RH e Dashboard
            if (rhRole != null)
            {
                var rhResources = await context.Resources
                    .Where(r => r.Module == "RH" || r.Code == "Dashboard.Main")
                    .ToListAsync();

                foreach (var resource in rhResources)
                {
                    var exists = await context.ResourcePermissions
                        .AnyAsync(rp => rp.RoleId == rhRole.Id && rp.ResourceId == resource.Id);

                    if (!exists)
                    {
                        context.ResourcePermissions.Add(new ResourcePermission
                        {
                            RoleId = rhRole.Id,
                            ResourceId = resource.Id,
                            Level = PermissionLevel.Full
                        });
                    }
                }
            }

            // Contabilidade tem acesso ao módulo Financeiro e Dashboard
            if (contabilidadeRole != null)
            {
                var financeResources = await context.Resources
                    .Where(r => r.Module == "Financeiro" || r.Code == "Dashboard.Main")
                    .ToListAsync();

                foreach (var resource in financeResources)
                {
                    var exists = await context.ResourcePermissions
                        .AnyAsync(rp => rp.RoleId == contabilidadeRole.Id && rp.ResourceId == resource.Id);

                    if (!exists)
                    {
                        // Contabilidade pode ver relatórios com acesso Full, mas pagamentos apenas Read
                        var level = resource.Code.Contains("Reports") 
                            ? PermissionLevel.Full 
                            : PermissionLevel.Read;

                        context.ResourcePermissions.Add(new ResourcePermission
                        {
                            RoleId = contabilidadeRole.Id,
                            ResourceId = resource.Id,
                            Level = level
                        });
                    }
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
