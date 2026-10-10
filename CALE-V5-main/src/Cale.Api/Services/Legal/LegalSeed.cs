using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Legal;

/// <summary>
/// Publica la versión inicial (borrador) de cada documento que aún no tenga ninguna versión.
/// Nunca toca documentos que ya existen: los cambios se hacen creando versiones nuevas desde el admin.
/// </summary>
public static class LegalSeed
{
    public const string InitialVersion = "1.0-borrador";

    public static async Task EnsureAsync(CaleDbContext db, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            var existing = await db.Set<LegalDocumentVersion>()
                .Select(v => v.DocumentKey)
                .Distinct()
                .ToListAsync(ct);
            var now = DateTime.UtcNow;
            var added = 0;
            foreach (var doc in LegalDocuments.All.Where(d => !existing.Contains(d.Key)))
            {
                var version = LegalDocumentVersion.CreateDraft(
                    doc.Key,
                    InitialVersion,
                    doc.Title,
                    ReadDraft(doc.Key),
                    "Versión inicial en borrador, con datos pendientes y sin revisión jurídica.",
                    requiresReacceptance: true,
                    actorUserId: null,
                    now);
                version.Publish(actorUserId: null, now);
                db.Add(version);
                added++;
            }

            if (added > 0)
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation("LegalSeed published {Count} initial draft documents.", added);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LegalSeed failed; legal documents may be missing until the next start.");
        }
    }

    public static string ReadDraft(string key)
    {
        var name = $"Cale.Api.Legal.Drafts.{key}.md";
        using var stream = typeof(LegalSeed).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing embedded legal draft {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
