using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class AccountStatusStore : IAccountStatusStore
{
    private readonly CaleDbContext _db;

    public AccountStatusStore(CaleDbContext db) => _db = db;

    public async Task AddAsync(AccountStatusEvent statusEvent, CancellationToken ct) =>
        await _db.Set<AccountStatusEvent>().AddAsync(statusEvent, ct);

    public Task<AccountStatusEvent?> LatestAsync(int userId, CancellationToken ct) =>
        _db.Set<AccountStatusEvent>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AccountStatusEvent>> ListAsync(int userId, CancellationToken ct) =>
        await _db.Set<AccountStatusEvent>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
}
