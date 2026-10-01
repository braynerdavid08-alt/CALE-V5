using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Media;

/// <summary>
/// Copies question/option images still pointing at /uploads/{file} (container disk, wiped on
/// redeploy) into the database media store and rewrites their URLs. Runs once per startup.
/// </summary>
public sealed class LegacyUploadMigrationService : BackgroundService
{
    private const string Prefix = "/uploads/";
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LegacyUploadMigrationService> _logger;

    public LegacyUploadMigrationService(
        IServiceScopeFactory scopes,
        ILogger<LegacyUploadMigrationService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            await MigrateAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Legacy upload migration failed.");
        }
    }

    private async Task MigrateAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var media = scope.ServiceProvider.GetRequiredService<ICatalogMediaStore>();

        var questionUrls = await db.Set<Question>().AsNoTracking()
            .Where(q => q.ImageUrl != null && q.ImageUrl.StartsWith(Prefix))
            .Select(q => q.ImageUrl!)
            .Distinct()
            .ToListAsync(ct);
        var optionUrls = await db.Set<QuestionOption>().AsNoTracking()
            .Where(o => o.ImageUrl != null && o.ImageUrl.StartsWith(Prefix))
            .Select(o => o.ImageUrl!)
            .Distinct()
            .ToListAsync(ct);
        var urls = questionUrls.Concat(optionUrls)
            .Where(u => !u.StartsWith("/uploads/presentations/", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
        if (urls.Count == 0)
        {
            return;
        }

        var migrated = 0;
        var missing = new List<string>();
        foreach (var url in urls)
        {
            var fileName = url[Prefix.Length..];
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('/') || fileName.Contains('\\'))
            {
                missing.Add(url);
                continue;
            }

            var blob = await media.TryReadLegacyDiskAsync(fileName, ct);
            if (blob is null)
            {
                missing.Add(url);
                continue;
            }

            using var stream = new MemoryStream(blob.Value.Data);
            var newUrl = await media.SaveAsync(stream, blob.Value.FileName, blob.Value.ContentType, null, ct);
            await db.Set<Question>()
                .Where(q => q.ImageUrl == url)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.ImageUrl, newUrl), ct);
            await db.Set<QuestionOption>()
                .Where(o => o.ImageUrl == url)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.ImageUrl, newUrl), ct);
            migrated++;
        }

        if (migrated > 0)
        {
            _logger.LogInformation("Moved {Count} legacy /uploads images into the database.", migrated);
        }

        if (missing.Count > 0)
        {
            _logger.LogWarning(
                "{Count} question images point to /uploads files that no longer exist (re-upload them): {Urls}",
                missing.Count,
                string.Join(", ", missing.Take(30)));
        }
    }
}
