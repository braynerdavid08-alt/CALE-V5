namespace Cale.Modules.LiveClassroom.Domain;

public sealed class LiveParticipant
{
    public int Id { get; private set; }
    public int SessionId { get; private set; }
    public int? UserId { get; private set; }
    public string DisplayName { get; private set; } = "";
    public Guid ParticipantToken { get; private set; }
    public string? ConnectionId { get; private set; }
    public bool IsConnected { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private LiveParticipant()
    {
    }

    public static LiveParticipant Create(
        int sessionId,
        string displayName,
        int? userId,
        DateTime utcNow)
    {
        var name = string.IsNullOrWhiteSpace(displayName)
            ? "Participante"
            : displayName.Trim();
        if (name.Length > 80)
        {
            name = name[..80];
        }

        return new LiveParticipant
        {
            SessionId = sessionId,
            UserId = userId,
            DisplayName = name,
            ParticipantToken = Guid.NewGuid(),
            IsConnected = false,
            JoinedAt = utcNow
        };
    }

    public void Connect(string connectionId)
    {
        ConnectionId = connectionId;
        IsConnected = true;
    }

    public void Disconnect()
    {
        ConnectionId = null;
        IsConnected = false;
    }
}
