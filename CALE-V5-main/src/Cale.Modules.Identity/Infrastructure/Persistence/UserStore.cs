using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class UserStore : IUserStore
{
    private readonly CaleDbContext _db;

    public UserStore(CaleDbContext db) => _db = db;

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct) =>
        _db.Set<User>().FirstOrDefaultAsync(x => x.Email == email, ct);

    public Task<User?> GetByIdAsync(int id, CancellationToken ct) =>
        _db.Set<User>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct) =>
        _db.Set<User>().AnyAsync(x => x.Email == email, ct);

    public Task<bool> ExistsByEmailAsync(
        string email,
        int excludingUserId,
        CancellationToken ct) =>
        _db.Set<User>().AnyAsync(
            x => x.Email == email && x.Id != excludingUserId,
            ct);

    public async Task AddAsync(User user, CancellationToken ct) =>
        await _db.Set<User>().AddAsync(user, ct);

    public void Remove(User user) => _db.Set<User>().Remove(user);

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct) =>
        await _db.Set<User>()
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<User>> ListBySchoolAsync(
        int schoolId,
        CancellationToken ct) =>
        await _db.Set<User>()
            .AsNoTracking()
            .Where(x => x.SchoolId == schoolId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

    public Task<int> CountBySchoolAndRoleAsync(
        int schoolId,
        string role,
        CancellationToken ct) =>
        _db.Set<User>().CountAsync(
            x => x.SchoolId == schoolId && x.Role == role,
            ct);

    public Task<int> CountAsync(CancellationToken ct) =>
        _db.Set<User>().CountAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}
