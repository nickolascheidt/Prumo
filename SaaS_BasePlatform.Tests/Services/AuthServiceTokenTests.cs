using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;
using SaaS_BasePlatform.Infrastructure.Authorization;
using Xunit;

namespace SaaS_BasePlatform.Tests.Services
{
    /// <summary>
    /// Drives the REAL <see cref="AuthService.GenerateJwtToken"/> path end to end
    /// (through the public <see cref="AuthService.SelectTenantAsync"/> entry point)
    /// with substituted dependencies, and asserts the claim shape production emits.
    /// </summary>
    public class AuthServiceTokenTests
    {
        private const string TestKey = "test-key-for-auth-service-token-tests-32-chars!!";
        private const string TestIssuer = "test-issuer";
        private const string TestAudience = "test-audience";

        // Production builds role claims via `new Claim(ClaimTypes.Role, r)` and serializes
        // through `new JwtSecurityToken(...)` + `WriteToken` directly. That path does NOT
        // apply JwtSecurityTokenHandler's OutboundClaimTypeMap, so the serialized payload
        // key is the full ClaimTypes.Role URI, not the short "role".
        private const string RoleClaimKey =
            "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

        private static UserManager<ApplicationUser> MakeUserManager()
        {
            var store = Substitute.For<IUserStore<ApplicationUser>>();
            return Substitute.For<UserManager<ApplicationUser>>(
                store, null, null, null, null, null, null, null, null);
        }

        private static SignInManager<ApplicationUser> MakeSignInManager(
            UserManager<ApplicationUser> userManager)
        {
            var contextAccessor = Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var claimsFactory = Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>();
            return Substitute.For<SignInManager<ApplicationUser>>(
                userManager, contextAccessor, claimsFactory, null, null, null, null);
        }

        private static IConfiguration MakeConfiguration()
        {
            var config = Substitute.For<IConfiguration>();
            config["Jwt:Key"].Returns(TestKey);
            config["Jwt:Issuer"].Returns(TestIssuer);
            config["Jwt:Audience"].Returns(TestAudience);
            return config;
        }

        /// <summary>
        /// Decodes the JWT payload (base64url second segment) into a JsonDocument
        /// so we can assert on raw claim names as they appear in the token,
        /// independent of JwtSecurityTokenHandler's InboundClaimTypeMap.
        /// </summary>
        private static JsonDocument DecodePayload(string token)
        {
            var parts = token.Split('.');
            var payload = parts[1];
            // Base64url -> Base64
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            var bytes = Convert.FromBase64String(payload);
            return JsonDocument.Parse(bytes);
        }

        private static List<string?> StringValues(JsonElement el) =>
            el.ValueKind == JsonValueKind.Array
                ? el.EnumerateArray().Select(e => e.GetString()).ToList()
                : new List<string?> { el.GetString() };

        [Fact]
        public async Task Token_for_tenant_includes_tenant_feature_roles_not_global()
        {
            // Arrange: a user with NO global roles but the RH feature role in the selected tenant.
            var tenantId = Guid.NewGuid();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "u@x.com",
                Email = "u@x.com",
                FullName = "Test User",
                IsActive = true
            };

            var userManager = MakeUserManager();
            userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
            // No global Identity roles.
            userManager.GetRolesAsync(user).Returns(new List<string>());

            var signInManager = MakeSignInManager(userManager);
            var configuration = MakeConfiguration();

            var permissionService = Substitute.For<IPermissionService>();
            permissionService.GetUserPermissionsForTenantAsync(user.Id, tenantId, Arg.Any<CancellationToken>())
                .Returns(new List<string> { "employees.view" });

            var tenantService = Substitute.For<ITenantService>();
            tenantService.IsMemberAsync(tenantId, user.Id, Arg.Any<CancellationToken>())
                .Returns(true);
            tenantService.GetUserRoleAsync(tenantId, user.Id, Arg.Any<CancellationToken>())
                .Returns(TenantRole.Admin);

            var tenantRoleService = Substitute.For<ITenantRoleService>();
            // The tenant grants the RH feature role.
            tenantRoleService.GetTenantRoleNamesAsync(user.Id, tenantId, Arg.Any<CancellationToken>())
                .Returns(new List<string> { "RH" });
            // Effective roles = global (master) union per-tenant feature roles. Do the real union
            // so the test proves the wiring rather than feeding the answer in literally.
            tenantRoleService
                .GetEffectiveRoleNamesAsync(user.Id, tenantId, Arg.Any<IReadOnlyCollection<string>>(),
                    Arg.Any<CancellationToken>())
                .Returns(async call =>
                {
                    var globalRoles = call.ArgAt<IReadOnlyCollection<string>>(2);
                    var tenantRoles = await tenantRoleService.GetTenantRoleNamesAsync(
                        user.Id, tenantId, call.ArgAt<CancellationToken>(3));
                    return (IReadOnlyList<string>)globalRoles.Concat(tenantRoles).Distinct().ToList();
                });

