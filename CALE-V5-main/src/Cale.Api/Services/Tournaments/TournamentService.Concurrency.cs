using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Tournaments;

public sealed partial class TournamentService
{
    /// <summary>
    /// Cross-process concurrency for round advancement and champion declaration relies on:
    /// unique indexes (TorneoRondas.TournamentId+RoundNumber, TrofeosTorneo.TournamentId+UserId,
    /// PartidasJuego.SourceKey) plus optimistic RowVersion on Tournament. In-memory locks are
    /// intentionally not used as the sole guard — they would not cover multi-instance deployments.
    /// Expected races surface as unique/concurrency exceptions and are treated as "another worker won".
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            var msg = current.Message;
            if (msg.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("23505", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// SQLite busy/locked under concurrent writers — expected in multi-request tests and
    /// multi-instance pressure; callers should retry or map to conflict, never raw 500.
    /// </summary>
    private static bool IsSqliteBusy(DbUpdateException ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            var msg = current.Message;
            if (msg.Contains("database is locked", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("database is busy", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("SQLITE_BUSY", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("SQLITE_LOCKED", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task SaveChangesResilientAsync(CancellationToken ct)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (IsSqliteBusy(ex) && attempt < maxAttempts)
            {
                await Task.Delay(15 * attempt, ct);
            }
        }
    }

    private void ClearTrackedState() => _db.ChangeTracker.Clear();
}
