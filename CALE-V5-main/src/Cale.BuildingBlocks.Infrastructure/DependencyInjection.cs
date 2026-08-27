using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.BuildingBlocks.Infrastructure.Security;
using Cale.BuildingBlocks.Infrastructure.Time;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBuildingBlocks(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        return services;
    }

    public static IServiceCollection AddCalePersistence(
        this IServiceCollection services,
        IConfiguration config,
        MappingAssemblies mappings)
    {
        var connection = config.GetConnectionString("Cale")
            ?? throw new InvalidOperationException(
                "Missing ConnectionStrings:Cale");

        services.AddSingleton(mappings);
        services.AddDbContext<CaleDbContext>(options =>
        {
            if (IsSqlite(connection))
            {
                var builder = new SqliteConnectionStringBuilder(connection)
                {
                    DefaultTimeout = 30,
                    Cache = SqliteCacheMode.Shared
                };

                options.UseSqlite(builder.ToString(), sqlite =>
                {
                    sqlite.CommandTimeout(30);
                });
            }
            else
            {
                options.UseSqlServer(connection);
            }
        });
        return services;
    }

    private static bool IsSqlite(string connection) =>
        connection.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
        || connection.Contains("Filename=", StringComparison.OrdinalIgnoreCase);
}
