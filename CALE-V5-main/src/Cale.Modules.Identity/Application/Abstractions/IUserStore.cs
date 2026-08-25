using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Abstractions;

public interface IUserStore
{
    Task<User?> FindByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByIdAsync(int id, CancellationToken ct);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
