using Microsoft.AspNetCore.HttpOverrides;

namespace Prumo.Api.Configuration;

/// <summary>
/// The API is never reached directly: in the deployment design it sits behind Caddy (TLS)
/// and the Angular nginx, and port 8080 is not published outside the compose network.
/// Without processing X-Forwarded-*, RemoteIpAddress is the nginx container's IP and
/// Request.Scheme is http: the request log's ClientIp, the per-IP rate limit and any
/// forensics between two clients go blind.
/// </summary>
public static class ForwardedHeadersConfiguration
{
    public static IServiceCollection AddForwardedHeadersConfiguration(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Two hops, counted from the right: nginx appends Caddy's IP to the header
            // (proxy_add_x_forwarded_for), and Caddy put the client's. A higher limit would
            // let a client prepend a fake IP and be believed.
            options.ForwardLimit = 2;

            // Container IPs change on every start, so they cannot be listed. The empty list
            // trusts any immediate proxy — acceptable only because the API port is not
            // published and the only way to it is the compose network.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }
}
