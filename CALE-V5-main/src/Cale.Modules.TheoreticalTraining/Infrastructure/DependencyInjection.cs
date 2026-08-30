using Cale.Modules.TheoreticalTraining.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.Modules.TheoreticalTraining.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTheoreticalTrainingModule(
        this IServiceCollection services)
    {
        services.AddScoped<TheoryTrainingService>();
        services.AddScoped<PracticalTrainingService>();
        services.AddHostedService<TheoryReminderService>();
        return services;
    }
}
