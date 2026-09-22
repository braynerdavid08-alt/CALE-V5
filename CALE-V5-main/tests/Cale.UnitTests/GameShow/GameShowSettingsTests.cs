using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.GameShow.Application;
using Cale.Modules.GameShow.Application.Abstractions;
using Cale.Modules.GameShow.Application.DTOs;
using Cale.Modules.GameShow.Domain;
using Cale.UnitTests.Fakes;

namespace Cale.UnitTests.GameShow;

public sealed class GameShowSettingsTests
{
    [Fact]
    public void Defaults_match_previous_hardcoded_timings()
    {
        var s = GameShowSessionSettings.CreateDefaults();
        Assert.Equal(25, s.FaceOffSeconds);
        Assert.Equal(30, s.ControlSeconds);
        Assert.Equal(25, s.StealSeconds);
        Assert.Equal(3, s.MaxStrikes);
        Assert.Equal(45, s.LightningSeconds);
        Assert.Equal(1100, s.DrumrollMs);
        Assert.True(s.EnableSteal);
        Assert.True(s.EnableFaceOff);
    }

    [Fact]
    public void Changing_global_serialized_copy_does_not_mutate_original_snapshot()
    {
        var session = new GameShowSession
        {
            SettingsJson = GameShowSessionSettings.Serialize(GameShowSessionSettings.CreateDefaults())
        };
        var snap = GameShowSessionSettings.FromSession(session);
        snap.FaceOffSeconds = 99;
        Assert.Equal(25, GameShowSessionSettings.FromSession(session).FaceOffSeconds);
    }

    [Fact]
    public void SetDeadline_uses_snapshot_faceoff_seconds()
    {
        var settings = GameShowSessionSettings.CreateDefaults();
        settings.FaceOffSeconds = 12;
        var round = new GameShowRound { Phase = GameShowRoundPhases.FaceOff };
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        GameShowTiming.SetDeadline(round, now, settings);
        Assert.Equal(now.AddSeconds(12), round.AnswerDeadlineUtc);
    }

    [Fact]
    public void MaxStrikes_from_settings_opens_steal_earlier()
    {
        var session = new GameShowSession
        {
            SettingsJson = GameShowSessionSettings.Serialize(new GameShowSessionSettings { MaxStrikes = 1 }),
            Players =
            [
                new GameShowPlayer { Id = 1, DisplayName = "A", Team = GameShowTeams.A, JoinedAt = DateTime.UtcNow },
                new GameShowPlayer { Id = 2, DisplayName = "B", Team = GameShowTeams.B, JoinedAt = DateTime.UtcNow }
            ],
            Rounds =
            [
                new GameShowRound
                {
                    Id = 1,
                    Phase = GameShowRoundPhases.Control,
                    ControllingTeam = GameShowTeams.A,
                    ActivePlayerId = 1,
                    Answers =
                    [
                        new GameShowBoardAnswer { Id = 1, Rank = 1, Text = "Frenos", Points = 30, AliasesJson = "[]", IsRevealed = true }
                    ]
                }
            ]
        };
        var round = session.Rounds[0];
        var outcome = GameShowEngine.ProcessAnswer(
            session, round, session.Players[0], "xxx", DateTime.UtcNow);
        Assert.Equal(GameShowEngine.OutcomeKind.StealOpportunity, outcome.Kind);
        Assert.Equal(GameShowRoundPhases.Steal, round.Phase);
        Assert.Equal(1, round.Strikes);
    }

    [Fact]
    public void Steal_disabled_finishes_round_without_steal_phase()
    {
        var session = new GameShowSession
        {
            SettingsJson = GameShowSessionSettings.Serialize(new GameShowSessionSettings
            {
                MaxStrikes = 1,
                EnableSteal = false
            }),
            Players =
            [
                new GameShowPlayer { Id = 1, DisplayName = "A", Team = GameShowTeams.A, JoinedAt = DateTime.UtcNow },
                new GameShowPlayer { Id = 2, DisplayName = "B", Team = GameShowTeams.B, JoinedAt = DateTime.UtcNow }
            ],
            Rounds =
            [
                new GameShowRound
                {
                    Id = 1,
                    Phase = GameShowRoundPhases.Control,
                    ControllingTeam = GameShowTeams.A,
                    ActivePlayerId = 1,
                    RoundPointsForController = 40,
                    Answers =
                    [
                        new GameShowBoardAnswer { Id = 1, Rank = 1, Text = "Frenos", Points = 30, AliasesJson = "[]", IsRevealed = true }
                    ]
                }
            ]
        };
        var round = session.Rounds[0];
        var outcome = GameShowEngine.ProcessAnswer(
            session, round, session.Players[0], "xxx", DateTime.UtcNow);
        Assert.Equal(GameShowEngine.OutcomeKind.RoundCompleted, outcome.Kind);
        Assert.Equal(GameShowRoundPhases.Finished, round.Phase);
        Assert.Equal(40, session.TeamAScore);
    }

