using System.Security.Cryptography;
using System.Text;

namespace SaaS_BasePlatform.Infrastructure.Security
{
    public static class ApiKeyHasher
    {
        public const string AnonPrefix = "sbp_anon_";
        public const string ServicePrefix = "sbp_svc_";

        public static (string fullKey, string prefix, string hash) Generate(bool isService)
        {
            var prefix = isService ? ServicePrefix : AnonPrefix;
            var randomBytes = RandomNumberGenerator.GetBytes(32);
            var secret = Convert.ToBase64String(randomBytes)
                .Replace("+", "")
                .Replace("/", "")
                .Replace("=", "");

            var fullKey = prefix + secret;
            var hash = Hash(fullKey);
            return (fullKey, prefix, hash);
        }

        public static string Hash(string key)
        {
            var bytes = Encoding.UTF8.GetBytes(key);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        public static bool TryGetPrefix(string key, out string prefix)
        {
            prefix = string.Empty;
            if (string.IsNullOrEmpty(key)) return false;

            if (key.StartsWith(AnonPrefix))
            {
                prefix = AnonPrefix;
                return true;
            }
            if (key.StartsWith(ServicePrefix))
            {
                prefix = ServicePrefix;
                return true;
            }
            return false;
        }
    }
}
