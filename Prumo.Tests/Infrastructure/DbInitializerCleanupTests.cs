using Xunit;

namespace Prumo.Tests.Infrastructure;

public class DbInitializerCleanupTests
{
    [Fact]
    public void UserRole_ShouldNotExist_InRolesConfig()
    {
        // Arrange — the roles dictionary in DbInitializer must not contain "User"
        var rolesConfig = new Dictionary<string, string>
        {
            { "Administrador", "Acesso total ao sistema" },
            { "Funcionario",   "Acesso para funcionários do sistema" },
            { "Cliente",       "Acesso para clientes" },
            { "RH",            "Acesso ao módulo de Recursos Humanos" },
            { "Financeiro",    "Acesso ao módulo Financeiro" },
            { "ContasAPagar",  "Acesso ao módulo de Contas a Pagar" }
        };

        // Act & Assert
        Assert.DoesNotContain("User", rolesConfig.Keys);
    }
}
