namespace Cale.Api.Services.Assistant;

public sealed class AssistantUsageDaily
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? SchoolUserId { get; set; }
    public DateOnly Day { get; set; }
    public int MessagesUsed { get; set; }
    public int RequestsUsed { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AssistantUsageEvent
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int? SchoolUserId { get; set; }
    public DateOnly Day { get; set; }
    public string Status { get; set; } = "";
    public string Kind { get; set; } = "";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static class AssistantUsageStatuses
{
    public const string Reserved = "Reserved";
    public const string Committed = "Committed";
    public const string Released = "Released";
    public const string Rejected = "Rejected";
}

public static class AssistantUsageKinds
{
    public const string Message = "Message";
    public const string ProviderRequest = "ProviderRequest";
}

public sealed record AssistantUsageSnapshot(
    int UserId,
    int? SchoolUserId,
    DateOnly Day,
    int MessagesUsed,
    int MessagesLimit,
    int MessagesRemaining,
    int RequestsUsed,
    bool NearLimit,
    bool AtLimit);

public sealed record AssistantSchoolUsageRow(
    int UserId,
    string UserName,
    string Role,
    int MessagesUsed,
    int MessagesLimit,
    int MessagesRemaining);

public sealed record AssistantSchoolUsageSummary(
    DateOnly Day,
    int SchoolUserId,
    int TotalMessages,
    int StudentCount,
    IReadOnlyList<AssistantSchoolUsageRow> Students);
