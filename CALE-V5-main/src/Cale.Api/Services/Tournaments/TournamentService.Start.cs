using System.Text.Json;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Assessment.Domain.Gamification;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Tournaments;

public sealed partial class TournamentService
{
    public async Task<TournamentBracketDto> StartAsync(int tournamentId, int actorUserId, CancellationToken ct)
    {
        var t = await LoadTournamentAsync(tournamentId, ct, tracking: true);
        await EnsureTeacherOrSchoolAsync(t, actorUserId, ct);

        if (t.Status is TournamentStatuses.InProgress or TournamentStatuses.Finished)
        {
            return await GetBracketAsync(tournamentId, actorUserId, ct);
        }

        if (t.Status == TournamentStatuses.Cancelled)
        {
            throw new DomainException("El torneo está cancelado.", 400, "cancelled");
        }

        var participants = t.Participants.Where(p => !p.IsEliminated).ToList();
        if (participants.Count < MinCapacity)
        {
            throw new DomainException(
                $"Se necesitan al menos {MinCapacity} participantes.",
                400,
                "not_enough_players");
        }

        if (participants.Count > MaxCapacity)
        {
            throw new DomainException($"Máximo {MaxCapacity} participantes por aula.", 400, "too_many_players");
        }

        // School isolation
        var schoolOk = await _db.Set<Modules.Identity.Domain.User>().AsNoTracking()
            .Where(u => participants.Select(p => p.UserId).Contains(u.Id))
            .AllAsync(u => u.SchoolId == t.SchoolUserId, ct);
        if (!schoolOk)
        {
            throw new DomainException("Hay participantes de otra escuela.", 400, "wrong_school");
        }

        var expectedVersion = t.RowVersion;
        t.Status = TournamentStatuses.Preparing;
        t.UpdatedAt = _clock.UtcNow;
        t.RowVersion++;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException("Otro proceso ya está iniciando el torneo.", 409, "conflict");
        }

        if (t.RowVersion != expectedVersion + 1)
        {
            // soft check — EF concurrency token handles the hard case
        }

        if (t.Rounds.Count > 0)
        {
            throw new DomainException("El torneo ya tiene un cuadro generado.", 400, "bracket_exists");
        }

        var playerIds = participants.Select(p => p.UserId).ToList();
        var pairs = TournamentBracket.PairRound(playerIds, Random.Shared);
        var now = _clock.UtcNow;

        // BracketSize stores the starting field size (not a padded power-of-two).
        t.BracketSize = participants.Count;
        t.CurrentRoundNumber = 1;
        t.Status = TournamentStatuses.InProgress;
        t.UpdatedAt = now;
        t.RowVersion++;

        var firstRound = new TournamentRound
        {
            TournamentId = t.Id,
            RoundNumber = 1,
            Name = TournamentBracket.RoundNameForField(participants.Count),
            Status = TournamentMatchStatuses.Active,
            StartedAt = now
        };
        t.Rounds.Add(firstRound);

        for (var i = 0; i < pairs.Count; i++)
        {
            var (p1, p2, p1Bye, p2Bye) = pairs[i];
            firstRound.Matches.Add(new TournamentMatch
            {
                TournamentId = t.Id,
                SlotIndex = i,
                Status = TournamentMatchStatuses.Pending,
                Player1UserId = p1,
                Player2UserId = p2,
                Player1IsBye = p1Bye,
                Player2IsBye = p2Bye
            });

            if (p1 is int uid1)
            {
                participants.First(x => x.UserId == uid1).BracketSlot = i * 2;
            }

            if (p2 is int uid2)
            {
                participants.First(x => x.UserId == uid2).BracketSlot = i * 2 + 1;
            }
        }

        await _db.SaveChangesAsync(ct);

        await ActivateRoundMatchesAsync(t, firstRound.Matches.OrderBy(m => m.SlotIndex).ToList(), ct);
        await _db.SaveChangesAsync(ct);
        await MaybeAdvanceRoundAsync(t.Id, ct);

