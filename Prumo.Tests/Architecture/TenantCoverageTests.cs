using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Prumo.Api.Attributes;
using Prumo.Infrastructure.Data.Seeders;
using System.Reflection;

namespace Prumo.Tests.Architecture
{
    public class TenantCoverageTests
    {
        /// <summary>
        /// Exceções declaradas de propósito, para que sejam visíveis e revisáveis.
        /// TenantsController gerencia associação — exigir associação provada nele seria
        /// circular. Os outros três não são roteados por tenant.
        /// </summary>
        private static readonly string[] Exempt =
        {
            "TenantsController",
            "AuthController",
            "PermissionsController",
            "ResourcesController"
        };

        private static IEnumerable<Type> Controllers() =>
            typeof(TenantModuleAttribute).Assembly
                .GetTypes()
                .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        [Fact]
        public void Every_tenant_routed_action_is_covered_by_TenantModule()
        {
            var offenders = new List<string>();

            foreach (var controller in Controllers())
            {
                if (Exempt.Contains(controller.Name)) continue;

                var controllerTemplate = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;
                var coveredAtClassLevel = controller.GetCustomAttribute<TenantModuleAttribute>() != null;

                var actions = controller
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

                foreach (var action in actions)
                {
                    var actionTemplate = action.GetCustomAttributes<HttpMethodAttribute>()
                        .Select(a => a.Template)
                        .FirstOrDefault(t => !string.IsNullOrEmpty(t)) ?? string.Empty;

                    var effective = $"{controllerTemplate}/{actionTemplate}";
                    if (!effective.Contains("{tenantId")) continue;

                    var covered = coveredAtClassLevel
                                  || action.GetCustomAttribute<TenantModuleAttribute>() != null;

                    if (!covered) offenders.Add($"{controller.Name}.{action.Name}");
                }
            }

            Assert.True(
                offenders.Count == 0,
                "Actions roteadas por tenant sem [TenantModule] e fora da lista de exceções: "
                + string.Join(", ", offenders));
        }

        [Fact]
        public void Every_declared_resource_code_exists_in_the_catalog()
        {
            var known = TenantBootstrapSeeder.DefaultResourceCodes;
            var unknown = new List<string>();

            foreach (var controller in Controllers())
            {
                foreach (var attribute in controller.GetCustomAttributes<TenantModuleAttribute>())
                {
                    if (!known.Contains(attribute.ResourceCode))
                        unknown.Add($"{controller.Name} → '{attribute.ResourceCode}'");
                }

                foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    foreach (var attribute in action.GetCustomAttributes<TenantModuleAttribute>())
                    {
                        if (!known.Contains(attribute.ResourceCode))
                            unknown.Add($"{controller.Name}.{action.Name} → '{attribute.ResourceCode}'");
                    }
                }
            }

            Assert.True(
                unknown.Count == 0,
                "resourceCode declarado que não existe no catálogo — negaria o endpoint para todos, "
                + "para sempre: " + string.Join(", ", unknown));
        }
    }
}
