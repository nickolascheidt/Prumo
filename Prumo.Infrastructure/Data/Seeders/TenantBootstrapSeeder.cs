using Microsoft.EntityFrameworkCore;
using Prumo.Domain.Authorization;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;

namespace Prumo.Infrastructure.Data.Seeders
{
    /// <summary>
    /// Seeds tenant-scoped defaults (Resources, RolePermissions, ResourcePermissions)
    /// the first time a tenant is created. Runs against the global Identity roles
    /// and the global Permission catalog.
    /// </summary>
    public static class TenantBootstrapSeeder
    {
        private static readonly Resource[] DefaultResources =
        {
            new()
            {
                Code = "User.Management",
                Name = "Gestão de Usuários",
                Description = "Cadastro e gerenciamento de usuários",
                Module = "Administrativo",
                FrontendRoute = "/admin/usuarios",
                Icon = "manage_accounts",
                DisplayOrder = 20
            },
            new()
            {
                Code = "Role.Management",
                Name = "Gestão de Roles",
                Description = "Cadastro e gerenciamento de roles",
                Module = "Administrativo",
                FrontendRoute = "/admin/roles",
                Icon = "admin_panel_settings",
                DisplayOrder = 21
            },
            new()
            {
                Code = "Permission.Management",
                Name = "Gestão de Permissões",
                Description = "Configuração de permissões e acessos",
                Module = "Administrativo",
                FrontendRoute = "/admin/permissoes",
                Icon = "security",
                DisplayOrder = 22
            },
            new()
            {
                Code = "System.Configuration",
                Name = "Configurações do Sistema",
                Description = "Configurações gerais da aplicação",
                Module = "Administrativo",
                FrontendRoute = "/admin/configuracoes",
                Icon = "settings",
                DisplayOrder = 23
            },
            new()
            {
                Code = "Dashboard.Main",
                Name = "Dashboard Principal",
                Description = "Dashboard com visão geral",
                Module = "Dashboard",
                FrontendRoute = "/dashboard",
                Icon = "dashboard",
                DisplayOrder = 0
            },
            new()
            {
                Code = "ChartOfAccounts.Management",
                Name = "Plano de Contas",
                Description = "Cadastro e gerenciamento do plano de contas",
                Module = "Financeiro",
                FrontendRoute = "/finance/chart-of-accounts",
                Icon = "account_tree",
                DisplayOrder = 29
            },
            new()
            {
                Code = "GeneralLedger.Management",
                Name = "Razao Geral",
                Description = "Consulta e gerenciamento de lancamentos contabeis do razao geral",
                Module = "Financeiro",
                FrontendRoute = "/finance/general-ledger",
                Icon = "menu_book",
                DisplayOrder = 30
            },
            new()
            {
                Code = "HR.Employees",
                Name = "Funcionários",
                Description = "Gestão de funcionários",
                Module = "RH",
                FrontendRoute = "/hr/employees",
                Icon = "badge",
                DisplayOrder = 40
            },
            new()
            {
                Code = "HR.WorkLogs",
                Name = "Horas Trabalhadas",
                Description = "Registro de horas",
                Module = "RH",
                FrontendRoute = "/hr/worklogs",
                Icon = "schedule",
                DisplayOrder = 41
            },
            new()
            {
                Code = "HR.Payments",
                Name = "Pagamentos RH",
                Description = "Pagamentos de funcionários",
                Module = "RH",
                FrontendRoute = "/hr/payments",
                Icon = "payments",
                DisplayOrder = 42
            },
            new()
            {
                Code = "HR.PaymentPeriods",
                Name = "Períodos de Pagamento",
                Description = "Períodos gerados para pagamento",
                Module = "RH",
                FrontendRoute = "/hr/periodos",
                Icon = "event_note",
                DisplayOrder = 43
            },
            new()
            {
                Code = "AccountsPayable.Entries",
                Name = "Contas a Pagar",
                Description = "Lançamentos de contas a pagar",
                Module = "ContasAPagar",
                FrontendRoute = "/accounts-payable",
                Icon = "request_quote",
                DisplayOrder = 50
            },
        };

        /// <summary>
        /// Códigos do catálogo padrão. Existe para o teste de arquitetura poder provar
        /// que todo [TenantModule] declara um recurso que realmente existe.
        /// </summary>
        public static IReadOnlyList<string> DefaultResourceCodes =>
            DefaultResources.Select(r => r.Code).ToList();

