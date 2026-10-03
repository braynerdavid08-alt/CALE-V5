using Cale.Modules.Courses.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.Modules.Courses.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCoursesModule(this IServiceCollection services)
    {
        services.AddScoped<CourseService>();
        return services;
    }
}
