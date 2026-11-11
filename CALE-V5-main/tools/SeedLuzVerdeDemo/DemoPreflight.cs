using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace SeedLuzVerdeDemo;

internal static class DemoPreflight
{
    public sealed record Result(
        IReadOnlyList<string> PlannedEmails,
        IReadOnlyList<string> ExistingDemoEmails,
        IReadOnlyList<string> BlockingNonDemoEmails,
        IReadOnlyList<string> ExistingDemoSchools);

    public static async Task<Result> InspectAsync(CaleDbContext db, CancellationToken ct)
    {
        var planned = DemoMarkers.AllPlannedEmails()
            .Select(e => e.ToLowerInvariant())
            .Distinct()
            .ToList();

        var existing = await db.Set<User>()
            .AsNoTracking()
            .Where(u => planned.Contains(u.Email.ToLower()))
            .Select(u => new { u.Email, u.Role })
            .ToListAsync(ct);

        var demoExisting = existing
            .Where(u => DemoMarkers.IsDemoEmail(u.Email))
            .Select(u => u.Email)
            .OrderBy(x => x)
            .ToList();

        var blocking = existing
            .Where(u => !DemoMarkers.IsDemoEmail(u.Email))
            .Select(u => u.Email)
            .OrderBy(x => x)
            .ToList();

        // Also block if a non-DEMO school already uses our LegalName (shouldn't happen).
        var demoSchools = await db.Set<SchoolProfile>()
            .AsNoTracking()
            .Where(p => p.LegalName.StartsWith(DemoMarkers.LegalNamePrefix))
            .Select(p => p.LegalName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);

        return new Result(planned, demoExisting, blocking, demoSchools);
    }

    public static void Print(Result result)
    {
        Console.WriteLine($"Correos DEMO planificados: {result.PlannedEmails.Count}");
        Console.WriteLine($"Ya existen (DEMO, se regenerarán/purgarán): {result.ExistingDemoEmails.Count}");
        foreach (var e in result.ExistingDemoEmails.Take(12))
        {
            Console.WriteLine($"  - {e}");
        }

        if (result.ExistingDemoEmails.Count > 12)
        {
            Console.WriteLine($"  … +{result.ExistingDemoEmails.Count - 12} más");
        }

        Console.WriteLine($"Escuelas LegalName DEMO existentes: {result.ExistingDemoSchools.Count}");
        foreach (var s in result.ExistingDemoSchools)
        {
            Console.WriteLine($"  - {s}");
        }

        if (result.BlockingNonDemoEmails.Count > 0)
        {
            Console.WriteLine("BLOQUEO — correos planificados ocupados por cuentas NO DEMO:");
            foreach (var e in result.BlockingNonDemoEmails)
            {
                Console.WriteLine($"  - {e}");
            }
        }
        else
        {
            Console.WriteLine("Sin colisiones con cuentas reales (correos planificados libres o solo DEMO).");
        }
    }
}
