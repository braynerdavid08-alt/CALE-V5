using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Abstractions;

public interface IUserStore
{
    Task<User?> FindByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByIdAsync(int id, CancellationToken ct);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct);
    Task<bool> ExistsByEmailAsync(string email, int excludingUserId, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    void Remove(User user);
    Task<IReadOnlyList<User>> ListAsync(CancellationToken ct);
    Task<IReadOnlyList<User>> ListByRoleAsync(string role, CancellationToken ct);
    Task<IReadOnlyList<User>> ListBySchoolAsync(int schoolId, CancellationToken ct);
    Task<int> CountBySchoolAndRoleAsync(int schoolId, string role, CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
