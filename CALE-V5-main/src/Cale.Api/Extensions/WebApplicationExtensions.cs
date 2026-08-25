using Cale.Api.Middleware;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Catalog.Infrastructure;
using Cale.Modules.Identity.Domain;
using Cale.Modules.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Extensions;

public static class WebApplicationExtensions
{
    public static async Task UseCalePipelineAsync(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors("Cale");
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        await db.Database.EnsureCreatedAsync();
        await FeatureSchema.EnsureAsync(db);

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        await IdentitySeed.EnsureDemoUsersAsync(db, hasher, clock);

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
