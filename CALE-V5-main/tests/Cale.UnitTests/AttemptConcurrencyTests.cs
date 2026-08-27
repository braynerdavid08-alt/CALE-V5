using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Assessment.Domain;
using Cale.UnitTests.Fakes;

namespace Cale.UnitTests;

public sealed class AttemptConcurrencyTests
{
    [Fact]
    public void Finish_rejects_after_grace_window()
    {
        var clock = new FakeClock(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        var attempt = Attempt.Start(1, 10, 5, "exam", 5, timeMinutes: 1, clock.UtcNow);

        clock.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(6));

        var ex = Assert.Throws<ForbiddenException>(
            () => attempt.Finish(3, clock.UtcNow));
        Assert.Equal("attempt_expired", ex.ErrorCode);
    }

    [Fact]
    public void Finish_allows_within_grace_and_clamps_time()
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var attempt = Attempt.Start(1, 10, 5, "exam", 5, timeMinutes: 1, start);
        var late = start.AddMinutes(1).AddSeconds(3);

        attempt.Finish(4, late);

        Assert.Equal(start.AddMinutes(1), attempt.FinishedAt);
        Assert.Equal(60, attempt.TimeSeconds);
        Assert.Equal(80m, attempt.Percent);
    }

    [Fact]
    public void Finish_second_call_is_conflict()
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var attempt = Attempt.Start(1, 10, null, "practice", 2, 5, start);
        attempt.Finish(1, start.AddMinutes(1));

        var ex = Assert.Throws<ConflictException>(
            () => attempt.Finish(2, start.AddMinutes(2)));
        Assert.Equal("attempt_finished", ex.ErrorCode);
    }

    [Fact]
    public void IsOpen_false_after_expires_even_if_not_finished()
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var attempt = Attempt.Start(1, 10, 5, "exam", 3, 1, start);
        Assert.False(attempt.IsOpen(start.AddMinutes(1).AddSeconds(1)));
        Assert.True(attempt.IsWithinFinishGrace(start.AddMinutes(1).AddSeconds(3)));
        Assert.False(attempt.IsWithinFinishGrace(start.AddMinutes(1).AddSeconds(6)));
    }

    [Fact]
    public void CloseExpired_scores_and_frees_open_slot()
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var attempt = Attempt.Start(1, 10, 5, "exam", 4, timeMinutes: 1, start);
        var afterGrace = start.AddMinutes(1).AddSeconds(6);

        attempt.CloseExpired(2, afterGrace);

        Assert.Equal(start.AddMinutes(1), attempt.FinishedAt);
        Assert.Equal(50m, attempt.Percent);
        Assert.Equal(60, attempt.TimeSeconds);
        Assert.False(attempt.IsOpen(afterGrace));
    }

    [Fact]
    public void CloseExpired_rejects_while_still_in_grace()
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var attempt = Attempt.Start(1, 10, 5, "exam", 3, 1, start);

        var ex = Assert.Throws<DomainException>(
            () => attempt.CloseExpired(1, start.AddMinutes(1).AddSeconds(2)));
        Assert.Equal("attempt_still_open", ex.ErrorCode);
    }
}
