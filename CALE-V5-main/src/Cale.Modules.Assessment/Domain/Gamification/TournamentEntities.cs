namespace Cale.Modules.Assessment.Domain.Gamification;

public static class TournamentStatuses
{
    public const string RegistrationOpen = "registration_open";
    public const string RegistrationClosed = "registration_closed";
    public const string Preparing = "preparing";
    public const string InProgress = "in_progress";
    public const string Finished = "finished";
    public const string Cancelled = "cancelled";
}

public static class TournamentMatchStatuses
{
    public const string Pending = "pending";
    public const string Active = "active";
    public const string Finished = "finished";
}

public static class TournamentMatchFinishReasons
{
    public const string Score = "score";
    public const string Time = "time";
    public const string SuddenDeath = "sudden_death";
    public const string Walkover = "walkover";
    public const string Bye = "bye";
}

public static class TournamentQuestionModes
{
    public const string Random = "random";
    public const string Manual = "manual";
}

/// <summary>School-scoped elimination tournament (V1: 4–20 players; bracket uses BYEs).</summary>
public sealed class Tournament
{
    public int Id { get; set; }
    public int SchoolUserId { get; set; }
    public int CreatedByUserId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime ScheduledAt { get; set; }
    public int Capacity { get; set; }
    public int QuestionsPerMatch { get; set; } = 10;
    public int SecondsPerQuestion { get; set; } = 30;
    public string QuestionMode { get; set; } = TournamentQuestionModes.Random;
    public string? CategoryFilterJson { get; set; }
    public string? ManualQuestionIdsJson { get; set; }
    /// <summary>Optional JSON array of bank ids; null/empty = default school+official mix.</summary>
    public string? BankIdsJson { get; set; }
    public string Status { get; set; } = TournamentStatuses.RegistrationOpen;
    public int BracketSize { get; set; }
    public int CurrentRoundNumber { get; set; }
    public int? ChampionUserId { get; set; }
    /// <summary>Optimistic concurrency token for start/advance/finish.</summary>
    public uint RowVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<TournamentParticipant> Participants { get; set; } = [];
    public List<TournamentRound> Rounds { get; set; } = [];
}

public sealed class TournamentParticipant
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public Tournament? Tournament { get; set; }
    public int UserId { get; set; }
    public string DisplayNameSnapshot { get; set; } = "";
    public DateTime JoinedAt { get; set; }
    public bool IsConnected { get; set; }
    public string? ConnectionId { get; set; }
    public DateTime? DisconnectedAt { get; set; }
    public bool IsEliminated { get; set; }
    public int? EliminatedInRound { get; set; }
    /// <summary>Bracket seed slot 0..BracketSize-1 after start; null while registering.</summary>
    public int? BracketSlot { get; set; }
}

public sealed class TournamentRound
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public Tournament? Tournament { get; set; }
    public int RoundNumber { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = TournamentMatchStatuses.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public List<TournamentMatch> Matches { get; set; } = [];
}

public sealed class TournamentMatch
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public int RoundId { get; set; }
    public TournamentRound? Round { get; set; }
    public int SlotIndex { get; set; }
    public int? Player1UserId { get; set; }
    public int? Player2UserId { get; set; }
    public bool Player1IsBye { get; set; }
    public bool Player2IsBye { get; set; }
    public string Status { get; set; } = TournamentMatchStatuses.Pending;
    public int? WinnerUserId { get; set; }
    public string? FinishReason { get; set; }
    public int Player1Correct { get; set; }
    public int Player2Correct { get; set; }
    public int Player1TimeMs { get; set; }
    public int Player2TimeMs { get; set; }
    public int CurrentQuestionIndex { get; set; }
    public DateTime? QuestionStartedAt { get; set; }
    public DateTime? Player1DisconnectedAt { get; set; }
    public DateTime? Player2DisconnectedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public uint RowVersion { get; set; }

    public List<TournamentMatchQuestion> Questions { get; set; } = [];
    public List<TournamentAnswer> Answers { get; set; } = [];
}

public sealed class TournamentMatchQuestion
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public TournamentMatch? Match { get; set; }
    public int Ordinal { get; set; }
    public int QuestionId { get; set; }
    public int CorrectOptionId { get; set; }
    public bool IsSuddenDeath { get; set; }
}

public sealed class TournamentAnswer
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public int MatchQuestionId { get; set; }
    public int UserId { get; set; }
    public int? SelectedOptionId { get; set; }
    public bool IsCorrect { get; set; }
    public int ResponseTimeMs { get; set; }
    public DateTime AnsweredAt { get; set; }
}

/// <summary>One championship trophy per tournament win (historical snapshot).</summary>
public sealed class TournamentTrophy
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TournamentId { get; set; }
    public string TournamentNameSnapshot { get; set; } = "";
    public DateTime ObtainedAt { get; set; }
    public string ChampionResult { get; set; } = "";
}
