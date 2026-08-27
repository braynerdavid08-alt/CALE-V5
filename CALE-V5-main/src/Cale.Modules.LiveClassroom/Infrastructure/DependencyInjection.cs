using Cale.Modules.LiveClassroom.Application.Abstractions;
using Cale.Modules.LiveClassroom.Application.Commands;
using Cale.Modules.LiveClassroom.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.Modules.LiveClassroom.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLiveClassroomModule(
        this IServiceCollection services)
    {
        services.AddScoped<ILiveSessionStore, LiveSessionStore>();
        services.AddScoped<LiveSessionHandler>();
        services.AddHostedService<LiveQuestionTimerService>();
        return services;
    }
}
