using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.Commands;
using Cale.Modules.Catalog.Application.Queries;
using Cale.Modules.Catalog.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.Modules.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services)
    {
        services.AddScoped<ICatalogStore, CatalogStore>();
        services.AddScoped<ListBanksHandler>();
        services.AddScoped<ListBlocksHandler>();
        services.AddScoped<ListQuestionsHandler>();
        services.AddScoped<GetQuestionHandler>();
        services.AddScoped<SaveBankHandler>();
        services.AddScoped<SaveQuestionHandler>();
        services.AddScoped<SaveExamHandler>();
        services.AddScoped<ImportExamFromWordHandler>();
        services.AddScoped<AssignExamToGroupHandler>();
        services.AddScoped<ListExamsHandler>();
        return services;
    }
}
