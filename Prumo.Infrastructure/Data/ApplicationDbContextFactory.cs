using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Prumo.Infrastructure.Data
{
    /// <summary>
    /// Usado só pelo `dotnet ef` em design-time. Lê a mesma configuração da aplicação
    /// para que migration e runtime nunca apontem para bases diferentes — a divergência
    /// que o item 12 do backlog descreve.
    /// </summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Prumo.Api");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.GetFullPath(basePath))
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection não encontrada. O design-time factory lê a "
                    + "configuração de Prumo.Api; rode o comando a partir da raiz da solution.");

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
