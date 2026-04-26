using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SaaS_BasePlatform.Application.Services;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace SaaS_BasePlatform.Api.Authentication
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        public const string TenantIdClaim = "tenant_id";
        public const string ApiKeyTypeClaim = "api_key_type";
        public const string ApiKeyIdClaim = "api_key_id";

        private readonly IApiKeyService _apiKeyService;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<ApiKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IApiKeyService apiKeyService)
            : base(options, logger, encoder)
        {
            _apiKeyService = apiKeyService;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var presented = ExtractKey();
            if (string.IsNullOrEmpty(presented))
                return AuthenticateResult.NoResult();

            var key = await _apiKeyService.ValidateAsync(presented);
            if (key == null)
                return AuthenticateResult.Fail("Invalid API key");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, key.CreatedByUserId.ToString()),
                new Claim(ClaimTypes.Name, $"apikey:{key.Prefix}"),
                new Claim(TenantIdClaim, key.TenantId.ToString()),
                new Claim(ApiKeyTypeClaim, key.Type.ToString()),
                new Claim(ApiKeyIdClaim, key.Id.ToString())
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }

        private string? ExtractKey()
        {
            if (Request.Headers.TryGetValue(Options.HeaderName, out var headerValues))
            {
                var v = headerValues.ToString();
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            }

            if (Request.Headers.TryGetValue("Authorization", out var authValues))
            {
                var raw = authValues.ToString();
                if (raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = raw.Substring("Bearer ".Length).Trim();
                    if (token.StartsWith("sbp_")) return token;
                }
            }

            return null;
        }
    }
}
