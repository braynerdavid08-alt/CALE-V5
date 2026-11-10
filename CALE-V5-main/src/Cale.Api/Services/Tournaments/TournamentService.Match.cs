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
        try
        {
            await SaveChangesResilientAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Concurrent duplicate answer for (MatchQuestionId, UserId).
            ClearTrackedState();
            throw new DomainException("Ya respondiste esta pregunta.", 400, "already_answered");
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || IsSqliteBusy(ex))
        {
            // Opponent answer / timeout bumped RowVersion, or SQLite was briefly busy.
            // If our answer landed (or a duplicate), continue; otherwise ask the client to retry.
            ClearTrackedState();
            match = await LoadMatchAsync(matchId, ct, tracking: true);
            if (!match.Answers.Any(a => a.MatchQuestionId == question.Id && a.UserId == userId))
            {
                throw new DomainException("El enfrentamiento cambió. Intenta de nuevo.", 409, "conflict");
            }

            tournament = await _db.Set<Tournament>().FirstAsync(t => t.Id == match.TournamentId, ct);
            await TryAdvanceQuestionOrFinishAsync(match, tournament, ct);
            var racedState = await BuildMatchViewAsync(match.Id, userId, ct);
            await _broadcast.MatchUpdated(tournament.Id, racedState, ct);
            return new TournamentAnswerResultDto(
                correct,
                match.Status == TournamentMatchStatuses.Finished ? question.CorrectOptionId : null,
                null,
                racedState,
                []);
        }

        // Reload answers so a concurrent opponent answer is visible before advancing.
        match = await LoadMatchAsync(matchId, ct, tracking: true);
        tournament = await _db.Set<Tournament>().FirstAsync(t => t.Id == match.TournamentId, ct);
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
            try
            {
                await SaveChangesResilientAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Concurrent opponent answer already advanced the question pointer — idempotent.
                ClearTrackedState();
            }
            catch (DbUpdateException ex) when (IsSqliteBusy(ex))
            {
                ClearTrackedState();
            }

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
            excludeIds: existingIds,
            bankIds: ParseIds(tournament.BankIdsJson));

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
        try
        {
            await SaveChangesResilientAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Concurrent resolver already moved the match (finish or sudden death).
            ClearTrackedState();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // UNIQUE (MatchId, Ordinal) — the other request already seeded sudden death.
            ClearTrackedState();
        }
        catch (DbUpdateException ex) when (IsSqliteBusy(ex))
        {
            ClearTrackedState();
        }
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

        try
        {
            await SaveChangesResilientAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request already finished this match (concurrent last answers).
            ClearTrackedState();
            var latest = await LoadMatchAsync(match.Id, ct, tracking: true);
            if (latest.Status == TournamentMatchStatuses.Finished)
            {
                await MaybeAdvanceRoundAsync(latest.TournamentId, ct);
                return;
            }

            // Lost RowVersion to a non-finish update — never leak as HTTP 500 db_error.
            throw new DomainException("El enfrentamiento cambió. Intenta de nuevo.", 409, "conflict");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) || IsSqliteBusy(ex))
        {
            // Unique/busy during finish — reload; if already finished, treat as idempotent.
            ClearTrackedState();
            var latest = await LoadMatchAsync(match.Id, ct, tracking: true);
            if (latest.Status == TournamentMatchStatuses.Finished)
            {
                await MaybeAdvanceRoundAsync(latest.TournamentId, ct);
                return;
            }

            throw new DomainException("El enfrentamiento cambió. Intenta de nuevo.", 409, "conflict");
        }

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
        // Hybrid recovery: wake tournaments stuck with Pending seats after a crash mid-activation.
        await RecoverStuckPendingRoundsAsync(ct);

        var now = _clock.UtcNow;
        var active = await _db.Set<TournamentMatch>()
            .Include(m => m.Questions)
            .Include(m => m.Answers)
            .Where(m => m.Status == TournamentMatchStatuses.Active && m.QuestionStartedAt != null)
            .OrderBy(m => m.Id)
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
            .OrderBy(p => p.Id)
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

        var opponentId = match.Player1UserId == userId ? match.Player2UserId : match.Player1UserId;
        if (opponentId is not int w)
        {
            return;
        }

        // If both players exceeded the grace window, award the one who disconnected later
        // (stayed longer). Avoids order-dependent results when the timeout worker scans both.
        var opponent = t.Participants.FirstOrDefault(x => x.UserId == w);
        if (opponent is { IsConnected: false, DisconnectedAt: DateTime oppAt }
            && (_clock.UtcNow - oppAt).TotalSeconds >= DisconnectGraceSeconds
            && p.DisconnectedAt is DateTime selfAt)
        {
            w = oppAt >= selfAt ? opponent.UserId : userId;
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
            // Crash after round insert can leave CurrentRoundNumber on a Pending field.
            // Concurrent finishers that already see the new round must recover activation on demand.
            if (matches.Any(m => m.Status == TournamentMatchStatuses.Pending))
            {
                await EnsureRoundActivatedAsync(t, t.CurrentRoundNumber, ct);
            }

            return;
        }

        var nextRoundNumber = t.CurrentRoundNumber + 1;
        // Another concurrent finisher may already have persisted the next round — recover activation if stuck.
        if (t.Rounds.Any(r => r.RoundNumber == nextRoundNumber)
            || await _db.Set<TournamentRound>().AsNoTracking()
                .AnyAsync(r => r.TournamentId == tournamentId && r.RoundNumber == nextRoundNumber, ct))
        {
            ClearTrackedState();
            await EnsureRoundActivatedByNumberAsync(tournamentId, nextRoundNumber, ct);
            return;
        }

        round.Status = TournamentMatchStatuses.Finished;
        round.FinishedAt = _clock.UtcNow;

        var winners = matches
            .OrderBy(m => m.SlotIndex)
            .Select(m => m.WinnerUserId)
            .Where(id => id is int)
            .Select(id => id!.Value)
            .ToList();

        if (winners.Count <= 1)
        {
            if (winners.Count == 1)
            {
                var final = matches.OrderBy(m => m.SlotIndex).First(m => m.WinnerUserId == winners[0]);
                await DeclareChampionAsync(t, winners[0], final, ct);
            }

            return;
        }

        // Build the next round from the living field (max pairs + at most one BYE).
        var pairs = TournamentBracket.PairRound(winners, Random.Shared);
        var nextRound = new TournamentRound
        {
            TournamentId = t.Id,
            RoundNumber = nextRoundNumber,
            Name = TournamentBracket.RoundNameForField(winners.Count),
            Status = TournamentMatchStatuses.Active,
            StartedAt = _clock.UtcNow
        };
        t.Rounds.Add(nextRound);

        for (var i = 0; i < pairs.Count; i++)
        {
            var (p1, p2, p1Bye, p2Bye) = pairs[i];
            nextRound.Matches.Add(new TournamentMatch
            {
                TournamentId = t.Id,
                SlotIndex = i,
                Status = TournamentMatchStatuses.Pending,
                Player1UserId = p1,
                Player2UserId = p2,
                Player1IsBye = p1Bye,
                Player2IsBye = p2Bye
            });
        }

        t.CurrentRoundNumber = nextRound.RoundNumber;
        t.UpdatedAt = _clock.UtcNow;
        t.RowVersion++;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request advanced CurrentRoundNumber / RowVersion first — recover their round if needed.
            ClearTrackedState();
            _logger.LogInformation(
                "Tournament {TournamentId} round advance lost RowVersion race; treating as idempotent.",
                tournamentId);
            await EnsureRoundActivatedByNumberAsync(tournamentId, nextRoundNumber, ct);
            return;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // UNIQUE (TournamentId, RoundNumber) — the other request already created the round.
            ClearTrackedState();
            _logger.LogInformation(
                "Tournament {TournamentId} round {RoundNumber} already created by concurrent request.",
                tournamentId,
                nextRoundNumber);
            await EnsureRoundActivatedByNumberAsync(tournamentId, nextRoundNumber, ct);
            return;
        }

        var activated = await ActivateRoundMatchesAsync(
            t,
            nextRound.Matches.OrderBy(m => m.SlotIndex).ToList(),
            ct);
        await _db.SaveChangesAsync(ct);
        await _broadcast.RoundStarted(t.Id, nextRound.RoundNumber, nextRound.Name, ct);
        await NotifyMatchAssignmentsAsync(t.Id, activated, ct);

        // Pure-BYE rounds (should be rare) collapse immediately.
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
        var photos = await PhotoUrlsAsync(
            new[] { match.Player1UserId, match.Player2UserId }
                .Where(id => id is int)
                .Select(id => id!.Value),
            ct);

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
            myAnswers,
            match.Player1UserId is int a ? photos.GetValueOrDefault(a) : null,
            match.Player2UserId is int b ? photos.GetValueOrDefault(b) : null);
    }
}
