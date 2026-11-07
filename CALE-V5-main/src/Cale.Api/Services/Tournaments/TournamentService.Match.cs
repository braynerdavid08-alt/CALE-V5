using Cale.Api.Services.Play;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Assessment.Domain.Gamification;
using Cale.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Tournaments;

public sealed partial class TournamentService
{
    public async Task<TournamentMatchViewDto> GetMatchAsync(int matchId, int actorUserId, CancellationToken ct)
    {
        var match = await LoadMatchAsync(matchId, ct);
        var tournament = await LoadTournamentAsync(match.TournamentId, ct);
        await EnsureCanViewAsync(tournament, actorUserId, ct);
        return await BuildMatchViewAsync(matchId, actorUserId, ct);
    }

    public async Task<TournamentAnswerResultDto> AnswerAsync(
        int matchId,
        int userId,
        TournamentAnswerRequest request,
        CancellationToken ct)
    {
        var match = await LoadMatchAsync(matchId, ct, tracking: true);
        if (match.Status != TournamentMatchStatuses.Active)
        {
            throw new DomainException("El enfrentamiento no está activo.", 400, "match_not_active");
        }

        if (match.Player1UserId != userId && match.Player2UserId != userId)
        {
            throw new DomainException("No eres jugador de este enfrentamiento.", 403, "forbidden");
        }

        var question = match.Questions.FirstOrDefault(q => q.Id == request.MatchQuestionId)
            ?? throw new DomainException("Pregunta no válida.", 400, "invalid_question");

        if (question.Ordinal != match.CurrentQuestionIndex)
        {
            throw new DomainException("Esa no es la pregunta actual.", 400, "wrong_question");
        }

        if (match.Answers.Any(a => a.MatchQuestionId == question.Id && a.UserId == userId))
        {
            throw new DomainException("Ya respondiste esta pregunta.", 400, "already_answered");
        }

        var tournament = await _db.Set<Tournament>().FirstAsync(t => t.Id == match.TournamentId, ct);
        var now = _clock.UtcNow;
        var started = match.QuestionStartedAt ?? match.StartedAt ?? now;
        var elapsedMs = (int)Math.Clamp((now - started).TotalMilliseconds, 0, tournament.SecondsPerQuestion * 1000);
        var timedOut = (now - started).TotalSeconds > tournament.SecondsPerQuestion;
        var correct = !timedOut
            && request.OptionId is int opt
            && opt == question.CorrectOptionId;

        match.Answers.Add(new TournamentAnswer
        {
            MatchId = match.Id,
            MatchQuestionId = question.Id,
            UserId = userId,
            SelectedOptionId = timedOut ? null : request.OptionId,
            IsCorrect = correct,
            ResponseTimeMs = elapsedMs,
            AnsweredAt = now
        });

        if (match.Player1UserId == userId)
        {
            if (correct)
            {
                match.Player1Correct++;
            }

            match.Player1TimeMs += elapsedMs;
        }
        else
        {
            if (correct)
            {
                match.Player2Correct++;
            }

            match.Player2TimeMs += elapsedMs;
        }

        match.RowVersion++;
        await _db.SaveChangesAsync(ct);

        await TryAdvanceQuestionOrFinishAsync(match, tournament, ct);

        var state = await BuildMatchViewAsync(match.Id, userId, ct);
        await _broadcast.MatchUpdated(tournament.Id, state, ct);

        string? explanation = null;
        if (match.Status == TournamentMatchStatuses.Finished
            || match.Answers.Count(a => a.UserId == userId) >= match.Questions.Count(q => !q.IsSuddenDeath)
                && match.Answers.Any(a => a.MatchQuestionId == question.Id && a.UserId != userId))
        {
            explanation = (await _db.Set<Question>().AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == question.QuestionId, ct))?.Explanation;
        }

