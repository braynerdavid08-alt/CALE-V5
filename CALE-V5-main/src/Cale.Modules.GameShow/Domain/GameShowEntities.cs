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

    /// <summary>With 3+ teams: the non-controlling teams race to press RESPONDER to earn the steal.</summary>
    public const string StealBuzz = "StealBuzz";

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
    public const string C = "C";
    public const string D = "D";
    public const int MinTeams = 2;
    public const int MaxTeams = 4;

    private static readonly string[] AllKeys = [A, B, C, D];

    public static int ClampCount(int count) => Math.Clamp(count, MinTeams, MaxTeams);

    public static IReadOnlyList<string> Keys(GameShowSession session) =>
        AllKeys.Take(ClampCount(session.TeamCount)).ToArray();

    public static string? Normalize(GameShowSession session, string? team)
    {
        var key = (team ?? "").Trim().ToUpperInvariant();
        return Keys(session).Contains(key) ? key : null;
    }

    public static string Name(GameShowSession session, string? team) => team switch
    {
        A => session.TeamAName,
        B => session.TeamBName,
        C => session.TeamCName,
        D => session.TeamDName,
        _ => "Sin definir"
    };

    public static int Score(GameShowSession session, string team) => team switch
    {
        A => session.TeamAScore,
        B => session.TeamBScore,
        C => session.TeamCScore,
        D => session.TeamDScore,
        _ => 0
    };

    public static void AddScore(GameShowSession session, string team, int delta)
    {
        switch ((team ?? "").ToUpperInvariant())
        {
            case A: session.TeamAScore = Math.Max(0, session.TeamAScore + delta); break;
            case B: session.TeamBScore = Math.Max(0, session.TeamBScore + delta); break;
            case C: session.TeamCScore = Math.Max(0, session.TeamCScore + delta); break;
            case D: session.TeamDScore = Math.Max(0, session.TeamDScore + delta); break;
        }
    }

    /// <summary>Teams that can play: those with players, or every team while nobody joined.</summary>
    public static IReadOnlyList<string> Playing(GameShowSession session)
    {
        var keys = Keys(session);
        var withPlayers = keys
            .Where(k => session.Players.Any(p => string.Equals(p.Team, k, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return withPlayers.Count >= MinTeams ? withPlayers : keys;
    }

    /// <summary>Next playing team after <paramref name="team"/>, wrapping around.</summary>
    public static string Next(GameShowSession session, string team)
    {
        var order = Playing(session);
        var all = Keys(session);
        var start = all.ToList().IndexOf((team ?? "").ToUpperInvariant());
        for (var step = 1; step <= all.Count; step++)
        {
            var candidate = all[(Math.Max(start, 0) + step) % all.Count];
            if (order.Contains(candidate)) return candidate;
        }

        return team ?? A;
    }

    /// <summary>Teams other than the controller that can attempt a steal.</summary>
    public static IReadOnlyList<string> Stealers(GameShowSession session, string controller) =>
        Playing(session)
            .Where(k => !string.Equals(k, controller, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>Teams holding the highest score (more than one means a tie).</summary>
    public static IReadOnlyList<string> Leaders(GameShowSession session)
    {
        var keys = Keys(session);
        var max = keys.Max(k => Score(session, k));
        return keys.Where(k => Score(session, k) == max).ToList();
    }
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
    public string TeamCName { get; set; } = "Equipo C";
    public string TeamDName { get; set; } = "Equipo D";
    public int TeamAScore { get; set; }
    public int TeamBScore { get; set; }
    public int TeamCScore { get; set; }
    public int TeamDScore { get; set; }
    /// <summary>How many teams play (2–4); students pick among them.</summary>
    public int TeamCount { get; set; } = GameShowTeams.MinTeams;
    public int CurrentRoundIndex { get; set; } = -1;
    /// <summary>Pack used to create this session (school challenge / leaderboard).</summary>
    public int? SourcePackId { get; set; }
    /// <summary>While UtcNow &lt; this, bank commits are doubled (final-round lightning).</summary>
    public DateTime? LightningUntilUtc { get; set; }
    /// <summary>JSON snapshot of <see cref="GameShowSessionSettings"/> taken at create (Lobby-editable).</summary>
    public string SettingsJson { get; set; } = "";
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
    /// <summary>Team attempting the steal (null while no one has claimed it).</summary>
    public string? StealingTeam { get; set; }
    public int Strikes { get; set; }
    public int RoundPointsForController { get; set; }
    public bool StealSucceeded { get; set; }
    public DateTime? BuzzOpenedAt { get; set; }
    /// <summary>UTC instant when the current buzz/answer window ends. Null when Finished or idle.</summary>
    public DateTime? AnswerDeadlineUtc { get; set; }
    /// <summary>Player currently allowed to submit an answer (team rotation).</summary>
    public int? ActivePlayerId { get; set; }
    /// <summary>Player with most correct answers in this round (set on finish).</summary>
    public int? RoundChampionPlayerId { get; set; }
    public int RoundChampionCorrectAnswers { get; set; }
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
    public int CorrectAnswers { get; set; }
    public int StealsWon { get; set; }
    public int BuzzWins { get; set; }
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
