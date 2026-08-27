using Cale.Api.Middleware;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Email;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Email;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Catalog.Infrastructure;
using Cale.Modules.Identity.Domain;
using Cale.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
        var providerKind = DatabaseConnection.Detect(
            DatabaseConnection.Resolve(app.Configuration));
        var bootLogger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Cale.Startup");
        bootLogger.LogInformation(
            "Database provider: {Provider}; {Description}",
            providerKind,
            DatabaseConnection.Describe(DatabaseConnection.Resolve(app.Configuration)));

        var emailOpts = scope.ServiceProvider
            .GetRequiredService<IOptions<EmailOptions>>()
            .Value;
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        if (emailSender.IsConfigured)
        {
            bootLogger.LogInformation(
                "Email SMTP ready: host={Host} port={Port} from={From}",
                emailOpts.Smtp.Host,
                emailOpts.Smtp.Port,
                emailOpts.From);
        }
        else
        {
            bootLogger.LogWarning(
                "Email SMTP NOT configured. Users will NOT receive verification codes by email. " +
                "Set Email__Enabled=true, Email__From, Email__Smtp__Host/User/Password on Render (Gmail app password).");
        }
     
        try
        {
            await db.Database.OpenConnectionAsync();
            await db.Database.CloseConnectionAsync();

            await db.Database.EnsureCreatedAsync();
            await FeatureSchema.EnsureAsync(db);
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
            await IdentitySeed.EnsureBootstrapAdminIfNoneAsync(
                db,
                hasher,
                clock,
                seedLogger);
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
        await CatalogSeed.EnsureOfficialBanksAsync(
            db,
            seedDir,
            clock,
            adminId,
            catalogLogger);
    }
}
