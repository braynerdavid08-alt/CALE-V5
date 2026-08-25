using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.Modules.Classroom.Application.Abstractions;
using Cale.Modules.Classroom.Application.Commands;
using Cale.Modules.Classroom.Application.Queries;
using Cale.Modules.Classroom.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.Modules.Classroom.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddClassroomModule(
        this IServiceCollection services)
    {
        services.AddScoped<IClassroomStore, ClassroomStore>();
        services.AddScoped<IGroupAccess, GroupAccessService>();
        services.AddScoped<GroupCommandHandler>();
        services.AddScoped<ClassroomContentHandler>();
        services.AddScoped<ClassroomQueryHandler>();
        return services;
    }
}
