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
using Cale.Modules.LiveClassroom.Application.Abstractions;
using Cale.Modules.LiveClassroom.Infrastructure;
using Cale.Modules.LiveClassroom.Infrastructure.Persistence;
using Cale.Modules.TheoreticalTraining.Infrastructure;
using Cale.Modules.TheoreticalTraining.Infrastructure.Persistence;
using Cale.Modules.Presentation.Infrastructure;
using Cale.Modules.Presentation.Infrastructure.Persistence;
using Cale.Api.Hubs;
using Cale.Api.Infrastructure;
using Cale.Api.Services;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
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

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = UploadLimits.PresentationImportBytes;
        });
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = UploadLimits.PresentationImportBytes;
        });

        services.AddControllers()
            .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions));
        services.AddSignalR()
            .AddJsonProtocol(options => ConfigureJson(options.PayloadSerializerOptions));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("login", limiter =>
            {
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.PermitLimit = 12;
                limiter.QueueLimit = 0;
            });
        });
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
                typeof(PresentationDeckConfiguration).Assembly,
                typeof(LiveSessionConfiguration).Assembly,
                typeof(TheoryTopicConfiguration).Assembly));
        services.AddIdentityModule();
        services.AddCatalogModule();
        services.AddAssessmentModule();
        services.AddClassroomModule();
        services.AddLiveClassroomModule();
        services.AddTheoreticalTrainingModule();
        services.AddEngagementModule();
        services.AddPresentationModule();
        services.AddSingleton<UploadStorage>();
        services.AddScoped<ILiveSessionBroadcaster, LiveSessionBroadcaster>();
        services.AddMemoryCache(options =>
        {
            // Presentation media cache entries set Size = byte length.
            options.SizeLimit = 400L * 1024 * 1024;
        });
        services.AddScoped<Cale.Api.Services.PilotMetricsService>();
        services.AddScoped<Cale.Api.Services.HomepageService>();
        services.AddScoped<Cale.Api.Services.AuthCookieService>();
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
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken)
                            && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        else if (string.IsNullOrEmpty(context.Token)
                            && context.Request.Cookies.TryGetValue(
                                AuthCookieNames.Access,
                                out var cookieToken)
                            && !string.IsNullOrWhiteSpace(cookieToken))
                        {
                            context.Token = cookieToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        if (context.Response.HasStarted)
                        {
                            return;
                        }

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";
                        await context.Response.WriteAsJsonAsync(new ProblemDetails
                        {
                            Status = StatusCodes.Status401Unauthorized,
                            Title = "Necesitas iniciar sesión.",
                            Detail = "unauthorized",
                            Type = "https://httpstatuses.com/401",
                            Instance = context.Request.Path
                        });
                    }
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
                        .WithExposedHeaders("X-Request-Id", "Accept-Ranges", "Content-Range", "Content-Length");
                    return;
                }

                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()
                    .WithExposedHeaders("X-Request-Id", "Accept-Ranges", "Content-Range", "Content-Length");
            });
        });
    }

    private static void ConfigureJson(System.Text.Json.JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.Converters.Add(new UtcDateTimeConverter());
        options.Converters.Add(new NullableUtcDateTimeConverter());
        options.Converters.Add(new JsonStringEnumConverter());
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
