using Cale.BuildingBlocks.Domain.Email;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Email;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.BuildingBlocks.Infrastructure.Security;
using Cale.BuildingBlocks.Infrastructure.Time;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
        services.Configure<EmailOptions>(config.GetSection(EmailOptions.SectionName));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<LoggingEmailSender>();
        services.AddSingleton<SmtpEmailSender>();
        services.AddSingleton<IEmailSender, ConfigurableEmailSender>();
        return services;
    }

    public static IServiceCollection AddCalePersistence(
        this IServiceCollection services,
        IConfiguration config,
        MappingAssemblies mappings)
    {
        var connection = DatabaseConnection.Resolve(config);
        var provider = DatabaseConnection.Detect(connection);

        services.AddSingleton(mappings);
        services.AddDbContext<CaleDbContext>(options =>
        {
            switch (provider)
            {
                case DatabaseProviderKind.Sqlite:
                {
                    var builder = new SqliteConnectionStringBuilder(connection)
                    {
                        DefaultTimeout = 30,
                        Cache = SqliteCacheMode.Shared
                    };
                    options.UseSqlite(builder.ToString(), sqlite =>
                    {
                        sqlite.CommandTimeout(30);
                        sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    });
                    break;
                }
                case DatabaseProviderKind.PostgreSql:
                    options.UseNpgsql(connection, npgsql =>
                    {
                        npgsql.CommandTimeout(30);
                        npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    });
                    break;
                default:
                    options.UseSqlServer(connection, sql =>
                    {
                        sql.CommandTimeout(30);
                        sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    });
                    break;
            }
        });
        return services;
    }
}
