namespace Prumo.Infrastructure.Services
{
    /// <summary>
    /// Hands a notification off for delivery.
    ///
    /// Publishing is best-effort on purpose: an invitation is the admin adding someone by
    /// e-mail, so the e-mail is *a notice, not an authorization*. If publishing fails,
    /// nobody gets into a tenant by mistake — the person just isn't told, and every flow
    /// can be resent by hand.
    ///
    /// Lives in Infrastructure, not Application, because references in this repo point
    /// Application → Infrastructure.
    /// </summary>
    public interface INotificationPublisher
    {
        Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default);
    }
}
