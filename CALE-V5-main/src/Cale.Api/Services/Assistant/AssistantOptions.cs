namespace Cale.Api.Services.Assistant;

/// <summary>
/// "Assistant" config section. Any OpenAI-compatible chat-completions endpoint works
/// (GitHub Models by default; Gemini, Groq or OpenAI by changing Endpoint/Model/ApiKey).
/// The assistant stays off until ApiKey is set.
/// </summary>
/// <remarks>
/// Gemini (Render típico):
/// <c>Assistant__Endpoint=https://generativelanguage.googleapis.com/v1beta/openai/chat/completions</c>
/// <c>Assistant__Model=gemini-3.5-flash-lite</c> (rápido; evita que el chat se quede en “Pensando…”)
/// <c>Assistant__ApiKey=...</c>
/// No uses <c>openai/gpt-4o-mini</c> con Gemini: responde 404 generateContent.
/// Evita <c>gemini-3.8-flash</c> en chat con herramientas: piensa demasiado y parece colgado.
/// </remarks>
public sealed class AssistantOptions
{
    public const string Section = "Assistant";

    /// <summary>Fast Gemini for chat + tools (OpenAI-compatible endpoint).</summary>
    public const string DefaultGeminiModel = "gemini-3.5-flash-lite";

    /// <summary>When true, heavy Gemini 3.x Flash ids are remapped to <see cref="DefaultGeminiModel"/>.</summary>
    public bool PreferFastChat { get; set; } = true;

    public bool Enabled { get; set; } = true;
    public string Endpoint { get; set; } = "https://models.github.ai/inference/chat/completions";
    public string Model { get; set; } = "openai/gpt-4o-mini";
    public string? ApiKey { get; set; }
    public string AppName { get; set; } = "Luz Verde";

    /// <summary>Messages a single user may send per Colombia day.</summary>
    public int DailyMessagesPerUser { get; set; } = 20;

    /// <summary>Provider calls for the whole app per day (free tiers cap requests, not users).</summary>
    public int DailyRequestBudget { get; set; } = 140;

    /// <summary>Provider calls one school (its staff and students together) may spend per day.</summary>
    public int DailyRequestsPerSchool { get; set; } = 60;

    /// <summary>Provider calls one user may spend per day (each message can take several tool rounds).</summary>
    public int DailyRequestsPerUser { get; set; } = 40;

    public int MaxToolRounds { get; set; } = 4;
    public int MaxOutputTokens { get; set; } = 600;
    public int TimeoutSeconds { get; set; } = 55;

    /// <summary>
    /// OpenAI-compat <c>reasoning_effort</c> for Gemini thinking models (low/medium/high).
    /// Keep <c>low</c> for chat latency; 3.x Flash cannot fully disable thinking.
    /// </summary>
    public string ReasoningEffort { get; set; } = "low";

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);

    public bool LooksLikeGeminiEndpoint =>
        Endpoint.Contains("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase)
        || Endpoint.Contains("aiplatform.googleapis.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Fix common Render misconfig: Gemini endpoint + GitHub/OpenAI model id → 404 generateContent.
    /// </summary>
    public void NormalizeProviderPairing()
    {
        Endpoint = (Endpoint ?? "").Trim();
        Model = (Model ?? "").Trim();
        if (string.IsNullOrWhiteSpace(Endpoint) || string.IsNullOrWhiteSpace(Model))
        {
            return;
        }

        if (!LooksLikeGeminiEndpoint)
        {
            return;
        }

        // GitHub Models style ("openai/gpt-4o-mini") or plain OpenAI ids do not work on Gemini.
        var openaiStyle =
            Model.Contains('/', StringComparison.Ordinal)
            || Model.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase)
            || Model.StartsWith("o1", StringComparison.OrdinalIgnoreCase)
            || Model.StartsWith("chatgpt", StringComparison.OrdinalIgnoreCase);
        if (openaiStyle && !Model.StartsWith("gemini", StringComparison.OrdinalIgnoreCase))
        {
            Model = DefaultGeminiModel;
        }

        // 3.x Flash “thinking” makes multi-tool school/student chats hang on mobile.
        if (PreferFastChat && IsHeavyThinkingGemini(Model))
        {
            Model = DefaultGeminiModel;
            // Flash-Lite accepts "none"; keeps chat usable.
            ReasoningEffort = "none";
        }
    }

    private static bool IsHeavyThinkingGemini(string model) =>
        model.Equals("gemini-3.8-flash", StringComparison.OrdinalIgnoreCase)
        || model.Equals("gemini-3.6-flash", StringComparison.OrdinalIgnoreCase)
        || model.Equals("gemini-3.7-flash", StringComparison.OrdinalIgnoreCase)
        || model.Equals("gemini-3-flash", StringComparison.OrdinalIgnoreCase)
        || model.Equals("gemini-3-flash-preview", StringComparison.OrdinalIgnoreCase)
        // Retired for many new Google AI Studio keys — remap to DefaultGeminiModel.
        || model.Equals("gemini-2.5-flash-lite", StringComparison.OrdinalIgnoreCase)
        || model.Equals("gemini-2.5-flash", StringComparison.OrdinalIgnoreCase)
        || model.Equals("gemini-2.0-flash-lite", StringComparison.OrdinalIgnoreCase)
        || model.Equals("gemini-2.0-flash", StringComparison.OrdinalIgnoreCase);
}
