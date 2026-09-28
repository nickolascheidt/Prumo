using Microsoft.EntityFrameworkCore;
using Prumo.Domain.Authorization;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;

namespace Prumo.Infrastructure.Data.Seeders
{
    /// <summary>
    /// Seeds tenant-scoped defaults (Resources and ResourcePermissions) the first time a
    /// tenant is created, against the canonical Identity roles.
    /// </summary>
    public static class TenantBootstrapSeeder
    {
        private static readonly Resource[] DefaultResources =
        {
            new()
            {
                Code = "User.Management",
                Name = "User Management",
                Description = "Create and manage users",
                Module = "Administration",
                FrontendRoute = "/admin/members",
                Icon = "manage_accounts",
                DisplayOrder = 20
            },
            new()
            {
                Code = "Role.Management",
                Name = "Role Management",
                Description = "Create and manage roles",
                Module = "Administration",
                FrontendRoute = "/admin/roles",
                Icon = "admin_panel_settings",
                DisplayOrder = 21
            },
            new()
            {
                Code = "Permission.Management",
                Name = "Permission Management",
                Description = "Configure permissions and access",
                Module = "Administration",
                FrontendRoute = "/admin/permissions",
                Icon = "security",
                DisplayOrder = 22
            },
            new()
            {
                Code = "System.Configuration",
                Name = "System Settings",
                Description = "General application settings",
                Module = "Administration",
                FrontendRoute = "/admin/settings",
                Icon = "settings",
                DisplayOrder = 23
            },
            new()
            {
                Code = "Dashboard.Main",
                Name = "Main Dashboard",
                Description = "Overview dashboard",
                Module = "Dashboard",
                FrontendRoute = "/dashboard",
                Icon = "dashboard",
                DisplayOrder = 0
            },
            // Each dashboard tab has its own resource so it can be granted separately
            // from the module's screen. Borrowing the module's resource (the HR tab gated
            // by HR.Employees) glued the two together: there was no way to show the HR
            // dashboard to someone who cannot open the employees screen, or vice versa.
            new()
            {
                Code = "Dashboard.Accounting",
                Name = "Accounting Dashboard",
                Description = "Accounting indicators",
                Module = "Dashboard",
                FrontendRoute = "/dashboard/accounting",
                Icon = "account_balance",
                DisplayOrder = 1
            },
            new()
            {
                Code = "Dashboard.Finance",
                Name = "Finance Dashboard",
                Description = "Finance indicators",
                Module = "Dashboard",
                FrontendRoute = "/dashboard/finance",
                Icon = "payments",
                DisplayOrder = 2
            },
            new()
            {
                Code = "Dashboard.HR",
                Name = "HR Dashboard",
                Description = "Human resources indicators",
                Module = "Dashboard",
                FrontendRoute = "/dashboard/hr",
                Icon = "groups",
                DisplayOrder = 3
            },
            new()
            {
                Code = "Dashboard.Admin",
                Name = "Admin Dashboard",
                Description = "Administration indicators",
                Module = "Dashboard",
                FrontendRoute = "/dashboard/admin",
                Icon = "admin_panel_settings",
                DisplayOrder = 4
            },
            new()
            {
                Code = "ChartOfAccounts.Management",
                Name = "Chart of Accounts",
                Description = "Create and manage the chart of accounts",
                Module = "Finance",
                FrontendRoute = "/finance/chart-of-accounts",
                Icon = "account_tree",
                DisplayOrder = 29
            },
            new()
            {
                Code = "GeneralLedger.Management",
                Name = "General Ledger",
                Description = "View and manage general ledger journal entries",
                Module = "Finance",
                FrontendRoute = "/finance/general-ledger",
                Icon = "menu_book",
                DisplayOrder = 30
            },
            new()
            {
                Code = "HR.Employees",
                Name = "Employees",
                Description = "Employee management",
                Module = "HR",
                FrontendRoute = "/hr/employees",
                Icon = "badge",
                DisplayOrder = 40
            },
            new()
            {
                Code = "HR.WorkLogs",
                Name = "Work Logs",
                Description = "Hours worked",
                Module = "HR",
                FrontendRoute = "/hr/worklogs",
                Icon = "schedule",
                DisplayOrder = 41
            },
            new()
            {
                Code = "HR.Payments",
                Name = "HR Payments",
                Description = "Employee payments",
                Module = "HR",
                FrontendRoute = "/hr/payments",
                Icon = "payments",
                DisplayOrder = 42
            },
            new()
            {
                Code = "HR.PaymentPeriods",
                Name = "Payment Periods",
                Description = "Periods generated for payment",
                Module = "HR",
                FrontendRoute = "/hr/payment-periods",
                Icon = "event_note",
                DisplayOrder = 43
            },
            new()
            {
                Code = "AccountsPayable.Entries",
                Name = "Accounts Payable",
                Description = "Accounts payable entries",
                Module = "AccountsPayable",
                FrontendRoute = "/accounts-payable",
                Icon = "request_quote",
                DisplayOrder = 50
            },
        };

