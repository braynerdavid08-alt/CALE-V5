using Cale.BuildingBlocks.Domain.Time;

namespace Cale.UnitTests.Fakes;

public sealed class FakeClock : IClock
{
    public FakeClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; set; }
}