        return new TournamentAnswerResultDto(
            correct,
            match.Status == TournamentMatchStatuses.Finished ? question.CorrectOptionId : null,
            explanation,
            state,
            []);
    }

    private async Task TryAdvanceQuestionOrFinishAsync(
        TournamentMatch match,
        Tournament tournament,
        CancellationToken ct)
    {
        var current = match.Questions.FirstOrDefault(q => q.Ordinal == match.CurrentQuestionIndex);
        if (current is null)
        {
            return;
        }

        var answersForCurrent = match.Answers.Where(a => a.MatchQuestionId == current.Id).ToList();
        var expectedPlayers = new[] { match.Player1UserId, match.Player2UserId }
            .Where(id => id is int)
            .Select(id => id!.Value)
            .ToHashSet();

        if (answersForCurrent.Select(a => a.UserId).Distinct().Count() < expectedPlayers.Count)
        {
            // Wait for both, unless one timed out — auto-timeout handled by ProcessTimeouts
            return;
        }

        var nextOrdinal = match.CurrentQuestionIndex + 1;
        var next = match.Questions.FirstOrDefault(q => q.Ordinal == nextOrdinal && !q.IsSuddenDeath);
        if (next is not null)
        {
            match.CurrentQuestionIndex = nextOrdinal;
            match.QuestionStartedAt = _clock.UtcNow;
            match.RowVersion++;
            await _db.SaveChangesAsync(ct);
            return;
        }

        // All regular questions done — resolve
        await ResolveMatchAsync(match, tournament, ct);
    }

    private async Task ResolveMatchAsync(TournamentMatch match, Tournament tournament, CancellationToken ct)
    {
        if (match.Status == TournamentMatchStatuses.Finished)
        {
            return;
        }

        var p1 = match.Player1Correct;
        var p2 = match.Player2Correct;
        var t1 = match.Player1TimeMs;
        var t2 = match.Player2TimeMs;

        int? winner = null;
        string reason;

        if (p1 != p2)
        {
            winner = p1 > p2 ? match.Player1UserId : match.Player2UserId;
            reason = TournamentMatchFinishReasons.Score;
        }
        else if (t1 != t2)
        {
            winner = t1 < t2 ? match.Player1UserId : match.Player2UserId;
            reason = TournamentMatchFinishReasons.Time;
        }
        else
        {
            // Sudden death — add question and continue
            await AddSuddenDeathQuestionAsync(match, tournament, ct);
            return;
        }

        await CompleteMatchAsync(match, tournament, winner!.Value, reason, awardXp: true, ct);
    }

    private async Task AddSuddenDeathQuestionAsync(
        TournamentMatch match,
        Tournament tournament,
        CancellationToken ct)
    {
        var existingIds = match.Questions.Select(q => q.QuestionId).ToHashSet();
        var extra = await _play.BuildTournamentQuestionsAsync(
            tournament.CreatedByUserId,
            tournament.SchoolUserId,
            1,
            TournamentQuestionModes.Random,
            ParseCategories(tournament.CategoryFilterJson),
            null,
            ct,
            excludeIds: existingIds);

        if (extra.Count == 0)
        {
            // No question left — coin flip by user id for determinism
            var winner = (match.Player1UserId ?? 0) < (match.Player2UserId ?? 0)
                ? match.Player1UserId!.Value
                : match.Player2UserId!.Value;
            await CompleteMatchAsync(match, tournament, winner, TournamentMatchFinishReasons.SuddenDeath, true, ct);
            return;
        }

        var q = extra[0];
        var ordinal = match.Questions.Count == 0 ? 0 : match.Questions.Max(x => x.Ordinal) + 1;
        match.Questions.Add(new TournamentMatchQuestion
        {
            MatchId = match.Id,
            Ordinal = ordinal,
            QuestionId = q.QuestionId,
            CorrectOptionId = q.CorrectOptionId,
            IsSuddenDeath = true
        });
        match.CurrentQuestionIndex = ordinal;
        match.QuestionStartedAt = _clock.UtcNow;
        match.RowVersion++;
        await _db.SaveChangesAsync(ct);
    }

    private async Task CompleteMatchAsync(
        TournamentMatch match,
        Tournament tournament,
        int winnerUserId,
        string reason,
        bool awardXp,
        CancellationToken ct)
    {
        if (match.Status == TournamentMatchStatuses.Finished)
        {
            return;
        }

        match.Status = TournamentMatchStatuses.Finished;
        match.WinnerUserId = winnerUserId;
        match.FinishReason = reason;
        match.FinishedAt = _clock.UtcNow;
        match.RowVersion++;

        var loser = match.Player1UserId == winnerUserId ? match.Player2UserId : match.Player1UserId;
        if (loser is int loserId)
        {
            var participant = await _db.Set<TournamentParticipant>()
                .FirstOrDefaultAsync(p => p.TournamentId == tournament.Id && p.UserId == loserId, ct);
            if (participant is not null)
            {
                participant.IsEliminated = true;
                participant.EliminatedInRound = tournament.CurrentRoundNumber;
            }
        }

        await _db.SaveChangesAsync(ct);

        if (awardXp
            && reason is TournamentMatchFinishReasons.Score
                or TournamentMatchFinishReasons.Time
                or TournamentMatchFinishReasons.SuddenDeath)
        {
            await AwardMatchXpAsync(match, winnerUserId, loser, ct);
        }

        var bracketMatch = (await GetBracketAsync(tournament.Id, winnerUserId, ct))
            .Matches.First(m => m.Id == match.Id);
        await _broadcast.MatchFinished(tournament.Id, bracketMatch, ct);

        foreach (var uid in new[] { match.Player1UserId, match.Player2UserId }.Where(x => x is int).Select(x => x!.Value))
        {
            await _broadcast.MatchUpdated(tournament.Id, await BuildMatchViewAsync(match.Id, uid, ct), ct);
        }

        await MaybeAdvanceRoundAsync(tournament.Id, ct);
    }

    public async Task ProcessTimeoutsAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var active = await _db.Set<TournamentMatch>()
            .Include(m => m.Questions)
            .Include(m => m.Answers)
            .Where(m => m.Status == TournamentMatchStatuses.Active && m.QuestionStartedAt != null)
            .Take(100)
            .ToListAsync(ct);

        foreach (var match in active)
        {
            var tournament = await _db.Set<Tournament>().FirstAsync(t => t.Id == match.TournamentId, ct);
            var started = match.QuestionStartedAt!.Value;
            if ((now - started).TotalSeconds < tournament.SecondsPerQuestion + 1)
            {
                continue;
            }

            var current = match.Questions.FirstOrDefault(q => q.Ordinal == match.CurrentQuestionIndex);
            if (current is null)
            {
                continue;
            }

            foreach (var uid in new[] { match.Player1UserId, match.Player2UserId }.Where(x => x is int).Select(x => x!.Value))
            {
                if (match.Answers.Any(a => a.MatchQuestionId == current.Id && a.UserId == uid))
                {
                    continue;
                }

                match.Answers.Add(new TournamentAnswer
                {
                    MatchId = match.Id,
                    MatchQuestionId = current.Id,
                    UserId = uid,
                    SelectedOptionId = null,
                    IsCorrect = false,
                    ResponseTimeMs = tournament.SecondsPerQuestion * 1000,
                    AnsweredAt = now
                });
                if (match.Player1UserId == uid)
                {
                    match.Player1TimeMs += tournament.SecondsPerQuestion * 1000;
                }
                else
                {
                    match.Player2TimeMs += tournament.SecondsPerQuestion * 1000;
                }
            }

            match.RowVersion++;
            await _db.SaveChangesAsync(ct);
            await TryAdvanceQuestionOrFinishAsync(match, tournament, ct);
        }

        // Walkover after disconnect grace
        var disconnected = await _db.Set<TournamentParticipant>()
            .Where(p => !p.IsConnected && p.DisconnectedAt != null && !p.IsEliminated)
            .Take(50)
            .ToListAsync(ct);
        foreach (var p in disconnected)
        {
            if ((_clock.UtcNow - p.DisconnectedAt!.Value).TotalSeconds < DisconnectGraceSeconds)
            {
                continue;
            }

            var t = await LoadTournamentAsync(p.TournamentId, ct, tracking: true);
            await TryResolveWalkoverOnDisconnectAsync(t, p.UserId, ct);
        }
    }

    private async Task TryResolveWalkoverOnDisconnectAsync(Tournament t, int userId, CancellationToken ct)
    {
        if (t.Status != TournamentStatuses.InProgress)
        {
            return;
        }

        var p = t.Participants.FirstOrDefault(x => x.UserId == userId);
        if (p is null || p.IsConnected || p.DisconnectedAt is null)
        {
            return;
        }

        if ((_clock.UtcNow - p.DisconnectedAt.Value).TotalSeconds < DisconnectGraceSeconds)
        {
            return;
        }

        var match = await _db.Set<TournamentMatch>()
            .Include(m => m.Questions)
            .Include(m => m.Answers)
            .FirstOrDefaultAsync(m =>
                m.TournamentId == t.Id
                && m.Status == TournamentMatchStatuses.Active
                && (m.Player1UserId == userId || m.Player2UserId == userId), ct);
        if (match is null)
        {
            return;
        }

        var winner = match.Player1UserId == userId ? match.Player2UserId : match.Player1UserId;
        if (winner is not int w)
        {
            return;
        }

        await CompleteMatchAsync(match, t, w, TournamentMatchFinishReasons.Walkover, awardXp: false, ct);
    }

    private async Task MaybeAdvanceRoundAsync(int tournamentId, CancellationToken ct)
    {
        var t = await LoadTournamentAsync(tournamentId, ct, tracking: true);
        if (t.Status != TournamentStatuses.InProgress)
        {
            return;
        }

        var round = t.Rounds.FirstOrDefault(r => r.RoundNumber == t.CurrentRoundNumber);
        if (round is null)
        {
            return;
        }

        var matches = await _db.Set<TournamentMatch>()
            .Where(m => m.RoundId == round.Id)
            .ToListAsync(ct);
        if (matches.Count == 0 || matches.Any(m => m.Status != TournamentMatchStatuses.Finished))
        {
            return;
        }

        round.Status = TournamentMatchStatuses.Finished;
        round.FinishedAt = _clock.UtcNow;

        var nextRound = t.Rounds.FirstOrDefault(r => r.RoundNumber == t.CurrentRoundNumber + 1);
        if (nextRound is null)
        {
            // Champion
            var final = matches.OrderBy(m => m.SlotIndex).First();
            if (final.WinnerUserId is int champion)
            {
                await DeclareChampionAsync(t, champion, final, ct);
            }

            return;
        }

        // Feed winners into next round matches
        var orderedWinners = matches.OrderBy(m => m.SlotIndex).Select(m => m.WinnerUserId).ToList();
        var nextMatches = await _db.Set<TournamentMatch>()
            .Where(m => m.RoundId == nextRound.Id)
            .OrderBy(m => m.SlotIndex)
            .ToListAsync(ct);

        for (var i = 0; i < nextMatches.Count; i++)
        {
            var m = nextMatches[i];
            var w1 = orderedWinners.ElementAtOrDefault(i * 2);
            var w2 = orderedWinners.ElementAtOrDefault(i * 2 + 1);
            m.Player1UserId = w1;
            m.Player2UserId = w2;
            m.Player1IsBye = w1 is null;
            m.Player2IsBye = w2 is null;
        }

        t.CurrentRoundNumber = nextRound.RoundNumber;
        nextRound.Status = TournamentMatchStatuses.Active;
        nextRound.StartedAt = _clock.UtcNow;
        t.UpdatedAt = _clock.UtcNow;
        t.RowVersion++;
        await _db.SaveChangesAsync(ct);

        foreach (var m in nextMatches)
        {
            if (m.Player1IsBye && m.Player2UserId is int only2)
            {
                await FinishMatchAsByeAsync(m, only2, ct);
            }
            else if (m.Player2IsBye && m.Player1UserId is int only1)
            {
                await FinishMatchAsByeAsync(m, only1, ct);
            }
            else if (m.Player1UserId is int && m.Player2UserId is int)
            {
                await ActivateMatchAsync(t, m, ct);
            }
        }

        await _db.SaveChangesAsync(ct);
        await _broadcast.RoundStarted(t.Id, nextRound.RoundNumber, nextRound.Name, ct);

        var active = await _db.Set<TournamentMatch>()
            .Where(m => m.RoundId == nextRound.Id && m.Status == TournamentMatchStatuses.Active)
            .ToListAsync(ct);
        foreach (var m in active)
        {
            if (m.Player1UserId is int u1)
            {
                await _broadcast.MatchAssigned(t.Id, u1, await BuildMatchViewAsync(m.Id, u1, ct), ct);
            }

            if (m.Player2UserId is int u2)
            {
                await _broadcast.MatchAssigned(t.Id, u2, await BuildMatchViewAsync(m.Id, u2, ct), ct);
            }
        }

        // In case next round was all BYEs
        await MaybeAdvanceRoundAsync(t.Id, ct);
    }

    private async Task<TournamentMatch> LoadMatchAsync(int matchId, CancellationToken ct, bool tracking = false)
    {
        var query = tracking
            ? _db.Set<TournamentMatch>().AsQueryable()
            : _db.Set<TournamentMatch>().AsNoTracking();

        return await query
            .Include(m => m.Questions)
            .Include(m => m.Answers)
            .Include(m => m.Round)
            .FirstOrDefaultAsync(m => m.Id == matchId, ct)
            ?? throw new DomainException("Enfrentamiento no encontrado.", 404, "match_not_found");
    }

    private async Task<TournamentMatchViewDto> BuildMatchViewAsync(int matchId, int actorUserId, CancellationToken ct)
    {
        var match = await LoadMatchAsync(matchId, ct);
        var tournament = await _db.Set<Tournament>().AsNoTracking().FirstAsync(t => t.Id == match.TournamentId, ct);
        var names = await _db.Set<TournamentParticipant>().AsNoTracking()
            .Where(p => p.TournamentId == tournament.Id)
            .ToDictionaryAsync(p => p.UserId, p => p.DisplayNameSnapshot, ct);

        var currentQ = match.Questions.FirstOrDefault(q => q.Ordinal == match.CurrentQuestionIndex);
        PlayQuestionDto? questionDto = null;
        if (currentQ is not null && match.Status == TournamentMatchStatuses.Active)
        {
            var loaded = await _play.LoadQuestionsAsync([currentQ.QuestionId], ct);
            if (loaded.TryGetValue(currentQ.QuestionId, out var q))
            {
                questionDto = PlayService.ToDto(q, match.Id);
                // Strip correctness: ToDto already doesn't include correct flag on options
            }
        }

        int? secondsLeft = null;
        if (match.QuestionStartedAt is DateTime qs && match.Status == TournamentMatchStatuses.Active)
        {
            var elapsed = (_clock.UtcNow - qs).TotalSeconds;
            secondsLeft = Math.Max(0, (int)Math.Ceiling(tournament.SecondsPerQuestion - elapsed));
        }

        var myAnswers = match.Answers
            .Where(a => a.UserId == actorUserId)
            .Select(a =>
            {
                var mq = match.Questions.First(q => q.Id == a.MatchQuestionId);
                return new AnsweredQuestionDto(
                    mq.QuestionId,
                    a.SelectedOptionId,
                    a.IsCorrect,
                    match.Status == TournamentMatchStatuses.Finished ? mq.CorrectOptionId : null,
                    null);
            })
            .ToList();

        var amI = match.Player1UserId == actorUserId || match.Player2UserId == actorUserId;
        return new TournamentMatchViewDto(
            match.Id,
            tournament.Id,
            tournament.Name,
            match.Round?.RoundNumber ?? 0,
            match.Round?.Name ?? "",
            match.Status,
            match.Player1UserId,
            match.Player1UserId is int p1 ? names.GetValueOrDefault(p1, "Jugador") : "BYE",
            match.Player2UserId,
            match.Player2UserId is int p2 ? names.GetValueOrDefault(p2, "Jugador") : "BYE",
            match.Player1Correct,
            match.Player2Correct,
            match.Player1TimeMs,
            match.Player2TimeMs,
            match.CurrentQuestionIndex,
            currentQ?.Id,
            match.Questions.Count(q => !q.IsSuddenDeath),
            tournament.SecondsPerQuestion,
            match.QuestionStartedAt,
            secondsLeft,
            questionDto,
            amI,
            false,
            match.WinnerUserId,
            match.FinishReason,
            myAnswers);
    }
}
