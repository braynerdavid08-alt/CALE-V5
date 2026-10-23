using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class CommercialPlanStore : ICommercialPlanStore
{
    private readonly CaleDbContext _db;

    public CommercialPlanStore(CaleDbContext db) => _db = db;

    public async Task<IReadOnlyList<CommercialPlan>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var q = _db.Set<CommercialPlan>().AsQueryable();
        if (!includeInactive)
        {
            q = q.Where(x => x.IsActive);
        }

        return await q.OrderBy(x => x.DurationMonths).ThenBy(x => x.Code).ToListAsync(ct);
    }

    public Task<CommercialPlan?> GetByCodeAsync(string code, CancellationToken ct)
    {
        var normalized = SchoolPlans.Normalize(code);
        return _db.Set<CommercialPlan>().FirstOrDefaultAsync(x => x.Code == normalized, ct);
    }

    public async Task UpsertAsync(CommercialPlan plan, CancellationToken ct)
    {
        var existing = await GetByCodeAsync(plan.Code, ct);
        if (existing is null)
        {
            await _db.Set<CommercialPlan>().AddAsync(plan, ct);
            return;
        }

        existing.Update(
            plan.LabelEs,
            plan.PriceCop,
            plan.DurationMonths,
            plan.MaxTeachers,
            plan.MaxStudents,
            plan.IsActive,
            plan.UpdatedAt);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public async Task<SchoolPlanInfo?> ResolvePlanInfoAsync(string? code, CancellationToken ct)
    {
        var normalized = SchoolPlans.Normalize(code);
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        var row = await GetByCodeAsync(normalized, ct);
        if (row is not null)
        {
            return row.ToPlanInfo();
        }

        return SchoolPlans.Find(normalized);
    }
}