        public static async Task SeedAsync(ApplicationDbContext db, Guid tenantId, CancellationToken cancellationToken = default)
        {
            // Resources
            var existingCodes = await db.Resources
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId)
                .Select(r => r.Code)
                .ToListAsync(cancellationToken);

            var newResources = DefaultResources
                .Where(r => !existingCodes.Contains(r.Code))
                .Select(r => new Resource
                {
                    TenantId = tenantId,
                    Code = r.Code,
                    Name = r.Name,
                    Description = r.Description,
                    Module = r.Module,
                    FrontendRoute = r.FrontendRoute,
                    Icon = r.Icon,
                    DisplayOrder = r.DisplayOrder
                })
                .ToList();

            if (newResources.Count > 0)
            {
                db.Resources.AddRange(newResources);
                await db.SaveChangesAsync(cancellationToken);
            }

            // Role permissions (catalog-style permissions per Identity role)
            var permissions = await db.Permissions.ToListAsync(cancellationToken);
            var permissionByName = permissions.ToDictionary(p => p.Name, p => p.Id);

            var rolesByName = await db.Roles.ToDictionaryAsync(r => r.Name!, r => r.Id, cancellationToken);

            var rolePermissionConfig = new Dictionary<string, IReadOnlyCollection<string>>
            {
                { "Administrador", Permissions.DefaultRolePermissions.Admin },
                { "Funcionario",   Permissions.DefaultRolePermissions.Funcionario },
                { "Cliente",       Permissions.DefaultRolePermissions.Cliente },
                { "RH",            Permissions.DefaultRolePermissions.RH },
                { "Financeiro",    Permissions.DefaultRolePermissions.Financeiro },
                { "ContasAPagar",  Permissions.DefaultRolePermissions.ContasAPagar }
            };

            foreach (var (roleName, perms) in rolePermissionConfig)
            {
                if (!rolesByName.TryGetValue(roleName, out var roleId)) continue;

                foreach (var permName in perms)
                {
                    if (!permissionByName.TryGetValue(permName, out var permissionId)) continue;

                    var exists = await db.RolePermissions
                        .IgnoreQueryFilters()
                        .AnyAsync(rp =>
                            rp.TenantId == tenantId &&
                            rp.RoleId == roleId &&
                            rp.PermissionId == permissionId, cancellationToken);

                    if (!exists)
                    {
                        db.RolePermissions.Add(new RolePermission
                        {
                            TenantId = tenantId,
                            RoleId = roleId,
                            PermissionId = permissionId,
                            GrantedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            // Resource permissions: Admin gets Full access to every default resource
            if (rolesByName.TryGetValue("Administrador", out var adminRoleId))
            {
                var tenantResources = await db.Resources
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId)
                    .Select(r => r.Id)
                    .ToListAsync(cancellationToken);

                foreach (var resourceId in tenantResources)
                {
                    var exists = await db.ResourcePermissions
                        .IgnoreQueryFilters()
                        .AnyAsync(rp =>
                            rp.TenantId == tenantId &&
                            rp.RoleId == adminRoleId &&
                            rp.ResourceId == resourceId, cancellationToken);

                    if (!exists)
                    {
                        db.ResourcePermissions.Add(new ResourcePermission
                        {
                            TenantId = tenantId,
                            RoleId = adminRoleId,
                            ResourceId = resourceId,
                            Level = PermissionLevel.Full
                        });
                    }
                }

                await db.SaveChangesAsync(cancellationToken);
            }

            // Module roles: each gets Full access only to their own module's resources
            var moduleRoleResourceMap = new Dictionary<string, string>
            {
                { "RH",           "RH" },
                { "Financeiro",   "Financeiro" },
                { "ContasAPagar", "ContasAPagar" }
            };

            foreach (var (roleName, moduleName) in moduleRoleResourceMap)
            {
                if (!rolesByName.TryGetValue(roleName, out var moduleRoleId)) continue;

                var moduleResources = await db.Resources
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId && r.Module == moduleName)
                    .Select(r => r.Id)
                    .ToListAsync(cancellationToken);

                foreach (var resourceId in moduleResources)
                {
                    var exists = await db.ResourcePermissions
                        .IgnoreQueryFilters()
                        .AnyAsync(rp =>
                            rp.TenantId == tenantId &&
                            rp.RoleId == moduleRoleId &&
                            rp.ResourceId == resourceId, cancellationToken);

                    if (!exists)
                    {
                        db.ResourcePermissions.Add(new ResourcePermission
                        {
                            TenantId   = tenantId,
                            RoleId     = moduleRoleId,
                            ResourceId = resourceId,
                            Level      = PermissionLevel.Full
                        });
                    }
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
