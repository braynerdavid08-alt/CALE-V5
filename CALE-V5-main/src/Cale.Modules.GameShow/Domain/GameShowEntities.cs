namespace Cale.Modules.GameShow.Domain;

public static class GameShowSessionStatuses
{
    public const string Lobby = "Lobby";
    public const string Running = "Running";
    public const string Paused = "Paused";
    public const string Ended = "Ended";
}

public static class GameShowRoundPhases
{
    public const string WaitingBuzz = "WaitingBuzz";
    public const string Playing = "Playing";
    public const string Steal = "Steal";
    public const string Finished = "Finished";
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
