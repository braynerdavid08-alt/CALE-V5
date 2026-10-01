namespace Cale.Modules.Engagement.Domain;

public static class UserRequestKinds
{
    public const string Question = "question";
    public const string Idea = "idea";

    public static bool IsValid(string? kind) => kind is Question or Idea;
}

public static class UserRequestStatuses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
}

/// <summary>
/// A user's proposal to the admin: a CALE question draft or an idea (new mode, improvement).
/// The admin accepts or rejects it; accepting a question creates it in a bank.
/// </summary>
public sealed class UserRequest
{
    public int Id { get; private set; }
    public int UserId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string UserRole { get; private set; } = string.Empty;
    public string Kind { get; private set; } = UserRequestKinds.Idea;
    public string Status { get; private set; } = UserRequestStatuses.Pending;
    public string? Title { get; private set; }
    public string? Message { get; private set; }
    /// <summary>Question draft as JSON (only for <see cref="UserRequestKinds.Question"/>).</summary>
    public string? PayloadJson { get; private set; }
    public string? AdminNote { get; private set; }
    public int? ReviewedById { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public int? CreatedQuestionId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private UserRequest()
    {
    }

    public static UserRequest Create(
        int userId,
        string userName,
        string userRole,
        string kind,
        string? title,
        string? message,
        string? payloadJson,
        DateTime now) => new()
    {
        UserId = userId,
        UserName = userName,
        UserRole = userRole,
        Kind = kind,
        Title = title,
        Message = message,
        PayloadJson = payloadJson,
        CreatedAt = now
    };

    public bool IsPending => Status == UserRequestStatuses.Pending;

    public void Accept(int adminId, string? note, int? createdQuestionId, string? finalPayloadJson, DateTime now)
    {
        Status = UserRequestStatuses.Accepted;
        AdminNote = note;
        ReviewedById = adminId;
        ReviewedAt = now;
        CreatedQuestionId = createdQuestionId;
        if (finalPayloadJson is not null)
        {
            PayloadJson = finalPayloadJson;
        }
    }

    public void Reject(int adminId, string? note, DateTime now)
    {
        Status = UserRequestStatuses.Rejected;
        AdminNote = note;
        ReviewedById = adminId;
        ReviewedAt = now;
    }
}
