using Prumo.Notifications.Contracts;

namespace Prumo.Infrastructure.Services
{
    /// <summary>
    /// Entrega uma notificação à fila e devolve. **Não** envia e-mail, não espera envio e
    /// não sabe qual é o provedor — quem envia é o serviço de notificação, do outro lado.
    ///
    /// Publicar é best-effort de propósito: como o convite é o admin adicionando por
    /// e-mail, o e-mail é *notificação e não autorização*. Se a publicação falhar, ninguém
    /// entra em tenant nenhum por engano — a pessoa só não é avisada, e todo fluxo tem
    /// reenvio manual. É essa propriedade que permite o serviço ser assíncrono.
    ///
    /// Fica em Infrastructure, junto de <see cref="ICacheService"/>, porque a direção de
    /// referência deste repo é Application → Infrastructure.
    /// </summary>
    public interface INotificationPublisher
    {
        Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default);
    }
}
