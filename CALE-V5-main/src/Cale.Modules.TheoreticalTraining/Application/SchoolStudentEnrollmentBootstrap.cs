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
        CancellationToken ct = default,
        StudentOnboardingSeed? seed = null)
    {
        var now = _clock.UtcNow;
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(
                x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId,
                ct);

        if (enrollment is null)
        {
            enrollment = new SchoolStudentEnrollment
            {
                SchoolUserId = schoolUserId,
                StudentUserId = studentUserId,
                Status = StudentEnrollmentStatuses.Pending,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _db.Set<SchoolStudentEnrollment>().AddAsync(enrollment, ct);
        }

        if (seed is not null)
        {
            ApplyEnrollmentSeed(enrollment, seed, now);

            var profile = await _db.Set<SchoolApprenticeProfile>()
                .FirstOrDefaultAsync(
                    x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId,
                    ct);
            if (profile is null)
            {
                profile = new SchoolApprenticeProfile
                {
                    SchoolUserId = schoolUserId,
                    StudentUserId = studentUserId,
                    CreatedAt = now
                };
                await _db.Set<SchoolApprenticeProfile>().AddAsync(profile, ct);
            }

            ApplyProfileSeed(profile, seed, now);
        }

        await _db.SaveChangesAsync(ct);
    }

    private static void ApplyEnrollmentSeed(
        SchoolStudentEnrollment enrollment,
        StudentOnboardingSeed seed,
        DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(seed.LicenseCategories)
            && StudentLicenseCategories.IsValid(seed.LicenseCategories))
        {
            enrollment.LicenseCategories = StudentLicenseCategories.Presets
                .First(p => p.Equals(seed.LicenseCategories.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(seed.AttendanceDayType)
            && StudentAttendanceDayTypes.IsValid(seed.AttendanceDayType.Trim()))
        {
            enrollment.AttendanceDayType = seed.AttendanceDayType.Trim();
        }

        if (enrollment.Status is StudentEnrollmentStatuses.Pending or StudentEnrollmentStatuses.Accepted
            && !string.IsNullOrWhiteSpace(enrollment.AttendanceDayType)
            && !string.IsNullOrWhiteSpace(enrollment.LicenseCategories))
        {
            enrollment.Status = StudentEnrollmentStatuses.Active;
            enrollment.AcceptedAt ??= now;
        }

        enrollment.UpdatedAt = now;
    }

    private static void ApplyProfileSeed(
        SchoolApprenticeProfile profile,
        StudentOnboardingSeed seed,
        DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(seed.DocumentType))
        {
            profile.DocumentType = seed.DocumentType.Trim();
        }

        if (!string.IsNullOrWhiteSpace(seed.DocumentNumber))
        {
            profile.DocumentNumber = seed.DocumentNumber.Trim();
        }

        if (!string.IsNullOrWhiteSpace(seed.Phone))
        {
            profile.Phone = seed.Phone.Trim();
        }

        if (!string.IsNullOrWhiteSpace(seed.Address))
        {
            profile.Address = seed.Address.Trim();
        }

        if (!string.IsNullOrWhiteSpace(seed.ContactEmail))
        {
            profile.ContactEmail = seed.ContactEmail.Trim();
        }

        if (!string.IsNullOrWhiteSpace(seed.ScheduleSlot))
        {
            profile.ScheduleSlot = seed.ScheduleSlot.Trim();
        }

        if (!string.IsNullOrWhiteSpace(seed.EnrollmentPin))
        {
            profile.EnrollmentPin = seed.EnrollmentPin.Trim();
        }

        if (!string.IsNullOrWhiteSpace(seed.PaymentMethod))
        {
            profile.PaymentMethod = seed.PaymentMethod.Trim();
        }

        if (!string.IsNullOrWhiteSpace(seed.ReceiptNumber))
        {
            profile.ReceiptNumber = seed.ReceiptNumber.Trim();
        }

        if (!string.IsNullOrWhiteSpace(seed.Notes))
        {
            profile.Notes = seed.Notes.Trim();
        }

        if (seed.AmountDue is decimal due)
        {
            profile.AmountDue = Math.Max(0, due);
        }

        if (seed.AmountPaid is decimal paid)
        {
            profile.AmountPaid = Math.Max(0, paid);
        }

        profile.BalanceDue = Math.Max(0, profile.AmountDue - profile.AmountPaid);
        profile.RuntRegistered = seed.RuntRegistered;
        profile.IsEnrolled = seed.IsEnrolled;
        profile.UpdatedAt = now;
    }
}
