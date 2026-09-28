using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Infrastructure
{
    /// <summary>
    /// Covers the little of the least-privilege setup that is testable in memory: which
    /// credential the migration path picks, and the schema guard not getting in the tests'
    /// way.
    ///
    /// The proof that it matters — `CREATE TABLE` over the app connection returning
    /// "permission denied" — needs a real Postgres. An in-memory test of that would pass
    /// vacuously.
    /// </summary>
    public class SchemaGuardTests
    {
        private static IConfiguration Config(params (string Key, string? Value)[] entries) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(entries.Select(e =>
                    new KeyValuePair<string, string?>(e.Key, e.Value)))
                .Build();

        [Fact]
        public void Migration_uses_the_migrator_credential_when_there_is_one()
        {
            var configuration = Config(
                ("ConnectionStrings:DefaultConnection", "Host=db;Username=prumo_app"),
                ("ConnectionStrings:MigratorConnection", "Host=db;Username=prumo_migrator"));

            var resolved = ApplicationDbContextFactory.ResolveMigrationConnectionString(configuration);

            Assert.Equal("Host=db;Username=prumo_migrator", resolved);
        }

        [Fact]
        public void Migration_falls_back_to_the_application_credential()
        {
            // A database that predates the role split: without MigratorConnection, `dotnet ef`
            // still has to work.
            var configuration = Config(
                ("ConnectionStrings:DefaultConnection", "Host=db;Username=postgres"));

            var resolved = ApplicationDbContextFactory.ResolveMigrationConnectionString(configuration);

            Assert.Equal("Host=db;Username=postgres", resolved);
        }

        [Fact]
        public void An_empty_migrator_connection_does_not_win_over_the_default()
        {
            // An environment variable set to an empty string is the most common way to
            // "turn off" a setting without removing it.
            var configuration = Config(
                ("ConnectionStrings:DefaultConnection", "Host=db;Username=postgres"),
                ("ConnectionStrings:MigratorConnection", ""));

            var resolved = ApplicationDbContextFactory.ResolveMigrationConnectionString(configuration);

            Assert.Equal("Host=db;Username=postgres", resolved);
        }

        [Fact]
        public async Task The_schema_guard_stays_out_of_the_way_of_in_memory_providers()
        {
            await using var db = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
                new TenantContext());

            // GetPendingMigrationsAsync throws on a non-relational provider; without the
            // guard, any test calling the initializer would die here.
            await DbInitializer.EnsureSchemaUpToDateAsync(db, Config(), NullLogger.Instance);
        }
    }
}
