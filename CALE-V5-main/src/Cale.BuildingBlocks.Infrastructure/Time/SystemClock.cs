using Cale.BuildingBlocks.Domain.Time;

namespace Cale.BuildingBlocks.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
