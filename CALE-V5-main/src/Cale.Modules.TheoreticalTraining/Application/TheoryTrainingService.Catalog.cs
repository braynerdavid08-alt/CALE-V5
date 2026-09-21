using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed partial class TheoryTrainingService
{
    // ── Topics ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TheoryTopicDto>> ListTopicsAsync(
        int schoolUserId,
        bool activeOnly,
        CancellationToken ct)
    {
        var query = _db.Set<TheoryTopic>().Where(x => x.SchoolUserId == schoolUserId);
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new TheoryTopicDto(x.Id, x.Name, x.Description, x.Color, x.Category, x.IsActive))
            .ToListAsync(ct);
    }

    public async Task<TheoryTopicDto> SaveTopicAsync(
        int schoolUserId,
        int? id,
        SaveTheoryTopicRequest request,
        CancellationToken ct)
    {
        await EnsureSchoolMembershipActiveAsync(schoolUserId, ct);
        var now = _clock.UtcNow;
        TheoryTopic entity;
        if (id is > 0)
        {
            entity = await _db.Set<TheoryTopic>()
                .FirstOrDefaultAsync(x => x.Id == id && x.SchoolUserId == schoolUserId, ct)
                ?? throw new NotFoundException("Tema no encontrado.", "topic_not_found");
        }
        else
        {
            entity = new TheoryTopic { SchoolUserId = schoolUserId, CreatedAt = now };
            await _db.Set<TheoryTopic>().AddAsync(entity, ct);
        }

        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        entity.Color = string.IsNullOrWhiteSpace(request.Color) ? "#3B82F6" : request.Color.Trim();
        entity.Category = TheoryTopicCategories.IsValid(request.Category)
            ? request.Category
            : TheoryTopicCategories.InferFromName(request.Name);
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return new TheoryTopicDto(entity.Id, entity.Name, entity.Description, entity.Color, entity.Category, entity.IsActive);
    }

    // ── Classrooms ──────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TheoryClassroomDto>> ListClassroomsAsync(
        int schoolUserId,
        bool activeOnly,
        CancellationToken ct)
    {
        var query = _db.Set<TheoryClassroom>().Where(x => x.SchoolUserId == schoolUserId);
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new TheoryClassroomDto(
                x.Id, x.Name, x.Identifier, x.Capacity, x.Location, x.IsActive))
            .ToListAsync(ct);
    }

    public async Task<TheoryClassroomDto> SaveClassroomAsync(
        int schoolUserId,
        int? id,
        SaveTheoryClassroomRequest request,
        CancellationToken ct)
    {
        await EnsureSchoolMembershipActiveAsync(schoolUserId, ct);
        if (request.Capacity < 1)
        {
            throw new DomainException("La capacidad debe ser al menos 1.", 400, "invalid_capacity");
        }

        var now = _clock.UtcNow;
        TheoryClassroom entity;
        if (id is > 0)
        {
            entity = await _db.Set<TheoryClassroom>()
                .FirstOrDefaultAsync(x => x.Id == id && x.SchoolUserId == schoolUserId, ct)
                ?? throw new NotFoundException("Aula no encontrada.", "classroom_not_found");
        }
        else
        {
            entity = new TheoryClassroom { SchoolUserId = schoolUserId, CreatedAt = now };
            await _db.Set<TheoryClassroom>().AddAsync(entity, ct);
        }

        entity.Name = request.Name.Trim();
        entity.Identifier = string.IsNullOrWhiteSpace(request.Identifier)
            ? null
            : request.Identifier.Trim();
        entity.Capacity = request.Capacity;
        entity.Location = string.IsNullOrWhiteSpace(request.Location)
            ? null
            : request.Location.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return new TheoryClassroomDto(
            entity.Id, entity.Name, entity.Identifier, entity.Capacity, entity.Location, entity.IsActive);
    }

    // ── Settings ────────────────────────────────────────────────────────

    public async Task<TheorySettingsDto> GetSettingsAsync(int schoolUserId, CancellationToken ct)
    {
        try
        {
            var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
            return MapSettings(settings);
        }
        catch (Exception ex)
        {
            if (!AllowRequestPathRepair)
            {
                throw;
            }

            _logger.LogError(ex, "GetSettings failed for school {SchoolUserId}; repairing", schoolUserId);
            await FeatureSchema.EnsureTheoryTrainingColumnsAsync(_db, ct);
            await BackfillSettingsJsonNullsAsync(ct);
            _db.ChangeTracker.Clear();
            return MapSettings(await GetOrCreateSettingsAsync(schoolUserId, ct));
        }
    }

    public async Task<TheorySettingsDto> UpdateSettingsAsync(
        int schoolUserId,
        TheorySettingsDto request,
        CancellationToken ct)
    {
        await EnsureSchoolMembershipActiveAsync(schoolUserId, ct);
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        settings.DefaultDurationMinutes = Math.Clamp(request.DefaultDurationMinutes, 30, 240);
        settings.MinCancelHours = Math.Clamp(request.MinCancelHours, 0, 72);
        settings.ReservationCloseMinutesBefore = Math.Clamp(request.ReservationCloseMinutesBefore, 0, 180);
        // Hour requirements are platform constants — never accept client overrides.
        settings.RequiredTheoryHours = TheoryHourStandards.DefaultTheoryHours;
        settings.RequiredWorkshopHours = TheoryHourStandards.DefaultWorkshopHours;
        settings.TheoryExamId = request.TheoryExamId;
        settings.WeekdaysEnabled = request.WeekdaysEnabled;
        settings.SaturdayEnabled = request.SaturdayEnabled;
        settings.MaxWeekdayClassesPerDay = Math.Clamp(request.MaxWeekdayClassesPerDay, 0, 10);
        settings.MaxSaturdayClassesPerDay = Math.Clamp(request.MaxSaturdayClassesPerDay, 0, 12);
        settings.MaxDailyTheoryMinutes = Math.Clamp(request.MaxDailyTheoryMinutes, 0, 720);
        settings.WeekdayReservationOpenDaysBefore = Math.Clamp(request.WeekdayReservationOpenDaysBefore, 0, 14);
        settings.SaturdayReservationOpenDaysBefore = Math.Clamp(request.SaturdayReservationOpenDaysBefore, 0, 14);
        settings.StudentBookingWindowStart = TheoryBookingPolicy.ParseOptionalTime(request.StudentBookingWindowStart);
        settings.StudentBookingWindowEnd = TheoryBookingPolicy.ParseOptionalTime(request.StudentBookingWindowEnd);
        settings.LicenseCategoryPoliciesJson = "{}";
        settings.SavedBookingPresetsJson = TheoryBookingPresetHelper.SerializeSaved(
            request.SavedBookingPresets);
        settings.HiddenBookingPresetKeysJson = TheoryBookingPresetHelper.SerializeHidden(
            request.HiddenBookingPresetKeys);
        settings.NotifyReservationOpen = request.NotifyReservationOpen;
        settings.NotifyClassReminder24h = request.NotifyClassReminder24h;
        settings.NotifyClassReminder1h = request.NotifyClassReminder1h;
        settings.NotifyExamReminder24h = request.NotifyExamReminder24h;
        settings.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapSettings(settings);
    }
}
