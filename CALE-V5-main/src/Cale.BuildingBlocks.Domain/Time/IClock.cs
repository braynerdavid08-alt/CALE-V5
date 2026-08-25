namespace Cale.BuildingBlocks.Domain.Time;

public interface IClock
{
    DateTime UtcNow { get; }
}
