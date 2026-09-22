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
        app.UseMiddleware<LegacyPresentationUploadMiddleware>();
        app.UseMiddleware<LegacyCatalogUploadMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors("Cale");
        app.UseRateLimiter();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<MustChangePasswordMiddleware>();
        app.MapControllers();
        app.MapHub<Cale.Api.Hubs.LiveClassroomHub>("/hubs/live");
        app.MapHub<Cale.Api.Hubs.GameShowHub>("/hubs/game-show");

        // Angular SPA deep links (keep /api/* on controllers).
        app.MapFallbackToFile("index.html");

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var providerKind = DatabaseConnection.Detect(
            DatabaseConnection.Resolve(app.Configuration));
        var bootLogger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Cale.Startup");
        bootLogger.LogInformation(
            "Database provider: {Provider}; {Description}",
            providerKind,
            DatabaseConnection.Describe(DatabaseConnection.Resolve(app.Configuration)));

        var allowEnsureCreated = app.Configuration.GetValue(
            "Database:AllowEnsureCreated",
            app.Environment.IsDevelopment());
        var applyFeatureSchema = app.Configuration.GetValue(
            "Database:ApplyFeatureSchema",
            app.Environment.IsDevelopment());
        var useEfMigrations = app.Configuration.GetValue("Database:UseEfMigrations", false);

        try
        {
            await db.Database.OpenConnectionAsync();
            await db.Database.CloseConnectionAsync();

            if (useEfMigrations)
            {
                bootLogger.LogInformation(
                    "Database:UseEfMigrations=true — applying EF Core migrations.");
                await db.Database.MigrateAsync();
            }
            else if (allowEnsureCreated)
            {
                bootLogger.LogWarning(
                    "Database:AllowEnsureCreated is enabled — creating missing tables via EnsureCreated (dev/bootstrap only).");
                await db.Database.EnsureCreatedAsync();
            }
            else
            {
                bootLogger.LogInformation(
                    "Skipping EnsureCreated/Migrate (UseEfMigrations=false, AllowEnsureCreated=false). Schema must already exist.");
            }

            // Always repair attempt columns needed for simulacros — independent of FeatureSchema.
            await AttemptSchemaGuard.EnsureAsync(db, bootLogger);

            if (!useEfMigrations && applyFeatureSchema)
            {
                bootLogger.LogWarning(
                    "Database:ApplyFeatureSchema is enabled — applying FeatureSchema patches at startup.");
                await FeatureSchema.EnsureAsync(db);
            }
            else if (applyFeatureSchema && useEfMigrations)
            {
                bootLogger.LogInformation(
                    "Skipping FeatureSchema because EF migrations are the schema source of truth.");
            }
            else
            {
                bootLogger.LogInformation(
                    "Skipping FeatureSchema (Database:ApplyFeatureSchema=false).");
            }
        }
        catch (Exception ex)
        {
            bootLogger.LogError(ex, "Database initialization failed ({Provider})", providerKind);

            if (ex is InvalidOperationException && ex.Message.Contains("Cannot connect", StringComparison.Ordinal))
            {
                throw;
            }

            var description = DatabaseConnection.Describe(
                DatabaseConnection.Resolve(app.Configuration));
            throw new InvalidOperationException(
                $"Cannot connect to the database ({description}). " +
                "On Render: Postgres → Connect → Connect to MICALE (DATABASE_URL), " +
                "same region as the web service, Internal URL only. " +
                $"Driver error: {ex.GetBaseException().Message}",
                ex);
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var seedLogger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("IdentitySeed");

        var adminEmail = app.Configuration["Seed:Admin:Email"];
        var adminPassword = app.Configuration["Seed:Admin:Password"];
        var adminPasswordHash = app.Configuration["Seed:Admin:PasswordHash"];
        var adminName = app.Configuration["Seed:Admin:Name"] ?? "Administrador";
        var purgeOthers = app.Configuration.GetValue("Seed:Admin:PurgeOthers", false);
        var bootstrapAdmin = app.Configuration.GetValue("Seed:BootstrapAdmin", true);
        var allowPurgeOutsideDev = app.Configuration.GetValue(
            "Seed:Admin:AllowPurgeInNonDevelopment",
            false);

        if (purgeOthers && !app.Environment.IsDevelopment() && !allowPurgeOutsideDev)
        {
            seedLogger.LogError(
                "Refusing Seed:Admin:PurgeOthers outside Development. " +
                "Set Seed:Admin:AllowPurgeInNonDevelopment=true only for a deliberate one-shot cleanup.");
            purgeOthers = false;
        }

        var hasSecret = !string.IsNullOrWhiteSpace(adminPassword)
            || !string.IsNullOrWhiteSpace(adminPasswordHash);

        if (!string.IsNullOrWhiteSpace(adminEmail) && hasSecret)
        {
            await IdentitySeed.EnsureSoleAdminAsync(
                db,
                hasher,
                clock,
                adminEmail,
                adminName,
                purgeOthers,
                password: adminPassword,
                passwordHash: adminPasswordHash,
                logger: seedLogger);
        }
        else if (bootstrapAdmin)
        {
            // Temporary admin once; after you change email/password it is never recreated.
            // Requires explicit Seed:Admin credentials — never uses hardcoded passwords.
            await IdentitySeed.EnsureBootstrapAdminIfNoneAsync(
                db,
                hasher,
                clock,
                seedLogger,
                email: adminEmail,
                password: adminPassword,
                name: adminName);
        }
        else if (app.Configuration.GetValue("Seed:DemoUsers", false))
        {
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
        var allowPartialRebuild = app.Configuration.GetValue("Seed:Catalog:AllowPartialRebuild", false);
        var allowReplaceExisting = app.Configuration.GetValue("Seed:Catalog:AllowReplaceExisting", false);
        await CatalogSeed.EnsureOfficialBanksAsync(
            db,
            seedDir,
            clock,
            adminId,
            catalogLogger,
            allowPartialRebuild: allowPartialRebuild,
            allowReplaceExisting: allowReplaceExisting);
    }
}
