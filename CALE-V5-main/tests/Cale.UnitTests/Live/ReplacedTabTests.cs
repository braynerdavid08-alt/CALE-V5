using System.Security.Claims;
using Cale.Api.Hubs;
using Cale.Api.Services.Live;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.LiveClassroom.Application.Abstractions;
using Cale.Modules.LiveClassroom.Application.Commands;
using Cale.Modules.LiveClassroom.Domain;
using Cale.UnitTests.Security.Integration;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cale.UnitTests.Live;

public sealed class ReplacedTabTests : IAsyncLifetime
{
    private readonly SecurityApiFactory _factory = new();

    public async Task InitializeAsync()
    {
        _ = _factory.Server;
        await _factory.SeedAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Newest_tab_replaces_the_previous_one_for_the_same_seat()
    {
        var (sessionId, token) = await AddSessionWithParticipantAsync();
        var groups = new RecordingGroups();
        var clients = new RecordingClients();

        await JoinAsync(sessionId, token, "tab-1", groups, clients);
        Assert.DoesNotContain(clients.Sent, s => s.Method == LiveClassroomHub.ReplacedByAnotherTabEvent);

        await JoinAsync(sessionId, token, "tab-2", groups, clients);

        var group = LiveClassroomHub.GroupName(sessionId);
        Assert.Contains(("tab-1", group, false), groups.Changes);
        Assert.Contains(("tab-2", group, true), groups.Changes);
        var replaced = Assert.Single(clients.Sent, s => s.Method == LiveClassroomHub.ReplacedByAnotherTabEvent);
        Assert.Equal("tab-1", replaced.ConnectionId);
    }

    [Fact]
    public async Task Rejoining_from_the_same_connection_does_not_kick_itself()
    {
        var (sessionId, token) = await AddSessionWithParticipantAsync();
        var groups = new RecordingGroups();
        var clients = new RecordingClients();

        await JoinAsync(sessionId, token, "tab-1", groups, clients);
        await JoinAsync(sessionId, token, "tab-1", groups, clients);

        Assert.DoesNotContain(clients.Sent, s => s.Method == LiveClassroomHub.ReplacedByAnotherTabEvent);
        Assert.DoesNotContain(groups.Changes, c => !c.Added);
    }

    private async Task JoinAsync(int sessionId, Guid token, string connectionId, RecordingGroups groups, RecordingClients clients)
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        using var hub = new LiveClassroomHub(
            sp.GetRequiredService<LiveSessionHandler>(),
            sp.GetRequiredService<ILiveSessionStore>(),
            sp.GetRequiredService<ILivePresentationSlideState>(),
            new LiveActivityTracker(),
            sp.GetRequiredService<IClock>(),
            NullLogger<LiveClassroomHub>.Instance)
        {
            Context = new FakeCallerContext(connectionId),
            Groups = groups,
            Clients = clients.For(connectionId)
        };

        await hub.JoinAsParticipant(sessionId, token.ToString());
    }

    private async Task<(int SessionId, Guid Token)> AddSessionWithParticipantAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var now = DateTime.UtcNow;
        var session = LiveSession.Create(
            _factory.Users.Teacher1.Id,
            "Clase",
            "TB" + Random.Shared.Next(1000, 9999),
            LiveSessionModes.Exam,
            _factory.Data.PrivateBankId,
            "{}",
            now);
        db.Add(session);
        await db.SaveChangesAsync();
        var participant = LiveParticipant.Create(session.Id, "Alumno", _factory.Users.StudentA.Id, now);
        session.AddParticipant(participant);
        await db.SaveChangesAsync();
        return (session.Id, participant.ParticipantToken);
    }

    private sealed class FakeCallerContext(string connectionId) : HubCallerContext
    {
        public override string ConnectionId { get; } = connectionId;
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => new(new ClaimsIdentity());
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort()
        {
        }
    }

    private sealed class RecordingGroups : IGroupManager
    {
        public List<(string ConnectionId, string Group, bool Added)> Changes { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Changes.Add((connectionId, groupName, true));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Changes.Add((connectionId, groupName, false));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingClients
    {
        public List<(string ConnectionId, string Method)> Sent { get; } = [];

        public IHubCallerClients For(string callerConnectionId) => new Callers(this, callerConnectionId);

        private sealed class Callers(RecordingClients owner, string caller) : IHubCallerClients
        {
            public ISingleClientProxy Caller => new Proxy(owner, caller);
            public IClientProxy Others => new Proxy(owner, "*others");
            public IClientProxy All => new Proxy(owner, "*all");
            public ISingleClientProxy Client(string connectionId) => new Proxy(owner, connectionId);
            public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new Proxy(owner, "*all-except");
            public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new Proxy(owner, "*clients");
            public IClientProxy Group(string groupName) => new Proxy(owner, "group:" + groupName);
            public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new Proxy(owner, "group:" + groupName);
            public IClientProxy Groups(IReadOnlyList<string> groupNames) => new Proxy(owner, "*groups");
            public IClientProxy OthersInGroup(string groupName) => new Proxy(owner, "group:" + groupName);
            public IClientProxy User(string userId) => new Proxy(owner, "user:" + userId);
            public IClientProxy Users(IReadOnlyList<string> userIds) => new Proxy(owner, "*users");
            IClientProxy IHubCallerClients<IClientProxy>.Caller => Caller;
            IClientProxy IHubClients<IClientProxy>.Client(string connectionId) => Client(connectionId);
        }

        private sealed class Proxy(RecordingClients owner, string target) : ISingleClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            {
                owner.Sent.Add((target, method));
                return Task.CompletedTask;
            }

            public Task<T> InvokeCoreAsync<T>(string method, object?[] args, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }
    }
}
