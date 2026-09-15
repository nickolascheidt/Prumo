using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Prumo.Api.Configuration;
using System.Net;

namespace Prumo.Tests.Api
{
    /// <summary>
    /// No piloto a API fica atrás de dois proxies: Caddy (TLS) e o nginx do Angular.
    /// Sem processar X-Forwarded-*, RemoteIpAddress é o IP do container do nginx e o
    /// scheme é http — o log de request, o rate limit por IP e qualquer forense entre os
    /// dois clientes ficam cegos. O nginx anexa o seu próprio salto ao cabeçalho, então
    /// a cadeia vista pela API é "cliente, caddy".
    /// </summary>
    public class ForwardedHeadersConfigurationTests
    {
        private static ForwardedHeadersOptions Options()
        {
            var services = new ServiceCollection();
            services.AddForwardedHeadersConfiguration();
            return services.BuildServiceProvider().GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        }

        private static async Task<HttpContext> Run(string forwardedFor, string forwardedProto)
        {
            var ctx = new DefaultHttpContext();
            ctx.Connection.RemoteIpAddress = IPAddress.Parse("172.18.0.4"); // nginx
            ctx.Request.Scheme = "http";
            ctx.Request.Headers["X-Forwarded-For"] = forwardedFor;
            ctx.Request.Headers["X-Forwarded-Proto"] = forwardedProto;

            var middleware = new ForwardedHeadersMiddleware(
                _ => Task.CompletedTask, NullLoggerFactory.Instance, Microsoft.Extensions.Options.Options.Create(Options()));
            await middleware.Invoke(ctx);
            return ctx;
        }

        [Fact]
        public async Task Client_ip_is_read_through_caddy_and_nginx()
        {
            var ctx = await Run("203.0.113.5, 172.18.0.3", "https");

            Assert.Equal(IPAddress.Parse("203.0.113.5"), ctx.Connection.RemoteIpAddress);
            Assert.Equal("https", ctx.Request.Scheme);
        }

        [Fact]
        public async Task A_client_cannot_spoof_its_ip_by_prepending_to_the_chain()
        {
            // O Caddy descarta X-Forwarded-For de quem não é proxy confiável, mas a API não
            // depende disso: só anda dois saltos a partir da direita.
            var ctx = await Run("10.0.0.1, 203.0.113.5, 172.18.0.3", "https");

            Assert.Equal(IPAddress.Parse("203.0.113.5"), ctx.Connection.RemoteIpAddress);
        }
    }
}
