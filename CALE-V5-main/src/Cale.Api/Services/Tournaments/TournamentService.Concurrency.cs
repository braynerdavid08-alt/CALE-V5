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

    private void ClearTrackedState() => _db.ChangeTracker.Clear();
}
