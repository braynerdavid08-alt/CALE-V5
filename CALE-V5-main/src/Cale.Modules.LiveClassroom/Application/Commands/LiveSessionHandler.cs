using System.Text.Json;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.LiveClassroom.Application.Abstractions;
using Cale.Modules.LiveClassroom.Application.DTOs;
using Cale.Modules.LiveClassroom.Domain;

namespace Cale.Modules.LiveClassroom.Application.Commands;

public sealed class LiveSessionHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILiveSessionStore _store;
    private readonly ICatalogStore _catalog;
    private readonly ICatalogAccessGuard _access;
    private readonly ILiveSessionBroadcaster _broadcast;
    private readonly IClock _clock;

    public LiveSessionHandler(
        ILiveSessionStore store,
        ICatalogStore catalog,
        ICatalogAccessGuard access,
        ILiveSessionBroadcaster broadcast,
        IClock clock)
    {
        _store = store;
        _catalog = catalog;
        _access = access;
        _broadcast = broadcast;
        _clock = clock;
    }

    public async Task<LiveLobbyDto> CreateAsync(
        CreateLiveSessionRequest request,
        int hostUserId,
        string role,
        string publicBaseUrl,
        CancellationToken ct)
    {
        await _access.EnsureSimulacroAsync(hostUserId, role, ct);

        var config = MapConfig(request.Config);
        if (config.CaleStandardPreset)
        {
            config = LiveSessionConfig.CaleStandard();
        }

        var bankId = request.BankId
            ?? await ResolveDefaultBankIdAsync(ct)
            ?? throw new DomainException("No hay bancos de preguntas disponibles.", 400, "bank_required");

        var bank = await _catalog.GetBankAsync(bankId, ct)
            ?? throw new NotFoundException("Bank not found.", "bank_not_found");

        var code = await GenerateUniqueCodeAsync(ct);
        var session = LiveSession.Create(
            hostUserId,
            request.Title ?? "CALE Aula en Vivo",
            code,
            request.Mode,
            bank.Id,
            JsonSerializer.Serialize(config, JsonOpts),
            _clock.UtcNow);

        await _store.AddAsync(session, ct);
        await _store.SaveChangesAsync(ct);

        return await ToLobbyAsync(session, publicBaseUrl, includeCorrect: true, ct);
    }

    public async Task<LiveLobbyDto> GetHostAsync(
        int sessionId,
        int hostUserId,
        bool isAdmin,
        string publicBaseUrl,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        EnsureHost(session, hostUserId, isAdmin);
        return await ToLobbyAsync(session, publicBaseUrl, includeCorrect: true, ct);
    }

    public async Task<LiveLobbyDto> GetParticipantViewAsync(
        int sessionId,
        Guid participantToken,
        string publicBaseUrl,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        var participant = await _store.GetParticipantByTokenAsync(participantToken, ct)
            ?? throw new ForbiddenException("Invalid participant.", "invalid_participant");
        if (participant.SessionId != sessionId)
        {
            throw new ForbiddenException("Participant not in session.", "invalid_participant");
        }

        var includeCorrect = session.RevealCorrect
            && session.Mode is not LiveSessionModes.Exam;
        return await ToLobbyAsync(session, publicBaseUrl, includeCorrect, ct);
    }

    public async Task<JoinLiveSessionResponse> JoinAsync(
        JoinLiveSessionRequest request,
        int? userId,
        string? userName,
        CancellationToken ct)
    {
        var code = (request.Code ?? "").Trim().ToUpperInvariant();
        if (code.Length < 4)
        {
            throw new DomainException("Código inválido.", 400, "invalid_join_code");
        }

        var session = await _store.GetByJoinCodeAsync(code, ct)
            ?? throw new NotFoundException("Sala no encontrada.", "session_not_found");

        if (session.Status == LiveSessionStatuses.Ended)
        {
            throw new DomainException("La sesión ya terminó.", 400, "session_ended");
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? (userName ?? "Participante")
            : request.DisplayName;

        if (userId is int uid)
        {
            var existing = session.Participants.FirstOrDefault(p => p.UserId == uid);
            if (existing is not null)
            {
                return new JoinLiveSessionResponse(
                    session.Id,
                    existing.ParticipantToken,
                    existing.Id,
                    existing.DisplayName,
                    session.Title,
                    session.Status,
                    session.JoinCode);
            }
        }

        var participant = LiveParticipant.Create(
            session.Id,
            displayName,
            userId,
            _clock.UtcNow);
        session.AddParticipant(participant);
        await _store.SaveChangesAsync(ct);

        await BroadcastLobbyAsync(session, ct);

        return new JoinLiveSessionResponse(
            session.Id,
            participant.ParticipantToken,
            participant.Id,
            participant.DisplayName,
            session.Title,
            session.Status,
            session.JoinCode);
    }

    public async Task SetConnectionAsync(
        Guid participantToken,
        string connectionId,
        bool connected,
        CancellationToken ct)
    {
        var participant = await _store.GetParticipantByTokenAsync(participantToken, ct);
        if (participant is null)
        {
            return;
        }

        if (connected)
        {
            participant.Connect(connectionId);
        }
        else
        {
            participant.Disconnect();
        }

        await _store.SaveChangesAsync(ct);
        var session = await RequireSessionAsync(participant.SessionId, ct);
        await BroadcastLobbyAsync(session, ct);
    }

    public async Task<LiveLobbyDto> ControlAsync(
        int sessionId,
        string action,
        int hostUserId,
        bool isAdmin,
        string publicBaseUrl,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        EnsureHost(session, hostUserId, isAdmin);
        var config = ReadConfig(session);
        var now = _clock.UtcNow;

        switch ((action ?? "").Trim().ToLowerInvariant())
        {
            case "start":
                await EnsureQuestionsPreparedAsync(session, config, ct);
                if (session.Questions.Count == 0)
                {
                    throw new DomainException(
                        "No hay preguntas en el banco con esos filtros.",
                        400,
                        "no_questions");
                }
                session.MarkRunning(now);
                OpenAt(session, 0, config, now);
                await _store.SaveChangesAsync(ct);
                await BroadcastQuestionAsync(session, includeCorrect: false, ct);
                break;

            case "pause":
                session.Pause();
                await _store.SaveChangesAsync(ct);
                await BroadcastLobbyAsync(session, ct);
                break;

            case "resume":
                session.Resume();
                await _store.SaveChangesAsync(ct);
                await BroadcastLobbyAsync(session, ct);
                break;

            case "next":
                await EnsureQuestionsPreparedAsync(session, config, ct);
                var next = session.CurrentQuestionIndex + 1;
                if (next >= session.Questions.Count)
                {
                    session.End(now);
                    await _store.SaveChangesAsync(ct);
                    await _broadcast.SessionEndedAsync(session.Id, new { sessionId = session.Id }, ct);
                    break;
                }
                OpenAt(session, next, config, now);
                await _store.SaveChangesAsync(ct);
                await BroadcastQuestionAsync(session, includeCorrect: false, ct);
                break;

            case "close":
                session.CloseCurrentQuestion(now);
                await _store.SaveChangesAsync(ct);
                await BroadcastQuestionClosedAsync(session, ct);
                break;

            case "reveal":
                session.SetReveal(true);
                await _store.SaveChangesAsync(ct);
                await BroadcastRevealAsync(session, ct);
                break;

            case "end":
                session.End(now);
                await _store.SaveChangesAsync(ct);
                await _broadcast.SessionEndedAsync(session.Id, new { sessionId = session.Id }, ct);
                break;

            default:
                throw new DomainException("Acción no válida.", 400, "invalid_action");
        }

        return await ToLobbyAsync(session, publicBaseUrl, includeCorrect: true, ct);
    }

    public async Task AnswerAsync(
        int sessionId,
        int sessionQuestionId,
        LiveAnswerRequest request,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        var participant = await _store.GetParticipantByTokenAsync(request.ParticipantToken, ct)
            ?? throw new ForbiddenException("Invalid participant.", "invalid_participant");

        if (participant.SessionId != sessionId)
        {
            throw new ForbiddenException("Participant not in session.", "invalid_participant");
        }

        if (session.Status == LiveSessionStatuses.Ended)
        {
            throw new DomainException("La sesión ya terminó.", 400, "session_ended");
        }

        var now = _clock.UtcNow;
        if (!session.IsQuestionOpen(now))
        {
            throw new DomainException("La pregunta está cerrada.", 400, "question_closed");
        }

        var current = session.Questions
            .OrderBy(q => q.SortOrder)
            .ElementAtOrDefault(session.CurrentQuestionIndex)
            ?? throw new DomainException("No hay pregunta activa.", 400, "no_active_question");

        if (current.Id != sessionQuestionId)
        {
            throw new DomainException("Esa no es la pregunta actual.", 400, "wrong_question");
        }

        var existing = await _store.FindAnswerAsync(sessionQuestionId, participant.Id, ct);
        if (existing is not null)
        {
            throw new DomainException("Ya respondiste esta pregunta.", 400, "already_answered");
        }

        var snap = DeserializeSnapshot(current.SnapshotJson);
        var option = snap.Options.FirstOrDefault(o => o.Id == request.OptionId)
            ?? throw new DomainException("Opción inválida.", 400, "invalid_option");

        var elapsedMs = session.QuestionOpenedAt is { } opened
            ? (int)Math.Min(int.MaxValue, (now - opened).TotalMilliseconds)
            : 0;

        var answer = LiveAnswer.Create(
            sessionQuestionId,
            participant.Id,
            option.Id,
            option.IsCorrect,
            elapsedMs,
            now);
        await _store.AddAnswerAsync(answer, ct);
        await _store.SaveChangesAsync(ct);

        var count = await _store.CountAnswersAsync(sessionQuestionId, ct);
        var connected = session.Participants.Count(p => p.IsConnected);
        await _broadcast.AnswerReceivedAsync(
            session.Id,
            new
            {
                sessionId = session.Id,
                sessionQuestionId,
                answersReceived = count,
                participantCount = Math.Max(connected, session.Participants.Count),
                participationPercent = session.Participants.Count == 0
                    ? 0
                    : (int)Math.Round(100.0 * count / session.Participants.Count)
            },
            ct);
    }

    private async Task EnsureQuestionsPreparedAsync(
        LiveSession session,
        LiveSessionConfig config,
        CancellationToken ct)
    {
        if (session.Questions.Count > 0)
        {
            return;
        }

        var pool = await _catalog.ListActiveQuestionsInBankAsync(session.BankId, ct);
        IEnumerable<Question> filtered = pool;
        if (!string.IsNullOrWhiteSpace(config.TopicFilter))
        {
            var topic = config.TopicFilter.Trim();
            filtered = filtered.Where(q =>
                string.Equals(q.Topic, topic, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(config.DifficultyFilter))
        {
            var diff = config.DifficultyFilter.Trim();
            filtered = filtered.Where(q =>
                string.Equals(q.Difficulty, diff, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        if (config.Randomize)
        {
            list = list.OrderBy(_ => Guid.NewGuid()).ToList();
        }

        var take = Math.Clamp(config.QuestionCount, 1, 100);
        list = list.Take(take).ToList();

        var snapshots = new List<LiveSessionQuestion>();
        var order = 0;
        foreach (var q in list)
        {
            var options = q.Options.Select(o => new SnapshotOption(o.Id, o.Text, o.ImageUrl, o.IsCorrect)).ToList();
            if (config.ShuffleOptions)
            {
                options = options.OrderBy(_ => Guid.NewGuid()).ToList();
            }

            var snap = new QuestionSnapshot(
                q.Id,
                q.Text,
                q.ImageUrl,
                q.Topic,
                q.Explanation,
                options);
            snapshots.Add(LiveSessionQuestion.Create(
                session.Id,
                q.Id,
                order++,
                JsonSerializer.Serialize(snap, JsonOpts),
                q.Topic,
                q.Difficulty));
        }

        session.SetQuestions(snapshots);
        await _store.SaveChangesAsync(ct);
    }

    private static void OpenAt(
        LiveSession session,
        int index,
        LiveSessionConfig config,
        DateTime now)
    {
        var closes = config.SecondsPerQuestion > 0
            ? now.AddSeconds(config.SecondsPerQuestion)
            : (DateTime?)null;
        session.OpenQuestion(index, now, closes);
    }

    private async Task BroadcastLobbyAsync(LiveSession session, CancellationToken ct)
    {
        var dto = await ToLobbyAsync(session, "", includeCorrect: false, ct);
        await _broadcast.LobbyUpdatedAsync(session.Id, dto, ct);
    }

    private async Task BroadcastQuestionAsync(
        LiveSession session,
        bool includeCorrect,
        CancellationToken ct)
    {
        var payload = BuildCurrentQuestion(session, includeCorrect);
        await _broadcast.QuestionStartedAsync(session.Id, payload!, ct);
        await BroadcastLobbyAsync(session, ct);
    }

    private async Task BroadcastQuestionClosedAsync(LiveSession session, CancellationToken ct)
    {
        var payload = BuildCurrentQuestion(session, includeCorrect: false);
        await _broadcast.QuestionClosedAsync(session.Id, payload!, ct);
        await BroadcastLobbyAsync(session, ct);
    }

    private async Task BroadcastRevealAsync(LiveSession session, CancellationToken ct)
    {
        var payload = BuildCurrentQuestion(session, includeCorrect: true);
        await _broadcast.RevealUpdatedAsync(session.Id, payload!, ct);
    }

    private async Task<LiveLobbyDto> ToLobbyAsync(
        LiveSession session,
        string publicBaseUrl,
        bool includeCorrect,
        CancellationToken ct)
    {
        var config = ReadConfig(session);
        var participants = session.Participants
            .OrderBy(p => p.JoinedAt)
            .Select(p => new LiveParticipantDto(
                p.Id,
                config.AnonymousNames ? $"Jugador {p.Id}" : p.DisplayName,
                p.IsConnected,
                config.AnonymousNames ? null : p.UserId))
            .ToList();

        var current = BuildCurrentQuestion(session, includeCorrect);
        var answers = 0;
        if (current is not null)
        {
            answers = await _store.CountAnswersAsync(current.SessionQuestionId, ct);
        }

        var joinUrl = string.IsNullOrWhiteSpace(publicBaseUrl)
            ? $"/live/join/{session.JoinCode}"
            : $"{publicBaseUrl.TrimEnd('/')}/live/join/{session.JoinCode}";

        return new LiveLobbyDto(
            session.Id,
            session.Title,
            session.JoinCode,
            session.Status,
            session.Mode,
            session.BankId,
            session.Participants.Count,
            session.Participants.Count(p => p.IsConnected),
            participants,
            ToConfigDto(config),
            session.Questions.Count,
            session.CurrentQuestionIndex,
            session.RevealCorrect,
            current,
            answers,
            joinUrl);
    }

    private LiveQuestionPayloadDto? BuildCurrentQuestion(LiveSession session, bool includeCorrect)
    {
        if (session.CurrentQuestionIndex < 0 || session.Questions.Count == 0)
        {
            return null;
        }

        var ordered = session.Questions.OrderBy(q => q.SortOrder).ToList();
        if (session.CurrentQuestionIndex >= ordered.Count)
        {
            return null;
        }

        var q = ordered[session.CurrentQuestionIndex];
        var snap = DeserializeSnapshot(q.SnapshotJson);
        var config = ReadConfig(session);
        var options = snap.Options
            .Select(o => new LiveOptionDto(
                o.Id,
                o.Text,
                o.ImageUrl,
                includeCorrect ? o.IsCorrect : null))
            .ToList();

        return new LiveQuestionPayloadDto(
            q.Id,
            snap.QuestionId,
            session.CurrentQuestionIndex,
            ordered.Count,
            snap.Text,
            snap.ImageUrl,
            snap.Topic,
            includeCorrect ? snap.Explanation : null,
            options,
            session.QuestionOpenedAt,
            session.QuestionClosesAt,
            config.SecondsPerQuestion,
            session.RevealCorrect && includeCorrect);
    }

    private async Task<int?> ResolveDefaultBankIdAsync(CancellationToken ct)
    {
        var banks = await _catalog.ListBanksAsync(activeOnly: true, ct);
        var normas = banks.FirstOrDefault(b =>
            b.Name.Contains("Normas", StringComparison.OrdinalIgnoreCase));
        return (normas ?? banks.FirstOrDefault())?.Id;
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var chars = new char[6];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = alphabet[Random.Shared.Next(alphabet.Length)];
            }

            var code = new string(chars);
            if (!await _store.JoinCodeExistsAsync(code, ct))
            {
                return code;
            }
        }

        throw new DomainException("No se pudo generar código de sala.", 500, "join_code_failed");
    }

    private async Task<LiveSession> RequireSessionAsync(int id, CancellationToken ct) =>
        await _store.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Session not found.", "session_not_found");

    private static void EnsureHost(LiveSession session, int userId, bool isAdmin)
    {
        if (!isAdmin && session.HostUserId != userId)
        {
            throw new ForbiddenException("Only the host can control this session.", "not_host");
        }
    }

    private static LiveSessionConfig MapConfig(LiveSessionConfigDto? dto)
    {
        if (dto is null)
        {
            return new LiveSessionConfig();
        }

        if (dto.CaleStandardPreset)
        {
            return LiveSessionConfig.CaleStandard();
        }

        return new LiveSessionConfig
        {
            QuestionCount = Math.Clamp(dto.QuestionCount, 1, 100),
            SecondsPerQuestion = Math.Clamp(dto.SecondsPerQuestion, 5, 600),
            Randomize = dto.Randomize,
            ShuffleOptions = dto.ShuffleOptions,
            ShowRanking = dto.ShowRanking,
            AnonymousNames = dto.AnonymousNames,
            FeedbackTiming = string.IsNullOrWhiteSpace(dto.FeedbackTiming) ? "end" : dto.FeedbackTiming,
            TopicFilter = dto.TopicFilter,
            DifficultyFilter = dto.DifficultyFilter,
            CaleStandardPreset = false
        };
    }

    private static LiveSessionConfig ReadConfig(LiveSession session)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<LiveSessionConfigDto>(session.ConfigJson, JsonOpts);
            return MapConfig(dto);
        }
        catch
        {
            return new LiveSessionConfig();
        }
    }

    private static LiveSessionConfigDto ToConfigDto(LiveSessionConfig c) =>
        new(
            c.QuestionCount,
            c.SecondsPerQuestion,
            c.Randomize,
            c.ShuffleOptions,
            c.ShowRanking,
            c.AnonymousNames,
            c.FeedbackTiming,
            c.TopicFilter,
            c.DifficultyFilter,
            c.CaleStandardPreset);

    private static QuestionSnapshot DeserializeSnapshot(string json) =>
        JsonSerializer.Deserialize<QuestionSnapshot>(json, JsonOpts)
        ?? new QuestionSnapshot(0, "", null, null, null, []);

    private sealed record SnapshotOption(int Id, string Text, string? ImageUrl, bool IsCorrect);

    private sealed record QuestionSnapshot(
        int QuestionId,
        string Text,
        string? ImageUrl,
        string? Topic,
        string? Explanation,
        List<SnapshotOption> Options);
}
