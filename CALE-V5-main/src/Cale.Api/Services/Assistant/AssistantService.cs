using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.TheoreticalTraining.Application;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Cale.Api.Services.Assistant;

public sealed record AssistantChatMessage(string Role, string Content);

public sealed record AssistantPendingActionDto(string Id, string Title);

/// <summary>Structured exam-slot card for the chat UI (camelCase JSON).</summary>
public sealed record AssistantExamSlotUi(string Time, int AvailableCapacity);

public sealed record AssistantExamDayUi(string Date, IReadOnlyList<AssistantExamSlotUi> AvailableSlots);

public sealed record AssistantExamAvailabilityUi(
    string Type,
    string Title,
    string DateRangeFrom,
    string DateRangeTo,
    bool CanBook,
    string? BlockReason,
    string? MyBookingSummary,
    bool Selectable,
    IReadOnlyList<AssistantExamDayUi> Days);

public sealed record AssistantChatResult(
    string Reply,
    AssistantPendingActionDto? Action,
    int RemainingToday,
    AssistantExamAvailabilityUi? ExamAvailability = null);

public sealed record AssistantActionResult(bool Ok, string Message);

public sealed record AssistantPrepareExamRequest(string Date, string Time);

public sealed record AssistantStatusDto(
    bool Enabled,
    int RemainingToday,
    int LimitToday = 0,
    bool NearLimit = false,
    bool AtLimit = false);

/// <summary>
/// Chat orchestration: role-scoped tools, a confirmation step before any change,
/// and durable daily caps (reserve / commit / release).
/// </summary>
public sealed class AssistantService
{
    private const int MaxHistory = 10;
    private const int MaxMessageChars = 1000;
    private const int MaxToolResultChars = 6000;
    private const string QuotaContactMessage =
        "Ya usaste todos tus mensajes del asistente por hoy. Si tu escuela necesita más, contacta al administrador de Luz Verde para revisar una ampliación. Mañana se restablece la cuota.";
    private static readonly TimeSpan ActionLifetime = TimeSpan.FromMinutes(15);
    private static readonly string[] DayNames = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    private readonly IAssistantLlm _llm;
    private readonly IAssistantToolbox _toolbox;
    private readonly IAssistantUsageStore _usage;
    private readonly IMemoryCache _cache;
    private readonly AssistantOptions _options;
    private readonly ILogger<AssistantService> _logger;

