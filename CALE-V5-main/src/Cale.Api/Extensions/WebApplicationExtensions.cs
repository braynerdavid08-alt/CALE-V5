using Cale.Api.Middleware;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Catalog.Infrastructure;
using Cale.Modules.Identity.Domain;
using Cale.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Extensions;

public static class WebApplicationExtensions
{
    public static async Task UseCalePipelineAsync(this WebApplication app)
    {
        if (app.Configuration.GetValue("ForwardedHeaders:Enabled", false))
        {
            var opts = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };
            // Behind Traefik / nginx / cloud load balancer.
            opts.KnownNetworks.Clear();
            opts.KnownProxies.Clear();
            app.UseForwardedHeaders(opts);
        }

        if (app.Configuration.GetValue("Hosting:UseHttpsRedirection", false))
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseMiddleware<RequestTelemetryMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors("Cale");
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        // Angular SPA deep links (keep /api/* on controllers).
        app.MapFallbackToFile("index.html");

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        await db.Database.EnsureCreatedAsync();
        await FeatureSchema.EnsureAsync(db);

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var seedLogger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("IdentitySeed");

        var adminEmail = app.Configuration["Seed:Admin:Email"];
        var adminPassword = app.Configuration["Seed:Admin:Password"];
        var adminName = app.Configuration["Seed:Admin:Name"] ?? "Administrador";
        var purgeOthers = app.Configuration.GetValue("Seed:Admin:PurgeOthers", false);

        if (!string.IsNullOrWhiteSpace(adminEmail)
            && !string.IsNullOrWhiteSpace(adminPassword))
        {
            await IdentitySeed.EnsureSoleAdminAsync(
                db,
                hasher,
                clock,
                adminEmail,
                adminPassword,
                adminName,
                purgeOthers,
                seedLogger);
        }
        else if (app.Configuration.GetValue("Seed:DemoUsers", false))
        {
            // Optional local demos — never enable on public internet.
            await IdentitySeed.EnsureDemoUsersAsync(db, hasher, clock);
        }

        var adminId = await db.Set<User>()
            .Where(x => x.Role == Roles.Admin)
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (adminId is null && !string.IsNullOrWhiteSpace(adminEmail))
        {
            adminId = await db.Set<User>()
                .Where(x => x.Email == adminEmail.Trim().ToLowerInvariant())
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();
        }

        var seedDir = Path.Combine(app.Environment.ContentRootPath, "SeedData");
        if (!Directory.Exists(seedDir))
        {
            seedDir = Path.Combine(AppContext.BaseDirectory, "SeedData");
        }

        var catalogLogger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("CatalogSeed");
        await CatalogSeed.EnsureOfficialBanksAsync(
            db,
            seedDir,
            clock,
            adminId,
            catalogLogger);
    }
}
