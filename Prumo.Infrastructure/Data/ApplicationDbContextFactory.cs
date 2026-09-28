using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Prumo.Infrastructure.Data
{
    /// <summary>
    /// Used only by `dotnet ef` at design time. Reads the same configuration as the app, so
    /// migrations and runtime never point at different databases.
    ///
    /// Connects with the migrator credential when there is one. The app's credential does
    /// not run DDL (see `db/roles.sql`), so using it here would only produce
    /// "permission denied".
    /// </summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        /// <summary>
        /// Prefers the migrator connection and falls back to the app's when it is missing —
        /// databases that predate the role split stay migratable.
        /// </summary>
        public static string? ResolveMigrationConnectionString(IConfiguration configuration) =>
            configuration.GetConnectionString("MigratorConnection")
                is { Length: > 0 } migrator
                ? migrator
                : configuration.GetConnectionString("DefaultConnection");

        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Prumo.Api");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.GetFullPath(basePath))
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = ResolveMigrationConnectionString(configuration)
                ?? throw new InvalidOperationException(
                    "No connection string found (MigratorConnection or DefaultConnection). "
                    + "The design-time factory reads Prumo.Api's configuration; run the command from "
                    + "the solution root.");

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
