using System.Security.Cryptography;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.GameShow.Application.Abstractions;
using Cale.Modules.GameShow.Application.DTOs;
using Cale.Modules.GameShow.Domain;

namespace Cale.Modules.GameShow.Application;

public sealed class GameShowHandler
{
    private static readonly char[] CodeAlphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    private readonly IGameShowStore _store;
    private readonly IGameShowBroadcaster _broadcaster;
    private readonly IClock _clock;

    public GameShowHandler(
        IGameShowStore store,
        IGameShowBroadcaster broadcaster,
        IClock clock)
    {
        _store = store;
        _broadcaster = broadcaster;
        _clock = clock;
    }

    public async Task<GameShowLobbyDto> CreateAsync(
        int hostUserId,
        int? schoolUserId,
        CreateGameShowRequest request,
        CancellationToken ct)
    {
        if (request.Rounds is null || request.Rounds.Count < 1)
        {
            throw new DomainException("Agrega al menos una ronda.", 400, "invalid_rounds");
        }

        if (request.Rounds.Count > 50)
        {
            throw new DomainException("Máximo 50 rondas por partida.", 400, "too_many_rounds");
        }

        var session = new GameShowSession
        {
            HostUserId = hostUserId,
            SchoolUserId = schoolUserId,
            Title = string.IsNullOrWhiteSpace(request.Title)
                ? "100 Estudiantes Dijeron"
                : request.Title.Trim(),
            JoinCode = await GenerateUniqueCodeAsync(ct),
            Status = GameShowSessionStatuses.Lobby,
            TeamAName = string.IsNullOrWhiteSpace(request.TeamAName) ? "Equipo A" : request.TeamAName.Trim(),
            TeamBName = string.IsNullOrWhiteSpace(request.TeamBName) ? "Equipo B" : request.TeamBName.Trim(),
            CreatedAt = _clock.UtcNow
        };

        var order = 0;
        foreach (var roundReq in request.Rounds)
        {
            var round = BuildRound(roundReq, order++);
            session.Rounds.Add(round);
        }

        await _store.AddAsync(session, ct);
        await _store.SaveChangesAsync(ct);
        return MapLobby(session, hostView: true);
    }

    public async Task<JoinGameShowResultDto> JoinAsync(
        JoinGameShowRequest request,
        int? userId,
        CancellationToken ct)
    {
        var code = (request.Code ?? "").Trim().ToUpperInvariant();
        if (code.Length < 4)
        {
            throw new DomainException("Código inválido.", 400, "invalid_code");
        }

        var name = (request.DisplayName ?? "").Trim();
        if (name.Length < 2)
        {
            throw new DomainException("Escribe tu nombre.", 400, "invalid_name");
        }

        var session = await _store.GetByJoinCodeAsync(code, ct)
            ?? throw new NotFoundException("Partida no encontrada.", "game_not_found");

        if (session.Status == GameShowSessionStatuses.Ended)
        {
            throw new DomainException("Esta partida ya terminó.", 400, "game_ended");
        }

        // Same logged-in user rejoining keeps their seat/token.
        if (userId is not null)
        {
            var existing = session.Players.FirstOrDefault(p => p.UserId == userId);
            if (existing is not null)
            {
                var rejoinLobby = MapLobby(
                    session,
                    hostView: false,
                    viewerPlayerId: existing.Id,
                    viewerTeam: existing.Team);
                await BroadcastLobbyAsync(session, ct);
                return new JoinGameShowResultDto(
                    session.Id,
                    existing.PlayerToken,
                    existing.Id,
                    existing.Team,
                    rejoinLobby);
            }
        }

        var team = string.IsNullOrWhiteSpace(request.Team)
            || string.Equals(request.Team, "auto", StringComparison.OrdinalIgnoreCase)
            ? PickBalancedTeam(session)
            : NormalizeTeam(request.Team);

        var player = new GameShowPlayer
        {
            SessionId = session.Id,
            UserId = userId,
            DisplayName = name[..Math.Min(name.Length, 80)],
            Team = team,
            PlayerToken = Guid.NewGuid(),
            IsConnected = false,
            JoinedAt = _clock.UtcNow
        };
        session.Players.Add(player);
        await _store.SaveChangesAsync(ct);

        var lobby = MapLobby(session, hostView: false, viewerPlayerId: player.Id, viewerTeam: player.Team);
        await _broadcaster.LobbyUpdatedAsync(session.Id, MapLobby(session, hostView: false), ct);
        return new JoinGameShowResultDto(session.Id, player.PlayerToken, player.Id, player.Team, lobby);
    }

