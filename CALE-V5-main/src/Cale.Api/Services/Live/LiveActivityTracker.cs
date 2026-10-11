namespace Cale.Api.Services.Live;

public enum LiveRoomKind
{
    LiveClass,
    GameShow
}

/// <summary>
/// In-memory record of who is running each live class / game show: host-side SignalR connections
/// (host panel, projector screen) and the last write action on the room (host controls, answers,
/// buzzes). Student connections alone do not keep a room alive, so a tab forgotten open by a
/// student does not block the auto-end. Lost on restart; callers fall back to the boot time.
/// </summary>
public sealed class LiveActivityTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<(LiveRoomKind, int), RoomState> _rooms = new();
    private readonly Dictionary<string, (LiveRoomKind Kind, int RoomId)> _connections = new(StringComparer.Ordinal);

    public void HostConnected(LiveRoomKind kind, int roomId, string connectionId, DateTime utcNow)
    {
        lock (_gate)
        {
            if (_connections.TryGetValue(connectionId, out var previous) && previous != (kind, roomId))
            {
                RemoveConnection(connectionId, previous, utcNow);
            }

            _connections[connectionId] = (kind, roomId);
            var room = Room(kind, roomId);
            room.HostConnections.Add(connectionId);
            room.LastActivityAt = utcNow;
        }
    }

    public void ConnectionClosed(string connectionId, DateTime utcNow)
    {
        lock (_gate)
        {
            if (_connections.TryGetValue(connectionId, out var key))
            {
                RemoveConnection(connectionId, key, utcNow);
            }
        }
    }

    public void Touch(LiveRoomKind kind, int roomId, DateTime utcNow)
    {
        lock (_gate)
        {
            Room(kind, roomId).LastActivityAt = utcNow;
        }
    }

    /// <summary>True when a host-side connection is open or there was activity at/after <paramref name="since"/>.</summary>
    public bool IsActiveSince(LiveRoomKind kind, int roomId, DateTime since)
    {
        lock (_gate)
        {
            return _rooms.TryGetValue((kind, roomId), out var room)
                && (room.HostConnections.Count > 0 || room.LastActivityAt >= since);
        }
    }

    public void Forget(LiveRoomKind kind, int roomId)
    {
        lock (_gate)
        {
            if (_rooms.Remove((kind, roomId), out var room))
            {
                foreach (var connectionId in room.HostConnections)
                {
                    _connections.Remove(connectionId);
                }
            }
        }
    }

    private RoomState Room(LiveRoomKind kind, int roomId)
    {
        if (!_rooms.TryGetValue((kind, roomId), out var room))
        {
            room = new RoomState();
            _rooms[(kind, roomId)] = room;
        }

        return room;
    }

    private void RemoveConnection(string connectionId, (LiveRoomKind Kind, int RoomId) key, DateTime utcNow)
    {
        _connections.Remove(connectionId);
        if (_rooms.TryGetValue(key, out var room) && room.HostConnections.Remove(connectionId))
        {
            room.LastActivityAt = utcNow;
        }
    }

    private sealed class RoomState
    {
        public HashSet<string> HostConnections { get; } = new(StringComparer.Ordinal);
        public DateTime LastActivityAt { get; set; }
    }
}
