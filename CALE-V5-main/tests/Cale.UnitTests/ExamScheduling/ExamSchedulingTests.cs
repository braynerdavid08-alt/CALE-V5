using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.TheoreticalTraining.Application;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.UnitTests.ExamScheduling;

public sealed class ExamSchedulingTests : IDisposable
{
    private static readonly DateOnly Wed7 = new(2026, 10, 7);
    private static readonly DateOnly Wed14 = new(2026, 10, 14);
    private static readonly DateOnly Fri9 = new(2026, 10, 9);
    private static readonly DateOnly Mon12 = new(2026, 10, 12);

    private readonly ExamSchedulingFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    private async Task SeedWeeklyAsync(int capacity = 1, params string[] times)
    {
        await using var db = _fx.NewDb();
        var service = _fx.Exams(db);
        foreach (var time in times.Length == 0 ? ["09:00", "10:00", "11:00", "14:00", "15:00"] : times)
        {
            await service.CreateTemplatesAsync(_fx.SchoolA, _fx.SchoolA,
                new CreateExamTemplatesRequest([1, 2, 3, 4, 5], time, capacity), default);
        }
    }

    private async Task<ExamSlotDto> SlotAsync(DateOnly date, string time, int? school = null)
    {
        await using var db = _fx.NewDb();
        var day = await _fx.Exams(db).GetDayAsync(school ?? _fx.SchoolA, date, default);
        return Assert.Single(day.Slots, s => s.Time == time);
    }

    private async Task<StudentExamBookingDto> StudentBookAsync(int student, DateOnly date, string time)
    {
        await using var db = _fx.NewDb();
        return await _fx.Exams(db).BookAsStudentAsync(student, new StudentBookExamRequest(date, time), default);
    }

    // Caso 1
    [Fact]
    public async Task Creates_recurring_template_with_default_capacity_one()
    {
        await using var db = _fx.NewDb();
        var created = await _fx.Exams(db).CreateTemplatesAsync(_fx.SchoolA, _fx.SchoolA,
            new CreateExamTemplatesRequest([1], "09:00", null), default);

        var template = Assert.Single(created);
        Assert.Equal(1, template.DayOfWeek);
        Assert.Equal("09:00", template.Time);
        Assert.Equal(1, template.Capacity);
        Assert.True(template.IsActive);
    }

