using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.GameShow.Application.DTOs;
using Cale.Modules.GameShow.Domain;

namespace Cale.Modules.GameShow.Application;

public sealed partial class GameShowHandler
{
    public async Task<CreateGameShowRequest> WithOfficialAliasesAsync(
        CreateGameShowRequest body,
        CancellationToken ct)
    {
        var row = await _store.GetSettingsRowAsync(GameShowSettings.OfficialAliasesId, ct);
        return GameShowOfficialAliases.Merge(body, row?.PayloadJson);
    }

    public async Task<GameShowHostReviewDto> GetHostReviewAsync(
        int sessionId,
        int hostUserId,
        bool isAdmin,
        CreateGameShowRequest? officialPack,
        CancellationToken ct)
    {
        var session = await RequireHostWithAttemptsAsync(sessionId, hostUserId, ct);
        var pack = session.SourcePackId is int packId ? await _store.GetPackByIdAsync(packId, ct) : null;
        var canEditPack = pack is not null && (pack.OwnerUserId == hostUserId || isAdmin);
        var names = session.Players.ToDictionary(p => p.Id, p => p.DisplayName);
        var running = session.Status == GameShowSessionStatuses.Running;

        var rounds = session.Rounds
            .OrderBy(r => r.SortOrder)
            .Select((r, index) =>
            {
                var isCurrent = running && index == session.CurrentRoundIndex;
                return new GameShowReviewRoundDto(
                    r.Id,
                    r.SortOrder,
                    r.QuestionText,
                    r.Phase,
                    isCurrent,
                    GameShowOfficialAliases.ContainsQuestion(officialPack, r.QuestionText),
                    r.Answers
                        .OrderBy(a => a.Rank)
                        .Select(a => new GameShowReviewAnswerDto(
                            a.Id,
                            a.Rank,
                            a.Text,
                            a.Points,
                            a.IsRevealed,
                            GameShowAnswerMatcher.ParseAliases(a.AliasesJson)))
                        .ToList(),
                    r.Attempts
                        .OrderByDescending(a => a.CreatedAt)
                        .ThenByDescending(a => a.Id)
                        .Select(a =>
                        {
                            var mode = GameShowReviewEngine.GetAcceptMode(r, a, isCurrent);
                            return new GameShowReviewAttemptDto(
                                a.Id,
                                a.PlayerId,
                                a.PlayerId is int pid && names.TryGetValue(pid, out var n) ? n : null,
                                a.Team,
                                a.RawText,
                                a.IsCorrect,
                                a.IsSteal,
                                a.MatchedAnswerId,
                                a.CreatedAt,
                                mode == GameShowReviewEngine.AcceptMode.None ? null : mode.ToString(),
                                GameShowReviewEngine.CanReject(r, a, isCurrent));
                        })
                        .ToList());
            })
            .ToList();

        return new GameShowHostReviewDto(session.Id, session.SourcePackId, canEditPack, isAdmin, rounds);
    }

    public async Task AcceptAttemptAsync(
        int sessionId,
        int hostUserId,
        int attemptId,
        int answerId,
        CancellationToken ct)
    {
        var session = await RequireHostWithAttemptsAsync(sessionId, hostUserId, ct);
        EnsureRunning(session);
        var round = CurrentRound(session);
        var attempt = round.Attempts.FirstOrDefault(a => a.Id == attemptId)
            ?? throw new DomainException("Esa respuesta ya no pertenece a la ronda actual.", 400, "attempt_not_current");
        var answer = round.Answers.FirstOrDefault(a => a.Id == answerId)
            ?? throw new NotFoundException("Respuesta no encontrada.", "answer_not_found");

        GameShowEngine.Outcome outcome;
        try
        {
            outcome = GameShowReviewEngine.Accept(session, round, attempt, answer, _clock.UtcNow);
        }
        catch (InvalidOperationException ex) when (ex.Message == "answer_revealed")
        {
            throw new DomainException("Esa respuesta del tablero ya está descubierta. Elige otra.", 400, "answer_revealed");
        }
        catch (InvalidOperationException)
        {
            throw new DomainException("Ya no se puede corregir esa respuesta: el juego avanzó.", 400, "cannot_accept");
        }

        await _store.SaveChangesAsync(ct);
        await BroadcastLobbyAsync(session, ct);
        await BroadcastAttemptAsync(session, round, attempt, corrected: true, repeated: false, ct);
        await _broadcaster.EventAsync(session.Id, outcome.EventName, outcome.Payload, ct);
        if (GameShowRoundPhases.IsControl(round.Phase))
        {
            await BroadcastYourTurnAsync(session, round, ct);
        }
    }

    public async Task RejectAttemptAsync(
        int sessionId,
        int hostUserId,
        int attemptId,
        CancellationToken ct)
    {
        var session = await RequireHostWithAttemptsAsync(sessionId, hostUserId, ct);
        EnsureRunning(session);
        var round = CurrentRound(session);
        var attempt = round.Attempts.FirstOrDefault(a => a.Id == attemptId)
            ?? throw new DomainException("Esa respuesta ya no pertenece a la ronda actual.", 400, "attempt_not_current");

        GameShowEngine.Outcome outcome;
        try
        {
            outcome = GameShowReviewEngine.Reject(session, round, attempt, _clock.UtcNow);
        }
        catch (InvalidOperationException)
        {
            throw new DomainException("Ya no se puede corregir esa respuesta: el juego avanzó.", 400, "cannot_reject");
        }

        await _store.SaveChangesAsync(ct);
        await BroadcastLobbyAsync(session, ct);
        await BroadcastAttemptAsync(session, round, attempt, corrected: true, repeated: false, ct);
        await _broadcaster.EventAsync(session.Id, outcome.EventName, outcome.Payload, ct);
        await BroadcastYourTurnAsync(session, round, ct);
    }

