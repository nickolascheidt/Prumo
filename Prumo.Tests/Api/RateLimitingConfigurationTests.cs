using Microsoft.AspNetCore.Http;
using Prumo.Api.Configuration;
using System.Net;

namespace Prumo.Tests.Api
{
    /// <summary>
    /// O limite dos endpoints públicos (login, cadastro, esqueci-senha) era uma janela
    /// única para o site inteiro: 10 requisições por minuto somando todo mundo. Com dois
    /// clientes, o pico de login das 8h já batia nisso, e um anônimo derrubava o login de
    /// todos com 10 requisições. A partição é por IP de origem.
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
            // Sem IP (teste, socket unix) não pode estourar: cai numa partição fixa.
            var key = RateLimitingConfiguration.PublicPartitionKey(WithRemoteIp(null));

            Assert.False(string.IsNullOrEmpty(key));
        }
    }
}
