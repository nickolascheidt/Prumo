using Prumo.Domain.Authorization;

namespace Prumo.Tests.Domain
{
    /// <summary>
    /// O item 2 do backlog nasceu de uma lista de roles chumbada no frontend, que ficou
    /// para trás do backend: inventava uma role "Usuario" que nunca existiu e não conhecia
    /// RH, Financeiro nem ContasAPagar. A cura é o frontend perguntar ao backend, e para
    /// isso o backend precisa de uma lista canônica — testada, para não derivar de novo.
    /// </summary>
    public class CanonicalRolesTests
    {
        [Fact]
        public void All_contains_every_canonical_role()
        {
            Assert.Equal(
                new[]
                {
                    Permissions.Roles.MasterAdmin,
                    Permissions.Roles.Funcionario,
                    Permissions.Roles.Cliente,
                    Permissions.Roles.RH,
                    Permissions.Roles.Financeiro,
                    Permissions.Roles.ContasAPagar
                },
                Permissions.Roles.All);
        }

        [Fact]
        public void All_is_the_assignable_feature_roles_plus_the_master_admin()
        {
            // Se alguém acrescentar uma feature role e esquecer de uma das duas listas,
            // este teste quebra antes de a tela de permissões ficar incompleta de novo.
            var expected = new[] { Permissions.Roles.MasterAdmin }
                .Concat(Permissions.Roles.AssignableFeatureRoles);

            Assert.Equal(expected, Permissions.Roles.All);
        }

        [Fact]
        public void All_does_not_contain_the_phantom_Usuario_role()
        {
            Assert.DoesNotContain("Usuario", Permissions.Roles.All);
        }
    }
}
