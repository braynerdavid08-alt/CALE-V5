using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Abstractions;

public interface IMembershipEventStore
{
    Task AddAsync(MembershipEvent membershipEvent, CancellationToken ct);
    Task<IReadOnlyList<MembershipEvent>> ListBySchoolAsync(
        int schoolUserId,
        CancellationToken ct);
    Task<IReadOnlyList<MembershipEvent>> ListSinceAsync(
        DateTime utcFrom,
        CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
