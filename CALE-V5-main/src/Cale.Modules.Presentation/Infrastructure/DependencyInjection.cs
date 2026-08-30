using Cale.Modules.Presentation.Application.Abstractions;
using Cale.Modules.Presentation.Application.Commands;
using Cale.Modules.Presentation.Application.Queries;
using Cale.Modules.Presentation.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.Modules.Presentation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentationModule(this IServiceCollection services)
    {
        services.AddScoped<IPresentationStore, PresentationStore>();
        services.AddScoped<PresentationCommandHandler>();
        services.AddScoped<PresentationQueryHandler>();
        services.AddScoped<PresentationExchangeService>();
        return services;
    }
}
