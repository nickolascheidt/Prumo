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
    /// In the deployment design the API sits behind two proxies: Caddy (TLS) and the Angular
    /// nginx. Without processing X-Forwarded-*, RemoteIpAddress is the nginx container's IP
    /// and the scheme is http — the request log, the per-IP rate limit and any forensics
    /// between two clients go blind. nginx appends its own hop to the header, so the chain
    /// the API sees is "client, caddy".
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
            // Caddy drops X-Forwarded-For from anyone who is not a trusted proxy, but the API
            // does not depend on that: it only walks two hops from the right.
            var ctx = await Run("10.0.0.1, 203.0.113.5, 172.18.0.3", "https");

            Assert.Equal(IPAddress.Parse("203.0.113.5"), ctx.Connection.RemoteIpAddress);
        }
    }
}
