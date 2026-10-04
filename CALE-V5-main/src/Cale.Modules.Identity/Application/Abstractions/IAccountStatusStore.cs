using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Abstractions;

public interface IAccountStatusStore
{
    Task AddAsync(AccountStatusEvent statusEvent, CancellationToken ct);
    Task<AccountStatusEvent?> LatestAsync(int userId, CancellationToken ct);
    Task<IReadOnlyList<AccountStatusEvent>> ListAsync(int userId, CancellationToken ct);
}