        var bracket = await GetBracketAsync(t.Id, actorUserId, ct);
        await _broadcast.TournamentStarted(t.Id, bracket, ct);
        await _broadcast.RoundStarted(t.Id, 1, firstRound.Name, ct);
        await NotifyActiveMatchAssignmentsAsync(t.Id, ct);

        return bracket;
    }

    /// <summary>
    /// Activates only <see cref="TournamentMatchStatuses.Pending"/> matches. Active/Finished are left untouched
    /// so recovery and concurrent workers are idempotent.
    /// </summary>
    /// <returns>Ids of matches that transitioned out of Pending in this call.</returns>
    private async Task<IReadOnlyList<int>> ActivateRoundMatchesAsync(
        Tournament t,
        IReadOnlyList<TournamentMatch> matches,
        CancellationToken ct)
    {
        var changed = new List<int>();
        foreach (var match in matches.OrderBy(m => m.SlotIndex))
        {
            if (match.Status != TournamentMatchStatuses.Pending)
            {
                continue;
            }

            if (match.Player1IsBye && match.Player2UserId is int only2)
            {
                await FinishMatchAsByeAsync(match, only2, ct);
                changed.Add(match.Id);
            }
            else if (match.Player2IsBye && match.Player1UserId is int only1)
            {
                await FinishMatchAsByeAsync(match, only1, ct);
                changed.Add(match.Id);
            }
            else if (match.Player1UserId is int && match.Player2UserId is int)
            {
                await ActivateMatchAsync(t, match, ct);
                if (match.Status == TournamentMatchStatuses.Active)
                {
                    changed.Add(match.Id);
                }
            }
            else
            {
                match.Status = TournamentMatchStatuses.Finished;
                match.FinishReason = TournamentMatchFinishReasons.Bye;
                match.FinishedAt = _clock.UtcNow;
                match.RowVersion++;
                changed.Add(match.Id);
            }
        }

        return changed;
    }

    private async Task NotifyActiveMatchAssignmentsAsync(int tournamentId, CancellationToken ct)
    {
        var active = await _db.Set<TournamentMatch>()
            .Where(m => m.TournamentId == tournamentId && m.Status == TournamentMatchStatuses.Active)
            .ToListAsync(ct);
        await NotifyMatchAssignmentsAsync(tournamentId, active.Select(m => m.Id).ToList(), ct);
    }

    private async Task NotifyMatchAssignmentsAsync(
        int tournamentId,
        IReadOnlyList<int> matchIds,
        CancellationToken ct)
    {
        if (matchIds.Count == 0)
        {
            return;
        }

        var matches = await _db.Set<TournamentMatch>()
            .Where(m => matchIds.Contains(m.Id) && m.Status == TournamentMatchStatuses.Active)
            .ToListAsync(ct);
        foreach (var m in matches)
        {
            if (m.Player1UserId is int u1)
            {
                await _broadcast.MatchAssigned(tournamentId, u1, await BuildMatchViewAsync(m.Id, u1, ct), ct);
                await _broadcast.MatchReady(tournamentId, u1, m.Id, ct);
            }

            if (m.Player2UserId is int u2)
            {
                await _broadcast.MatchAssigned(tournamentId, u2, await BuildMatchViewAsync(m.Id, u2, ct), ct);
                await _broadcast.MatchReady(tournamentId, u2, m.Id, ct);
            }
        }
    }

    public async Task<TournamentBracketDto> GetBracketAsync(
        int tournamentId,
        int actorUserId,
        CancellationToken ct)
    {
        var t = await LoadTournamentAsync(tournamentId, ct);
        await EnsureCanViewAsync(t, actorUserId, ct);

        var names = t.Participants.ToDictionary(p => p.UserId, p => p.DisplayNameSnapshot);
        var photos = await PhotoUrlsAsync(
            t.Participants.Select(p => p.UserId)
                .Concat(t.Rounds.SelectMany(r => r.Matches)
                    .SelectMany(m => new[] { m.Player1UserId, m.Player2UserId, m.WinnerUserId })
                    .Where(id => id is int)
                    .Select(id => id!.Value)),
            ct);
        var matches = t.Rounds
            .SelectMany(r => r.Matches.Select(m => new TournamentBracketMatchDto(
                m.Id,
                r.RoundNumber,
                r.Name,
                m.SlotIndex,
                m.Player1UserId,
                m.Player1UserId is int p1 ? names.GetValueOrDefault(p1) : null,
                m.Player2UserId,
                m.Player2UserId is int p2 ? names.GetValueOrDefault(p2) : null,
                m.Player1IsBye,
                m.Player2IsBye,
                m.Status,
                m.WinnerUserId,
                m.FinishReason,
                m.Player1Correct,
                m.Player2Correct,
                m.Player1UserId is int a ? photos.GetValueOrDefault(a) : null,
                m.Player2UserId is int b ? photos.GetValueOrDefault(b) : null)))
            .OrderBy(m => m.RoundNumber)
            .ThenBy(m => m.SlotIndex)
            .ToList();

        string? championName = null;
        string? championPhoto = null;
        if (t.ChampionUserId is int c)
        {
            championName = names.GetValueOrDefault(c);
            championPhoto = photos.GetValueOrDefault(c);
        }

        return new TournamentBracketDto(
            t.Id,
            t.Status,
            t.BracketSize,
            t.CurrentRoundNumber,
            matches,
            t.ChampionUserId,
            championName,
            championPhoto);
    }

    private Task FinishMatchAsByeAsync(TournamentMatch match, int winnerUserId, CancellationToken ct)
    {
        _ = ct;
        if (match.Status == TournamentMatchStatuses.Finished)
        {
            return Task.CompletedTask;
        }

        match.Status = TournamentMatchStatuses.Finished;
        match.WinnerUserId = winnerUserId;
        match.FinishReason = TournamentMatchFinishReasons.Bye;
        match.StartedAt ??= _clock.UtcNow;
        match.FinishedAt = _clock.UtcNow;
        match.RowVersion++;
        // Persist with the caller's SaveChanges so recovery batches stay atomic.
        // No XP / no win stats for BYE
        return Task.CompletedTask;
    }

    private async Task ActivateMatchAsync(Tournament tournament, TournamentMatch match, CancellationToken ct)
    {
        if (match.Status != TournamentMatchStatuses.Pending)
        {
            return;
        }

        // Crash mid-activation may leave Pending with questions already seeded — reuse them.
        // Never Clear()+reseed: concurrent recovery would race on UNIQUE (MatchId, Ordinal).
        var existingCount = match.Questions.Count;
        if (existingCount < tournament.QuestionsPerMatch)
        {
            var need = tournament.QuestionsPerMatch - existingCount;
            var excludeIds = match.Questions.Select(q => q.QuestionId).ToHashSet();
            var questions = await _play.BuildTournamentQuestionsAsync(
                tournament.CreatedByUserId,
                tournament.SchoolUserId,
                need,
                tournament.QuestionMode,
                ParseCategories(tournament.CategoryFilterJson),
                ParseIds(tournament.ManualQuestionIdsJson),
                ct,
                excludeIds: excludeIds,
                bankIds: ParseIds(tournament.BankIdsJson));

            if (questions.Count < need)
            {
                throw new DomainException(
                    "No hay suficientes preguntas para iniciar el enfrentamiento.",
                    400,
                    "no_questions");
            }

            var nextOrdinal = existingCount == 0
                ? 0
                : match.Questions.Max(q => q.Ordinal) + 1;
            foreach (var q in questions.Take(need))
            {
                match.Questions.Add(new TournamentMatchQuestion
                {
                    Ordinal = nextOrdinal++,
                    QuestionId = q.QuestionId,
                    CorrectOptionId = q.CorrectOptionId,
                    IsSuddenDeath = false
                });
            }
        }

        match.Status = TournamentMatchStatuses.Active;
        match.CurrentQuestionIndex = 0;
        match.StartedAt = _clock.UtcNow;
        match.QuestionStartedAt = _clock.UtcNow;
        match.RowVersion++;
    }

    private static List<string>? ParseCategories(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private static List<int>? ParseIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<int>>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }
}
