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
        private const string AdminEmail = "admin@SBP.com";

        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var services = scope.ServiceProvider;

            var context = services.GetRequiredService<ApplicationDbContext>();
            var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();
            var configuration = services.GetRequiredService<IConfiguration>();

            logger.LogInformation("=== Database initialization started ===");

            // Outside the try on purpose. An outdated schema is not something to log and
            // move past: the app would start and fail later, somewhere that does not
            // explain the cause.
            await EnsureSchemaUpToDateAsync(context, configuration, logger);

            try
            {
                var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
                var environment = services.GetRequiredService<IHostEnvironment>();

                var rolesConfig = new Dictionary<string, string>
                {
                    { Permissions.Roles.MasterAdmin,     "Full access to the system" },
                    { Permissions.Roles.Employee,        "Access for employees" },
                    { Permissions.Roles.Customer,        "Access for customers" },
                    { Permissions.Roles.HR,              "Access to the Human Resources module" },
                    { Permissions.Roles.Finance,         "Access to the Finance module" },
                    { Permissions.Roles.AccountsPayable, "Access to the Accounts Payable module" }
                };

                foreach (var (roleName, description) in rolesConfig)
                {
                    if (await roleManager.RoleExistsAsync(roleName))
                    {
                        logger.LogDebug("Role '{Role}' already exists", roleName);
                        continue;
                    }

                    var roleResult = await roleManager.CreateAsync(new ApplicationRole
                    {
                        Name = roleName,
                        Description = description,
                        CreatedAt = DateTime.UtcNow
                    });

                    if (roleResult.Succeeded)
                    {
                        logger.LogInformation("✓ Role '{Role}' created", roleName);
                    }
                    else
                    {
                        var roleErrors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                        logger.LogError("✗ Failed to create role '{Role}': {Errors}", roleName, roleErrors);
                    }
                }

                // Tenant resources and permissions are seeded by TenantBootstrapSeeder when a
                // tenant is created, never at startup: re-applying them on every boot brought
                // revoked grants back (see SeederIdempotenceTests).

                var adminUser = await userManager.FindByEmailAsync(AdminEmail);
                if (adminUser != null)
                {
                    logger.LogDebug("Admin user already exists");

                    if (!await userManager.IsInRoleAsync(adminUser, Permissions.Roles.MasterAdmin))
                    {
                        await userManager.AddToRoleAsync(adminUser, Permissions.Roles.MasterAdmin);
                        logger.LogInformation("✓ Role {Role} added to the admin user", Permissions.Roles.MasterAdmin);
                    }

                    await EnsureDefaultTenantAsync(context, adminUser, logger);
                    await SyncResourceCatalogAsync(context, logger);

                    logger.LogInformation("=== Database initialization finished ===");
                    return;
                }

                var seedPassword = configuration["Seed:AdminPassword"];
                var isDevelopment = environment.IsDevelopment() || environment.IsEnvironment("Demo");

                if (string.IsNullOrWhiteSpace(seedPassword))
                {
                    if (isDevelopment)
                    {
                        logger.LogWarning(
                            "Admin not created: set Seed:AdminPassword (user secrets) to seed the local admin.");
                        logger.LogInformation("=== Database initialization finished ===");
                        return;
                    }

                    throw new InvalidOperationException(
                        "Seed:AdminPassword is not set. Outside development the master admin is never "
                        + "created with a default password — provide Seed__AdminPassword as an environment "
                        + "variable or remove admin seeding from this environment.");
                }

                logger.LogInformation("Creating the admin user...");
                adminUser = new ApplicationUser
                {
                    UserName = AdminEmail,
                    Email = AdminEmail,
                    EmailConfirmed = true,
                    FullName = "System Administrator",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(adminUser, seedPassword);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, Permissions.Roles.MasterAdmin);
                    await EnsureDefaultTenantAsync(context, adminUser, logger);
                    // The password NEVER goes to the log: Serilog has a database sink, and
                    // this would store the master admin credential in the log table.
                    logger.LogInformation("✓ Admin user created: {Email}", adminUser.Email);
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger.LogError("✗ Failed to create the admin user: {Errors}", errors);
                }

                await SyncResourceCatalogAsync(context, logger);

                logger.LogInformation("=== Database initialization finished ===");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database initialization failed.");
            }
        }

        /// <summary>
        /// Checks that the schema matches the assembly's migrations — without the
        /// application connection needing DDL.
        ///
        /// The app connects as `prumo_app`, which only has DML (see `db/roles.sql`), so
        /// migrating here is a privilege it should not carry. The default is to **check**
        /// and fail early; migrating at startup is an escape hatch, turned on by
        /// `Database:MigrateOnStartup`, and even then over a separate connection with the
        /// migrator credential.
        /// </summary>
        internal static async Task EnsureSchemaUpToDateAsync(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger logger)
        {
            // The in-memory provider (tests) has no migrations to compare against.
            if (!context.Database.IsRelational())
            {
                return;
            }

            if (configuration.GetValue("Database:MigrateOnStartup", false))
            {
                var connectionString = ApplicationDbContextFactory.ResolveMigrationConnectionString(configuration)
                    ?? throw new InvalidOperationException(
                        "Database:MigrateOnStartup is on, but there is no connection string to migrate with.");

                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseNpgsql(connectionString)
                    .Options;

                await using var migrationContext = new ApplicationDbContext(options);
                await migrationContext.Database.MigrateAsync();
                logger.LogInformation("Migrations applied at startup (Database:MigrateOnStartup).");
                return;
            }

            var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count == 0)
            {
                logger.LogInformation("Schema is up to date with the assembly migrations.");
                return;
            }

            throw new InvalidOperationException(
                $"The database is {pending.Count} migration(s) behind the application: {string.Join(", ", pending)}. "
                + "The application does not run DDL — run "
                + "`dotnet ef database update -p Prumo.Infrastructure -s Prumo.Api` "
                + "or turn on Database:MigrateOnStartup in this environment.");
        }

        /// <summary>
        /// Completes every tenant's resource catalog on each boot.
        /// </summary>
        /// <remarks>
        /// Without this, a new module only reaches tenants created <b>after</b> it: the
        /// <c>TenantBootstrapSeeder</c> runs once, when the tenant is created.
        ///
        /// It only adds <c>Resource</c> rows. No permission is touched, so nothing that was
        /// revoked comes back.
        /// </remarks>
        private static async Task SyncResourceCatalogAsync(
            ApplicationDbContext context, ILogger logger)
        {
            try
            {
                // Cross-tenant on purpose: a count over every tenant, at startup, with no
                // TenantContext — only used to report how many resources were added.
                var before = await context.Resources.IgnoreQueryFilters().CountAsync();

                await TenantBootstrapSeeder.SyncResourcesForAllTenantsAsync(context);

                // Cross-tenant on purpose: the same count, after the sync.
                var after = await context.Resources.IgnoreQueryFilters().CountAsync();

                if (after > before)
                {
                    logger.LogInformation(
                        "✓ Resource catalog synced: {Count} resource(s) added to existing tenants",
                        after - before);
                }
            }
            catch (Exception ex)
            {
                // Does not bring startup down: without the new resources the app starts with
                // the new screens hidden, which is bad but recoverable. Crashing here is not.
                logger.LogError(ex, "✗ Failed to sync the resource catalog");
            }
        }

        /// <summary>
        /// Ensures the 'default' tenant and the admin's membership. Seeds ONLY when the
        /// tenant is born — re-applying the seeders on every boot brought revoked grants
        /// back. `internal` so DbInitializerReseedTests can prove it.
        /// </summary>
        internal static async Task EnsureDefaultTenantAsync(
            ApplicationDbContext context,
            ApplicationUser owner,
            ILogger logger)
        {
            const string defaultSlug = "default";

            // Cross-tenant on purpose: looking up the "default" tenant is what decides
            // whether it must be created — no tenant is resolved before this.
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
                    Role = TenantRole.Owner
                });
                await context.SaveChangesAsync();
                logger.LogInformation("✓ Tenant 'default' created for the admin user");

                // Seeds ONLY when the tenant is born. Re-applying this on every startup
                // brought revoked grants back — see SeederIdempotenceTests.
                await TenantBootstrapSeeder.SeedAsync(context, tenant.Id);
                await ChartOfAccountsSeeder.SeedAsync(context, tenant.Id);
            }
            else
            {
                // Cross-tenant on purpose: startup has no TenantContext. Without the bypass
                // the check would be false and the owner's membership would be recreated on
                // every boot.
                var membershipExists = await context.TenantUsers
                    .IgnoreQueryFilters()
                    .AnyAsync(tu => tu.TenantId == tenant.Id && tu.UserId == owner.Id);
                if (!membershipExists)
                {
                    context.TenantUsers.Add(new TenantUser
                    {
                        TenantId = tenant.Id,
                        UserId = owner.Id,
                        Role = TenantRole.Owner
                    });
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}
