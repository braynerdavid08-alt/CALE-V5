using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.Modules.Engagement.Application.Abstractions;
using Cale.Modules.Engagement.Application.Queries;
using Cale.Modules.Engagement.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.Modules.Engagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEngagementModule(
        this IServiceCollection services)
    {
        services.AddScoped<INotificationStore, NotificationStore>();
        services.AddScoped<NotificationPublisher>();
        services.AddScoped<INotificationPublisher>(sp =>
            sp.GetRequiredService<NotificationPublisher>());
        services.AddScoped<INotificationQueries>(sp =>
            sp.GetRequiredService<NotificationPublisher>());
        services.AddScoped<ListNotificationsHandler>();
        return services;
    }
}
