using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using NSubstitute;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Entities;
using Xunit;

namespace SaaS_BasePlatform.Tests.Services
{
    public class AuthServiceTokenTests
    {
        /// <summary>
        /// Decodes the JWT payload (base64url second segment) into a JsonDocument
        /// so we can assert on raw claim names as they appear in the token,
        /// independent of JwtSecurityTokenHandler's InboundClaimTypeMap.
        /// </summary>
        private static JsonDocument DecodePayload(string token)
        {
            var parts = token.Split('.');
            var payload = parts[1];
            // Base64url → Base64
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            var bytes = Convert.FromBase64String(payload);
            return JsonDocument.Parse(bytes);
        }

        [Fact]
        public async Task Token_for_tenant_includes_tenant_feature_roles_not_global()
        {
            // Arrange a user with NO global roles but RH in the selected tenant.
            var tenantId = Guid.NewGuid();
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "u@x.com", Email = "u@x.com" };

            var tenantRoleService = Substitute.For<ITenantRoleService>();
            tenantRoleService.GetTenantRoleNamesAsync(user.Id, tenantId, Arg.Any<CancellationToken>())
                .Returns(new List<string> { "RH" });

            var token = await AuthServiceTestHarness.GenerateTokenAsync(
                user, tenantId, globalRoles: Array.Empty<string>(),
                tenantFeatureRoles: new[] { "RH" },
                tenantPermissions: new[] { "employees.view" },
                tenantRole: "Admin");

            // Decode raw JWT payload to see claim names exactly as written in the token.
            using var doc = DecodePayload(token);
            var root = doc.RootElement;

            // "role" claim — short-form emitted by the OutboundClaimTypeMap for ClaimTypes.Role
            Assert.True(
                root.TryGetProperty("role", out var roleEl),
                "Expected 'role' claim in JWT payload");
            var roleValues = roleEl.ValueKind == JsonValueKind.Array
                ? roleEl.EnumerateArray().Select(e => e.GetString()).ToList()
                : new List<string?> { roleEl.GetString() };
            Assert.Contains("RH", roleValues);

            // "tenant_role" claim
            Assert.True(root.TryGetProperty("tenant_role", out var tenantRoleEl), "Expected 'tenant_role' claim");
            Assert.Equal("Admin", tenantRoleEl.GetString());

            // "permission" claim
            Assert.True(root.TryGetProperty("permission", out var permEl), "Expected 'permission' claim");
            var permValues = permEl.ValueKind == JsonValueKind.Array
                ? permEl.EnumerateArray().Select(e => e.GetString()).ToList()
                : new List<string?> { permEl.GetString() };
            Assert.Contains("employees.view", permValues);
        }
    }
}
