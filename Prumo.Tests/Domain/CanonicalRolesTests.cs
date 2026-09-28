using Prumo.Domain.Authorization;

namespace Prumo.Tests.Domain
{
    /// <summary>
    /// A role list hard-coded in the frontend once fell behind the backend: it invented a
    /// "User" role that never existed and did not know HR, Finance or AccountsPayable. The
    /// fix is for the frontend to ask the backend, and for that the backend needs a
    /// canonical list — tested, so it does not drift again.
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
                    Permissions.Roles.Employee,
                    Permissions.Roles.Customer,
                    Permissions.Roles.HR,
                    Permissions.Roles.Finance,
                    Permissions.Roles.AccountsPayable
                },
                Permissions.Roles.All);
        }

        [Fact]
        public void All_is_the_assignable_feature_roles_plus_the_master_admin()
        {
            // If someone adds a feature role and forgets one of the two lists, this test
            // breaks before the permissions screen goes incomplete again.
            var expected = new[] { Permissions.Roles.MasterAdmin }
                .Concat(Permissions.Roles.AssignableFeatureRoles);

            Assert.Equal(expected, Permissions.Roles.All);
        }

        [Fact]
        public void All_does_not_contain_the_phantom_Usuario_role()
        {
            Assert.DoesNotContain("User", Permissions.Roles.All);
        }
    }
}
