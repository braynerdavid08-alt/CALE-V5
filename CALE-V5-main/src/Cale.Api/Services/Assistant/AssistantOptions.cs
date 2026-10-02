namespace Cale.Api.Services.Assistant;

/// <summary>
/// "Assistant" config section. Any OpenAI-compatible chat-completions endpoint works
/// (GitHub Models by default; Gemini, Groq or OpenAI by changing Endpoint/Model/ApiKey).
/// The assistant stays off until ApiKey is set.
/// </summary>
public sealed class AssistantOptions
{
    public const string Section = "Assistant";

    public bool Enabled { get; set; } = true;
    public string Endpoint { get; set; } = "https://models.github.ai/inference/chat/completions";
    public string Model { get; set; } = "openai/gpt-4o-mini";
    public string? ApiKey { get; set; }
    public string AppName { get; set; } = "Luz Verde";

    /// <summary>Messages a single user may send per Colombia day.</summary>
    public int DailyMessagesPerUser { get; set; } = 20;

    /// <summary>Provider calls for the whole app per day (free tiers cap requests, not users).</summary>
    public int DailyRequestBudget { get; set; } = 140;

    public int MaxToolRounds { get; set; } = 4;
    public int MaxOutputTokens { get; set; } = 600;
    public int TimeoutSeconds { get; set; } = 40;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
