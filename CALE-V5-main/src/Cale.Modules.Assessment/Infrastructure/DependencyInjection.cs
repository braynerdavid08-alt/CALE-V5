using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Application.Commands;
using Cale.Modules.Assessment.Application.Queries;
using Cale.Modules.Assessment.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.Modules.Assessment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAssessmentModule(
        this IServiceCollection services)
    {
        services.AddScoped<IAttemptStore, AttemptStore>();
        services.AddScoped<IAttemptStats, AttemptStatsService>();
        services.AddScoped<StartExamHandler>();
        services.AddScoped<AnswerQuestionHandler>();
        services.AddScoped<FinishExamHandler>();
        services.AddScoped<ReviewAttemptHandler>();
        services.AddScoped<SaveRatingHandler>();
        services.AddScoped<ListRatingsHandler>();
        services.AddScoped<ListResultsHandler>();
        return services;
    }
}