    public AssistantService(
        IAssistantLlm llm,
        IAssistantToolbox toolbox,
        IAssistantUsageStore usage,
        IMemoryCache cache,
        IOptions<AssistantOptions> options,
        ILogger<AssistantService> logger)
    {
        _llm = llm;
        _toolbox = toolbox;
        _usage = usage;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public static bool SupportsRole(string role) => role is Roles.Student or Roles.School;

    public async Task<AssistantStatusDto> StatusAsync(AssistantUser user, CancellationToken ct)
    {
        var enabled = _options.IsConfigured && SupportsRole(user.Role);
        if (!enabled)
        {
            return new AssistantStatusDto(false, 0);
        }

        var snap = await _usage.GetSnapshotAsync(
            user.UserId,
            user.SchoolUserId,
            _options.DailyMessagesPerUser,
            ct);
        return new AssistantStatusDto(
            true,
            snap.MessagesRemaining,
            snap.MessagesLimit,
            snap.NearLimit,
            snap.AtLimit);
    }

    public async Task<AssistantChatResult> ChatAsync(
        AssistantUser user,
        IReadOnlyList<AssistantChatMessage> history,
        CancellationToken ct)
    {
        EnsureEnabled(user);
        var turns = history
            .Where(m => (m.Role is "user" or "assistant") && !string.IsNullOrWhiteSpace(m.Content))
            .TakeLast(MaxHistory)
            .ToList();
        if (turns.Count == 0 || turns[^1].Role != "user")
        {
            throw new DomainException("Escribe tu pregunta.", 400, "assistant_empty");
        }

        var reserved = await _usage.TryReserveMessageAsync(
            user.UserId,
            user.SchoolUserId,
            _options.DailyMessagesPerUser,
            ct);
        if (!reserved)
        {
            return new AssistantChatResult(QuotaContactMessage, null, 0);
        }

        var messages = new JsonArray { Msg("system", SystemPrompt(user)) };
        foreach (var t in turns)
        {
            var content = t.Content.Length > MaxMessageChars ? t.Content[..MaxMessageChars] : t.Content;
            messages.Add(Msg(t.Role, content));
        }

        var tools = BuildTools(user.Role);
        AssistantPendingActionDto? pending = null;
        AssistantExamAvailabilityUi? examUi = null;
        try
        {
            for (var round = 0; round <= _options.MaxToolRounds; round++)
            {
                var requestOk = await _usage.TryReserveProviderRequestAsync(
                    user.UserId,
                    user.SchoolUserId,
                    _options.DailyRequestsPerUser,
                    _options.DailyRequestsPerSchool,
                    _options.DailyRequestBudget,
                    ct);
                if (!requestOk)
                {
                    await _usage.CommitMessageAsync(user.UserId, user.SchoolUserId, ct);
                    return new AssistantChatResult(
                        "El asistente llegó a su límite de hoy. Puedes seguir usando las pantallas normales o contactar al administrador si necesitas más cupo.",
                        pending,
                        await RemainingAsync(user, ct));
                }

                var lastRound = round == _options.MaxToolRounds;
                var reply = await _llm.CompleteAsync(messages, lastRound ? new JsonArray() : tools, ct);
                if (reply.ToolCalls.Count == 0)
                {
                    var text = string.IsNullOrWhiteSpace(reply.Content)
                        ? (pending is null ? "No entendí bien. ¿Me lo puedes decir de otra forma?" : "Revisa el botón de abajo y confirma si está bien.")
                        : reply.Content.Trim();
                    if (examUi is not null)
                    {
                        text = ExamAvailabilityReply(examUi, text);
                    }

                    await _usage.CommitMessageAsync(user.UserId, user.SchoolUserId, ct);
                    return new AssistantChatResult(text, pending, await RemainingAsync(user, ct), examUi);
                }

                messages.Add(AssistantToolCallMessage(reply));
                foreach (var call in reply.ToolCalls)
                {
                    var (result, action, ui) = await RunToolAsync(user, call, pending is not null, ct);
                    pending ??= action;
                    examUi ??= ui;
                    messages.Add(new JsonObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = call.Id,
                        ["content"] = result
                    });
                }
            }
        }
        catch (AssistantProviderException ex)
        {
            await _usage.ReleaseMessageAsync(user.UserId, user.SchoolUserId, "provider_error", ct);
            return new AssistantChatResult(ex.Message, pending, await RemainingAsync(user, ct), examUi);
        }
        catch (Exception ex) when (ex is not DomainException and not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Assistant chat failed after reserve");
            await _usage.ReleaseMessageAsync(user.UserId, user.SchoolUserId, "unexpected_error", ct);
            throw;
        }

        await _usage.CommitMessageAsync(user.UserId, user.SchoolUserId, ct);
        return new AssistantChatResult(
            "No pude terminar esa consulta. Intenta preguntarlo más corto.",
            pending,
            await RemainingAsync(user, ct),
            examUi);
    }

    /// <summary>
    /// Fixed questions answered straight from the database, without the AI provider,
    /// so they cost no messages and keep working when the free tier is saturated.
    /// </summary>
    public async Task<AssistantChatResult> QuickAsync(AssistantUser user, string key, CancellationToken ct)
    {
        EnsureEnabled(user);
        if (!QuickQuestions.TryGetValue((user.Role, key), out var quick))
        {
            throw new DomainException("Esa consulta rápida no existe.", 404, "assistant_quick_unknown");
        }

        try
        {
            var (text, examUi) = await _toolbox.ReadWithUiAsync(user, quick.Tool, quick.Args(), ct);
            if (examUi is not null)
            {
                return new AssistantChatResult(
                    ExamAvailabilityReply(examUi),
                    null,
                    await RemainingAsync(user, ct),
                    examUi);
            }

            var reply = CleanForUser(text) + "\n\nSi quieres reservar, cancelar o cambiar algo, escríbeme.";
            return new AssistantChatResult(reply, null, await RemainingAsync(user, ct));
        }
        catch (Exception ex) when (ex is DomainException or AssistantToolException)
        {
            return new AssistantChatResult(ex.Message, null, await RemainingAsync(user, ct));
        }
    }

