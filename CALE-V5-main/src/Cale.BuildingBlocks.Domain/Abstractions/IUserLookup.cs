namespace Cale.BuildingBlocks.Domain.Abstractions;

public interface IUserLookup
{
    Task<string?> GetNameAsync(int userId, CancellationToken ct);
    Task<int?> FindIdByEmailAsync(string email, CancellationToken ct);
    Task<string?> GetRoleAsync(int userId, CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
}