    [Fact]
    public async Task Creating_same_template_twice_never_duplicates()
    {
        await SeedWeeklyAsync(1, "09:00");
        await SeedWeeklyAsync(3, "09:00");

        await using var db = _fx.NewDb();
        var templates = await _fx.Exams(db).ListTemplatesAsync(_fx.SchoolA, default);
        Assert.Equal(5, templates.Count);
        Assert.All(templates, t => Assert.Equal(3, t.Capacity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Capacity_below_one_is_rejected(int capacity)
    {
        await using var db = _fx.NewDb();
        var ex = await Assert.ThrowsAsync<DomainException>(() => _fx.Exams(db).CreateTemplatesAsync(
            _fx.SchoolA, _fx.SchoolA, new CreateExamTemplatesRequest([1], "09:00", capacity), default));
        Assert.Equal("capacity_invalid", ex.ErrorCode);
    }

    // Caso 2
    [Fact]
    public async Task Recurring_template_appears_in_future_weeks()
    {
        await SeedWeeklyAsync();
        await using var db = _fx.NewDb();
        var service = _fx.Exams(db);

        foreach (var weeks in new[] { 0, 1, 4, 8 })
        {
            var monday = ExamSchedulingFixture.Monday.AddDays(7 * weeks);
            var week = await service.GetWeekAsync(_fx.SchoolA, monday, monday.AddDays(6), default);
            Assert.True(week.HasTemplates);
            Assert.Equal(5, week.Days[0].Slots.Count);
            Assert.Equal(5, week.Days[4].Slots.Count);
            Assert.Empty(week.Days[5].Slots);
        }
    }

    // Caso 3
    [Fact]
    public async Task Changing_capacity_from_one_to_three_updates_availability()
    {
        await SeedWeeklyAsync(1, "11:00");
        await using (var db = _fx.NewDb())
        {
            var service = _fx.Exams(db);
            var templates = await service.ListTemplatesAsync(_fx.SchoolA, default);
            var wednesday = templates.Single(t => t.DayOfWeek == 3);
            await service.UpdateTemplateAsync(_fx.SchoolA, _fx.SchoolA, wednesday.Id,
                new UpdateExamTemplateRequest("11:00", 3, true), default);
        }

        var slot = await SlotAsync(Wed7, "11:00");
        Assert.Equal(3, slot.Capacity);
        Assert.Equal(3, slot.Available);
        Assert.Equal(TheoryExamSlotStatuses.Available, slot.Status);
    }

    // Casos 4, 17 y 33 (ejemplo completo)
    [Fact]
    public async Task Date_override_changes_capacity_only_that_date()
    {
        await SeedWeeklyAsync(1, "14:00");
        await using (var db = _fx.NewDb())
        {
            await _fx.Exams(db).SaveSlotOverrideAsync(_fx.SchoolA, _fx.SchoolA,
                new SaveExamSlotOverrideRequest(Wed14, "14:00", 4, false, null), default);
        }

        var students = new[] { _fx.Juan, _fx.Maria, _fx.Carlos, _fx.Ana };
        for (var i = 0; i < students.Length; i++)
        {
            await StudentBookAsync(students[i], Wed14, "14:00");
            var slot = await SlotAsync(Wed14, "14:00");
            Assert.Equal(4, slot.Capacity);
            Assert.Equal(i + 1, slot.Occupied);
            Assert.Equal(3 - i, slot.Available);
        }

        var full = await SlotAsync(Wed14, "14:00");
        Assert.Equal(TheoryExamSlotStatuses.Full, full.Status);

        var otherWednesday = await SlotAsync(Wed7, "14:00");
        Assert.Equal(1, otherWednesday.Capacity);
        var nextWednesday = await SlotAsync(Wed14.AddDays(7), "14:00");
        Assert.Equal(1, nextWednesday.Capacity);

        await using var check = _fx.NewDb();
        var template = await check.Set<TheoryExamScheduleTemplate>()
            .SingleAsync(t => t.SchoolUserId == _fx.SchoolA && t.DayOfWeek == 3);
        Assert.Equal(1, template.Capacity);
    }

    // Caso 5
    [Fact]
    public async Task Closing_one_hour_on_one_date_keeps_following_weeks()
    {
        await SeedWeeklyAsync(1, "14:00");
        await using (var db = _fx.NewDb())
        {
            await _fx.Exams(db).SaveSlotOverrideAsync(_fx.SchoolA, _fx.SchoolA,
                new SaveExamSlotOverrideRequest(Wed7, "14:00", null, true, "Sin examen"), default);
        }

        Assert.Equal(TheoryExamSlotStatuses.Closed, (await SlotAsync(Wed7, "14:00")).Status);
        Assert.Equal(TheoryExamSlotStatuses.Available, (await SlotAsync(Wed14, "14:00")).Status);
    }

    // Caso 6
    [Fact]
    public async Task Extraordinary_hour_exists_only_on_that_date()
    {
        await SeedWeeklyAsync(1, "09:00");
        await using (var db = _fx.NewDb())
        {
            await _fx.Exams(db).SaveSlotOverrideAsync(_fx.SchoolA, _fx.SchoolA,
                new SaveExamSlotOverrideRequest(Fri9, "16:00", 2, false, null), default);
        }

        var extra = await SlotAsync(Fri9, "16:00");
        Assert.Equal(ExamSlotPlanner.SourceExtra, extra.Source);
        Assert.Equal(2, extra.Capacity);

        await using var db2 = _fx.NewDb();
        var nextFriday = await _fx.Exams(db2).GetDayAsync(_fx.SchoolA, Fri9.AddDays(7), default);
        Assert.DoesNotContain(nextFriday.Slots, s => s.Time == "16:00");
    }

    // Casos 7 y 8
    [Fact]
    public async Task Student_booking_persists_and_updates_counts()
    {
        await SeedWeeklyAsync(1, "10:00");
        var booking = await StudentBookAsync(_fx.Juan, Wed7, "10:00");

        Assert.Equal("Active", booking.Status);
        var slot = await SlotAsync(Wed7, "10:00");
        Assert.Equal(1, slot.Occupied);
        Assert.Equal(0, slot.Available);
        Assert.Equal(TheoryExamSlotStatuses.Full, slot.Status);
        Assert.Equal("Juan Pérez", Assert.Single(slot.Bookings).StudentName);
        Assert.True(slot.Bookings[0].BookedByStudent);
        Assert.Contains(_fx.Notifications.Sent, n => n.UserId == _fx.SchoolA);
    }

    // Caso 9
    [Fact]
    public async Task Booking_full_slot_is_rejected()
    {
        await SeedWeeklyAsync(1, "10:00");
        await StudentBookAsync(_fx.Juan, Wed7, "10:00");

        var ex = await Assert.ThrowsAsync<DomainException>(() => StudentBookAsync(_fx.Maria, Wed7, "10:00"));
        Assert.Equal("exam_slot_full", ex.ErrorCode);
        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("El horario ya no tiene cupos disponibles.", ex.Message);
    }

    // Caso 10
    [Fact]
    public async Task Two_students_racing_for_last_seat_only_one_wins()
    {
        await SeedWeeklyAsync(1, "10:00");

        for (var round = 0; round < 5; round++)
        {
            var date = Wed7.AddDays(7 * round);
            var results = await Task.WhenAll(
                Task.Run(() => TryBookAsync(_fx.Juan, date)),
                Task.Run(() => TryBookAsync(_fx.Maria, date)));

            Assert.Equal(1, results.Count(r => r is null));
            Assert.Equal("exam_slot_full", Assert.Single(results, r => r is not null));

            var slot = await SlotAsync(date, "10:00");
            Assert.Equal(1, slot.Occupied);

            await using var db = _fx.NewDb();
            await db.Set<TheoryExamAppointment>()
                .Where(x => x.ExamDate == date)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, TheoryExamBookingStatuses.Cancelled));
        }
    }

    private async Task<string?> TryBookAsync(int student, DateOnly date)
    {
        try
        {
            await StudentBookAsync(student, date, "10:00");
            return null;
        }
        catch (DomainException ex)
        {
            return ex.ErrorCode;
        }
    }

    [Fact]
    public async Task Database_rejects_a_second_row_in_the_same_seat()
    {
        await SeedWeeklyAsync(1, "10:00");
        await StudentBookAsync(_fx.Juan, Wed7, "10:00");

        await using var db = _fx.NewDb();
        db.Set<TheoryExamAppointment>().Add(new TheoryExamAppointment
        {
            SchoolUserId = _fx.SchoolA,
            ExamDate = Wed7,
            SlotTime = new TimeOnly(10, 0),
            StudentUserId = _fx.Maria,
            SeatNumber = 1,
            CreatedAt = _fx.Clock.UtcNow,
            UpdatedAt = _fx.Clock.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // Caso 11 (y 19)
    [Fact]
    public async Task Cancelling_frees_the_seat_and_keeps_history()
    {
        await SeedWeeklyAsync(1, "10:00");
        var booking = await StudentBookAsync(_fx.Juan, Wed7, "10:00");

        await using (var db = _fx.NewDb())
        {
            await _fx.Exams(db).CancelAsStudentAsync(_fx.Juan, booking.Id, default);
        }

        var slot = await SlotAsync(Wed7, "10:00");
        Assert.Equal(0, slot.Occupied);
        Assert.Equal(1, slot.Available);

        await using (var db = _fx.NewDb())
        {
            var row = await db.Set<TheoryExamAppointment>().SingleAsync(x => x.Id == booking.Id);
            Assert.Equal(TheoryExamBookingStatuses.Cancelled, row.Status);
            Assert.NotNull(row.CancelledAt);
            Assert.Equal(_fx.Juan, row.CancelledByUserId);
        }

        await StudentBookAsync(_fx.Maria, Wed7, "10:00");
        Assert.Equal(1, (await SlotAsync(Wed7, "10:00")).Occupied);
    }

    [Fact]
    public async Task Student_cannot_cancel_inside_min_cancel_window()
    {
        await SeedWeeklyAsync(1, "11:00");
        var booking = await StudentBookAsync(_fx.Juan, Wed7, "11:00");
        _fx.Clock.UtcNow = new DateTime(2026, 10, 7, 15, 30, 0, DateTimeKind.Utc); // 10:30 Colombia

        await using var db = _fx.NewDb();
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _fx.Exams(db).CancelAsStudentAsync(_fx.Juan, booking.Id, default));
        Assert.Equal("exam_cancel_too_late", ex.ErrorCode);
    }

    [Fact]
    public async Task Student_can_hold_only_one_upcoming_exam()
    {
        await SeedWeeklyAsync(1, "09:00", "10:00");
        await StudentBookAsync(_fx.Juan, Wed7, "09:00");

        var ex = await Assert.ThrowsAsync<DomainException>(() => StudentBookAsync(_fx.Juan, Wed14, "10:00"));
        Assert.Equal("exam_already_booked", ex.ErrorCode);
    }

    [Fact]
    public async Task Student_not_authorized_cannot_book()
    {
        await SeedWeeklyAsync(1, "09:00");
        _fx.Eligibility.Blocked.Add(_fx.Juan);

        var ex = await Assert.ThrowsAsync<DomainException>(() => StudentBookAsync(_fx.Juan, Wed7, "09:00"));
        Assert.Equal("theory_exam_not_authorized", ex.ErrorCode);

        await using var db = _fx.NewDb();
        var availability = await _fx.Exams(db).GetStudentAvailabilityAsync(_fx.Juan, null, null, default);
        Assert.False(availability.CanBook);
        Assert.NotNull(availability.BlockReason);
    }

    // Caso 12
    [Fact]
    public async Task Student_only_sees_and_books_own_school_schedule()
    {
        await SeedWeeklyAsync(1, "09:00");

        await using (var db = _fx.NewDb())
        {
            var availability = await _fx.Exams(db).GetStudentAvailabilityAsync(_fx.Pedro, Wed7, Wed7, default);
            Assert.Empty(availability.Days);
        }

        var ex = await Assert.ThrowsAsync<DomainException>(() => StudentBookAsync(_fx.Pedro, Wed7, "09:00"));
        Assert.Equal("exam_slot_not_found", ex.ErrorCode);
        Assert.Equal(0, (await SlotAsync(Wed7, "09:00")).Occupied);
    }

    // Caso 13
    [Fact]
    public async Task School_cannot_read_or_change_another_school_data()
    {
        await SeedWeeklyAsync(1, "09:00");
        var booking = await StudentBookAsync(_fx.Juan, Wed7, "09:00");

        await using var db = _fx.NewDb();
        var service = _fx.Exams(db);
        var otherWeek = await service.GetWeekAsync(_fx.SchoolB, ExamSchedulingFixture.Monday, null, default);
        Assert.False(otherWeek.HasTemplates);
        Assert.All(otherWeek.Days, d => Assert.Empty(d.Slots));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CancelAsSchoolAsync(_fx.SchoolB, _fx.SchoolB, booking.Id, null, default));
        var templateId = (await service.ListTemplatesAsync(_fx.SchoolA, default)).First().Id;
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateTemplateAsync(_fx.SchoolB, _fx.SchoolB, templateId,
                new UpdateExamTemplateRequest("09:00", 9, true), default));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.DeleteTemplateAsync(_fx.SchoolB, _fx.SchoolB, templateId, default));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _fx.Hours(db).GetAsync(_fx.SchoolB, _fx.Juan, default));
        Assert.Equal(1, (await SlotAsync(Wed7, "09:00")).Occupied);
    }

    // Caso 18
    [Fact]
    public async Task Closed_day_is_not_offered_to_students()
    {
        await SeedWeeklyAsync(1, "09:00", "10:00");
        await using (var db = _fx.NewDb())
        {
            await _fx.Exams(db).CloseDayAsync(_fx.SchoolA, _fx.SchoolA, Mon12,
                new CloseExamDayRequest("Festivo"), default);
        }

        await using (var db = _fx.NewDb())
        {
            var availability = await _fx.Exams(db)
                .GetStudentAvailabilityAsync(_fx.Juan, Mon12, Mon12.AddDays(1), default);
            Assert.DoesNotContain(availability.Days, d => d.Date == "2026-10-12");
            Assert.Contains(availability.Days, d => d.Date == "2026-10-13");
        }

        var ex = await Assert.ThrowsAsync<DomainException>(() => StudentBookAsync(_fx.Juan, Mon12, "09:00"));
        Assert.Equal("exam_slot_closed", ex.ErrorCode);
    }

    [Fact]
    public async Task Closing_hour_with_bookings_requires_confirmation_and_cancels()
    {
        await SeedWeeklyAsync(1, "10:00");
        await StudentBookAsync(_fx.Juan, Wed7, "10:00");

        await using (var db = _fx.NewDb())
        {
            var ex = await Assert.ThrowsAsync<DomainException>(() => _fx.Exams(db).SaveSlotOverrideAsync(
                _fx.SchoolA, _fx.SchoolA, new SaveExamSlotOverrideRequest(Wed7, "10:00", null, true, null), default));
            Assert.Equal("slot_has_bookings", ex.ErrorCode);
        }

        await using (var db = _fx.NewDb())
        {
            await _fx.Exams(db).SaveSlotOverrideAsync(_fx.SchoolA, _fx.SchoolA,
                new SaveExamSlotOverrideRequest(Wed7, "10:00", null, true, null, CancelBookings: true), default);
        }

        var slot = await SlotAsync(Wed7, "10:00");
        Assert.Equal(TheoryExamSlotStatuses.Closed, slot.Status);
        Assert.Equal(0, slot.Occupied);
        Assert.Contains(_fx.Notifications.Sent, n => n.UserId == _fx.Juan);
    }

    [Fact]
    public async Task Capacity_cannot_drop_below_existing_bookings()
    {
        await SeedWeeklyAsync(3, "10:00");
        await StudentBookAsync(_fx.Juan, Wed7, "10:00");
        await StudentBookAsync(_fx.Maria, Wed7, "10:00");

        await using var db = _fx.NewDb();
        var ex = await Assert.ThrowsAsync<DomainException>(() => _fx.Exams(db).SaveSlotOverrideAsync(
            _fx.SchoolA, _fx.SchoolA, new SaveExamSlotOverrideRequest(Wed7, "10:00", 1, false, null), default));
        Assert.Equal("capacity_below_bookings", ex.ErrorCode);
    }

    [Fact]
    public async Task Past_slots_are_marked_and_not_bookable()
    {
        await SeedWeeklyAsync(1, "09:00");
        _fx.Clock.UtcNow = new DateTime(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc); // lunes 10:00 Colombia

        var slot = await SlotAsync(ExamSchedulingFixture.Monday, "09:00");
        Assert.Equal(TheoryExamSlotStatuses.Past, slot.Status);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            StudentBookAsync(_fx.Juan, ExamSchedulingFixture.Monday, "09:00"));
        Assert.Equal("exam_slot_past", ex.ErrorCode);
    }

    [Fact]
    public async Task Late_evening_in_colombia_does_not_shift_the_day()
    {
        await SeedWeeklyAsync(1, "09:00");
        // Tuesday 6 Oct 23:30 Colombia = Wednesday 04:30 UTC.
        _fx.Clock.UtcNow = new DateTime(2026, 10, 7, 4, 30, 0, DateTimeKind.Utc);

        await using var db = _fx.NewDb();
        var availability = await _fx.Exams(db).GetStudentAvailabilityAsync(_fx.Juan, null, null, default);
        Assert.Equal("2026-10-06", availability.From);
        var wednesday = Assert.Single(availability.Days, d => d.Date == "2026-10-07");
        Assert.Equal("09:00", Assert.Single(wednesday.Slots).Time);
    }

    // Casos 14, 15 y 16
    [Fact]
    public async Task Manual_hours_adjustment_updates_totals_and_is_audited()
    {
        await using (var db = _fx.NewDb())
        {
            var hours = _fx.Hours(db);
            var before = await hours.GetAsync(_fx.SchoolA, _fx.Juan, default);
            Assert.Equal(0, before.Theory.Total);

            var afterTheory = await hours.AdjustAsync(_fx.SchoolA, _fx.SchoolA, _fx.Juan,
                new AdjustStudentHoursRequest("Theory", 18, "Registro de horas realizadas externamente"), default);
            Assert.Equal(18, afterTheory.Theory.Total);
            Assert.Equal(afterTheory.Theory.Required - 18, afterTheory.Theory.Pending);

            afterTheory = await hours.AdjustAsync(_fx.SchoolA, _fx.SchoolA, _fx.Juan,
                new AdjustStudentHoursRequest("Theory", 20, "Corrección de asistencia"), default);
            Assert.Equal(20, afterTheory.Theory.Total);
            Assert.Equal(20, afterTheory.Theory.Adjusted);

            var afterWorkshop = await hours.AdjustAsync(_fx.SchoolA, _fx.SchoolA, _fx.Juan,
                new AdjustStudentHoursRequest("Workshop", 2.5m, "Taller presencial"), default);
            Assert.Equal(2.5m, afterWorkshop.Workshop.Total);
            Assert.Equal(20, afterWorkshop.Theory.Total);
            Assert.Equal(3, afterWorkshop.History.Count);
        }

        await using (var db = _fx.NewDb())
        {
            var breakdown = await TrainingHoursCalculator.ComputeAsync(db, _fx.SchoolA, _fx.Juan, default);
            Assert.Equal(20, breakdown.TheoryTotal);
            Assert.Equal(2.5m, breakdown.WorkshopTotal);

            var audit = await new SchoolAuditService(db).ListAsync(_fx.SchoolA, _fx.Juan, SchoolAuditAreas.StudentHours, 10, default);
            Assert.Equal(3, audit.Count);
            var last = audit.Single(a => a.Action == "theory_hours_adjusted" && a.NewValue == "20");
            Assert.Equal("18", last.OldValue);
            Assert.Equal("Corrección de asistencia", last.Reason);
            Assert.Equal("Escuela A", last.ActorName);
            Assert.Equal("Juan Pérez", last.StudentName);
        }
    }

    [Fact]
    public async Task Hours_adjustment_requires_reason_and_valid_value()
    {
        await using var db = _fx.NewDb();
        var hours = _fx.Hours(db);
        var noReason = await Assert.ThrowsAsync<DomainException>(() => hours.AdjustAsync(_fx.SchoolA, _fx.SchoolA,
            _fx.Juan, new AdjustStudentHoursRequest("Theory", 5, " "), default));
        Assert.Equal("hours_reason_required", noReason.ErrorCode);

        var negative = await Assert.ThrowsAsync<DomainException>(() => hours.AdjustAsync(_fx.SchoolA, _fx.SchoolA,
            _fx.Juan, new AdjustStudentHoursRequest("Theory", -1, "Motivo válido"), default));
        Assert.Equal("hours_invalid", negative.ErrorCode);

        var same = await Assert.ThrowsAsync<DomainException>(() => hours.AdjustAsync(_fx.SchoolA, _fx.SchoolA,
            _fx.Juan, new AdjustStudentHoursRequest("Theory", 0, "Motivo válido"), default));
        Assert.Equal("hours_unchanged", same.ErrorCode);
    }

    [Fact]
    public async Task Schedule_and_booking_changes_are_audited()
    {
        await SeedWeeklyAsync(1, "10:00");
        await using (var db = _fx.NewDb())
        {
            await _fx.Exams(db).SaveSlotOverrideAsync(_fx.SchoolA, _fx.SchoolA,
                new SaveExamSlotOverrideRequest(Wed7, "10:00", 2, false, null), default);
        }

        var booking = await StudentBookAsync(_fx.Juan, Wed7, "10:00");
        await using (var db = _fx.NewDb())
        {
            await _fx.Exams(db).CancelAsSchoolAsync(_fx.SchoolA, _fx.SchoolA, booking.Id, "Reprogramado", default);
        }

        await using var check = _fx.NewDb();
        var audit = await new SchoolAuditService(check).ListAsync(_fx.SchoolA, null, null, 50, default);
        Assert.Contains(audit, a => a.Action == "template_created");
        Assert.Contains(audit, a => a.Action == "slot_capacity_changed" && a.NewValue == "2");
        Assert.Contains(audit, a => a.Action == "booking_created" && a.StudentUserId == _fx.Juan);
        Assert.Contains(audit, a => a.Action == "booking_cancelled" && a.Reason == "Reprogramado");
    }

    [Fact]
    public async Task Bookings_in_a_removed_hour_stay_visible_as_closed()
    {
        await SeedWeeklyAsync(1, "10:00");
        await StudentBookAsync(_fx.Juan, Wed7, "10:00");
        await using (var db = _fx.NewDb())
        {
            var service = _fx.Exams(db);
            var template = (await service.ListTemplatesAsync(_fx.SchoolA, default)).Single(t => t.DayOfWeek == 3);
            await service.DeleteTemplateAsync(_fx.SchoolA, _fx.SchoolA, template.Id, default);
        }

        var slot = await SlotAsync(Wed7, "10:00");
        Assert.Equal(TheoryExamSlotStatuses.Closed, slot.Status);
        Assert.Single(slot.Bookings);
    }

    [Fact]
    public async Task Planner_combines_templates_and_overrides()
    {
        var templates = new List<TheoryExamScheduleTemplate>
        {
            new() { Id = 1, DayOfWeek = 3, StartTime = new TimeOnly(9, 0), Capacity = 1, IsActive = true },
            new() { Id = 2, DayOfWeek = 3, StartTime = new TimeOnly(14, 0), Capacity = 1, IsActive = true },
            new() { Id = 3, DayOfWeek = 3, StartTime = new TimeOnly(15, 0), Capacity = 1, IsActive = false }
        };
        var overrides = new List<TheoryExamScheduleOverride>
        {
            new() { Date = Wed7, StartTime = new TimeOnly(9, 0), IsClosed = true },
            new() { Date = Wed7, StartTime = new TimeOnly(14, 0), Capacity = 5 },
            new() { Date = Wed7, StartTime = new TimeOnly(16, 0), Capacity = 2 }
        };

        var day = ExamSlotPlanner.PlanDay(Wed7, templates, overrides);
        Assert.Equal(3, day.Slots.Count);
        Assert.True(day.Slots[0].IsClosed);
        Assert.Equal(5, day.Slots[1].Capacity);
        Assert.Equal(ExamSlotPlanner.SourceExtra, day.Slots[2].Source);

        var nextWeek = ExamSlotPlanner.PlanDay(Wed14, templates, overrides);
        Assert.Equal(2, nextWeek.Slots.Count);
        Assert.All(nextWeek.Slots, s => Assert.Equal(1, s.Capacity));
        await Task.CompletedTask;
    }
}
