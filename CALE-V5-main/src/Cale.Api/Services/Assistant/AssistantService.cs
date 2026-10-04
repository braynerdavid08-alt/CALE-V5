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

public sealed record AssistantChatResult(string Reply, AssistantPendingActionDto? Action, int RemainingToday);

public sealed record AssistantActionResult(bool Ok, string Message);

public sealed record AssistantStatusDto(bool Enabled, int RemainingToday);

/// <summary>
/// Chat orchestration: role-scoped tools, a confirmation step before any change,
/// and daily caps that keep usage inside free provider tiers.
/// </summary>
public sealed class AssistantService
{
    private const int MaxHistory = 10;
    private const int MaxMessageChars = 1000;
    private const int MaxToolResultChars = 6000;
    private static readonly TimeSpan ActionLifetime = TimeSpan.FromMinutes(15);
    private static readonly string[] DayNames = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    private readonly IAssistantLlm _llm;
    private readonly IAssistantToolbox _toolbox;
    private readonly IMemoryCache _cache;
    private readonly AssistantOptions _options;
    private readonly ILogger<AssistantService> _logger;

    public AssistantService(
        IAssistantLlm llm,
        IAssistantToolbox toolbox,
        IMemoryCache cache,
        IOptions<AssistantOptions> options,
        ILogger<AssistantService> logger)
    {
        _llm = llm;
        _toolbox = toolbox;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public static bool SupportsRole(string role) => role is Roles.Student or Roles.School;

    public AssistantStatusDto Status(AssistantUser user)
    {
        var enabled = _options.IsConfigured && SupportsRole(user.Role);
        return new AssistantStatusDto(enabled, enabled ? RemainingFor(user.UserId) : 0);
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

        if (!TryConsume(UserKey(user.UserId), _options.DailyMessagesPerUser))
        {
            return new AssistantChatResult(
                "Ya usaste todos tus mensajes del asistente por hoy. Mañana puedes volver a escribir.",
                null,
                0);
        }

        var messages = new JsonArray { Msg("system", SystemPrompt(user)) };
        foreach (var t in turns)
        {
            var content = t.Content.Length > MaxMessageChars ? t.Content[..MaxMessageChars] : t.Content;
            messages.Add(Msg(t.Role, content));
        }

        var tools = BuildTools(user.Role);
        AssistantPendingActionDto? pending = null;
        try
        {
            for (var round = 0; round <= _options.MaxToolRounds; round++)
            {
                if (!TryConsume(UserCallsKey(user.UserId), _options.DailyRequestsPerUser)
                    || !TryConsume(SchoolKey(user.SchoolUserId ?? user.UserId), _options.DailyRequestsPerSchool)
                    || !TryConsume(BudgetKey(), _options.DailyRequestBudget))
                {
                    return new AssistantChatResult(
                        "El asistente llegó a su límite de hoy. Puedes seguir usando las pantallas normales o intentar mañana.",
                        pending,
                        RemainingFor(user.UserId));
                }

                var lastRound = round == _options.MaxToolRounds;
                var reply = await _llm.CompleteAsync(messages, lastRound ? new JsonArray() : tools, ct);
                if (reply.ToolCalls.Count == 0)
                {
                    var text = string.IsNullOrWhiteSpace(reply.Content)
                        ? (pending is null ? "No entendí bien. ¿Me lo puedes decir de otra forma?" : "Revisa el botón de abajo y confirma si está bien.")
                        : reply.Content.Trim();
                    return new AssistantChatResult(text, pending, RemainingFor(user.UserId));
                }

                messages.Add(AssistantToolCallMessage(reply));
                foreach (var call in reply.ToolCalls)
                {
                    var (result, action) = await RunToolAsync(user, call, pending is not null, ct);
                    pending ??= action;
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
            return new AssistantChatResult(ex.Message, pending, RemainingFor(user.UserId));
        }

        return new AssistantChatResult("No pude terminar esa consulta. Intenta preguntarlo más corto.", pending, RemainingFor(user.UserId));
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
            var text = await _toolbox.ReadAsync(user, quick.Tool, quick.Args(), ct);
            var reply = CleanForUser(text) + "\n\nSi quieres reservar, cancelar o cambiar algo, escríbeme.";
            return new AssistantChatResult(reply, null, RemainingFor(user.UserId));
        }
        catch (Exception ex) when (ex is DomainException or AssistantToolException)
        {
            return new AssistantChatResult(ex.Message, null, RemainingFor(user.UserId));
        }
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

    private async Task<(string Result, AssistantPendingActionDto? Action)> RunToolAsync(
        AssistantUser user,
        LlmToolCall call,
        bool alreadyPending,
        CancellationToken ct)
    {
        var def = _toolbox.ToolsFor(user.Role).FirstOrDefault(x => x.Name == call.Name);
        if (def is null)
        {
            return ($"ERROR: la herramienta {call.Name} no existe.", null);
        }

        JsonObject args;
        try
        {
            args = JsonNode.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson) as JsonObject
                ?? new JsonObject();
        }
        catch (JsonException)
        {
            return ("ERROR: argumentos inválidos.", null);
        }

        try
        {
            if (!def.RequiresConfirmation)
            {
                return (Truncate(await _toolbox.ReadAsync(user, call.Name, args, ct)), null);
            }

            if (alreadyPending)
            {
                return ("NO PREPARADA: solo se puede preparar una acción a la vez. Pide que confirme la primera.", null);
            }

            var title = await _toolbox.DescribeAsync(user, call.Name, args, ct);
            var id = Guid.NewGuid().ToString("N");
            _cache.Set(
                ActionKey(id),
                new PendingAction(user.UserId, call.Name, args.ToJsonString(), title),
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ActionLifetime, Size = 1 });
            return (
                $"PREPARADA (aún NO está hecha): \"{title}\". El usuario ve un botón para confirmar. Pídele que revise y confirme; no digas que ya quedó hecha.",
                new AssistantPendingActionDto(id, title));
        }
        catch (AssistantToolException ex)
        {
            return ($"ERROR: {ex.Message}", null);
        }
        catch (DomainException ex)
        {
            return ($"NO SE PUEDE: {ex.Message}", null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Assistant tool {Tool} failed for user {UserId}", call.Name, user.UserId);
            return ("ERROR: no se pudo consultar ese dato ahora.", null);
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
            - Usa las herramientas para consultar datos. Nunca inventes horarios, cupos, horas, saldos ni nombres.
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

    private int RemainingFor(int userId)
    {
        var used = _cache.TryGetValue(UserKey(userId), out Counter? c) && c is not null ? c.Value : 0;
        return Math.Max(0, _options.DailyMessagesPerUser - used);
    }

    private bool TryConsume(string key, int limit)
    {
        var counter = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(26);
            entry.Size = 1;
            return new Counter();
        })!;
        if (Interlocked.Increment(ref counter.Value) <= limit)
        {
            return true;
        }

        Interlocked.Decrement(ref counter.Value);
        return false;
    }

    private static string Today() => ColombiaTime.TodayInColombia().ToString("yyyyMMdd");

    private static string UserKey(int userId) => $"assistant:user:{Today()}:{userId}";

    private static string BudgetKey() => $"assistant:budget:{Today()}";

    private static string SchoolKey(int schoolUserId) => $"assistant:school:{Today()}:{schoolUserId}";

    private static string UserCallsKey(int userId) => $"assistant:calls:{Today()}:{userId}";

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

    private sealed class Counter
    {
        public int Value;
    }
}