    /// <summary>
    /// Student picks a slot in the chat UI; we prepare the same confirmable
    /// <c>agendar_examen</c> action the LLM would create (booking still needs Confirm).
    /// </summary>
    public async Task<AssistantChatResult> PrepareExamBookingAsync(
        AssistantUser user, string date, string time, CancellationToken ct)
    {
        EnsureEnabled(user);
        if (user.Role != Roles.Student)
        {
            throw new DomainException("Solo los estudiantes pueden agendar su examen desde el chat.", 403);
        }

        var args = new JsonObject
        {
            ["fecha"] = date.Trim(),
            ["hora"] = time.Trim()
        };

        try
        {
            var title = await _toolbox.DescribeAsync(user, "agendar_examen", args, ct);
            var id = Guid.NewGuid().ToString("N");
            _cache.Set(
                ActionKey(id),
                new PendingAction(user.UserId, "agendar_examen", args.ToJsonString(), title),
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ActionLifetime, Size = 1 });

            return new AssistantChatResult(
                $"Seleccionaste: {title}\n\nConfirma abajo. Volveremos a validar el cupo al confirmar.",
                new AssistantPendingActionDto(id, title),
                await RemainingAsync(user, ct));
        }
        catch (Exception ex) when (ex is DomainException or AssistantToolException)
        {
            return new AssistantChatResult(ex.Message, null, await RemainingAsync(user, ct));
        }
    }

    private static string ExamAvailabilityReply(AssistantExamAvailabilityUi ui, string? llmHint = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(ui.Title + ".");
        sb.AppendLine($"Rango consultado: {ui.DateRangeFrom} a {ui.DateRangeTo}.");
        if (!string.IsNullOrWhiteSpace(ui.BlockReason))
        {
            sb.AppendLine(ui.BlockReason);
        }

        if (!string.IsNullOrWhiteSpace(ui.MyBookingSummary))
        {
            sb.AppendLine($"Cita actual: {ui.MyBookingSummary}.");
        }

        if (ui.Days.Count == 0)
        {
            sb.AppendLine("No hay cupos libres en ese rango.");
        }
        else
        {
            sb.AppendLine(
                ui.Selectable
                    ? "Toca un horario para preparar la reserva (aún no se agenda hasta que confirmes)."
                    : "Los horarios aparecen en las tarjetas de abajo.");
        }

        if (!string.IsNullOrWhiteSpace(llmHint)
            && llmHint.Length < 280
            && !llmHint.Contains(':')
            && llmHint.IndexOf("cupo", StringComparison.OrdinalIgnoreCase) < 0)
        {
            sb.AppendLine();
            sb.Append(llmHint.Trim());
        }

        return sb.ToString().Trim();
    }

    private sealed record QuickQuestion(string Tool, Func<JsonObject> Args);

    private static readonly Dictionary<(string Role, string Key), QuickQuestion> QuickQuestions = new()
    {
        [(Roles.Student, "progreso")] = new("mi_progreso", () => new JsonObject()),
        [(Roles.Student, "clases_teoricas")] = new("clases_teoricas", () => new JsonObject()),
        [(Roles.Student, "cupos_examen")] = new("cupos_examen", () => new JsonObject()),
        [(Roles.Student, "clases_manejo")] = new("clases_manejo", () => new JsonObject()),
        [(Roles.School, "resumen")] = new("resumen_escuela", () => new JsonObject()),
        [(Roles.School, "listos_examen")] = new("progreso_estudiantes", () => new JsonObject { ["filtro"] = "listos_examen" }),
        [(Roles.School, "agenda")] = new("agenda_escuela", () => new JsonObject()),
        [(Roles.School, "con_saldo")] = new("progreso_estudiantes", () => new JsonObject { ["filtro"] = "con_saldo" })
    };

    /// <summary>Tool text is written for the model; hide internal ids and key=value noise.</summary>
    public static string CleanForUser(string text)
    {
        var s = Regex.Replace(text, @"\s*\((?:\w+_id)=\d+\)", "");
        s = Regex.Replace(s, @"(?m)^\w+_id=\d+\s*\|\s*", "");
        s = Regex.Replace(s, @"\s*\|?\s*YA RESERVADA \w+_id=\d+", " | ya la tienes reservada");
        s = Regex.Replace(s, @"\s*\w+_id=\d+", "");
        s = Regex.Replace(s, @"(\p{L}[\p{L} ]*?)=(sí|no)\b", "$1: $2");
        return s.Trim();
    }

    public async Task<AssistantActionResult> ConfirmAsync(AssistantUser user, string actionId, CancellationToken ct)
    {
        EnsureEnabled(user);
        var key = ActionKey(actionId);
        if (!_cache.TryGetValue(key, out PendingAction? action) || action is null || action.UserId != user.UserId)
        {
            return new AssistantActionResult(false, "Esa acción ya no está disponible. Pídela de nuevo al asistente.");
        }

        _cache.Remove(key);
        try
        {
            var args = JsonNode.Parse(action.ArgsJson) as JsonObject ?? new JsonObject();
            var message = await _toolbox.ExecuteAsync(user, action.Tool, args, ct);
            return new AssistantActionResult(true, message);
        }
        catch (DomainException ex)
        {
            return new AssistantActionResult(false, $"No se pudo: {ex.Message}");
        }
        catch (AssistantToolException ex)
        {
            return new AssistantActionResult(false, $"No se pudo: {ex.Message}");
        }
    }

    public void Discard(AssistantUser user, string actionId)
    {
        var key = ActionKey(actionId);
        if (_cache.TryGetValue(key, out PendingAction? action) && action?.UserId == user.UserId)
        {
            _cache.Remove(key);
        }
    }

    private async Task<(string Result, AssistantPendingActionDto? Action, AssistantExamAvailabilityUi? ExamUi)> RunToolAsync(
        AssistantUser user,
        LlmToolCall call,
        bool alreadyPending,
        CancellationToken ct)
    {
        var def = _toolbox.ToolsFor(user.Role).FirstOrDefault(x => x.Name == call.Name);
        if (def is null)
        {
            return ($"ERROR: la herramienta {call.Name} no existe.", null, null);
        }

        JsonObject args;
        try
        {
            args = JsonNode.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson) as JsonObject
                ?? new JsonObject();
        }
        catch (JsonException)
        {
            return ("ERROR: argumentos inválidos.", null, null);
        }

        try
        {
            if (!def.RequiresConfirmation)
            {
                var (text, ui) = await _toolbox.ReadWithUiAsync(user, call.Name, args, ct);
                return (Truncate(text), null, ui);
            }

            if (alreadyPending)
            {
                return ("NO PREPARADA: solo se puede preparar una acción a la vez. Pide que confirme la primera.", null, null);
            }

            var title = await _toolbox.DescribeAsync(user, call.Name, args, ct);
            var id = Guid.NewGuid().ToString("N");
            _cache.Set(
                ActionKey(id),
                new PendingAction(user.UserId, call.Name, args.ToJsonString(), title),
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ActionLifetime, Size = 1 });
            return (
                $"PREPARADA (aún NO está hecha): \"{title}\". El usuario ve un botón para confirmar. Pídele que revise y confirme; no digas que ya quedó hecha.",
                new AssistantPendingActionDto(id, title),
                null);
        }
        catch (AssistantToolException ex)
        {
            return ($"ERROR: {ex.Message}", null, null);
        }
        catch (DomainException ex)
        {
            return ($"NO SE PUEDE: {ex.Message}", null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Assistant tool {Tool} failed for user {UserId}", call.Name, user.UserId);
            return ("ERROR: no se pudo consultar ese dato ahora.", null, null);
        }
    }

    private JsonArray BuildTools(string role)
    {
        var tools = new JsonArray();
        foreach (var t in _toolbox.ToolsFor(role))
        {
            var function = new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description
            };

            // Gemini rejects object schemas with no properties; parameters is optional in the OpenAI format.
            if (t.Parameters["properties"] is JsonObject { Count: > 0 })
            {
                var parameters = (JsonObject)t.Parameters.DeepClone();
                if (parameters["required"] is JsonArray { Count: 0 })
                {
                    parameters.Remove("required");
                }

                function["parameters"] = parameters;
            }

            tools.Add(new JsonObject { ["type"] = "function", ["function"] = function });
        }

        return tools;
    }

    private string SystemPrompt(AssistantUser user)
    {
        var today = ColombiaTime.TodayInColombia();
        var who = user.Role == Roles.School
            ? "la escuela de conducción (personal administrativo, muchas veces personas mayores)"
            : "un estudiante de la escuela de conducción";
        return $"""
            Eres el asistente de {_options.AppName}, la app de formación vial. Hablas con {user.Name}, {who}.
            Hoy es {DayNames[(int)today.DayOfWeek]} {today:yyyy-MM-dd}, hora de Colombia.
            Reglas:
            - Responde en español de Colombia, con frases cortas, claras y amables. Sin tecnicismos ni identificadores internos (no muestres *_id).
            - Formato: una idea por línea. Cuando digas varios datos (horas, saldos, citas, cupos), usa viñetas con "•" y títulos cortos. Nunca amontones todo en un solo párrafo.
            - Usa las herramientas para consultar datos. Nunca inventes horarios, cupos, horas, saldos ni nombres. Si la herramienta ya viene ordenada, respeta esa estructura y solo aclara si hace falta.
            - Para reservar, cancelar, agendar, asignar o autorizar usa la herramienta correspondiente: eso solo PREPARA la acción y el usuario debe tocar "Confirmar". Nunca digas que ya quedó hecho.
            - Si algo no se puede (sin cupo, saldo pendiente, falta autorización), explica el motivo que da la herramienta y qué puede hacer.
            - Si faltan datos (fecha, hora, qué clase), pregunta antes de preparar la acción.
            - Solo ayudas con temas de la escuela y de la app. Si no puedes resolver algo, sugiere hablar con la escuela.
            """;
    }

    private void EnsureEnabled(AssistantUser user)
    {
        if (!_options.IsConfigured)
        {
            throw new DomainException("El asistente no está activado.", 503, "assistant_disabled");
        }

        if (!SupportsRole(user.Role))
        {
            throw new ForbiddenException("El asistente es para estudiantes y escuelas.", "assistant_role");
        }
    }

    private async Task<int> RemainingAsync(AssistantUser user, CancellationToken ct)
    {
        var snap = await _usage.GetSnapshotAsync(
            user.UserId,
            user.SchoolUserId,
            _options.DailyMessagesPerUser,
            ct);
        return snap.MessagesRemaining;
    }

    private static string ActionKey(string id) => $"assistant:action:{id}";

    private static string Truncate(string text) =>
        text.Length <= MaxToolResultChars ? text : text[..MaxToolResultChars] + "\n(… recortado)";

    private static JsonObject Msg(string role, string content) => new() { ["role"] = role, ["content"] = content };

    private static JsonObject AssistantToolCallMessage(LlmReply reply)
    {
        var calls = new JsonArray();
        foreach (var c in reply.ToolCalls)
        {
            var call = new JsonObject
            {
                ["id"] = c.Id,
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.ArgumentsJson }
            };
            if (c.ExtraContent is not null)
            {
                call["extra_content"] = c.ExtraContent.DeepClone();
            }

            calls.Add(call);
        }

        return new JsonObject { ["role"] = "assistant", ["content"] = reply.Content, ["tool_calls"] = calls };
    }

    private sealed record PendingAction(int UserId, string Tool, string ArgsJson, string Title);
}
