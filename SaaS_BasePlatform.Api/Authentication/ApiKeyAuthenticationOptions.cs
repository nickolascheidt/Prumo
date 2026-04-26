using Microsoft.AspNetCore.Authentication;

namespace SaaS_BasePlatform.Api.Authentication
{
    public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
        public const string DefaultScheme = "ApiKey";
        public string HeaderName { get; set; } = "apikey";
    }
}