            var sut = new AuthService(
                userManager, signInManager, configuration,
                permissionService, tenantService, tenantRoleService);

            // Act: drive the real GenerateJwtToken through the public SelectTenantAsync entry point.
            var response = await sut.SelectTenantAsync(user.Id, tenantId);
            var token = response.Token;

            // Assert against the raw serialized JWT payload.
            using var doc = DecodePayload(token);
            var root = doc.RootElement;

            // Role claim: production uses ClaimTypes.Role serialized directly (no OutboundClaimTypeMap),
            // so the payload key is the full URI, not the short "role".
            Assert.False(
                root.TryGetProperty("role", out _),
                "Production path serializes ClaimTypes.Role as the full URI, not the short 'role'.");
            Assert.True(
                root.TryGetProperty(RoleClaimKey, out var roleEl),
                $"Expected role claim under key '{RoleClaimKey}' in JWT payload.");
            var roleValues = StringValues(roleEl);
            Assert.Contains("RH", roleValues);

            // tenant_role claim = ITenantService.GetUserRoleAsync value (enum name).
            Assert.True(root.TryGetProperty("tenant_role", out var tenantRoleEl), "Expected 'tenant_role' claim");
            Assert.Equal("Admin", tenantRoleEl.GetString());

            // permission claim = IPermissionService.GetUserPermissionsForTenantAsync value.
            Assert.True(root.TryGetProperty("permission", out var permEl), "Expected 'permission' claim");
            Assert.Contains("employees.view", StringValues(permEl));

            // tenant_id claim is present.
            Assert.True(root.TryGetProperty("tenant_id", out var tenantIdEl), "Expected 'tenant_id' claim");
            Assert.Equal(tenantId.ToString(), tenantIdEl.GetString());
        }

        [Fact]
        public async Task Token_without_tenant_has_global_roles_and_no_tenant_claims()
        {
            // Arrange: an active user with a GLOBAL role and no tenant selected.
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "admin@x.com",
                Email = "admin@x.com",
                FullName = "Admin User",
                IsActive = true
            };

            var userManager = MakeUserManager();
            userManager.FindByEmailAsync(user.Email).Returns(user);
            userManager.GetRolesAsync(user).Returns(new List<string> { "Administrador" });

            var signInManager = MakeSignInManager(userManager);
            signInManager
                .CheckPasswordSignInAsync(user, "pw", Arg.Any<bool>())
                .Returns(SignInResult.Success);

            var configuration = MakeConfiguration();

            var permissionService = Substitute.For<IPermissionService>();
            var tenantService = Substitute.For<ITenantService>();
            var tenantRoleService = Substitute.For<ITenantRoleService>();

            var sut = new AuthService(
                userManager, signInManager, configuration,
                permissionService, tenantService, tenantRoleService);

            // Act: LoginAsync with no TenantSlug drives the real GenerateJwtToken(user, null).
            var response = await sut.LoginAsync(
                new SaaS_BasePlatform.Application.DTOs.Auth.LoginRequestDto(user.Email, "pw"));
            var token = response.Token;

            // Assert against the raw serialized JWT payload.
            using var doc = DecodePayload(token);
            var root = doc.RootElement;

            // Global role serialized under the full ClaimTypes.Role URI (no OutboundClaimTypeMap).
            Assert.True(
                root.TryGetProperty(RoleClaimKey, out var roleEl),
                $"Expected role claim under key '{RoleClaimKey}' in JWT payload.");
            Assert.Contains("Administrador", StringValues(roleEl));

            // No-tenant branch: none of the tenant-scoped claims are emitted.
            Assert.False(root.TryGetProperty("tenant_id", out _), "Expected no 'tenant_id' claim");
            Assert.False(root.TryGetProperty("tenant_role", out _), "Expected no 'tenant_role' claim");
            Assert.False(root.TryGetProperty("permission", out _), "Expected no 'permission' claim");

            // Tenant data services must not be consulted for a no-tenant token.
            await tenantRoleService.DidNotReceiveWithAnyArgs()
                .GetEffectiveRoleNamesAsync(default, default, default!, default);
            await permissionService.DidNotReceiveWithAnyArgs()
                .GetUserPermissionsForTenantAsync(default, default, default);
        }
    }
}
