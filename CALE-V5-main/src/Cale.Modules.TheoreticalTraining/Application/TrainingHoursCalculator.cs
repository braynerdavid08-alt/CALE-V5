using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

/// <summary>
/// Single source of a student's theory/workshop hours: attended classes plus manual adjustments.
/// </summary>
public static class TrainingHoursCalculator
{
    public sealed record HoursBreakdown(
        decimal TheoryAttended,
        decimal WorkshopAttended,
        decimal TheoryAdjusted,
        decimal WorkshopAdjusted,
        int Absences)
    {
        public decimal TheoryTotal => Math.Max(0, TheoryAttended + TheoryAdjusted);
        public decimal WorkshopTotal => Math.Max(0, WorkshopAttended + WorkshopAdjusted);
    }

    public static async Task<HoursBreakdown> ComputeAsync(
        CaleDbContext db,
        int schoolUserId,
        int studentUserId,
        CancellationToken ct)
    {
        var records = await db.Set<TheoryAttendanceRecord>()
            .AsNoTracking()
            .Include(x => x.ClassSession)!.ThenInclude(s => s!.Topic)
            .Where(x => x.StudentUserId == studentUserId
                && x.ClassSession != null
                && x.ClassSession.SchoolUserId == schoolUserId)
            .ToListAsync(ct);

        decimal theoryHours = 0;
        decimal workshopHours = 0;
        var absences = 0;
        foreach (var r in records)
        {
            if (r.Status is TheoryAttendanceStatuses.Present or TheoryAttendanceStatuses.Late)
            {
                var s = r.ClassSession;
                if (s is null)
                {
                    continue;
                }

                var duration = (decimal)(s.EndTime - s.StartTime).TotalHours;
                var category = s.Topic?.Category ?? TheoryTopicCategories.Theory;
                if (category == TheoryTopicCategories.Workshop)
                {
                    workshopHours += duration;
                }
                else
                {
                    theoryHours += duration;
                }
            }
            else if (r.Status == TheoryAttendanceStatuses.Absent)
            {
                absences++;
            }
        }

        var adjustments = await db.Set<TrainingHoursAdjustment>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId)
            .Select(x => new { x.Category, x.DeltaHours })
            .ToListAsync(ct);
        var theoryAdjusted = adjustments
            .Where(x => x.Category == TheoryTopicCategories.Theory)
            .Sum(x => x.DeltaHours);
        var workshopAdjusted = adjustments
            .Where(x => x.Category == TheoryTopicCategories.Workshop)
            .Sum(x => x.DeltaHours);

        return new HoursBreakdown(
            Math.Round(theoryHours, 1),
            Math.Round(workshopHours, 1),
            Math.Round(theoryAdjusted, 1),
            Math.Round(workshopAdjusted, 1),
            absences);
    }
}
