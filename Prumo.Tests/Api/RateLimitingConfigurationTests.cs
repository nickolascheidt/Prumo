using Microsoft.AspNetCore.Http;
using Prumo.Api.Configuration;
using System.Net;

namespace Prumo.Tests.Api
{
    /// <summary>
    /// The limit on public endpoints (login, sign-up, forgot password) used to be a single
    /// window for the whole site: 10 requests per minute across everyone. With two clients,
    /// the morning login peak already hit it, and one anonymous caller could lock everyone
    /// out with 10 requests. The partition is per source IP.
    /// </summary>
    public class RateLimitingConfigurationTests
    {
        private static HttpContext WithRemoteIp(string? ip)
        {
            var ctx = new DefaultHttpContext();
            ctx.Connection.RemoteIpAddress = ip is null ? null : IPAddress.Parse(ip);
            return ctx;
        }

        [Fact]
        public void Public_limiter_partitions_by_remote_ip()
        {
            var a = RateLimitingConfiguration.PublicPartitionKey(WithRemoteIp("203.0.113.5"));
            var b = RateLimitingConfiguration.PublicPartitionKey(WithRemoteIp("198.51.100.7"));

            Assert.NotEqual(a, b);
        }

        [Fact]
        public void Same_remote_ip_shares_a_partition()
        {
            var a = RateLimitingConfiguration.PublicPartitionKey(WithRemoteIp("203.0.113.5"));
            var b = RateLimitingConfiguration.PublicPartitionKey(WithRemoteIp("203.0.113.5"));

            Assert.Equal(a, b);
        }

        [Fact]
        public void Unknown_remote_ip_still_gets_a_partition()
        {
            // Without an IP (tests, unix socket) it must not throw: it falls into a fixed partition.
            var key = RateLimitingConfiguration.PublicPartitionKey(WithRemoteIp(null));

            Assert.False(string.IsNullOrEmpty(key));
        }
    }
}
