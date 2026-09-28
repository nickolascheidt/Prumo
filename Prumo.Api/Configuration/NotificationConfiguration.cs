using Prumo.Infrastructure.Services;

namespace Prumo.Api.Configuration;

public static class NotificationConfiguration
{
    /// <summary>
    /// Registers the notification publisher. There is no e-mail provider: notifications go
    /// to the log, with their payload (links and tokens) only in Development.
    /// </summary>
    public static IServiceCollection AddNotificationConfiguration(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        services.AddSingleton<INotificationPublisher>(sp => new LoggingNotificationPublisher(
            sp.GetRequiredService<ILogger<LoggingNotificationPublisher>>(),
            includePayload: environment.IsDevelopment()));

        return services;
    }
}
