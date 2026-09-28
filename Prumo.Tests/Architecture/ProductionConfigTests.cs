using System.Text.Json;

namespace Prumo.Tests.Architecture
{
    /// <summary>
    /// The production overlay once carried cloud placeholders for months — the connection
    /// string said CONFIGURE_VIA_AZURE_APP_SETTINGS_OR_KEY_VAULT and AllowedHosts said
    /// yourdomain.com — and the deploy worked around them with environment variables.
    /// Working around them silently is how they survived so long.
    ///
    /// This test breaks the build if one comes back. It does NOT require the file to hold
    /// the real value: in production the connection string still comes from the
    /// environment, and the placeholder that breaks startup on purpose is still the right
    /// thing — it just must not name a cloud the project no longer uses.
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
        public void Production_overlay_carries_no_azure_placeholder()
        {
            var content = File.ReadAllText(ProductionConfigPath());

            foreach (var term in Forbidden)
            {
                Assert.DoesNotContain(term, content, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// The existing fail-safe that must stay: without the
        /// ConnectionStrings__DefaultConnection environment variable, startup breaks instead
        /// of falling back to a default database.
        /// </summary>
        [Fact]
        public void Production_overlay_keeps_the_connection_string_as_a_placeholder()
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
