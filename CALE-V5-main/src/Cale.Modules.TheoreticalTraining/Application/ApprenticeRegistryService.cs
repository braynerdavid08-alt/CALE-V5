using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed class ApprenticeRegistryService
{
    private readonly CaleDbContext _db;
    private readonly IUserStore _users;
    private readonly IClock _clock;
    private readonly TheoryTrainingService _theory;
    private readonly PracticalTrainingService _practical;

    public ApprenticeRegistryService(
        CaleDbContext db,
        IUserStore users,
        IClock clock,
        TheoryTrainingService theory,
        PracticalTrainingService practical)
    {
        _db = db;
        _users = users;
        _clock = clock;
        _theory = theory;
        _practical = practical;
    }

    public async Task<IReadOnlyList<ApprenticeDto>> ListAsync(
        int schoolUserId,
        string? search,
        string? month,
        bool? withBalance,
        CancellationToken ct)
    {
        var students = await _users.ListBySchoolAsync(schoolUserId, ct);
        var studentMap = students.Where(x => x.Role == Roles.Student).ToDictionary(x => x.Id);
        var enrollments = await _db.Set<SchoolStudentEnrollment>()
            .Where(x => x.SchoolUserId == schoolUserId)
            .ToListAsync(ct);
        var enrollmentMap = enrollments.ToDictionary(x => x.StudentUserId);
        var profiles = await _db.Set<SchoolApprenticeProfile>()
            .Where(x => x.SchoolUserId == schoolUserId)
            .ToListAsync(ct);
        var profileMap = profiles.ToDictionary(x => x.StudentUserId);

        var ids = studentMap.Keys.Union(profileMap.Keys).Distinct();
        var rows = new List<ApprenticeDto>();
        foreach (var studentUserId in ids)
        {
            if (!studentMap.TryGetValue(studentUserId, out var user))
            {
                continue;
            }

            profileMap.TryGetValue(studentUserId, out var profile);
            enrollmentMap.TryGetValue(studentUserId, out var enrollment);
            rows.Add(MapDto(user.Name, user.Email, profile, enrollment));
        }

        IEnumerable<ApprenticeDto> query = rows.OrderBy(x => x.StudentName);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.StudentName.ToLowerInvariant().Contains(q)
                || (x.DocumentNumber ?? "").Contains(q)
                || (x.Phone ?? "").Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(month))
        {
            query = query.Where(x =>
                string.Equals(x.EnrollmentMonth, month, StringComparison.OrdinalIgnoreCase));
        }

        if (withBalance == true)
        {
            query = query.Where(x => x.BalanceDue > 0);
        }

        return query.ToList();
    }

    public async Task<ApprenticeDetailDto> GetDetailAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(studentUserId, ct)
            ?? throw new NotFoundException("Estudiante no encontrado.", "student_not_found");
        if (user.SchoolId != schoolUserId || user.Role != Roles.Student)
        {
            throw new DomainException("El estudiante no pertenece a tu escuela.", 400, "invalid_student");
        }

        var profile = await _db.Set<SchoolApprenticeProfile>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId, ct);
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId, ct);
        var apprentice = MapDto(user.Name, user.Email, profile, enrollment);
        var training = await _theory.GetPracticalEligibilityAsync(schoolUserId, studentUserId, ct);
        var practical = await _practical.GetApprenticePracticalSummaryAsync(schoolUserId, studentUserId, ct);

        var today = DateOnly.FromDateTime(_clock.UtcNow.Date);
        var exam = await _db.Set<TheoryExamAppointment>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId
                && x.ExamDate >= today)
            .OrderBy(x => x.ExamDate)
            .ThenBy(x => x.SlotTime)
            .FirstOrDefaultAsync(ct);

        ApprenticeExamSummaryDto? nextExam = exam is null
            ? null
            : new ApprenticeExamSummaryDto(
                exam.Id,
                exam.ExamDate.ToString("yyyy-MM-dd"),
                exam.SlotTime.ToString("HH:mm"));

        return new ApprenticeDetailDto(apprentice, training, practical, nextExam);
    }

    public async Task<SchoolOperationsDashboardDto> GetDashboardAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var students = await _users.ListBySchoolAsync(schoolUserId, ct);
        var studentMap = students
            .Where(x => x.Role == Roles.Student)
            .ToDictionary(x => x.Id);

        var profiles = await _db.Set<SchoolApprenticeProfile>()
            .Where(x => x.SchoolUserId == schoolUserId)
            .ToListAsync(ct);

        var apprenticeCount = studentMap.Count;
        var balanceRows = new List<SchoolDashboardBalanceRowDto>();

        foreach (var profile in profiles.Where(x => x.BalanceDue > 0))
        {
            if (!studentMap.TryGetValue(profile.StudentUserId, out var user))
            {
                continue;
            }

            balanceRows.Add(new SchoolDashboardBalanceRowDto(
                profile.StudentUserId,
                user.Name,
                profile.BalanceDue));
        }

        var topBalanceDue = balanceRows
            .Where(x => x.BalanceDue > 0)
            .OrderByDescending(x => x.BalanceDue)
            .Take(5)
            .ToList();

        var balancePendingCount = balanceRows.Count(x => x.BalanceDue > 0);
        var balancePendingTotal = balanceRows.Sum(x => x.BalanceDue);
        var pendingEnrollmentCount = profiles.Count(x => !x.IsEnrolled);

        var today = DateOnly.FromDateTime(_clock.UtcNow.Date);
        var weekEnd = today.AddDays(7);
        var examSlots = await _db.Set<TheoryExamAppointment>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.ExamDate >= today
                && x.ExamDate <= weekEnd)
            .OrderBy(x => x.ExamDate)
            .ThenBy(x => x.SlotTime)
            .ToListAsync(ct);

        var upcomingExams = new List<TheoryExamSlotDto>();
        foreach (var slot in examSlots.Take(6))
        {
            string? studentName = null;
            if (slot.StudentUserId is int sid)
            {
                studentName = studentMap.TryGetValue(sid, out var user) ? user.Name : null;
                if (studentName is null)
                {
                    studentName = (await _users.GetByIdAsync(sid, ct))?.Name;
                }
            }

            upcomingExams.Add(new TheoryExamSlotDto(
                slot.Id,
                slot.ExamDate.ToString("yyyy-MM-dd"),
                slot.SlotTime.ToString("HH:mm"),
                slot.StudentUserId,
                slot.StudentLabel,
                studentName,
                slot.Notes));
        }

        return new SchoolOperationsDashboardDto(
            apprenticeCount,
            balancePendingCount,
            balancePendingTotal,
            examSlots.Count,
            pendingEnrollmentCount,
            topBalanceDue,
            upcomingExams);
    }

    public async Task<ApprenticeDto> UpdateAsync(
        int schoolUserId,
        int studentUserId,
        SaveApprenticeRequest request,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(studentUserId, ct)
            ?? throw new NotFoundException("Estudiante no encontrado.", "student_not_found");
        if (user.SchoolId != schoolUserId)
        {
            throw new DomainException("El estudiante no pertenece a tu escuela.", 400, "invalid_student");
        }

        var now = _clock.UtcNow;
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId, ct);
        if (enrollment is null)
        {
            enrollment = new SchoolStudentEnrollment
            {
                SchoolUserId = schoolUserId,
                StudentUserId = studentUserId,
                Status = StudentEnrollmentStatuses.Active,
                CreatedAt = now,
                UpdatedAt = now,
                AcceptedAt = now
            };
            await _db.Set<SchoolStudentEnrollment>().AddAsync(enrollment, ct);
        }

        if (!string.IsNullOrWhiteSpace(request.LicenseCategories))
        {
            enrollment.LicenseCategories = request.LicenseCategories.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(request.AttendanceDayType))
        {
            enrollment.AttendanceDayType = request.AttendanceDayType;
        }

        enrollment.UpdatedAt = now;

        var profile = await _db.Set<SchoolApprenticeProfile>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId, ct);
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

        profile.DocumentType = request.DocumentType;
        profile.DocumentNumber = request.DocumentNumber;
        profile.Phone = request.Phone;
        profile.Address = request.Address;
        profile.ContactEmail = request.ContactEmail;
        profile.EnrollmentMonth = request.EnrollmentMonth;
        profile.EnrollmentDate = request.EnrollmentDate;
        profile.OrderNumber = request.OrderNumber;
        profile.ScheduleSlot = request.ScheduleSlot;
        profile.ReceiptNumber = request.ReceiptNumber;
        profile.AmountDue = request.AmountDue;
        profile.AmountPaid = request.AmountPaid;
        profile.BalanceDue = Math.Max(0, request.AmountDue - request.AmountPaid);
        profile.PaymentMethod = request.PaymentMethod;
        profile.BalancePaymentAmount = request.BalancePaymentAmount;
        profile.AccountsReceivable = request.AccountsReceivable;
        profile.BalancePaymentDate = request.BalancePaymentDate;
        profile.BalancePaymentMethod = request.BalancePaymentMethod;
        profile.BalanceReceiptNumber = request.BalanceReceiptNumber;
        profile.EnrollmentPin = request.EnrollmentPin;
        profile.RuntRegistered = request.RuntRegistered;
        profile.IsEnrolled = request.IsEnrolled;
        profile.Notes = request.Notes;
        profile.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return MapDto(user.Name, user.Email, profile, enrollment);
    }

    public async Task<IReadOnlyList<TheoryExamSlotDto>> ListExamSlotsAsync(
        int schoolUserId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var end = to ?? start.AddDays(30);
        var slots = await _db.Set<TheoryExamAppointment>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.ExamDate >= start
                && x.ExamDate <= end)
            .OrderBy(x => x.ExamDate)
            .ThenBy(x => x.SlotTime)
            .ToListAsync(ct);

        var result = new List<TheoryExamSlotDto>();
        foreach (var slot in slots)
        {
            string? studentName = null;
            if (slot.StudentUserId is int sid)
            {
                var user = await _users.GetByIdAsync(sid, ct);
                studentName = user?.Name;
            }

            result.Add(new TheoryExamSlotDto(
                slot.Id,
                slot.ExamDate.ToString("yyyy-MM-dd"),
                slot.SlotTime.ToString("HH:mm"),
                slot.StudentUserId,
                slot.StudentLabel,
                studentName,
                slot.Notes));
        }

        return result;
    }

    public async Task<TheoryExamSlotDto> SaveExamSlotAsync(
        int schoolUserId,
        int? id,
        SaveTheoryExamSlotRequest request,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var start = ParseTime(request.SlotTime);
        TheoryExamAppointment entity;
        if (id is > 0)
        {
            entity = await _db.Set<TheoryExamAppointment>()
                .FirstOrDefaultAsync(x => x.Id == id && x.SchoolUserId == schoolUserId, ct)
                ?? throw new NotFoundException("Cita no encontrada.", "slot_not_found");
        }
        else
        {
            entity = new TheoryExamAppointment
            {
                SchoolUserId = schoolUserId,
                CreatedAt = now
            };
            await _db.Set<TheoryExamAppointment>().AddAsync(entity, ct);
        }

        entity.ExamDate = request.ExamDate;
        entity.SlotTime = start;
        entity.StudentUserId = request.StudentUserId;
        entity.StudentLabel = string.IsNullOrWhiteSpace(request.StudentLabel)
            ? null
            : request.StudentLabel.Trim();
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        entity.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        string? studentName = null;
        if (entity.StudentUserId is int sid)
        {
            studentName = (await _users.GetByIdAsync(sid, ct))?.Name;
        }

        return new TheoryExamSlotDto(
            entity.Id,
            entity.ExamDate.ToString("yyyy-MM-dd"),
            entity.SlotTime.ToString("HH:mm"),
            entity.StudentUserId,
            entity.StudentLabel,
            studentName,
            entity.Notes);
    }

    public async Task DeleteExamSlotAsync(int schoolUserId, int id, CancellationToken ct)
    {
        var entity = await _db.Set<TheoryExamAppointment>()
            .FirstOrDefaultAsync(x => x.Id == id && x.SchoolUserId == schoolUserId, ct)
            ?? throw new NotFoundException("Cita no encontrada.", "slot_not_found");
        _db.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    private static ApprenticeDto MapDto(
        string studentName,
        string studentEmail,
        SchoolApprenticeProfile? profile,
        SchoolStudentEnrollment? enrollment) =>
        new(
            profile?.Id ?? 0,
            profile?.StudentUserId ?? enrollment?.StudentUserId ?? 0,
            studentName,
            studentEmail,
            profile?.DocumentType,
            profile?.DocumentNumber,
            profile?.Phone,
            profile?.Address,
            profile?.ContactEmail,
            profile?.EnrollmentMonth,
            profile?.EnrollmentDate,
            profile?.OrderNumber,
            enrollment?.LicenseCategories,
            enrollment?.AttendanceDayType,
            profile?.ScheduleSlot,
            profile?.ReceiptNumber,
            profile?.AmountDue ?? 0,
            profile?.AmountPaid ?? 0,
            profile?.BalanceDue ?? 0,
            profile?.PaymentMethod,
            profile?.BalancePaymentAmount,
            profile?.AccountsReceivable ?? 0,
            profile?.BalancePaymentDate,
            profile?.BalancePaymentMethod,
            profile?.BalanceReceiptNumber,
            profile?.EnrollmentPin,
            profile?.RuntRegistered ?? false,
            profile?.IsEnrolled ?? false,
            enrollment?.Status ?? StudentEnrollmentStatuses.Pending,
            profile?.Notes);

    private static TimeOnly ParseTime(string value)
    {
        if (TimeOnly.TryParse(value.Trim(), out var time))
        {
            return time;
        }

        throw new DomainException("La hora no es válida.", 400, "invalid_time");
    }
}
