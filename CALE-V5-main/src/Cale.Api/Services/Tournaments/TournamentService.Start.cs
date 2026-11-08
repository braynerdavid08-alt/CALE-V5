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

        var bracketSize = TournamentBracket.NextPowerOfTwo(participants.Count);
        var slots = TournamentBracket.SeedSlots(
            participants.Select(p => p.UserId).ToList(),
            bracketSize,
            Random.Shared);
        var totalRounds = TournamentBracket.RoundCount(bracketSize);
        var now = _clock.UtcNow;

        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i] is int uid)
            {
                var p = participants.First(x => x.UserId == uid);
                p.BracketSlot = i;
            }
        }

        // Clear any previous rounds if restarting from preparing (shouldn't happen)
        if (t.Rounds.Count > 0)
        {
            throw new DomainException("El torneo ya tiene un cuadro generado.", 400, "bracket_exists");
        }

        for (var roundNumber = 1; roundNumber <= totalRounds; roundNumber++)
        {
            var round = new TournamentRound
            {
                TournamentId = t.Id,
                RoundNumber = roundNumber,
                Name = TournamentBracket.RoundName(roundNumber, totalRounds),
                Status = roundNumber == 1 ? TournamentMatchStatuses.Active : TournamentMatchStatuses.Pending,
                StartedAt = roundNumber == 1 ? now : null
            };
            t.Rounds.Add(round);

            var matchCount = bracketSize / (1 << roundNumber);
            for (var slot = 0; slot < matchCount; slot++)
            {
                round.Matches.Add(new TournamentMatch
                {
                    TournamentId = t.Id,
                    SlotIndex = slot,
                    Status = TournamentMatchStatuses.Pending
                });
            }
        }

        var firstRound = t.Rounds.First(r => r.RoundNumber == 1);
        var pairs = TournamentBracket.FirstRoundPairs(slots);
        for (var i = 0; i < pairs.Count; i++)
        {
            var (p1, p2, p1Bye, p2Bye) = pairs[i];
            var match = firstRound.Matches.First(m => m.SlotIndex == i);
            match.Player1UserId = p1;
            match.Player2UserId = p2;
            match.Player1IsBye = p1Bye;
            match.Player2IsBye = p2Bye;
        }

        t.BracketSize = bracketSize;
        t.CurrentRoundNumber = 1;
        t.Status = TournamentStatuses.InProgress;
        t.UpdatedAt = now;
        t.RowVersion++;
        await _db.SaveChangesAsync(ct);

        // Resolve BYEs and start real matches
        foreach (var match in firstRound.Matches.OrderBy(m => m.SlotIndex).ToList())
        {
            if (match.Player1IsBye && match.Player2UserId is int only2)
            {
                await FinishMatchAsByeAsync(match, only2, ct);
            }
            else if (match.Player2IsBye && match.Player1UserId is int only1)
            {
                await FinishMatchAsByeAsync(match, only1, ct);
            }
            else if (match.Player1UserId is int a && match.Player2UserId is int b)
            {
                await ActivateMatchAsync(t, match, ct);
            }
            else
            {
                // Both BYE — should not happen with proper seeding
                match.Status = TournamentMatchStatuses.Finished;
                match.FinishReason = TournamentMatchFinishReasons.Bye;
                match.FinishedAt = _clock.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        await MaybeAdvanceRoundAsync(t.Id, ct);

        var bracket = await GetBracketAsync(t.Id, actorUserId, ct);
        await _broadcast.TournamentStarted(t.Id, bracket, ct);
        await _broadcast.RoundStarted(t.Id, 1, firstRound.Name, ct);

        // Notify players of their matches
        var active = await _db.Set<TournamentMatch>()
            .Where(m => m.TournamentId == t.Id && m.Status == TournamentMatchStatuses.Active)
            .ToListAsync(ct);
        foreach (var m in active)
        {
            if (m.Player1UserId is int u1)
            {
                await _broadcast.MatchAssigned(t.Id, u1, await BuildMatchViewAsync(m.Id, u1, ct), ct);
                await _broadcast.MatchReady(t.Id, u1, m.Id, ct);
            }

            if (m.Player2UserId is int u2)
            {
                await _broadcast.MatchAssigned(t.Id, u2, await BuildMatchViewAsync(m.Id, u2, ct), ct);
                await _broadcast.MatchReady(t.Id, u2, m.Id, ct);
            }
        }

        return bracket;
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

    private async Task FinishMatchAsByeAsync(TournamentMatch match, int winnerUserId, CancellationToken ct)
    {
        match.Status = TournamentMatchStatuses.Finished;
        match.WinnerUserId = winnerUserId;
        match.FinishReason = TournamentMatchFinishReasons.Bye;
        match.StartedAt = _clock.UtcNow;
        match.FinishedAt = _clock.UtcNow;
        match.RowVersion++;
        await _db.SaveChangesAsync(ct);
        // No XP / no win stats for BYE
    }

    private async Task ActivateMatchAsync(Tournament tournament, TournamentMatch match, CancellationToken ct)
    {
        var questions = await _play.BuildTournamentQuestionsAsync(
            tournament.CreatedByUserId,
            tournament.SchoolUserId,
            tournament.QuestionsPerMatch,
            tournament.QuestionMode,
            ParseCategories(tournament.CategoryFilterJson),
            ParseIds(tournament.ManualQuestionIdsJson),
            ct,
            bankIds: ParseIds(tournament.BankIdsJson));

        if (questions.Count < tournament.QuestionsPerMatch)
        {
            throw new DomainException(
                "No hay suficientes preguntas para iniciar el enfrentamiento.",
                400,
                "no_questions");
        }

        match.Questions.Clear();
        var ordinal = 0;
        foreach (var q in questions.Take(tournament.QuestionsPerMatch))
        {
            match.Questions.Add(new TournamentMatchQuestion
            {
                Ordinal = ordinal++,
                QuestionId = q.QuestionId,
                CorrectOptionId = q.CorrectOptionId,
                IsSuddenDeath = false
            });
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
