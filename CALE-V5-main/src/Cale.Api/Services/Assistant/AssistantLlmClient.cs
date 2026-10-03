using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Cale.Api.Services.Assistant;

public sealed record LlmToolCall(string Id, string Name, string ArgumentsJson);

public sealed record LlmReply(string? Content, IReadOnlyList<LlmToolCall> ToolCalls);

public sealed class AssistantProviderException : Exception
{
    public AssistantProviderException(string message, bool rateLimited = false)
        : base(message) => RateLimited = rateLimited;

    public bool RateLimited { get; }
}

public interface IAssistantLlm
{
    /// <param name="messages">OpenAI chat format (system/user/assistant/tool).</param>
    /// <param name="tools">OpenAI "tools" array (function definitions).</param>
    Task<LlmReply> CompleteAsync(JsonArray messages, JsonArray tools, CancellationToken ct);
}

/// <summary>Minimal OpenAI-compatible chat-completions client with function calling.</summary>
public sealed class AssistantLlmClient : IAssistantLlm
{
    private readonly HttpClient _http;
    private readonly AssistantOptions _options;
    private readonly ILogger<AssistantLlmClient> _logger;

    public AssistantLlmClient(
        HttpClient http,
        IOptions<AssistantOptions> options,
        ILogger<AssistantLlmClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 10, 120));
    }

    public async Task<LlmReply> CompleteAsync(JsonArray messages, JsonArray tools, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = _options.Model,
            ["messages"] = messages.DeepClone(),
            ["temperature"] = 0.2,
            ["max_tokens"] = _options.MaxOutputTokens
        };
        if (tools.Count > 0)
        {
            body["tools"] = tools.DeepClone();
            body["tool_choice"] = "auto";
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AssistantProviderException("El asistente tardó demasiado en responder.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Assistant provider unreachable");
            throw new AssistantProviderException("No se pudo conectar con el asistente.");
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new AssistantProviderException(
                    "El asistente llegó a su límite gratuito por ahora. Intenta más tarde.",
                    rateLimited: true);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Assistant provider returned {Status}: {Body}",
                    (int)response.StatusCode,
                    text.Length > 500 ? text[..500] : text);
                throw new AssistantProviderException(
                    $"El asistente no está disponible en este momento. (Detalle {(int)response.StatusCode}: {ProviderReason(text)})");
            }

            try
            {
                return Parse(text);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                _logger.LogWarning(
                    ex,
                    "Assistant provider returned unreadable body ({Status}, {ContentType}) from {Endpoint}: {Body}",
                    (int)response.StatusCode,
                    response.Content.Headers.ContentType?.ToString(),
                    _options.Endpoint,
                    text.Length > 500 ? text[..500] : text);
                throw new AssistantProviderException("El asistente no está disponible en este momento.");
            }
        }
    }

    /// <summary>Short provider error text, safe to show (providers never echo the API key).</summary>
    private static string ProviderReason(string body)
    {
        string? reason = null;
        try
        {
            var root = JsonNode.Parse(body);
            var error = (root is JsonArray arr ? arr.FirstOrDefault() : root)?["error"];
            reason = error is JsonValue ? error.GetValue<string>() : error?["message"]?.GetValue<string>();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
        }

        reason = string.IsNullOrWhiteSpace(reason) ? body : reason;
        reason = reason.ReplaceLineEndings(" ").Trim();
        return reason.Length > 180 ? reason[..180] + "…" : reason;
    }

    public static LlmReply Parse(string json)
    {
        var root = JsonNode.Parse(json);
        var message = root?["choices"]?[0]?["message"];
        if (message is null)
        {
            throw new AssistantProviderException("Respuesta vacía del asistente.");
        }

        var calls = new List<LlmToolCall>();
        if (message["tool_calls"] is JsonArray toolCalls)
        {
            foreach (var call in toolCalls)
            {
                var fn = call?["function"];
                var name = fn?["name"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                calls.Add(new LlmToolCall(
                    call?["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
                    name,
                    fn?["arguments"]?.GetValue<string>() ?? "{}"));
            }
        }

        return new LlmReply(message["content"]?.GetValue<string>(), calls);
    }
}
