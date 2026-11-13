using Cale.Modules.Assessment.Domain.Gamification;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Tournaments;

public sealed partial class TournamentService
{
    /// <summary>
    /// Hybrid recovery: on-demand (MaybeAdvanceRoundAsync) + periodic (ProcessTimeoutsAsync).
    /// Activates Pending matches on the current round of InProgress tournaments without recreating
    /// rounds, rewriting Active/Finished matches, or re-emitting RoundStarted.
    /// Cross-process safety: Pending→Active uses match RowVersion; concurrent recoveries are no-ops.
    /// </summary>
    public async Task RecoverStuckPendingRoundsAsync(CancellationToken ct)
    {
        var stuckIds = await _db.Set<Tournament>()
            .AsNoTracking()
            .Where(t => t.Status == TournamentStatuses.InProgress)
            .Where(t => t.Rounds.Any(r =>
                r.RoundNumber == t.CurrentRoundNumber
                && r.Matches.Any(m => m.Status == TournamentMatchStatuses.Pending)))
            .OrderBy(t => t.Id)
            .Select(t => t.Id)
            .Take(20)
            .ToListAsync(ct);

        foreach (var tournamentId in stuckIds)
        {
            try
            {
                await EnsureCurrentRoundActivatedAsync(tournamentId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Tournament {TournamentId} pending-round recovery failed; will retry on next tick.",
                    tournamentId);
            }
        }
    }

    private async Task EnsureCurrentRoundActivatedAsync(int tournamentId, CancellationToken ct)
    {
        var t = await LoadTournamentAsync(tournamentId, ct, tracking: true);
        if (t.Status != TournamentStatuses.InProgress)
        {
            return;
        }

        await EnsureRoundActivatedAsync(t, t.CurrentRoundNumber, ct);
    }

    /// <summary>
    /// Activate a known round number after a UNIQUE race (next round already persisted).
    /// Heals CurrentRoundNumber if the insert committed but the pointer lagged (should be rare).
    /// </summary>
    private async Task EnsureRoundActivatedByNumberAsync(int tournamentId, int roundNumber, CancellationToken ct)
    {
        var t = await LoadTournamentAsync(tournamentId, ct, tracking: true);
        if (t.Status != TournamentStatuses.InProgress)
        {
            return;
        }

        if (t.CurrentRoundNumber < roundNumber && t.Rounds.Any(r => r.RoundNumber == roundNumber))
        {
            t.CurrentRoundNumber = roundNumber;
            t.UpdatedAt = _clock.UtcNow;
            t.RowVersion++;
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                ClearTrackedState();
                t = await LoadTournamentAsync(tournamentId, ct, tracking: true);
                if (t.Status != TournamentStatuses.InProgress)
                {
                    return;
                }
            }
        }

        await EnsureRoundActivatedAsync(t, Math.Max(t.CurrentRoundNumber, roundNumber), ct);
    }

    private async Task EnsureRoundActivatedAsync(Tournament t, int roundNumber, CancellationToken ct)
    {
        if (t.Status != TournamentStatuses.InProgress)
        {
            return;
        }

        var round = t.Rounds.FirstOrDefault(r => r.RoundNumber == roundNumber);
        if (round is null)
        {
            return;
        }

        // Ensure matches are tracked with questions for ActivateMatchAsync reuse path.
        var matches = await _db.Set<TournamentMatch>()
            .Include(m => m.Questions)
            .Where(m => m.RoundId == round.Id)
            .OrderBy(m => m.SlotIndex)
            .ToListAsync(ct);

        var pending = matches.Where(m => m.Status == TournamentMatchStatuses.Pending).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        var changed = await ActivateRoundMatchesAsync(t, pending, ct);
        if (changed.Count == 0)
        {
            return;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another worker activated the same seats first (match RowVersion).
            ClearTrackedState();
            _logger.LogInformation(
                "Tournament {TournamentId} round {RoundNumber} activation lost RowVersion race; treating as idempotent.",
                t.Id,
                roundNumber);
            return;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // UNIQUE (MatchId, Ordinal) — concurrent ActivateMatchAsync already seeded questions.
            ClearTrackedState();
            _logger.LogInformation(
                "Tournament {TournamentId} round {RoundNumber} activation lost UNIQUE race on match questions; treating as idempotent.",
                t.Id,
                roundNumber);
            return;
        }
        catch (DbUpdateException ex) when (IsSqliteBusy(ex))
        {
            ClearTrackedState();
            _logger.LogInformation(
                "Tournament {TournamentId} round {RoundNumber} activation hit SQLite busy; will retry on next tick.",
                t.Id,
                roundNumber);
            return;
        }

        _logger.LogInformation(
            "Tournament {TournamentId} recovered {Count} pending match(es) on round {RoundNumber}.",
            t.Id,
            changed.Count,
            roundNumber);

        // Do not re-broadcast RoundStarted (may already have been sent). Notify only newly activated seats.
        await NotifyMatchAssignmentsAsync(t.Id, changed, ct);

        // BYE finishes may unblock the next advance.
        await MaybeAdvanceRoundAsync(t.Id, ct);
    }
}