    /// <summary>Lets the projector and phones show what a student typed and the verdict.</summary>
    private async Task BroadcastAttemptAsync(
        GameShowSession session,
        GameShowRound round,
        GameShowAttempt? attempt,
        bool corrected,
        bool repeated,
        CancellationToken ct)
    {
        if (attempt is null)
        {
            return;
        }

        var player = session.Players.FirstOrDefault(p => p.Id == attempt.PlayerId);
        var matched = round.Answers.FirstOrDefault(a => a.Id == attempt.MatchedAnswerId);
        await _broadcaster.EventAsync(
            session.Id,
            "AttemptSubmitted",
            new
            {
                attemptId = attempt.Id,
                roundId = round.Id,
                playerId = attempt.PlayerId,
                playerName = player?.DisplayName,
                accentColor = attempt.PlayerId is int pid ? AccentFor(pid) : null,
                team = attempt.Team,
                text = attempt.RawText,
                isCorrect = attempt.IsCorrect,
                isSteal = attempt.IsSteal,
                repeated,
                corrected,
                matchedText = matched?.Text
            },
            ct);
    }

    public async Task<AddGameShowAliasResultDto> AddAliasAsync(
        int sessionId,
        int hostUserId,
        bool isAdmin,
        AddGameShowAliasRequest request,
        CreateGameShowRequest? officialPack,
        CancellationToken ct)
    {
        var session = await RequireHostAsync(sessionId, hostUserId, ct);
        var round = session.Rounds.FirstOrDefault(r => r.Id == request.RoundId)
            ?? throw new NotFoundException("Ronda no encontrada.", "round_not_found");
        var answer = round.Answers.FirstOrDefault(a => a.Id == request.AnswerId)
            ?? throw new NotFoundException("Respuesta no encontrada.", "answer_not_found");

        var alias = (request.Alias ?? "").Trim();
        if (alias.Length > GameShowAnswerMatcher.MaxAliasLength)
        {
            alias = alias[..GameShowAnswerMatcher.MaxAliasLength].Trim();
        }

        var aliasKey = GameShowAnswerMatcher.Normalize(alias);
        if (aliasKey.Length == 0)
        {
            throw new DomainException("Escribe el sinónimo.", 400, "invalid_alias");
        }

        if (aliasKey == GameShowAnswerMatcher.Normalize(answer.Text))
        {
            throw new DomainException("Ese texto ya es la respuesta del tablero.", 400, "alias_is_answer");
        }

        var current = GameShowAnswerMatcher.ParseAliases(answer.AliasesJson);
        if (!current.Any(a => GameShowAnswerMatcher.Normalize(a) == aliasKey))
        {
            if (current.Count >= GameShowAnswerMatcher.MaxAliases)
            {
                throw new DomainException(
                    $"Esta respuesta ya tiene {GameShowAnswerMatcher.MaxAliases} sinónimos.",
                    400,
                    "too_many_aliases");
            }

            answer.AliasesJson = GameShowAnswerMatcher.SerializeAliases(current.Append(alias));
        }

        var now = _clock.UtcNow;
        var result = new AddGameShowAliasResultDto(
            "session",
            $"«{alias}» ya cuenta como «{answer.Text}» en esta partida.");

        var pack = session.SourcePackId is int packId ? await _store.GetPackByIdAsync(packId, ct) : null;
        if (pack is not null && (pack.OwnerUserId == hostUserId || isAdmin))
        {
            var updated = GameShowOfficialAliases.AddToBody(DeserializePackBody(pack), round.QuestionText, answer.Text, alias);
            if (updated is not null)
            {
                pack.PayloadJson = SerializePackBody(updated);
                pack.UpdatedAt = now;
                result = new AddGameShowAliasResultDto(
                    "pack",
                    $"«{alias}» se guardó como sinónimo de «{answer.Text}» en tu pack «{pack.Name}».");
            }
        }

        if (result.SavedTo == "session" && GameShowOfficialAliases.ContainsQuestion(officialPack, round.QuestionText))
        {
            if (isAdmin)
            {
                var row = await _store.GetSettingsRowAsync(GameShowSettings.OfficialAliasesId, ct);
                if (row is null)
                {
                    row = new GameShowSettings { Id = GameShowSettings.OfficialAliasesId, PayloadJson = "{}" };
                    await _store.AddSettingsRowAsync(row, ct);
                }

                var map = GameShowOfficialAliases.Parse(row.PayloadJson);
                GameShowOfficialAliases.Add(map, round.QuestionText, answer.Text, alias);
                row.PayloadJson = GameShowOfficialAliases.Serialize(map);
                row.UpdatedAt = now;
                row.UpdatedByUserId = hostUserId;
                result = new AddGameShowAliasResultDto(
                    "official",
                    $"«{alias}» se agregó al paquete oficial como sinónimo de «{answer.Text}».");
            }
            else
            {
                result = result with
                {
                    Message = result.Message + " Solo el administrador puede agregarlo al paquete oficial."
                };
            }
        }

        await _store.SaveChangesAsync(ct);
        return result;
    }

    private async Task<GameShowSession> RequireHostWithAttemptsAsync(
        int sessionId,
        int hostUserId,
        CancellationToken ct)
    {
        var session = await _store.GetByIdWithAttemptsAsync(sessionId, ct)
            ?? throw new NotFoundException("Partida no encontrada.", "game_not_found");
        if (session.HostUserId != hostUserId)
        {
            throw new DomainException("Solo el anfitrión puede controlar la partida.", 403, "forbidden");
        }

        return session;
    }
}
