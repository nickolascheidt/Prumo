using System.Text.Json;

namespace Prumo.Tests.Architecture
{
    /// <summary>
    /// O overlay de produção carregou placeholders de Azure por meses — a connection
    /// string dizia CONFIGURE_VIA_AZURE_APP_SETTINGS_OR_KEY_VAULT e o AllowedHosts
    /// dizia yourdomain.com —, e o deploy os contornava com variável de ambiente.
    /// Contornar em silêncio é como eles sobreviveram tanto tempo.
    ///
    /// Este teste quebra o build se algum voltar. Ele NÃO exige que o arquivo tenha o
    /// valor real: em produção a connection string continua vindo do ambiente, e o
    /// placeholder que quebra o startup de propósito continua sendo o certo — ele só
    /// não pode nomear uma nuvem que não usamos mais.
    /// </summary>
    public class ProductionConfigTests
    {
        private static readonly string[] Forbidden =
        {
            "AZURE",
            "yourdomain.com",
        };

        private static string ProductionConfigPath()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Prumo.slnx")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "Prumo.Api", "appsettings.Production.json");
        }

        [Fact]
        public void O_overlay_de_producao_nao_carrega_placeholder_de_azure()
        {
            var content = File.ReadAllText(ProductionConfigPath());

            foreach (var term in Forbidden)
            {
                Assert.DoesNotContain(term, content, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// A falha segura que já existe e deve continuar existindo: sem a env var
        /// ConnectionStrings__DefaultConnection, o startup quebra em vez de cair num
        /// banco default.
        /// </summary>
        [Fact]
        public void O_overlay_de_producao_mantem_a_connection_string_como_placeholder()
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(ProductionConfigPath()));

            var value = doc.RootElement
                .GetProperty("ConnectionStrings")
                .GetProperty("DefaultConnection")
                .GetString();

            Assert.NotNull(value);
            Assert.DoesNotContain("Host=", value!, StringComparison.OrdinalIgnoreCase);
        }
    }
}
