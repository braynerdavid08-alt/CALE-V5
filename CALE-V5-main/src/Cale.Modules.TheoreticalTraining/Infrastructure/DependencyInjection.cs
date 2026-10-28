using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.Modules.TheoreticalTraining.Application;
using Cale.Modules.TheoreticalTraining.Application.SchedulingReports;
using Cale.Modules.TheoreticalTraining.Application.WhatsApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.Modules.TheoreticalTraining.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTheoreticalTrainingModule(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        if (configuration is not null)
        {
            services.Configure<SchedulingReportsOptions>(
                configuration.GetSection(SchedulingReportsOptions.SectionName));
        }

        services.AddScoped<ITrainingEligibilityService, TrainingEligibilityService>();
        services.AddScoped<ISchoolStudentEnrollmentBootstrap, SchoolStudentEnrollmentBootstrap>();
        services.AddScoped<TheoryExamAutoAuthorizer>();
        services.AddScoped<TheoryTrainingService>();
        services.AddScoped<PracticalTrainingService>();
        services.AddScoped<IExamBookingEligibility, ExamBookingEligibility>();
        services.AddScoped<TheoryExamScheduleService>();
        services.AddScoped<StudentHoursService>();
        services.AddScoped<SchoolAuditService>();
        services.AddScoped<SchoolAgendaService>();
        services.AddScoped<ApprenticeRegistryService>();
        services.AddScoped<SchoolExcelImportService>();
        services.AddScoped<SchoolExcelExportService>();
        services.AddScoped<SchedulingReportDiffService>();
        services.AddScoped<SchedulingReportGenerator>();
        services.AddScoped<DailyFullReportHandler>();
        services.AddScoped<DailyChangesReportHandler>();
        services.AddSingleton<IWhatsAppCloudSender, DisabledWhatsAppCloudSender>();
        services.AddSingleton<SchoolExcelImportPreviewCache>();
        services.AddHostedService<TheoryReminderService>();
        // Intentionally no AddHostedService for Daily*Report while EnableDaily*Job is false.
        return services;
    }
}
