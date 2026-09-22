namespace Cale.Modules.GameShow.Domain;

public static class GameShowSessionStatuses
{
    public const string Lobby = "Lobby";
    public const string Running = "Running";
    public const string Paused = "Paused";
    public const string Ended = "Ended";
}

/// <summary>
/// Round state machine for 100 Estudiantes Dijeron.
/// Face-off is separate from Control (3 strikes) and Steal.
/// </summary>
public static class GameShowRoundPhases
{
    /// <summary>Both teams can buzz.</summary>
    public const string WaitingBuzz = "WaitingBuzz";

    /// <summary>First team after buzz attempts one answer — miss is NOT a strike.</summary>
    public const string FaceOff = "FaceOff";

    /// <summary>Other team gets one chance after a FaceOff miss.</summary>
    public const string FaceOffSecond = "FaceOffSecond";

    /// <summary>Controlling team hunts remaining answers with up to 3 strikes.</summary>
    public const string Control = "Control";

    /// <summary>Opposite team has one steal attempt.</summary>
    public const string Steal = "Steal";

    public const string Finished = "Finished";

    /// <summary>Legacy alias kept for in-flight sessions created before FaceOff/Control split.</summary>
    public const string Playing = "Playing";

    public static bool IsFaceOff(string? phase) =>
        phase is FaceOff or FaceOffSecond;

    public static bool IsControl(string? phase) =>
        phase is Control or Playing;

    public static bool CanAnswerAsController(string? phase) =>
        IsFaceOff(phase) || IsControl(phase);
}

public static class GameShowTeams
{
    public const string A = "A";
    public const string B = "B";
}

public sealed class GameShowSession
{
    public int Id { get; set; }
    public int HostUserId { get; set; }
    public int? SchoolUserId { get; set; }
    public string Title { get; set; } = "";
    public string JoinCode { get; set; } = "";
    public string Status { get; set; } = GameShowSessionStatuses.Lobby;
    public string TeamAName { get; set; } = "Equipo A";
    public string TeamBName { get; set; } = "Equipo B";
    public int TeamAScore { get; set; }
    public int TeamBScore { get; set; }
    public int CurrentRoundIndex { get; set; } = -1;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public List<GameShowRound> Rounds { get; set; } = [];
    public List<GameShowPlayer> Players { get; set; } = [];
}

public sealed class GameShowRound
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public int SortOrder { get; set; }
    public string QuestionText { get; set; } = "";
    public int? SourceQuestionId { get; set; }
    public string Phase { get; set; } = GameShowRoundPhases.WaitingBuzz;
    public string? ControllingTeam { get; set; }
    public string? BuzzWinnerTeam { get; set; }
    public int Strikes { get; set; }
    public int RoundPointsForController { get; set; }
    public bool StealSucceeded { get; set; }
    public DateTime? BuzzOpenedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public List<GameShowBoardAnswer> Answers { get; set; } = [];
    public List<GameShowAttempt> Attempts { get; set; } = [];
}

public sealed class GameShowBoardAnswer
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public int Rank { get; set; }
    public string Text { get; set; } = "";
    public string AliasesJson { get; set; } = "[]";
    public int Points { get; set; }
    public bool IsRevealed { get; set; }
    public DateTime? RevealedAt { get; set; }
}

public sealed class GameShowPlayer
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public int? UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Team { get; set; } = GameShowTeams.A;
    public Guid PlayerToken { get; set; }
    public string? ConnectionId { get; set; }
    public bool IsConnected { get; set; }
    public DateTime JoinedAt { get; set; }
}

public sealed class GameShowAttempt
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public int? PlayerId { get; set; }
    public string Team { get; set; } = "";
    public string RawText { get; set; } = "";
    public bool IsCorrect { get; set; }
    public bool IsSteal { get; set; }
    public int? MatchedAnswerId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Saved question pack owned by a teacher (or school). Payload is a CreateGameShowRequest JSON.
/// </summary>
public sealed class GameShowPack
{
    public int Id { get; set; }
    public int OwnerUserId { get; set; }
    public int? SchoolUserId { get; set; }
    public string Name { get; set; } = "";
    public string? Notes { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public int RoundCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
