using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Infrastructure
{
    /// <summary>
    /// Cobre o pouco do item 11 que é testável em memória: qual credencial o caminho de
    /// migration escolhe, e o fato de o guard de schema não estorvar os testes.
    ///
    /// A prova de que vale a pena — `CREATE TABLE` pela conexão da aplicação devolvendo
    /// "permission denied" — exige Postgres de verdade e está no registro de execução do
    /// plano, não aqui. Um teste em memória sobre isso passaria por vacuidade.
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
            // Banco anterior à separação de roles: sem MigratorConnection, `dotnet ef`
            // ainda precisa funcionar.
            var configuration = Config(
                ("ConnectionStrings:DefaultConnection", "Host=db;Username=postgres"));

            var resolved = ApplicationDbContextFactory.ResolveMigrationConnectionString(configuration);

            Assert.Equal("Host=db;Username=postgres", resolved);
        }

        [Fact]
        public void An_empty_migrator_connection_does_not_win_over_the_default()
        {
            // Variável de ambiente definida como string vazia é o jeito mais comum de
            // "desligar" uma configuração sem removê-la.
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

            // GetPendingMigrationsAsync lança em provider não relacional; sem a guarda,
            // qualquer teste que chamasse o initializer morreria aqui.
            await DbInitializer.EnsureSchemaUpToDateAsync(db, Config(), NullLogger.Instance);
        }
    }
}
