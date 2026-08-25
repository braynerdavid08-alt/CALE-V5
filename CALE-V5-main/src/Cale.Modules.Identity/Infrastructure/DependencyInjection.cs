using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.Queries;
using Cale.Modules.Identity.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.Modules.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services)
    {
        services.AddScoped<IUserStore, UserStore>();
        services.AddScoped<IUserLookup, UserLookupService>();
        services.AddScoped<LoginUserHandler>();
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<GetCurrentUserHandler>();
        return services;
    }
}