    public async Task<GameShowLobbyDto> GetLobbyAsync(
        int sessionId,
        int? hostUserId,
        Guid? playerToken,
        CancellationToken ct,
        bool preferHostView = false)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        var isHost = hostUserId is int hid && hid == session.HostUserId;
        GameShowPlayer? viewer = null;
        if (!isHost && playerToken is Guid token)
        {
            viewer = session.Players.FirstOrDefault(p => p.PlayerToken == token);
            if (viewer is null)
            {
                throw new DomainException("No perteneces a esta partida.", 403, "forbidden");
            }
        }
        else if (!isHost)
        {
            // Authenticated viewers (proyector / school) may watch public board.
            // Anonymous screen clients also allowed when they know the session id.
        }

        return MapLobby(
            session,
            hostView: isHost && preferHostView,
            viewerPlayerId: viewer?.Id,
            viewerTeam: viewer?.Team);
    }

    public async Task SetConnectionAsync(
        Guid playerToken,
        string? connectionId,
        bool connected,
        CancellationToken ct)
    {
        var player = await _store.GetPlayerByTokenAsync(playerToken, ct)
            ?? throw new NotFoundException("Jugador no encontrado.", "player_not_found");
        player.ConnectionId = connected ? connectionId : null;
        player.IsConnected = connected;
        // Persist presence even if the SignalR caller disconnects mid-flight.
        await _store.SaveChangesAsync(CancellationToken.None);

        if (ct.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var session = await RequireSessionAsync(player.SessionId, ct);
            await _broadcaster.LobbyUpdatedAsync(session.Id, MapLobby(session, false), ct);
        }
        catch (OperationCanceledException)
        {
            // Client left while loading lobby — presence already saved.
        }
    }

    public async Task AssignPlayerAsync(
        int sessionId,
        int hostUserId,
        AssignPlayerRequest request,
        CancellationToken ct)
    {
        var session = await RequireHostAsync(sessionId, hostUserId, ct);
        var player = session.Players.FirstOrDefault(p => p.Id == request.PlayerId)
            ?? throw new NotFoundException("Jugador no encontrado.", "player_not_found");
        player.Team = NormalizeTeam(request.Team);
        await _store.SaveChangesAsync(ct);
        await BroadcastLobbyAsync(session, ct);
    }

    public async Task StartAsync(int sessionId, int hostUserId, CancellationToken ct)
    {
        var session = await RequireHostAsync(sessionId, hostUserId, ct);
        if (session.Rounds.Count == 0)
        {
            throw new DomainException("La partida no tiene rondas.", 400, "invalid_rounds");
        }

        session.Status = GameShowSessionStatuses.Running;
        session.StartedAt ??= _clock.UtcNow;
        session.CurrentRoundIndex = 0;
        GameShowEngine.OpenBuzz(session.Rounds.OrderBy(r => r.SortOrder).First());
        await _store.SaveChangesAsync(ct);
        await BroadcastLobbyAsync(session, ct);
        await _broadcaster.EventAsync(session.Id, "RoundStarted", new { roundIndex = 0 }, ct);
    }

    public async Task PauseAsync(int sessionId, int hostUserId, CancellationToken ct)
    {
        var session = await RequireHostAsync(sessionId, hostUserId, ct);
        if (session.Status != GameShowSessionStatuses.Running)
        {
            throw new DomainException("La partida no está en curso.", 400, "invalid_state");
        }

        session.Status = GameShowSessionStatuses.Paused;
        await _store.SaveChangesAsync(ct);
        await BroadcastLobbyAsync(session, ct);
    }

    public async Task ResumeAsync(int sessionId, int hostUserId, CancellationToken ct)
    {
        var session = await RequireHostAsync(sessionId, hostUserId, ct);
        if (session.Status != GameShowSessionStatuses.Paused)
        {
            throw new DomainException("La partida no está pausada.", 400, "invalid_state");
        }

        session.Status = GameShowSessionStatuses.Running;
        await _store.SaveChangesAsync(ct);
        await BroadcastLobbyAsync(session, ct);
    }

    public async Task BuzzAsync(int sessionId, Guid playerToken, CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        EnsureRunning(session);
        var player = session.Players.FirstOrDefault(p => p.PlayerToken == playerToken)
            ?? throw new DomainException("Jugador no encontrado.", 404, "player_not_found");
        var round = CurrentRound(session);
        if (round.Phase != GameShowRoundPhases.WaitingBuzz)
        {
            throw new DomainException("El buzzer no está abierto.", 400, "buzz_closed");
        }

        var claimed = await _store.TryClaimBuzzAsync(round.Id, player.Team, ct);
        if (!claimed)
        {
            throw new DomainException("Otro equipo ya ganó el buzzer.", 409, "buzz_taken");
        }

        // Reload so in-memory graph matches the atomic claim.
        session = await RequireSessionAsync(sessionId, ct);
        round = CurrentRound(session);
        await BroadcastLobbyAsync(session, ct);
        await _broadcaster.EventAsync(
            session.Id,
            "BuzzWon",
            new { team = player.Team, playerId = player.Id, displayName = player.DisplayName },
            ct);
    }

    public async Task ForceBuzzWinnerAsync(
        int sessionId,
        int hostUserId,
        string team,
        CancellationToken ct)
    {
        var session = await RequireHostAsync(sessionId, hostUserId, ct);
        EnsureRunning(session);
        var round = CurrentRound(session);
        if (round.Phase != GameShowRoundPhases.WaitingBuzz)
        {
            throw new DomainException("El buzzer no está abierto.", 400, "buzz_closed");
        }

        var t = NormalizeTeam(team);
        var claimed = await _store.TryClaimBuzzAsync(round.Id, t, ct);
        if (!claimed)
        {
            throw new DomainException("Otro equipo ya ganó el buzzer.", 409, "buzz_taken");
        }

        session = await RequireHostAsync(sessionId, hostUserId, ct);
        await BroadcastLobbyAsync(session, ct);
        await _broadcaster.EventAsync(session.Id, "BuzzWon", new { team = t, forced = true }, ct);
    }

    public async Task AnswerAsync(
        int sessionId,
        Guid playerToken,
        AnswerGameShowRequest request,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        EnsureRunning(session);
        var player = session.Players.FirstOrDefault(p => p.PlayerToken == playerToken)
            ?? throw new DomainException("Jugador no encontrado.", 404, "player_not_found");
        var round = CurrentRound(session);

        try
        {
            var outcome = GameShowEngine.ProcessAnswer(
                session,
                round,
                player,
                request.Text,
                _clock.UtcNow);
            await _store.SaveChangesAsync(ct);
            await BroadcastLobbyAsync(session, ct);
            await _broadcaster.EventAsync(session.Id, outcome.EventName, outcome.Payload, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == "invalid_phase")
        {
            throw new DomainException("No es momento de responder.", 400, "invalid_phase");
        }
        catch (InvalidOperationException ex) when (ex.Message == "not_your_turn")
        {
            throw new DomainException("No es el turno de tu equipo.", 403, "not_your_turn");
        }
        catch (InvalidOperationException ex) when (ex.Message == "invalid_state")
        {
            throw new DomainException("Estado de ronda inválido.", 400, "invalid_state");
        }
    }

    public async Task HostRevealAsync(
        int sessionId,
        int hostUserId,
        int answerId,
        CancellationToken ct)
    {
        var session = await RequireHostAsync(sessionId, hostUserId, ct);
        EnsureRunning(session);
        var round = CurrentRound(session);
        var answer = round.Answers.FirstOrDefault(a => a.Id == answerId)
            ?? throw new NotFoundException("Respuesta no encontrada.", "answer_not_found");

        var outcome = GameShowEngine.HostReveal(session, round, answer, _clock.UtcNow);
        await _store.SaveChangesAsync(ct);
        await BroadcastLobbyAsync(session, ct);
        if (outcome is not null)
        {
            await _broadcaster.EventAsync(session.Id, outcome.EventName, outcome.Payload, ct);
        }
    }

    /// <summary>
    /// Host ends a silent steal: controlling team keeps banked points; round finishes.
    /// </summary>
    public async Task HostFailStealAsync(int sessionId, int hostUserId, CancellationToken ct)
    {
        var session = await RequireHostAsync(sessionId, hostUserId, ct);
        EnsureRunning(session);
        var round = CurrentRound(session);
        try
        {
            var outcome = GameShowEngine.HostFailSteal(session, round, _clock.UtcNow);
            await _store.SaveChangesAsync(ct);
            await BroadcastLobbyAsync(session, ct);
            await _broadcaster.EventAsync(session.Id, outcome.EventName, outcome.Payload, ct);
        }
        catch (InvalidOperationException)
        {
            throw new DomainException("No hay robo activo.", 400, "invalid_phase");
        }
    }

    /// <summary>
    /// Host force-finishes the current round keeping scores as-is.
    /// </summary>
    public async Task HostEndRoundAsync(int sessionId, int hostUserId, CancellationToken ct)
    {
        var session = await RequireHostAsync(sessionId, hostUserId, ct);
        EnsureRunning(session);
        var round = CurrentRound(session);
        var outcome = GameShowEngine.HostEndRound(session, round, _clock.UtcNow);
        await _store.SaveChangesAsync(ct);
        await BroadcastLobbyAsync(session, ct);
        await _broadcaster.EventAsync(session.Id, outcome.EventName, outcome.Payload, ct);
    }

    public async Task HostStrikeAsync(int sessionId, int hostUserId, CancellationToken ct)
    {
        var session = await RequireHostAsync(sessionId, hostUserId, ct);
        EnsureRunning(session);
        var round = CurrentRound(session);
        try
        {
            var outcome = GameShowEngine.HostStrike(round);
            await _store.SaveChangesAsync(ct);
            await BroadcastLobbyAsync(session, ct);
            await _broadcaster.EventAsync(session.Id, outcome.EventName, outcome.Payload, ct);
        }
        catch (InvalidOperationException)
        {
            throw new DomainException("No hay turno de control activo.", 400, "invalid_phase");
        }
    }

    public async Task NextRoundAsync(int sessionId, int hostUserId, CancellationToken ct)
    {
        var session = await RequireHostAsync(sessionId, hostUserId, ct);
        EnsureRunning(session);
        var rounds = session.Rounds.OrderBy(r => r.SortOrder).ToList();
        var next = session.CurrentRoundIndex + 1;
        if (next >= rounds.Count)
        {
            await FinishAsync(sessionId, hostUserId, ct);
            return;
        }

        session.CurrentRoundIndex = next;
        GameShowEngine.OpenBuzz(rounds[next]);
        await _store.SaveChangesAsync(ct);
        await BroadcastLobbyAsync(session, ct);
        await _broadcaster.EventAsync(session.Id, "RoundStarted", new { roundIndex = next }, ct);
    }

    public async Task FinishAsync(int sessionId, int hostUserId, CancellationToken ct)
    {
        var session = await RequireHostAsync(sessionId, hostUserId, ct);
        session.Status = GameShowSessionStatuses.Ended;
        session.EndedAt = _clock.UtcNow;
        await _store.SaveChangesAsync(ct);
        await BroadcastLobbyAsync(session, ct);
        await _broadcaster.EventAsync(
            session.Id,
            "GameEnded",
            new { teamAScore = session.TeamAScore, teamBScore = session.TeamBScore },
            ct);
    }

    public async Task<IReadOnlyList<GameShowHistoryItemDto>> ListHistoryAsync(
        int userId,
        string role,
        CancellationToken ct)
    {
        IReadOnlyList<GameShowSession> list = role switch
        {
            "School" => await _store.ListForSchoolAsync(userId, ct),
            _ => await _store.ListForHostAsync(userId, ct)
        };

        return list.Select(s => new GameShowHistoryItemDto(
            s.Id,
            s.Title,
            s.Status,
            s.TeamAName,
            s.TeamBName,
            s.TeamAScore,
            s.TeamBScore,
            s.CreatedAt,
            s.EndedAt,
            s.Players.Count)).ToList();
    }

    public async Task<(byte[] Bytes, string FileName, string ContentType)> ExportQuestionsAsync(
        int sessionId,
        int userId,
        bool isAdmin,
        string format,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        if (!isAdmin
            && session.HostUserId != userId
            && session.SchoolUserId != userId)
        {
            throw new DomainException("No puedes exportar esta partida.", 403, "forbidden");
        }

        var fmt = (format ?? "csv").Trim().ToLowerInvariant();
        var safe = string.Join("_", session.Title.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "partida";
        }

        if (fmt is "json")
        {
            var payload = new CreateGameShowRequest(
                session.Title,
                session.TeamAName,
                session.TeamBName,
                session.Rounds
                    .OrderBy(r => r.SortOrder)
                    .Select(r => new CreateGameShowRoundRequest(
                        r.QuestionText,
                        r.SourceQuestionId,
                        r.Answers
                            .OrderBy(a => a.Rank)
                            .Select(a => new CreateGameShowAnswerRequest(
                                a.Text,
                                a.Points,
                                GameShowAnswerMatcher.ParseAliases(a.AliasesJson).ToList()))
                            .ToList()))
                    .ToList());

            var json = System.Text.Json.JsonSerializer.Serialize(
                payload,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
            var jsonBytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(json))
                .ToArray();
            return (jsonBytes, $"cale-100-dijeron-{session.Id}-{safe}.json", "application/json; charset=utf-8");
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ronda,pregunta,rank,respuesta,puntos,aliases");
        foreach (var round in session.Rounds.OrderBy(r => r.SortOrder))
        {
            foreach (var answer in round.Answers.OrderBy(a => a.Rank))
            {
                var aliases = string.Join(" | ", GameShowAnswerMatcher.ParseAliases(answer.AliasesJson));
                sb.AppendLine(
                    $"{round.SortOrder + 1},{Csv(round.QuestionText)},{answer.Rank},{Csv(answer.Text)},{answer.Points},{Csv(aliases)}");
            }
        }

        var csvBytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();
        return (csvBytes, $"cale-100-dijeron-{session.Id}-{safe}.csv", "text/csv; charset=utf-8");
    }

    public async Task<IReadOnlyList<GameShowPackSummaryDto>> ListPacksAsync(
        int ownerUserId,
        CancellationToken ct)
    {
        var list = await _store.ListPacksForOwnerAsync(ownerUserId, ct);
        return list.Select(MapPackSummary).ToList();
    }

    public async Task<GameShowPackDetailDto> GetPackAsync(
        int packId,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var pack = await RequirePackAccessAsync(packId, userId, isAdmin, ct);
        return MapPackDetail(pack);
    }

    public async Task<GameShowPackDetailDto> SavePackAsync(
        int ownerUserId,
        int? schoolUserId,
        UpsertGameShowPackRequest request,
        CancellationToken ct)
    {
        var body = NormalizePackBody(request);
        var now = _clock.UtcNow;
        var pack = new GameShowPack
        {
            OwnerUserId = ownerUserId,
            SchoolUserId = schoolUserId,
            Name = body.Title,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            PayloadJson = SerializePackBody(body),
            RoundCount = body.Rounds.Count,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _store.AddPackAsync(pack, ct);
        await _store.SaveChangesAsync(ct);
        return MapPackDetail(pack);
    }

    public async Task<GameShowPackDetailDto> UpdatePackAsync(
        int packId,
        int userId,
        bool isAdmin,
        UpsertGameShowPackRequest request,
        CancellationToken ct)
    {
        var pack = await RequirePackAccessAsync(packId, userId, isAdmin, ct);
        var body = NormalizePackBody(request);
        pack.Name = body.Title;
        pack.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        pack.PayloadJson = SerializePackBody(body);
        pack.RoundCount = body.Rounds.Count;
        pack.UpdatedAt = _clock.UtcNow;
        await _store.SaveChangesAsync(ct);
        return MapPackDetail(pack);
    }

    public async Task DeletePackAsync(
        int packId,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var pack = await RequirePackAccessAsync(packId, userId, isAdmin, ct);
        await _store.RemovePackAsync(pack, ct);
        await _store.SaveChangesAsync(ct);
    }

    public async Task<GameShowLobbyDto> CreateFromPackAsync(
        int hostUserId,
        int? schoolUserId,
        int packId,
        bool isAdmin,
        CreateSessionFromPackRequest request,
        CancellationToken ct)
    {
        var pack = await RequirePackAccessAsync(packId, hostUserId, isAdmin, ct);
        var body = DeserializePackBody(pack);
        var title = string.IsNullOrWhiteSpace(request.Title) ? pack.Name : request.Title.Trim();
        var teamA = string.IsNullOrWhiteSpace(request.TeamAName) ? body.TeamAName : request.TeamAName.Trim();
        var teamB = string.IsNullOrWhiteSpace(request.TeamBName) ? body.TeamBName : request.TeamBName.Trim();
        return await CreateAsync(
            hostUserId,
            schoolUserId,
            new CreateGameShowRequest(title, teamA, teamB, body.Rounds),
            ct);
    }

    public async Task<GameShowLobbyDto> ReplayAsync(
        int sessionId,
        int hostUserId,
        int? schoolUserId,
        bool isAdmin,
        ReplayGameShowRequest request,
        CancellationToken ct)
    {
        var source = await RequireSessionAsync(sessionId, ct);
        if (!isAdmin
            && source.HostUserId != hostUserId
            && source.SchoolUserId != hostUserId)
        {
            throw new DomainException("No puedes reutilizar esta partida.", 403, "forbidden");
        }

        var body = SessionToCreateRequest(source);
        var title = string.IsNullOrWhiteSpace(request.Title)
            ? $"{source.Title} (nueva)"
            : request.Title.Trim();
        var teamA = string.IsNullOrWhiteSpace(request.TeamAName) ? body.TeamAName : request.TeamAName.Trim();
        var teamB = string.IsNullOrWhiteSpace(request.TeamBName) ? body.TeamBName : request.TeamBName.Trim();
        return await CreateAsync(
            hostUserId,
            schoolUserId,
            new CreateGameShowRequest(title, teamA, teamB, body.Rounds),
            ct);
    }

    public async Task<CreateGameShowRequest> GetSessionPackAsync(
        int sessionId,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        if (!isAdmin
            && session.HostUserId != userId
            && session.SchoolUserId != userId)
        {
            throw new DomainException("No puedes leer esta partida.", 403, "forbidden");
        }

        return SessionToCreateRequest(session);
    }

    public async Task<GameShowStatsDto> GetStatsAsync(
        int sessionId,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var session = await _store.GetByIdWithAttemptsAsync(sessionId, ct)
            ?? throw new NotFoundException("Partida no encontrada.", "game_not_found");
        if (!isAdmin
            && session.HostUserId != userId
            && session.SchoolUserId != userId
            && !session.Players.Any(p => p.UserId == userId))
        {
            // Anonymous projector can still see ended scores via lobby; stats stay host-scoped.
            throw new DomainException("No puedes ver las estadísticas.", 403, "forbidden");
        }

        var rounds = session.Rounds.OrderBy(r => r.SortOrder).ToList();
        var roundStats = rounds.Select(r =>
        {
            var attempts = r.Attempts;
            return new GameShowRoundStatDto(
                r.SortOrder,
                r.QuestionText,
                r.RoundPointsForController,
                r.StealSucceeded,
                r.ControllingTeam,
                r.Strikes,
                attempts.Count(a => a.IsCorrect),
                attempts.Count(a => !a.IsCorrect));
        }).ToList();

        var allAttempts = rounds.SelectMany(r => r.Attempts).ToList();
        string? winner = null;
        if (session.Status == GameShowSessionStatuses.Ended)
        {
            if (session.TeamAScore > session.TeamBScore) winner = GameShowTeams.A;
            else if (session.TeamBScore > session.TeamAScore) winner = GameShowTeams.B;
        }

        return new GameShowStatsDto(
            session.Id,
            session.Title,
            session.Status,
            session.TeamAName,
            session.TeamBName,
            session.TeamAScore,
            session.TeamBScore,
            winner,
            session.Players.Count,
            rounds.Count,
            allAttempts.Count(a => a.IsCorrect),
            allAttempts.Count(a => !a.IsCorrect),
            rounds.Count(r => r.StealSucceeded),
            allAttempts.Count(a => a.IsSteal && !a.IsCorrect),
            roundStats,
            session.CreatedAt,
            session.EndedAt);
    }

    private async Task<GameShowPack> RequirePackAccessAsync(
        int packId,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var pack = await _store.GetPackByIdAsync(packId, ct)
            ?? throw new NotFoundException("Pack no encontrado.", "pack_not_found");
        if (!isAdmin && pack.OwnerUserId != userId && pack.SchoolUserId != userId)
        {
            throw new DomainException("No puedes usar este pack.", 403, "forbidden");
        }

        return pack;
    }

    private CreateGameShowRequest NormalizePackBody(UpsertGameShowPackRequest request)
    {
        if (request.Rounds is null || request.Rounds.Count < 1)
        {
            throw new DomainException("Agrega al menos una ronda.", 400, "invalid_rounds");
        }

        if (request.Rounds.Count > 50)
        {
            throw new DomainException("Máximo 50 rondas por pack.", 400, "too_many_rounds");
        }

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? "Pack de preguntas"
            : request.Name.Trim();
        var teamA = string.IsNullOrWhiteSpace(request.DefaultTeamAName) ? "Equipo A" : request.DefaultTeamAName.Trim();
        var teamB = string.IsNullOrWhiteSpace(request.DefaultTeamBName) ? "Equipo B" : request.DefaultTeamBName.Trim();
        return new CreateGameShowRequest(name, teamA, teamB, request.Rounds);
    }

    private static string SerializePackBody(CreateGameShowRequest body) =>
        System.Text.Json.JsonSerializer.Serialize(
            body,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

    private static CreateGameShowRequest DeserializePackBody(GameShowPack pack)
    {
        var body = System.Text.Json.JsonSerializer.Deserialize<CreateGameShowRequest>(
            pack.PayloadJson,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        if (body?.Rounds is null || body.Rounds.Count < 1)
        {
            throw new DomainException("El pack no tiene rondas válidas.", 400, "invalid_pack");
        }

        return body;
    }

    private static CreateGameShowRequest SessionToCreateRequest(GameShowSession session) =>
        new(
            session.Title,
            session.TeamAName,
            session.TeamBName,
            session.Rounds
                .OrderBy(r => r.SortOrder)
                .Select(r => new CreateGameShowRoundRequest(
                    r.QuestionText,
                    r.SourceQuestionId,
                    r.Answers
                        .OrderBy(a => a.Rank)
                        .Select(a => new CreateGameShowAnswerRequest(
                            a.Text,
                            a.Points,
                            GameShowAnswerMatcher.ParseAliases(a.AliasesJson).ToList()))
                        .ToList()))
                .ToList());

    private static GameShowPackSummaryDto MapPackSummary(GameShowPack pack) =>
        new(pack.Id, pack.Name, pack.Notes, pack.RoundCount, pack.CreatedAt, pack.UpdatedAt);

    private static GameShowPackDetailDto MapPackDetail(GameShowPack pack) =>
        new(
            pack.Id,
            pack.Name,
            pack.Notes,
            pack.RoundCount,
            pack.CreatedAt,
            pack.UpdatedAt,
            DeserializePackBody(pack));

    private static string Csv(string? value)
    {
        var v = (value ?? "").Replace("\"", "\"\"");
        return $"\"{v}\"";
    }

    private static GameShowRound BuildRound(CreateGameShowRoundRequest request, int order)
    {
        var q = (request.QuestionText ?? "").Trim();
        if (q.Length < 5)
        {
            throw new DomainException("Cada ronda necesita una pregunta.", 400, "invalid_question");
        }

        if (request.Answers is null || request.Answers.Count != 5)
        {
            throw new DomainException("Cada ronda debe tener exactamente 5 respuestas.", 400, "invalid_answers");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var answers = new List<GameShowBoardAnswer>();
        var rank = 1;
        foreach (var a in request.Answers.OrderByDescending(x => x.Points))
        {
            var text = (a.Text ?? "").Trim();
            if (text.Length == 0)
            {
                throw new DomainException("Las respuestas no pueden estar vacías.", 400, "empty_answer");
            }

            if (a.Points < 1)
            {
                throw new DomainException("Los puntos deben ser positivos.", 400, "invalid_points");
            }

            var key = GameShowAnswerMatcher.Normalize(text);
            if (!seen.Add(key))
            {
                throw new DomainException("Hay respuestas duplicadas.", 400, "duplicate_answer");
            }

            answers.Add(new GameShowBoardAnswer
            {
                Rank = rank++,
                Text = text[..Math.Min(text.Length, 200)],
                Points = a.Points,
                AliasesJson = GameShowAnswerMatcher.SerializeAliases(a.Aliases),
                IsRevealed = false
            });
        }

        return new GameShowRound
        {
            SortOrder = order,
            QuestionText = q[..Math.Min(q.Length, 600)],
            SourceQuestionId = request.SourceQuestionId,
            Phase = GameShowRoundPhases.WaitingBuzz,
            Answers = answers
        };
    }

    private static GameShowRound CurrentRound(GameShowSession session)
    {
        var rounds = session.Rounds.OrderBy(r => r.SortOrder).ToList();
        if (session.CurrentRoundIndex < 0 || session.CurrentRoundIndex >= rounds.Count)
        {
            throw new DomainException("No hay ronda activa.", 400, "no_round");
        }

        return rounds[session.CurrentRoundIndex];
    }

    private static void EnsureRunning(GameShowSession session)
    {
        if (session.Status != GameShowSessionStatuses.Running)
        {
            throw new DomainException("La partida no está en curso.", 400, "invalid_state");
        }
    }

    private async Task<GameShowSession> RequireSessionAsync(int id, CancellationToken ct) =>
        await _store.GetByIdAsync(id, ct)
        ?? throw new NotFoundException("Partida no encontrada.", "game_not_found");

    private async Task<GameShowSession> RequireHostAsync(
        int sessionId,
        int hostUserId,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        if (session.HostUserId != hostUserId)
        {
            throw new DomainException("Solo el anfitrión puede controlar la partida.", 403, "forbidden");
        }

        return session;
    }

    private async Task BroadcastLobbyAsync(GameShowSession session, CancellationToken ct) =>
        await _broadcaster.LobbyUpdatedAsync(session.Id, MapLobby(session, hostView: false), ct);

    private static GameShowLobbyDto MapLobby(
        GameShowSession session,
        bool hostView,
        int? viewerPlayerId = null,
        string? viewerTeam = null)
    {
        GameShowRoundPublicDto? current = null;
        var rounds = session.Rounds.OrderBy(r => r.SortOrder).ToList();
        if (session.CurrentRoundIndex >= 0 && session.CurrentRoundIndex < rounds.Count)
        {
            var r = rounds[session.CurrentRoundIndex];
            current = new GameShowRoundPublicDto(
                r.Id,
                r.SortOrder,
                r.QuestionText,
                r.Phase,
                r.ControllingTeam,
                r.BuzzWinnerTeam,
                r.Strikes,
                r.RoundPointsForController,
                r.StealSucceeded,
                r.Answers.OrderBy(a => a.Rank).Select(a =>
                    hostView || a.IsRevealed || r.Phase == GameShowRoundPhases.Finished
                        ? new GameShowBoardAnswerPublicDto(a.Id, a.Rank, a.Text, a.Points, a.IsRevealed)
                        : new GameShowBoardAnswerPublicDto(a.Id, a.Rank, null, null, false)
                ).ToList());
        }

        return new GameShowLobbyDto(
            session.Id,
            session.Title,
            session.JoinCode,
            session.Status,
            session.TeamAName,
            session.TeamBName,
            session.TeamAScore,
            session.TeamBScore,
            session.CurrentRoundIndex,
            rounds.Count,
            session.Players
                .OrderBy(p => p.JoinedAt)
                .Select(p => new GameShowPlayerDto(p.Id, p.DisplayName, p.Team, p.IsConnected, p.UserId))
                .ToList(),
            current,
            hostView,
            viewerPlayerId,
            viewerTeam);
    }

    private static string NormalizeTeam(string? team) =>
        string.Equals(team, GameShowTeams.B, StringComparison.OrdinalIgnoreCase)
            ? GameShowTeams.B
            : GameShowTeams.A;

    private static string PickBalancedTeam(GameShowSession session)
    {
        var a = session.Players.Count(p =>
            string.Equals(p.Team, GameShowTeams.A, StringComparison.OrdinalIgnoreCase));
        var b = session.Players.Count(p =>
            string.Equals(p.Team, GameShowTeams.B, StringComparison.OrdinalIgnoreCase));
        return a <= b ? GameShowTeams.A : GameShowTeams.B;
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        var chars = new char[6];
        for (var i = 0; i < 40; i++)
        {
            for (var c = 0; c < chars.Length; c++)
            {
                chars[c] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
            }

            var code = new string(chars);
            if (!await _store.JoinCodeExistsAsync(code, ct))
            {
                return code;
            }
        }

        throw new DomainException("No se pudo generar un código único.", 500, "code_generation_failed");
    }
}