        /// <summary>
        /// Codes of the default catalog. Exists so the architecture test can prove that
        /// every [TenantModule] names a resource that actually exists.
        /// </summary>
        public static IReadOnlyList<string> DefaultResourceCodes =>
            DefaultResources.Select(r => r.Code).ToList();

        /// <summary>
        /// Ensures <b>every</b> tenant has every resource in the catalog, including the
        /// ones added after the tenant was created.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="SeedAsync"/> on purpose, so it can run on every boot:
        /// the full seeder also writes permissions, and re-applying it would bring a revoked
        /// grant back. Only missing <c>Resource</c> rows go in here; no permission is
        /// touched, so no revocation is undone.
        /// </remarks>
        public static async Task SyncResourcesForAllTenantsAsync(
            ApplicationDbContext db, CancellationToken cancellationToken = default)
        {
            // Cross-tenant on purpose: runs at startup, with no TenantContext, and must
            // see every tenant to complete each one's catalog.
            var tenantIds = await db.Tenants
                .IgnoreQueryFilters()
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            foreach (var tenantId in tenantIds)
            {
                await SyncResourcesAsync(db, tenantId, cancellationToken);
            }
        }

        /// <summary>Adds to a tenant the catalog resources it is still missing.</summary>
        private static async Task SyncResourcesAsync(
            ApplicationDbContext db, Guid tenantId, CancellationToken cancellationToken)
        {
            var existingCodes = await db.Resources
                // Cross-tenant on purpose: runs at startup, with no TenantContext. The
                // tenantId comes from the parameter and is filtered right below.
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId)
                .Select(r => r.Code)
                .ToListAsync(cancellationToken);

            var missing = DefaultResources
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

            if (missing.Count > 0)
            {
                db.Resources.AddRange(missing);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        public static async Task SeedAsync(ApplicationDbContext db, Guid tenantId, CancellationToken cancellationToken = default)
        {
            // Resources
            var existingCodes = await db.Resources
                // Cross-tenant on purpose: the seeder runs at startup, with no TenantContext.
                // The tenantId comes from the parameter and is filtered right below. Without
                // the bypass the fail-closed filter would return nothing and seeding would
                // silently break.
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

            // Canonical roles only (null TenantId). Without the filter, two tenants with
            // same-named roles would make ToDictionary throw on a duplicate key and bring
            // startup down — and this block only configures canonical roles anyway.
            var rolesByName = await db.Roles
                .Where(r => r.TenantId == null)
                .ToDictionaryAsync(r => r.Name!, r => r.Id, cancellationToken);

            // Resource permissions: Admin gets Full access to every default resource
            if (rolesByName.TryGetValue(Permissions.Roles.MasterAdmin, out var adminRoleId))
            {
                var tenantResources = await db.Resources
                    // Cross-tenant on purpose: the seeder runs at startup, with no TenantContext.
                    // The tenantId comes from the parameter and is filtered right below. Without
                    // the bypass the fail-closed filter would return nothing and seeding would
                    // silently break.
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId)
                    .Select(r => r.Id)
                    .ToListAsync(cancellationToken);

                foreach (var resourceId in tenantResources)
                {
                    var exists = await db.ResourcePermissions
                        // Cross-tenant on purpose: the seeder runs at startup, with no TenantContext.
                        // The tenantId comes from the parameter and is filtered right below. Without
                        // the bypass the fail-closed filter would return nothing and seeding would
                        // silently break.
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
                { Permissions.Roles.HR,              "HR" },
                { Permissions.Roles.Finance,         "Finance" },
                { Permissions.Roles.AccountsPayable, "AccountsPayable" }
            };

            foreach (var (roleName, moduleName) in moduleRoleResourceMap)
            {
                if (!rolesByName.TryGetValue(roleName, out var moduleRoleId)) continue;

                var moduleResources = await db.Resources
                    // Cross-tenant on purpose: the seeder runs at startup, with no TenantContext.
                    // The tenantId comes from the parameter and is filtered right below. Without
                    // the bypass the fail-closed filter would return nothing and seeding would
                    // silently break.
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId && r.Module == moduleName)
                    .Select(r => r.Id)
                    .ToListAsync(cancellationToken);

                foreach (var resourceId in moduleResources)
                {
                    var exists = await db.ResourcePermissions
                        // Cross-tenant on purpose: the seeder runs at startup, with no TenantContext.
                        // The tenantId comes from the parameter and is filtered right below. Without
                        // the bypass the fail-closed filter would return nothing and seeding would
                        // silently break.
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
