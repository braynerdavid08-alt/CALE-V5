using Cale.Api.Middleware;
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

        if (app.Environment.IsDevelopment()
            || app.Configuration.GetValue("Seed:DemoUsers", false))
        {
            await IdentitySeed.EnsureDemoUsersAsync(db, hasher, clock);
        }

        var adminId = await db.Set<User>()
            .Where(x => x.Email == IdentitySeed.AdminEmail)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        var seedDir = Path.Combine(app.Environment.ContentRootPath, "SeedData");
        if (!Directory.Exists(seedDir))
        {
            seedDir = Path.Combine(AppContext.BaseDirectory, "SeedData");
        }

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("CatalogSeed");
        await CatalogSeed.EnsureOfficialBanksAsync(
            db,
            seedDir,
            clock,
            adminId,
            logger);
    }
}
