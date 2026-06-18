using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Tests.Services
{
    /// <summary>
    /// Thin static test helper that builds the claim list exactly as
    /// AuthService.GenerateJwtToken does and signs it with a fixed test key.
    /// Its purpose is to lock the claim shape produced by Task B4.
    ///
    /// Parameters mirror the real method's inputs so tests can assert the
    /// token claim shape without wiring up the full service graph.
    /// </summary>
    public static class AuthServiceTestHarness
    {
        private const string TestKey = "test-key-for-auth-service-token-tests-32-chars!!";
        private const string TestIssuer = "test-issuer";
        private const string TestAudience = "test-audience";

        /// <summary>
        /// Builds a JWT with the same claim types and ordering as the rewritten
        /// AuthService.GenerateJwtToken (Task B4).
        ///
        /// Claim types emitted:
        ///   ClaimTypes.NameIdentifier, ClaimTypes.Name, ClaimTypes.Email,
        ///   "FullName", "tenant_id" (when tenant), ClaimTypes.Role per effective role,
        ///   "tenant_role" (when present), "permission" per permission.
        /// </summary>
        /// <param name="user">The user whose identity claims are added.</param>
        /// <param name="tenantId">Selected tenant, or null for a global (no-tenant) token.</param>
        /// <param name="globalRoles">Global Identity roles (e.g. Administrador) for this user.</param>
        /// <param name="tenantFeatureRoles">Per-tenant feature roles from TenantUserRoles.</param>
        /// <param name="tenantPermissions">Permission strings resolved for the tenant.</param>
        /// <param name="tenantRole">TenantRole enum name from TenantUser.Role (e.g. "Admin").</param>
        public static Task<string> GenerateTokenAsync(
            ApplicationUser user,
            Guid? tenantId,
            IReadOnlyCollection<string> globalRoles,
            IReadOnlyCollection<string>? tenantFeatureRoles = null,
            IReadOnlyCollection<string>? tenantPermissions = null,
            string? tenantRole = null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.UserName!),
                new(ClaimTypes.Email, user.Email!),
                new("FullName", user.FullName ?? string.Empty)
            };

            // Effective role names: global ∪ per-tenant (mirrors the real method's roleClaims variable).
            IEnumerable<string> roleClaims = globalRoles;

            if (tenantId.HasValue)
            {
                claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));

                // Effective roles = global (master) ∪ per-tenant feature roles.
                roleClaims = globalRoles
                    .Concat(tenantFeatureRoles ?? Array.Empty<string>())
                    .Distinct();

                if (!string.IsNullOrEmpty(tenantRole))
                    claims.Add(new Claim("tenant_role", tenantRole));

                if (tenantPermissions != null)
                    claims.AddRange(tenantPermissions.Select(p => new Claim("permission", p)));
            }

            // Use the JWT short-form claim name "role" so the payload contains "role" not the full URI.
            // This mirrors what JwtSecurityTokenHandler does via OutboundClaimTypeMap when using
            // SecurityTokenDescriptor, and is what clients receive in the JWT.
            claims.AddRange(roleClaims.Distinct().Select(r => new Claim("role", r)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: TestIssuer,
                audience: TestAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds);

            return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
        }
    }
}
