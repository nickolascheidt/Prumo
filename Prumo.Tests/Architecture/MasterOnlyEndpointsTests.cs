using Microsoft.AspNetCore.Authorization;
using Prumo.Api.Controllers;
using System.Reflection;

namespace Prumo.Tests.Architecture
{
    /// <summary>
    /// Resolving any e-mail to id and full name is data about everyone, in every tenant. A
    /// Member of A has no reason to enumerate who in B has an account. Invitations work by
    /// e-mail and do not need the lookup, so it stays master-only.
    /// </summary>
    public class MasterOnlyEndpointsTests
    {
        [Theory]
        [InlineData(typeof(AuthController), nameof(AuthController.LookupUserByEmail))]
        [InlineData(typeof(TenantsController), nameof(TenantsController.LookupUser))]
        public void Email_lookup_is_restricted_to_the_master_admin(Type controller, string action)
        {
            var authorize = controller.GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>();

            Assert.Equal("Administrator", authorize?.Roles);
        }
    }
}
