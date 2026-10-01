using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.Identity.Domain;
using Cale.Modules.LiveClassroom.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cale.Api.Services.Admin;

public sealed record BankUsageDto(
    int BankId,
    bool IsOfficial,
    DateTime CreatedAt,
    int Exams,
    int PublishedExams,
    int Attempts,
    int AttemptsLast30Days,
    int Students,
    int LiveSessions,
    DateTime? LastUsedAt,
    IReadOnlyList<string> Schools,
    bool InUse,
    string DuplicateRole);

/// <summary>
/// Admin view of how much each bank is really used (exams, attempts, students, schools),
/// so duplicated banks can be told apart from the one schools actually use.
/// </summary>
public sealed class BankUsageService
{
    private readonly CaleDbContext _db;
    private readonly IMemoryCache _cache;

    public BankUsageService(CaleDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>
    /// Official banks have no owner: every school, instructor and student sees them and the
    /// practice games draw from them. Removing the flag hands the bank to <paramref name="adminId"/>.
    /// </summary>
    public async Task SetOfficialAsync(int bankId, bool official, int adminId, CancellationToken ct)
    {
        int? owner = official ? null : adminId;
        var updated = await _db.Set<Bank>()
            .Where(b => b.Id == bankId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.CreatedById, owner), ct);
        if (updated == 0)
        {
            throw new NotFoundException("Bank not found.", "bank_not_found");
        }

        _cache.Remove("play:official-banks");
        _cache.Remove("play:official-questions");
        _cache.Remove("play:signs-questions");
        _cache.Remove("play:official-blocks");
    }

    public async Task<IReadOnlyList<BankUsageDto>> ListAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-30);

        var banks = await _db.Set<Bank>().AsNoTracking()
            .Select(b => new { b.Id, b.Name, b.CreatedAt, IsOfficial = b.CreatedById == null })
            .ToListAsync(ct);

        var exams = await _db.Set<Exam>().AsNoTracking()
            .Where(e => e.BankId != null)
            .Select(e => new { BankId = e.BankId!.Value, e.CreatedById, Live = e.Published && e.IsActive })
            .ToListAsync(ct);

        var attemptStats = await _db.Set<Attempt>().AsNoTracking()
            .GroupBy(a => a.BankId)
            .Select(g => new
            {
                BankId = g.Key,
                Count = g.Count(),
                Recent = g.Count(a => a.StartedAt >= since),
                Last = g.Max(a => a.StartedAt)
            })
            .ToListAsync(ct);

        var bankStudents = await _db.Set<Attempt>().AsNoTracking()
            .Select(a => new { a.BankId, a.UserId })
            .Distinct()
            .ToListAsync(ct);

        var liveCounts = await _db.Set<LiveSession>().AsNoTracking()
            .GroupBy(s => s.BankId)
            .Select(g => new { BankId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var userIds = bankStudents.Select(x => x.UserId)
            .Concat(exams.Select(e => e.CreatedById))
            .Distinct()
            .ToList();
        var users = await _db.Set<User>().AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Role, u.SchoolId })
            .ToDictionaryAsync(u => u.Id, ct);
        var schoolNames = await _db.Set<SchoolProfile>().AsNoTracking()
            .Select(s => new { s.UserId, s.LegalName })
            .ToDictionaryAsync(s => s.UserId, s => s.LegalName, ct);

        string? SchoolOf(int userId)
        {
            if (!users.TryGetValue(userId, out var u)) return null;
            var schoolUserId = u.Role == Roles.School ? u.Id : u.SchoolId;
            return schoolUserId is { } id && schoolNames.TryGetValue(id, out var name) ? name : null;
        }

        var rows = banks.Select(b =>
        {
            var bankExams = exams.Where(e => e.BankId == b.Id).ToList();
            var stats = attemptStats.FirstOrDefault(s => s.BankId == b.Id);
            var students = bankStudents.Where(s => s.BankId == b.Id).Select(s => s.UserId).ToList();
            var live = liveCounts.FirstOrDefault(l => l.BankId == b.Id)?.Count ?? 0;
            var schools = students.Concat(bankExams.Select(e => e.CreatedById))
                .Select(SchoolOf)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList();
            var attempts = stats?.Count ?? 0;
            return new
            {
                Key = b.Name.Trim().ToLowerInvariant(),
                Dto = new BankUsageDto(
                    b.Id,
                    b.IsOfficial,
                    b.CreatedAt,
                    bankExams.Count,
                    bankExams.Count(e => e.Live),
                    attempts,
                    stats?.Recent ?? 0,
                    students.Count,
                    live,
                    stats?.Last,
                    schools,
                    attempts > 0 || bankExams.Any(e => e.Live) || live > 0,
                    "unique")
            };
        }).ToList();

        var result = new List<BankUsageDto>();
        foreach (var group in rows.GroupBy(r => r.Key))
        {
            var list = group.Select(r => r.Dto).ToList();
            if (list.Count == 1)
            {
                result.Add(list[0]);
                continue;
            }

            var main = list
                .OrderByDescending(d => d.AttemptsLast30Days)
                .ThenByDescending(d => d.Attempts)
                .ThenByDescending(d => d.PublishedExams)
                .ThenByDescending(d => d.Exams)
                .ThenBy(d => d.CreatedAt)
                .First();
            result.AddRange(list.Select(d => d with
            {
                DuplicateRole = main.InUse && d.BankId == main.BankId ? "main" : "duplicate"
            }));
        }

        return result;
    }
}
