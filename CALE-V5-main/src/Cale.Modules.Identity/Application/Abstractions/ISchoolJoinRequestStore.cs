using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Abstractions;

public interface ISchoolJoinRequestStore
{
    Task AddAsync(SchoolJoinRequest request, CancellationToken ct = default);
    Task<SchoolJoinRequest?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<SchoolJoinRequest?> FindPendingAsync(
        int memberUserId,
        int schoolUserId,
        CancellationToken ct = default);
    Task<IReadOnlyList<SchoolJoinRequest>> ListPendingBySchoolAsync(
        int schoolUserId,
        CancellationToken ct = default);
    Task<IReadOnlyList<SchoolJoinRequest>> ListByMemberAsync(
        int memberUserId,
        CancellationToken ct = default);
    Task<IReadOnlyList<SchoolJoinRequest>> ListPendingByMemberAsync(
        int memberUserId,
        CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
