using System.Text;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Infrastructure;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.BuildingBlocks.Infrastructure.Security;
using Cale.Modules.Assessment.Infrastructure;
using Cale.Modules.Assessment.Infrastructure.Persistence;
using Cale.Modules.Catalog.Infrastructure;
using Cale.Modules.Catalog.Infrastructure.Persistence;
using Cale.Modules.Classroom.Infrastructure;
using Cale.Modules.Classroom.Infrastructure.Persistence;
using Cale.Modules.Engagement.Infrastructure;
using Cale.Modules.Engagement.Infrastructure.Persistence;
using Cale.Modules.Identity.Infrastructure;
using Cale.Modules.Identity.Infrastructure.Persistence;
using Cale.Modules.Presentation.Infrastructure;
using Cale.Modules.Presentation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace Cale.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddCaleServices(
        this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var config = builder.Configuration;

        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "CALE API",
                Version = "v5"
            });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header
            });
            options.AddSecurityRequirement(CreateBearerRequirement());
        });

        services.AddBuildingBlocks(config);
        services.AddCalePersistence(
            config,
            new MappingAssemblies(
                typeof(UserConfiguration).Assembly,
                typeof(BankConfiguration).Assembly,
                typeof(AttemptConfiguration).Assembly,
                typeof(GroupConfiguration).Assembly,
                typeof(NotificationConfiguration).Assembly,
                typeof(PresentationDeckConfiguration).Assembly));
        services.AddIdentityModule();
        services.AddCatalogModule();
        services.AddAssessmentModule();
        services.AddClassroomModule();
        services.AddEngagementModule();
        services.AddPresentationModule();
        services.AddMemoryCache();
        services.AddScoped<Cale.Api.Services.PilotMetricsService>();
        services.AddScoped<Cale.Api.Services.HomepageService>();
        services.AddCaleAuth(config);
        services.AddCaleCors(config);
        return builder;
    }

    private static void AddCaleAuth(
        this IServiceCollection services,
        IConfiguration config)
    {
        var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Missing Jwt section.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt.Key))
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin));
            options.AddPolicy(
                "SchoolOnly",
                p => p.RequireRole(Roles.School));
            options.AddPolicy(
                "CatalogReader",
                p => p.RequireRole(Roles.Admin, Roles.School, Roles.Teacher));
            options.AddPolicy(
                "TeacherOrAdmin",
                p => p.RequireRole(Roles.Teacher, Roles.Admin));
            options.AddPolicy(
                "StudentOnly",
                p => p.RequireRole(Roles.Student));
        });
    }

    private static void AddCaleCors(
        this IServiceCollection services,
        IConfiguration config)
    {
        var origins = config.GetSection("Cors:Origins").Get<string[]>()
            ?? ["http://localhost:4200", "http://127.0.0.1:4200"];

        // Allow comma-separated override: Cors__Origins=https://app.tld,https://www.app.tld
        var fromEnv = Environment.GetEnvironmentVariable("Cors__Origins")
            ?? Environment.GetEnvironmentVariable("CORS_ORIGINS");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            origins = fromEnv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        services.AddCors(options =>
        {
            options.AddPolicy("Cale", policy =>
            {
                if (origins.Length == 1 && origins[0] == "*")
                {
                    policy.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .WithExposedHeaders("X-Request-Id");
                    return;
                }

                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders("X-Request-Id");
            });
        });
    }

    private static OpenApiSecurityRequirement CreateBearerRequirement()
    {
        var scheme = new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        };

        return new OpenApiSecurityRequirement
        {
            [scheme] = Array.Empty<string>()
        };
    }
}
