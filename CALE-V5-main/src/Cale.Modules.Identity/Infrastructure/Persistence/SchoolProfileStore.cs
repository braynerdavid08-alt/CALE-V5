using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class SchoolProfileStore : ISchoolProfileStore
{
    private readonly CaleDbContext _db;

    public SchoolProfileStore(CaleDbContext db) => _db = db;

    public async Task AddAsync(SchoolProfile profile, CancellationToken ct) =>
        await _db.Set<SchoolProfile>().AddAsync(profile, ct);

    public Task<SchoolProfile?> GetByUserIdAsync(int userId, CancellationToken ct) =>
        _db.Set<SchoolProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

    public Task<SchoolProfile?> GetTrackedByUserIdAsync(int userId, CancellationToken ct) =>
        _db.Set<SchoolProfile>()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

    public async Task<IReadOnlyList<SchoolProfile>> ListByStatusAsync(
        string subscriptionStatus,
        CancellationToken ct) =>
        await _db.Set<SchoolProfile>()
            .AsNoTracking()
            .Where(x => x.SubscriptionStatus == subscriptionStatus)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SchoolProfile>> ListAllAsync(CancellationToken ct) =>
        await _db.Set<SchoolProfile>()
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public void Remove(SchoolProfile profile) =>
        _db.Set<SchoolProfile>().Remove(profile);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}
