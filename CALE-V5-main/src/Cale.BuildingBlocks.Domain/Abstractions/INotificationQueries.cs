namespace Cale.BuildingBlocks.Domain.Abstractions;

public interface INotificationQueries
{
    Task<int> CountUnreadAsync(int userId, CancellationToken ct);
}
