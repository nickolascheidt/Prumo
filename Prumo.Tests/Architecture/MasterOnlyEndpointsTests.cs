using Microsoft.AspNetCore.Authorization;
using Prumo.Api.Controllers;
using System.Reflection;

namespace Prumo.Tests.Architecture
{
    /// <summary>
    /// Resolver um e-mail qualquer para id e nome completo é dado de todo mundo, de todo
    /// tenant. Um Member de A não tem por que enumerar quem de B tem conta. Desde o item 8
    /// o convite é por e-mail e não precisa mais do lookup, então ele fica só com o master.
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
