using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Prumo.Infrastructure.Data
{
    /// <summary>
    /// Usado só pelo `dotnet ef` em design-time. Lê a mesma configuração da aplicação
    /// para que migration e runtime nunca apontem para bases diferentes — a divergência
    /// que o item 12 do backlog descreve.
    ///
    /// Conecta com a credencial do migrator quando ela existe. A da aplicação não faz
    /// DDL (ver `db/roles.sql`), então usá-la aqui só produziria "permission denied".
    /// </summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        /// <summary>
        /// Preferimos a conexão do migrator e caímos para a da aplicação quando ela não
        /// existe — bancos anteriores à separação de roles continuam migráveis.
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
                    "Nenhuma connection string encontrada (MigratorConnection ou DefaultConnection). "
                    + "O design-time factory lê a configuração de Prumo.Api; rode o comando a partir "
                    + "da raiz da solution.");

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
