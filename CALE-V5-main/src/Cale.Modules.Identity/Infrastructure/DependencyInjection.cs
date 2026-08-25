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
        services.AddScoped<ISchoolProfileStore, SchoolProfileStore>();
        services.AddScoped<IUserLookup, UserLookupService>();
        services.AddScoped<ISchoolAffiliationLookup, SchoolAffiliationLookup>();
        services.AddScoped<LoginUserHandler>();
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<RegisterTeacherHandler>();
        services.AddScoped<RegisterSchoolHandler>();
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<CreateTeacherHandler>();
        services.AddScoped<UpdateUserHandler>();
        services.AddScoped<DeleteUserHandler>();
        services.AddScoped<SetUserActiveHandler>();
        services.AddScoped<GetCurrentUserHandler>();
        services.AddScoped<UpdateMyProfileHandler>();
        services.AddScoped<ListUsersHandler>();
        services.AddScoped<GetSchoolProfileHandler>();
        services.AddScoped<ListSchoolPlansHandler>();
        services.AddScoped<ManageSchoolPlanHandler>();
        services.AddScoped<ListSchoolMembersHandler>();
        services.AddScoped<CreateSchoolMemberHandler>();
        services.AddScoped<AttachSchoolMemberHandler>();
        services.AddScoped<UpdateSchoolMemberHandler>();
        return services;
    }
}
