using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class MembershipEventStore : IMembershipEventStore
{
    private readonly CaleDbContext _db;

    public MembershipEventStore(CaleDbContext db) => _db = db;

    public async Task AddAsync(MembershipEvent membershipEvent, CancellationToken ct) =>
        await _db.Set<MembershipEvent>().AddAsync(membershipEvent, ct);

    public async Task<IReadOnlyList<MembershipEvent>> ListBySchoolAsync(
        int schoolUserId,
        CancellationToken ct) =>
        await _db.Set<MembershipEvent>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MembershipEvent>> ListSinceAsync(
        DateTime utcFrom,
        CancellationToken ct) =>
        await _db.Set<MembershipEvent>()
            .AsNoTracking()
            .Where(x => x.CreatedAt >= utcFrom)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}
