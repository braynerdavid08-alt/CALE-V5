using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed class SchoolStudentEnrollmentBootstrap : ISchoolStudentEnrollmentBootstrap
{
    private readonly CaleDbContext _db;
    private readonly IClock _clock;

    public SchoolStudentEnrollmentBootstrap(CaleDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task EnsurePendingAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct = default)
    {
        var exists = await _db.Set<SchoolStudentEnrollment>()
            .AnyAsync(
                x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId,
                ct);
        if (exists)
        {
            return;
        }

        var now = _clock.UtcNow;
        await _db.Set<SchoolStudentEnrollment>().AddAsync(
            new SchoolStudentEnrollment
            {
                SchoolUserId = schoolUserId,
                StudentUserId = studentUserId,
                Status = StudentEnrollmentStatuses.Pending,
                CreatedAt = now,
                UpdatedAt = now
            },
            ct);
        await _db.SaveChangesAsync(ct);
    }
}
