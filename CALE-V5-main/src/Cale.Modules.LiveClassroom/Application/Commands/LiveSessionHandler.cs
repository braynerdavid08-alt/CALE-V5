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

        var bankIds = await ResolveBankIdsAsync(request, ct);
        config.BankIds = bankIds.ToList();

        var code = await GenerateUniqueCodeAsync(ct);
        var session = LiveSession.Create(
            hostUserId,
            request.Title ?? "CALE Aula en Vivo",
            code,
            request.Mode,
            bankIds[0],
            JsonSerializer.Serialize(config, JsonOpts),
            _clock.UtcNow);

        await _store.AddAsync(session, ct);
        await _store.SaveChangesAsync(ct);

        return await ToLobbyAsync(session, publicBaseUrl, includeCorrect: session.RevealCorrect, ct);
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
        return await ToLobbyAsync(session, publicBaseUrl, includeCorrect: session.RevealCorrect, ct);
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
        return await ToLobbyAsync(
            session,
            publicBaseUrl,
            includeCorrect,
            ct,
            participant.Id);
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

    public async Task DisconnectByConnectionAsync(string connectionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return;
        }

        var participant = await _store.GetParticipantByConnectionIdAsync(connectionId, ct);
        if (participant is null)
        {
            return;
        }

        participant.Disconnect();
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
        CancellationToken ct,
        LiveQuickQuestionRequest? quickQuestion = null)
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
                await BroadcastRankingAsync(session, ct);
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
                    await BroadcastRankingAsync(session, ct);
                    break;
                }
                OpenAt(session, next, config, now);
                await _store.SaveChangesAsync(ct);
                await BroadcastQuestionAsync(session, includeCorrect: false, ct);
                await BroadcastRankingAsync(session, ct);
                break;

            case "close":
                await CloseQuestionInternalAsync(session, config, now, ct);
                break;

            case "reveal":
                session.SetReveal(true);
                await _store.SaveChangesAsync(ct);
                await BroadcastRevealAsync(session, ct);
                await BroadcastRankingAsync(session, ct);
                break;

            case "end":
                session.End(now);
                await _store.SaveChangesAsync(ct);
                await _broadcast.SessionEndedAsync(session.Id, new { sessionId = session.Id }, ct);
                await BroadcastRankingAsync(session, ct);
                break;

            case "surprise":
                await QueueSurpriseQuestionAsync(session, config, ct);
                break;

            case "quick":
                await QueueQuickQuestionAsync(session, config, quickQuestion, ct);
                break;

            default:
                throw new DomainException("Acción no válida.", 400, "invalid_action");
        }

        return await ToLobbyAsync(session, publicBaseUrl, includeCorrect: session.RevealCorrect, ct);
    }

    /// <summary>Called by the background timer when QuestionClosesAt has elapsed.</summary>
    public async Task AutoCloseExpiredAsync(int sessionId, CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        var now = _clock.UtcNow;
        if (session.Status != LiveSessionStatuses.Running
            || session.CurrentQuestionIndex < 0
            || session.QuestionClosesAt is null
            || session.QuestionClosesAt > now)
        {
            return;
        }

        var config = ReadConfig(session);
        await CloseQuestionInternalAsync(session, config, now, ct);
    }

    private async Task CloseQuestionInternalAsync(
        LiveSession session,
        LiveSessionConfig config,
        DateTime now,
        CancellationToken ct)
    {
        session.CloseCurrentQuestion(now);
        var autoReveal = string.Equals(
                config.FeedbackTiming,
                "immediate",
                StringComparison.OrdinalIgnoreCase)
            || session.Mode == LiveSessionModes.Pedagogical;
        if (autoReveal)
        {
            session.SetReveal(true);
        }

        await _store.SaveChangesAsync(ct);
        await BroadcastQuestionClosedAsync(session, ct);
        if (autoReveal)
        {
            await BroadcastRevealAsync(session, ct);
        }

        await BroadcastRankingAsync(session, ct);
    }

    public async Task<object> AnswerAsync(
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

        var config = ReadConfig(session);
        var elapsedMs = session.QuestionOpenedAt is { } opened
            ? (int)Math.Min(int.MaxValue, (now - opened).TotalMilliseconds)
            : 0;

        var points = LiveAnswer.ComputePoints(
            option.IsCorrect,
            elapsedMs,
            config.SecondsPerQuestion);

        var answer = LiveAnswer.Create(
            sessionQuestionId,
            participant.Id,
            option.Id,
            option.IsCorrect,
            elapsedMs,
            points,
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

        await BroadcastRankingAsync(session, ct);

        var revealPoints = session.Mode is not LiveSessionModes.Exam;
        return new
        {
            ok = true,
            points = revealPoints ? points : (int?)null
        };
    }

    public async Task<LiveDoubtDto> PostDoubtAsync(
        int sessionId,
        LiveDoubtRequest request,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        var participant = await RequireParticipantInSessionAsync(
            request.ParticipantToken,
            sessionId,
            ct);

        LiveDoubt doubt;
        try
        {
            doubt = LiveDoubt.Create(sessionId, participant.Id, request.Text, _clock.UtcNow);
        }
        catch (ArgumentException)
        {
            throw new DomainException("Texto de duda inválido.", 400, "invalid_doubt_text");
        }

        await _store.AddDoubtAsync(doubt, ct);
        await _store.SaveChangesAsync(ct);

        await BroadcastDoubtsAsync(session, ct);
        var list = await MapDoubtsAsync(session, participant.Id, ct);
        return list.First(d => d.Id == doubt.Id);
    }

    public async Task<LiveDoubtDto> VoteDoubtAsync(
        int sessionId,
        int doubtId,
        LiveDoubtVoteRequest request,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        var participant = await RequireParticipantInSessionAsync(
            request.ParticipantToken,
            sessionId,
            ct);

        var doubt = await _store.GetDoubtAsync(doubtId, ct)
            ?? throw new NotFoundException("Doubt not found.", "doubt_not_found");
        if (doubt.SessionId != sessionId)
        {
            throw new NotFoundException("Doubt not found.", "doubt_not_found");
        }

        if (doubt.IsResolved)
        {
            throw new DomainException("La duda ya está resuelta.", 400, "doubt_resolved");
        }

        var existingVote = await _store.FindDoubtVoteAsync(doubtId, participant.Id, ct);
        if (existingVote is not null)
        {
            throw new ConflictException("Ya votaste esta duda.", "already_voted");
        }

        doubt.AddVote();
        await _store.AddDoubtVoteAsync(
            LiveDoubtVote.Create(doubtId, participant.Id, _clock.UtcNow),
            ct);
        await _store.SaveChangesAsync(ct);

        await BroadcastDoubtsAsync(session, ct);
        var list = await MapDoubtsAsync(session, participant.Id, ct);
        return list.First(d => d.Id == doubtId);
    }

    public async Task<LiveDoubtDto> ResolveDoubtAsync(
        int sessionId,
        int doubtId,
        int hostUserId,
        bool isAdmin,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        EnsureHost(session, hostUserId, isAdmin);

        var doubt = await _store.GetDoubtAsync(doubtId, ct)
            ?? throw new NotFoundException("Doubt not found.", "doubt_not_found");
        if (doubt.SessionId != sessionId)
        {
            throw new NotFoundException("Doubt not found.", "doubt_not_found");
        }

        doubt.Resolve();
        await _store.SaveChangesAsync(ct);

        await BroadcastDoubtsAsync(session, ct);
        var list = await MapDoubtsAsync(session, viewerParticipantId: null, ct);
        return list.First(d => d.Id == doubtId);
    }

    public async Task<IReadOnlyList<LiveDoubtDto>> ListDoubtsAsync(
        int sessionId,
        Guid? viewerToken,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        int? viewerId = null;
        if (viewerToken is Guid token)
        {
            var participant = await _store.GetParticipantByTokenAsync(token, ct);
            if (participant is not null && participant.SessionId == sessionId)
            {
                viewerId = participant.Id;
            }
        }

        return await MapDoubtsAsync(session, viewerId, ct);
    }

    public async Task<LiveAnalyticsDto> GetAnalyticsAsync(
        int sessionId,
        int hostUserId,
        bool isAdmin,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        EnsureHost(session, hostUserId, isAdmin);

        var answers = await _store.ListAnswersForSessionAsync(sessionId, ct);
        var orderedQuestions = session.Questions.OrderBy(q => q.SortOrder).ToList();
        var answersByQuestion = answers
            .GroupBy(a => a.SessionQuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var questionStats = new List<LiveQuestionStatDto>();
        var topicBuckets = new Dictionary<string, (int Answered, int Correct)>(
            StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < orderedQuestions.Count; i++)
        {
            var q = orderedQuestions[i];
            var snap = DeserializeSnapshot(q.SnapshotJson);
            var qAnswers = answersByQuestion.TryGetValue(q.Id, out var list) ? list : [];
            var answered = qAnswers.Count;
            var correct = qAnswers.Count(a => a.IsCorrect);
            var accuracy = answered == 0 ? 0 : Math.Round(100.0 * correct / answered, 1);
            var topic = string.IsNullOrWhiteSpace(q.Topic) ? "General" : q.Topic!;

            questionStats.Add(new LiveQuestionStatDto(
                i,
                q.Id,
                snap.Text,
                topic,
                answered,
                correct,
                accuracy,
                q.IsSurprise));

            if (!topicBuckets.TryGetValue(topic, out var bucket))
            {
                bucket = (0, 0);
            }

            topicBuckets[topic] = (bucket.Answered + answered, bucket.Correct + correct);
        }

        var topics = topicBuckets
            .Select(kv =>
            {
                var answered = kv.Value.Answered;
                var correct = kv.Value.Correct;
                var accuracy = answered == 0 ? 0 : Math.Round(100.0 * correct / answered, 1);
                return new LiveTopicStatDto(kv.Key, answered, correct, accuracy);
            })
            .OrderBy(t => t.Topic, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var recommendations = topics
            .Where(t => t.Answered > 0 && t.AccuracyPercent < 60)
            .OrderBy(t => t.AccuracyPercent)
            .ThenBy(t => t.Topic, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Select(t => t.Topic)
            .ToList();

        var totalAnswers = answers.Count;
        var correctAnswers = answers.Count(a => a.IsCorrect);
        var overall = totalAnswers == 0
            ? 0
            : Math.Round(100.0 * correctAnswers / totalAnswers, 1);

        var ranking = await BuildRankingAsync(session, myParticipantId: null, ct);

        return new LiveAnalyticsDto(
            session.Id,
            session.Title,
            session.Mode,
            session.Participants.Count,
            orderedQuestions.Count,
            totalAnswers,
            correctAnswers,
            overall,
            questionStats,
            topics,
            recommendations,
            ranking);
    }

    public async Task<LiveRematchResponse> RematchAsync(
        int sessionId,
        int hostUserId,
        bool isAdmin,
        string publicBaseUrl,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(sessionId, ct);
        EnsureHost(session, hostUserId, isAdmin);

        if (session.Status != LiveSessionStatuses.Ended)
        {
            throw new DomainException(
                "Solo se puede crear rematch de una sesión terminada.",
                400,
                "session_not_ended");
        }

        var code = await GenerateUniqueCodeAsync(ct);
        var rematch = LiveSession.Create(
            session.HostUserId,
            $"{session.Title} (revancha)",
            code,
            session.Mode,
            session.BankId,
            session.ConfigJson,
            _clock.UtcNow);

        await _store.AddAsync(rematch, ct);
        await _store.SaveChangesAsync(ct);

        var joinUrl = BuildJoinUrl(publicBaseUrl, rematch.JoinCode);
        await _broadcast.RematchReadyAsync(
            session.Id,
            new
            {
                newSessionId = rematch.Id,
                joinCode = rematch.JoinCode,
                joinUrl
            },
            ct);

        var lobby = await ToLobbyAsync(rematch, publicBaseUrl, includeCorrect: rematch.RevealCorrect, ct);
        return new LiveRematchResponse(rematch.Id, rematch.JoinCode, joinUrl, lobby);
    }

    public async Task<(byte[] Bytes, string FileName)> ExportResultsCsvAsync(
        int sessionId,
        int hostUserId,
        bool isAdmin,
        CancellationToken ct)
    {
        var analytics = await GetAnalyticsAsync(sessionId, hostUserId, isAdmin, ct);
        var session = await RequireSessionAsync(sessionId, ct);
        var ranking = analytics.Ranking.Top;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("tipo,rank,nombre,puntaje,correctas,respondidas,tema,precision,recomendacion");
        foreach (var row in ranking)
        {
            sb.AppendLine(
                $"ranking,{row.Rank},{Csv(row.DisplayName)},{row.Score},{row.CorrectCount},{row.AnswerCount},,,");
        }

        foreach (var t in analytics.Topics)
        {
            sb.AppendLine(
                $"tema,,, ,,,{Csv(t.Topic)},{t.AccuracyPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
        }

        foreach (var r in analytics.Recommendations)
        {
            sb.AppendLine($"recomendacion,,,,,,,{Csv(r)}");
        }

        sb.AppendLine(
            $"resumen,,,{analytics.OverallAccuracyPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)},{analytics.CorrectAnswers},{analytics.TotalAnswers},,,");

        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();
        var safe = string.Join("_", session.Title.Split(Path.GetInvalidFileNameChars()));
        return (bytes, $"cale-live-{session.Id}-{safe}.csv");
    }

    private static string Csv(string? value)
    {
        var v = (value ?? "").Replace("\"", "\"\"");
        return $"\"{v}\"";
    }

    private async Task QueueSurpriseQuestionAsync(
        LiveSession session,
        LiveSessionConfig config,
        CancellationToken ct)
    {
        if (session.Status != LiveSessionStatuses.Running || session.CurrentQuestionIndex < 0)
        {
            throw new DomainException(
                "La sorpresa solo está disponible con una pregunta activa.",
                400,
                "surprise_unavailable");
        }

        var usedIds = session.Questions.Select(q => q.QuestionId).ToHashSet();
        var pool = await LoadQuestionPoolAsync(session, config, ct);
        var unused = pool.Where(q => !usedIds.Contains(q.Id)).ToList();
        if (unused.Count == 0)
        {
            throw new DomainException(
                "No quedan preguntas sorpresa disponibles.",
                400,
                "no_surprise_left");
        }

        var pick = unused[Random.Shared.Next(unused.Count)];
        var options = pick.Options
            .Select(o => new SnapshotOption(o.Id, o.Text, o.ImageUrl, o.IsCorrect))
            .ToList();
        if (config.ShuffleOptions)
        {
            options = options.OrderBy(_ => Guid.NewGuid()).ToList();
        }

        var snap = new QuestionSnapshot(
            pick.Id,
            pick.Text,
            pick.ImageUrl,
            pick.Topic,
            pick.Explanation,
            options);

        var surprise = LiveSessionQuestion.Create(
            session.Id,
            pick.Id,
            session.CurrentQuestionIndex + 1,
            JsonSerializer.Serialize(snap, JsonOpts),
            pick.Topic,
            pick.Difficulty,
            isSurprise: true);

        session.InsertQuestionAfterCurrent(surprise);
        await _store.SaveChangesAsync(ct);

        await _broadcast.SurpriseQueuedAsync(
            session.Id,
            new
            {
                questionCount = session.Questions.Count,
                message = "Se añadió una pregunta sorpresa."
            },
            ct);
        await BroadcastLobbyAsync(session, ct);
    }

    private async Task QueueQuickQuestionAsync(
        LiveSession session,
        LiveSessionConfig config,
        LiveQuickQuestionRequest? request,
        CancellationToken ct)
    {
        if (session.Status is not (LiveSessionStatuses.Running or LiveSessionStatuses.Lobby or LiveSessionStatuses.Paused))
        {
            throw new DomainException(
                "No se puede añadir una pregunta rápida ahora.",
                400,
                "quick_unavailable");
        }

        var text = (request?.Text ?? "").Trim();
        if (text.Length < 3)
        {
            throw new DomainException("Escribe el enunciado de la pregunta.", 400, "invalid_text");
        }

        var options = (request?.Options ?? [])
            .Select((o, i) => new SnapshotOption(
                -(i + 1),
                (o.Text ?? "").Trim(),
                null,
                o.IsCorrect))
            .Where(o => o.Text.Length > 0)
            .ToList();

        if (options.Count < 2)
        {
            throw new DomainException(
                "Cada respuesta necesita texto o imagen (mínimo dos).",
                400,
                "invalid_options");
        }

        if (options.Count(o => o.IsCorrect) != 1)
        {
            throw new DomainException("Marca exactamente una respuesta correcta.", 400, "invalid_correct");
        }

        if (config.ShuffleOptions)
        {
            options = options.OrderBy(_ => Guid.NewGuid()).ToList();
        }

        var snap = new QuestionSnapshot(
            0,
            text,
            null,
            string.IsNullOrWhiteSpace(request?.Topic) ? "Rápida" : request!.Topic!.Trim(),
            string.IsNullOrWhiteSpace(request?.Explanation) ? null : request!.Explanation!.Trim(),
            options);

        await EnsureQuestionsPreparedAsync(session, config, ct);

        var quick = LiveSessionQuestion.Create(
            session.Id,
            0,
            session.CurrentQuestionIndex + 1,
            JsonSerializer.Serialize(snap, JsonOpts),
            snap.Topic,
            "quick",
            isSurprise: true);

        session.InsertQuestionAfterCurrent(quick);
        await _store.SaveChangesAsync(ct);

        await _broadcast.SurpriseQueuedAsync(
            session.Id,
            new
            {
                questionCount = session.Questions.Count,
                message = "Pregunta rápida en cola."
            },
            ct);
        await BroadcastLobbyAsync(session, ct);
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

        var list = await LoadQuestionPoolAsync(session, config, ct);
        var take = Math.Clamp(config.QuestionCount, 1, 100);
        if (list.Count == 0)
        {
            throw new DomainException(
                "No hay preguntas en los bancos o temas seleccionados.",
                400,
                "no_questions");
        }

        list = PickQuestions(list, config, take);
        if (list.Count < take)
        {
            throw new DomainException(
                $"Hay {list.Count} preguntas con esos bancos/temas; necesitas al menos {take}.",
                400,
                "no_questions");
        }

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
        // Exam mode: never push correct answers to students over SignalR.
        var includeCorrect = session.Mode is not LiveSessionModes.Exam;
        var payload = BuildCurrentQuestion(session, includeCorrect);
        await _broadcast.RevealUpdatedAsync(session.Id, payload!, ct);
        if (!includeCorrect)
        {
            await BroadcastLobbyAsync(session, ct);
        }
    }

    private async Task BroadcastRankingAsync(LiveSession session, CancellationToken ct)
    {
        if (!ShouldIncludeRanking(session, ReadConfig(session)))
        {
            return;
        }

        var ranking = await BuildRankingAsync(session, myParticipantId: null, ct);
        await _broadcast.RankingUpdatedAsync(session.Id, ranking, ct);
    }

    private async Task BroadcastDoubtsAsync(LiveSession session, CancellationToken ct)
    {
        var list = await MapDoubtsAsync(session, viewerParticipantId: null, ct);
        await _broadcast.DoubtsUpdatedAsync(session.Id, list, ct);
    }

    private async Task<LiveLobbyDto> ToLobbyAsync(
        LiveSession session,
        string publicBaseUrl,
        bool includeCorrect,
        CancellationToken ct,
        int? myParticipantId = null)
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

        var joinUrl = BuildJoinUrl(publicBaseUrl, session.JoinCode);
        LiveRankingDto? ranking = null;
        if (ShouldIncludeRanking(session, config))
        {
            ranking = await BuildRankingAsync(session, myParticipantId, ct);
        }

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
            joinUrl,
            ranking);
    }

    private async Task<LiveRankingDto> BuildRankingAsync(
        LiveSession session,
        int? myParticipantId,
        CancellationToken ct)
    {
        var config = ReadConfig(session);
        var answers = await _store.ListAnswersForSessionAsync(session.Id, ct);
        var byParticipant = answers
            .GroupBy(a => a.ParticipantId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Score: g.Sum(a => a.Points),
                    CorrectCount: g.Count(a => a.IsCorrect),
                    AnswerCount: g.Count()));

        var ranked = session.Participants
            .Select(p =>
            {
                byParticipant.TryGetValue(p.Id, out var stats);
                return new
                {
                    Participant = p,
                    stats.Score,
                    stats.CorrectCount,
                    stats.AnswerCount
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.CorrectCount)
            .ThenBy(x => x.Participant.JoinedAt)
            .Select((x, index) => new LiveRankEntryDto(
                index + 1,
                x.Participant.Id,
                config.AnonymousNames
                    ? $"Jugador {x.Participant.Id}"
                    : x.Participant.DisplayName,
                x.Score,
                x.CorrectCount,
                x.AnswerCount))
            .ToList();

        int? myRank = null;
        int? myScore = null;
        if (myParticipantId is int mine)
        {
            var mineEntry = ranked.FirstOrDefault(e => e.ParticipantId == mine);
            if (mineEntry is not null)
            {
                myRank = mineEntry.Rank;
                myScore = mineEntry.Score;
            }
        }

        return new LiveRankingDto(
            ranked.Take(5).ToList(),
            myParticipantId,
            myRank,
            myScore);
    }

    private async Task<IReadOnlyList<LiveDoubtDto>> MapDoubtsAsync(
        LiveSession session,
        int? viewerParticipantId,
        CancellationToken ct)
    {
        var config = ReadConfig(session);
        var doubts = await _store.ListDoubtsAsync(session.Id, ct);
        var names = session.Participants.ToDictionary(p => p.Id, p => p.DisplayName);

        return doubts.Select(d =>
        {
            var author = names.TryGetValue(d.ParticipantId, out var name)
                ? (config.AnonymousNames ? $"Jugador {d.ParticipantId}" : name)
                : "Participante";
            var votedByMe = viewerParticipantId is int viewer
                && d.Votes.Any(v => v.ParticipantId == viewer);

            return new LiveDoubtDto(
                d.Id,
                d.ParticipantId,
                author,
                d.Text,
                d.VoteCount,
                d.IsResolved,
                votedByMe,
                d.CreatedAt);
        }).ToList();
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
            session.RevealCorrect && includeCorrect,
            q.IsSurprise);
    }

    private async Task<LiveParticipant> RequireParticipantInSessionAsync(
        Guid participantToken,
        int sessionId,
        CancellationToken ct)
    {
        var participant = await _store.GetParticipantByTokenAsync(participantToken, ct)
            ?? throw new ForbiddenException("Invalid participant.", "invalid_participant");
        if (participant.SessionId != sessionId)
        {
            throw new ForbiddenException("Participant not in session.", "invalid_participant");
        }

        return participant;
    }

    private static bool ShouldIncludeRanking(LiveSession session, LiveSessionConfig config) =>
        config.ShowRanking
        || session.Mode == LiveSessionModes.Competitive
        || session.Status == LiveSessionStatuses.Ended;

    private static string BuildJoinUrl(string publicBaseUrl, string joinCode) =>
        string.IsNullOrWhiteSpace(publicBaseUrl)
            ? $"/live/join/{joinCode}"
            : $"{publicBaseUrl.TrimEnd('/')}/live/join/{joinCode}";

    private async Task<IReadOnlyList<int>> ResolveBankIdsAsync(
        CreateLiveSessionRequest request,
        CancellationToken ct)
    {
        var ids = new List<int>();
        if (request.BankIds is { Count: > 0 })
        {
            ids.AddRange(request.BankIds.Where(id => id > 0).Distinct());
        }
        else if (request.BankId is int bankId && bankId > 0)
        {
            ids.Add(bankId);
        }

        if (ids.Count == 0)
        {
            var fallback = await ResolveDefaultBankIdAsync(ct);
            if (fallback is null)
            {
                throw new DomainException(
                    "No hay bancos de preguntas disponibles.",
                    400,
                    "bank_required");
            }

            ids.Add(fallback.Value);
        }

        foreach (var id in ids)
        {
            var bank = await _catalog.GetBankAsync(id, ct)
                ?? throw new NotFoundException("Bank not found.", "bank_not_found");
            if (!bank.IsActive)
            {
                throw new DomainException(
                    $"El banco «{bank.Name}» no está activo.",
                    400,
                    "bank_inactive");
            }
        }

        return ids;
    }

    private async Task<List<Question>> LoadQuestionPoolAsync(
        LiveSession session,
        LiveSessionConfig config,
        CancellationToken ct)
    {
        var bankIds = config.BankIds is { Count: > 0 }
            ? config.BankIds
            : [session.BankId];

        var pool = new List<Question>();
        foreach (var bankId in bankIds.Distinct())
        {
            pool.AddRange(await _catalog.ListActiveQuestionsInBankAsync(bankId, ct));
        }

        IEnumerable<Question> filtered = pool
            .GroupBy(q => q.Id)
            .Select(g => g.First());

        filtered = filtered.Where(q => MatchesSelectedThemes(q, config));
        filtered = filtered.Where(q => MatchesSelectedDifficulties(q, config));

        return filtered.ToList();
    }

    private static List<Question> PickQuestions(
        List<Question> pool,
        LiveSessionConfig config,
        int take)
    {
        if (config.BankQuestionQuotas is { Count: > 0 } quotas)
        {
            var picked = new List<Question>();
            var usedIds = new HashSet<int>();
            foreach (var (bankId, quota) in quotas.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key))
            {
                var bankPool = pool.Where(q => q.BankId == bankId).ToList();
                if (config.Randomize)
                {
                    bankPool = bankPool.OrderBy(_ => Guid.NewGuid()).ToList();
                }

                foreach (var question in bankPool.Take(quota))
                {
                    if (usedIds.Add(question.Id))
                    {
                        picked.Add(question);
                    }
                }
            }

            var remaining = take - picked.Count;
            if (remaining > 0)
            {
                var rest = pool.Where(q => !usedIds.Contains(q.Id)).ToList();
                if (config.Randomize)
                {
                    rest = rest.OrderBy(_ => Guid.NewGuid()).ToList();
                }

                foreach (var question in rest.Take(remaining))
                {
                    if (usedIds.Add(question.Id))
                    {
                        picked.Add(question);
                    }
                }
            }

            if (config.Randomize)
            {
                picked = picked.OrderBy(_ => Guid.NewGuid()).ToList();
            }

            return picked.Take(take).ToList();
        }

        if (config.Randomize)
        {
            pool = pool.OrderBy(_ => Guid.NewGuid()).ToList();
        }

        return pool.Take(take).ToList();
    }

    private static bool MatchesSelectedDifficulties(Question question, LiveSessionConfig config)
    {
        var filters = ResolveDifficultyFilters(config);
        if (filters.Count == 0)
        {
            return true;
        }

        var difficulty = string.IsNullOrWhiteSpace(question.Difficulty)
            ? "Sin nivel"
            : question.Difficulty.Trim();
        return filters.Contains(difficulty);
    }

    private static HashSet<string> ResolveDifficultyFilters(LiveSessionConfig config)
    {
        var filters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (config.DifficultyFilters is { Count: > 0 })
        {
            foreach (var difficulty in config.DifficultyFilters)
            {
                if (!string.IsNullOrWhiteSpace(difficulty))
                {
                    filters.Add(difficulty.Trim());
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(config.DifficultyFilter))
        {
            filters.Add(config.DifficultyFilter.Trim());
        }

        return filters;
    }

    private static bool MatchesSelectedThemes(Question question, LiveSessionConfig config)
    {
        if (config.BankTopicFilters is { Count: > 0 }
            && config.BankTopicFilters.TryGetValue(question.BankId, out var bankThemes)
            && bankThemes is { Count: > 0 })
        {
            var names = new HashSet<string>(
                bankThemes.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()),
                StringComparer.OrdinalIgnoreCase);
            return names.Count == 0 || MatchesTheme(question, names);
        }

        var topics = ResolveTopicFilters(config);
        return topics.Count == 0 || MatchesTheme(question, topics);
    }

    private static HashSet<string> ResolveTopicFilters(LiveSessionConfig config)
    {
        var topics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (config.TopicFilters is { Count: > 0 })
        {
            foreach (var topic in config.TopicFilters)
            {
                if (!string.IsNullOrWhiteSpace(topic))
                {
                    topics.Add(topic.Trim());
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(config.TopicFilter))
        {
            topics.Add(config.TopicFilter.Trim());
        }

        return topics;
    }

    private static bool MatchesTheme(Question question, HashSet<string> themes) =>
        themes.Contains(NormalizeTheme(question.Topic))
        || themes.Contains(NormalizeTheme(question.Subject))
        || themes.Contains(NormalizeTheme(question.Subtopic));

    private static string NormalizeTheme(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "General" : value.Trim();

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

        LiveSessionConfig config = new()
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
            CaleStandardPreset = dto.CaleStandardPreset
        };

        if (dto.CaleStandardPreset)
        {
            config.QuestionCount = 25;
            config.SecondsPerQuestion = 72;
            config.Randomize = true;
            config.ShuffleOptions = true;
            config.FeedbackTiming = "end";
        }

        if (dto.BankIds is { Count: > 0 })
        {
            config.BankIds = dto.BankIds.Where(id => id > 0).Distinct().ToList();
        }

        if (dto.TopicFilters is { Count: > 0 })
        {
            config.TopicFilters = dto.TopicFilters
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else if (!string.IsNullOrWhiteSpace(dto.TopicFilter))
        {
            config.TopicFilters = [dto.TopicFilter.Trim()];
        }

        if (config.TopicFilters.Count == 1)
        {
            config.TopicFilter = config.TopicFilters[0];
        }

        if (dto.BankTopicFilters is { Count: > 0 })
        {
            config.BankTopicFilters = dto.BankTopicFilters.ToDictionary(
                kv => kv.Key,
                kv => (kv.Value ?? [])
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList());
        }

        if (dto.BankQuestionQuotas is { Count: > 0 })
        {
            config.BankQuestionQuotas = dto.BankQuestionQuotas
                .Where(kv => kv.Key > 0 && kv.Value > 0)
                .ToDictionary(kv => kv.Key, kv => Math.Clamp(kv.Value, 1, 100));
        }

        if (dto.DifficultyFilters is { Count: > 0 })
        {
            config.DifficultyFilters = dto.DifficultyFilters
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (config.DifficultyFilters.Count == 1)
            {
                config.DifficultyFilter = config.DifficultyFilters[0];
            }
        }

        return config;
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
            c.CaleStandardPreset,
            c.BankIds.Count > 0 ? c.BankIds : null,
            c.TopicFilters.Count > 0 ? c.TopicFilters : null,
            c.BankTopicFilters.Count > 0 ? c.BankTopicFilters : null,
            c.BankQuestionQuotas.Count > 0 ? c.BankQuestionQuotas : null,
            c.DifficultyFilters.Count > 0 ? c.DifficultyFilters : null);

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
