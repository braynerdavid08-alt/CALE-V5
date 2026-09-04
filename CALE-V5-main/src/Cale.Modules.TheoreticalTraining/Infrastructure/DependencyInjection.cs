using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.Modules.TheoreticalTraining.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.Modules.TheoreticalTraining.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTheoreticalTrainingModule(
        this IServiceCollection services)
    {
        services.AddScoped<ITrainingEligibilityService, TrainingEligibilityService>();
        services.AddScoped<ISchoolStudentEnrollmentBootstrap, SchoolStudentEnrollmentBootstrap>();
        services.AddScoped<TheoryTrainingService>();
        services.AddScoped<PracticalTrainingService>();
        services.AddScoped<ApprenticeRegistryService>();
        services.AddScoped<SchoolExcelImportService>();
        services.AddSingleton<SchoolExcelImportPreviewCache>();
        services.AddHostedService<TheoryReminderService>();
        return services;
    }
}
