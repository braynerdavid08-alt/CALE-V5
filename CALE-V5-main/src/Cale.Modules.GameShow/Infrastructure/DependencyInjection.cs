using Cale.Modules.GameShow.Application;
using Cale.Modules.GameShow.Application.Abstractions;
using Cale.Modules.GameShow.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.Modules.GameShow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGameShowModule(this IServiceCollection services)
    {
        services.AddScoped<IGameShowStore, GameShowStore>();
        services.AddScoped<GameShowHandler>();
        return services;
    }
}
