using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Abstractions;

public interface ICommercialPlanStore
{
    Task<IReadOnlyList<CommercialPlan>> ListAsync(bool includeInactive, CancellationToken ct);
    Task<CommercialPlan?> GetByCodeAsync(string code, CancellationToken ct);
    Task UpsertAsync(CommercialPlan plan, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task<SchoolPlanInfo?> ResolvePlanInfoAsync(string? code, CancellationToken ct);
}
