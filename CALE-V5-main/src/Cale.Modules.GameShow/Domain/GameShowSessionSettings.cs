using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cale.Modules.GameShow.Domain;

/// <summary>
/// Snapshot / global configuration for 100 Estudiantes Dijeron.
/// Defaults match the previous hard-coded classroom timings and rules.
/// </summary>
public sealed class GameShowSessionSettings
{
    public const string TieBreakHost = "Host";
    public const string TieBreakAudience = "Audience";
    public const string TieBreakBoth = "Both";

    // Server turn clocks (seconds)
    /// <summary>Time to answer after a student presses RESPONDER (Face-Off / Face-Off second).</summary>
    public int FaceOffSeconds { get; set; } = 30;
    public int ControlSeconds { get; set; } = 30;
    public int StealSeconds { get; set; } = 25;
    public int LightningSeconds { get; set; } = 45;
    public int RoundTransitionSeconds { get; set; } = 3;

    // UX timings (milliseconds) — clients only; server remains deadline source of truth
    public int DrumrollMs { get; set; } = 1100;
    public int RevealHighlightMs { get; set; } = 2000;
    public int StrikeFlashMs { get; set; } = 1000;
    public int CelebrationMs { get; set; } = 2600;
    public int CorrectFlashMs { get; set; } = 2500;
    public int ScoreboardFlashMs { get; set; } = 2500;

    // Rules
    public int MaxStrikes { get; set; } = 3;
    public bool EnableFaceOff { get; set; } = true;
    public bool EnableSteal { get; set; } = true;
    public bool EnableLightning { get; set; } = true;
    public bool EnableSounds { get; set; } = true;
    public bool EnableAnimations { get; set; } = true;
    public bool AllowPause { get; set; } = true;
    public bool AllowSkipRound { get; set; } = true;
    public bool AllowHostEndRound { get; set; } = true;
    /// <summary>Reserved — no audience-vote engine yet.</summary>
    public bool EnableAudienceVote { get; set; }
    public string TieBreakMode { get; set; } = TieBreakHost;

    public static GameShowSessionSettings CreateDefaults() => new();

    public GameShowSessionSettings Clone() =>
        Parse(Serialize(this)) ?? CreateDefaults();

    public static string Serialize(GameShowSessionSettings settings) =>
        JsonSerializer.Serialize(settings, JsonOptions);

    public static GameShowSessionSettings? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GameShowSessionSettings>(json, JsonOptions)
                   ?? CreateDefaults();
        }
        catch
        {
            return CreateDefaults();
        }
    }

    public static GameShowSessionSettings FromSession(GameShowSession session) =>
        Parse(session.SettingsJson) ?? CreateDefaults();

    public void Validate()
    {
        FaceOffSeconds = Clamp(FaceOffSeconds, 3, 120);
        ControlSeconds = Clamp(ControlSeconds, 3, 180);
        StealSeconds = Clamp(StealSeconds, 3, 120);
        LightningSeconds = Clamp(LightningSeconds, 5, 300);
        RoundTransitionSeconds = Clamp(RoundTransitionSeconds, 0, 60);
        DrumrollMs = Clamp(DrumrollMs, 0, 10000);
        RevealHighlightMs = Clamp(RevealHighlightMs, 0, 10000);
        StrikeFlashMs = Clamp(StrikeFlashMs, 0, 10000);
        CelebrationMs = Clamp(CelebrationMs, 0, 15000);
        CorrectFlashMs = Clamp(CorrectFlashMs, 0, 10000);
        ScoreboardFlashMs = Clamp(ScoreboardFlashMs, 0, 10000);
        MaxStrikes = Clamp(MaxStrikes, 1, 5);
        TieBreakMode = TieBreakMode switch
        {
            TieBreakAudience => TieBreakAudience,
            TieBreakBoth => TieBreakBoth,
            _ => TieBreakHost
        };
    }

    private static int Clamp(int value, int min, int max) =>
        Math.Min(max, Math.Max(min, value));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>Singleton row (Id = 1) holding the school/tenant default GameShow config.</summary>
public sealed class GameShowSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string PayloadJson { get; set; } = GameShowSessionSettings.Serialize(GameShowSessionSettings.CreateDefaults());
    public DateTime UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }

    public GameShowSessionSettings Read() =>
        GameShowSessionSettings.Parse(PayloadJson) ?? GameShowSessionSettings.CreateDefaults();

    public void Write(GameShowSessionSettings settings, int? userId, DateTime utcNow)
    {
        settings.Validate();
        PayloadJson = GameShowSessionSettings.Serialize(settings);
        UpdatedAt = utcNow;
        UpdatedByUserId = userId;
    }
}
