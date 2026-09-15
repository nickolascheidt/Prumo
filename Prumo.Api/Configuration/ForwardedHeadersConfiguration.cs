using Microsoft.AspNetCore.HttpOverrides;

namespace Prumo.Api.Configuration;

/// <summary>
/// A API nunca é alcançada direto: no piloto ela fica atrás do Caddy (TLS) e do nginx do
/// Angular, e a porta 8080 não é publicada fora da rede do compose. Sem processar
/// X-Forwarded-*, RemoteIpAddress é o IP do container do nginx e Request.Scheme é http:
/// o ClientIp do log de request, o rate limit por IP e qualquer forense entre os dois
/// clientes ficam cegos.
/// </summary>
public static class ForwardedHeadersConfiguration
{
    public static IServiceCollection AddForwardedHeadersConfiguration(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Dois saltos, contados da direita: o nginx anexa o IP do Caddy ao cabeçalho
            // (proxy_add_x_forwarded_for), e o Caddy pôs o do cliente. Um limite maior
            // deixaria um cliente prefixar um IP falso e ser acreditado.
            options.ForwardLimit = 2;

            // Os IPs dos containers mudam a cada subida, então não dá para listá-los. A
            // lista vazia confia em qualquer proxy imediato — aceitável só porque a porta
            // da API não é publicada e o único caminho até ela é a rede do compose.
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }
}