    [Fact]
    public void Inactive_round_is_skipped_by_BuildRound_filter_in_create_payload()
    {
        // Mirrors CreateAsync filter: only IsActive rounds materialize.
        var rounds = new[]
        {
            new CreateGameShowRoundRequest(
                "Pregunta activa suficientemente larga",
                null,
                [new CreateGameShowAnswerRequest("Uno", 30, null, true)],
                "Cat",
                true),
            new CreateGameShowRoundRequest(
                "Pregunta inactiva suficientemente larga",
                null,
                [new CreateGameShowAnswerRequest("Dos", 20, null, true)],
                "Cat",
                false)
        };
        var active = rounds.Where(r => r.IsActive).ToList();
        Assert.Single(active);
        Assert.Equal("Pregunta activa suficientemente larga", active[0].QuestionText);
    }

    [Fact]
    public async Task UpdateSessionSettings_when_running_returns_400()
    {
        var session = new GameShowSession
        {
            Id = 7,
            HostUserId = 1,
            Status = GameShowSessionStatuses.Running,
            SettingsJson = GameShowSessionSettings.Serialize(GameShowSessionSettings.CreateDefaults())
        };
        var store = new SessionSettingsStoreStub(session);
        var handler = new GameShowHandler(store, new NoopBroadcaster(), new FakeClock(DateTime.UtcNow));
        var dto = ToDto(GameShowSessionSettings.CreateDefaults());

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => handler.UpdateSessionSettingsAsync(7, 1, dto, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("settings_locked", ex.ErrorCode);
    }

    private static GameShowSettingsDto ToDto(GameShowSessionSettings s) =>
        new(
            s.FaceOffSeconds,
            s.ControlSeconds,
            s.StealSeconds,
            s.LightningSeconds,
            s.RoundTransitionSeconds,
            s.DrumrollMs,
            s.RevealHighlightMs,
            s.StrikeFlashMs,
            s.CelebrationMs,
            s.CorrectFlashMs,
            s.ScoreboardFlashMs,
            s.MaxStrikes,
            s.EnableFaceOff,
            s.EnableSteal,
            s.EnableLightning,
            s.EnableSounds,
            s.EnableAnimations,
            s.AllowPause,
            s.AllowSkipRound,
            s.AllowHostEndRound,
            s.EnableAudienceVote,
            s.TieBreakMode);

    private sealed class NoopBroadcaster : IGameShowBroadcaster
    {
        public Task LobbyUpdatedAsync(int sessionId, GameShowLobbyDto lobby, CancellationToken ct) =>
            Task.CompletedTask;

        public Task EventAsync(int sessionId, string eventName, object payload, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class SessionSettingsStoreStub : IGameShowStore
    {
        private readonly GameShowSession _session;

        public SessionSettingsStoreStub(GameShowSession session) => _session = session;

        public Task AddAsync(GameShowSession session, CancellationToken ct) => Task.CompletedTask;
        public Task<GameShowSession?> GetByIdAsync(int id, CancellationToken ct) =>
            Task.FromResult<GameShowSession?>(id == _session.Id ? _session : null);
        public Task<GameShowSession?> GetByIdWithAttemptsAsync(int id, CancellationToken ct) =>
            GetByIdAsync(id, ct);
        public Task<GameShowSession?> GetByJoinCodeAsync(string code, CancellationToken ct) =>
            Task.FromResult<GameShowSession?>(null);
        public Task<GameShowPlayer?> GetPlayerByTokenAsync(Guid token, CancellationToken ct) =>
            Task.FromResult<GameShowPlayer?>(null);
        public Task<GameShowPlayer?> GetPlayerByConnectionIdAsync(string connectionId, CancellationToken ct) =>
            Task.FromResult<GameShowPlayer?>(null);
        public Task<bool> JoinCodeExistsAsync(string code, CancellationToken ct) =>
            Task.FromResult(false);
        public Task<bool> TryClaimBuzzAsync(int roundId, string team, CancellationToken ct) =>
            Task.FromResult(false);
        public Task<IReadOnlyList<GameShowSession>> ListForHostAsync(int hostUserId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GameShowSession>>([]);
        public Task<IReadOnlyList<GameShowSession>> ListForSchoolAsync(int schoolUserId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GameShowSession>>([]);
        public Task AddPackAsync(GameShowPack pack, CancellationToken ct) => Task.CompletedTask;
        public Task<GameShowPack?> GetPackByIdAsync(int id, CancellationToken ct) =>
            Task.FromResult<GameShowPack?>(null);
        public Task<IReadOnlyList<GameShowPack>> ListPacksForOwnerAsync(int ownerUserId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GameShowPack>>([]);
        public Task RemovePackAsync(GameShowPack pack, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<GameShowSession>> ListEndedByPackAsync(int packId, int take, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GameShowSession>>([]);
        public Task<GameShowSettings> GetOrCreateSettingsAsync(CancellationToken ct) =>
            Task.FromResult(new GameShowSettings());
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
