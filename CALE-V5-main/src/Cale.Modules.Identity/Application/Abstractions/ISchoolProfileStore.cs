using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Abstractions;

public interface ISchoolProfileStore
{
    Task AddAsync(SchoolProfile profile, CancellationToken ct);
    Task<SchoolProfile?> GetByUserIdAsync(int userId, CancellationToken ct);
    Task<SchoolProfile?> GetTrackedByUserIdAsync(int userId, CancellationToken ct);
    void Remove(SchoolProfile profile);
    Task SaveChangesAsync(CancellationToken ct);
}
