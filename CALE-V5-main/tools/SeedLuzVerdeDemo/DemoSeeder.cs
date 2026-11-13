using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.Identity.Domain;
using Cale.Modules.TheoreticalTraining.Application;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace SeedLuzVerdeDemo;

internal sealed class DemoSeeder
{
    private readonly CaleDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public DemoSeeder(CaleDbContext db, IPasswordHasher hasher, IClock clock)
    {
        _db = db;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<IReadOnlyList<DemoCredentialSet>> SeedAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var today = ColombiaTime.TodayInColombia();
        var credentials = new List<DemoCredentialSet>();

        var (bankId, examId) = await EnsureDemoExamAsync(now, ct);

        foreach (var school in DemoMarkers.Schools)
        {
            var set = await SeedSchoolAsync(school, bankId, examId, today, now, ct);
            credentials.Add(set);
        }

        return credentials;
    }

    private async Task<(int BankId, int ExamId)> EnsureDemoExamAsync(DateTime now, CancellationToken ct)
    {
        var bank = await _db.Set<Bank>()
            .FirstOrDefaultAsync(b => b.Name == DemoMarkers.BankName, ct);
        if (bank is null)
        {
            bank = Bank.Create(DemoMarkers.BankName, "Banco DEMO (sin preguntas reales requeridas para Attempt)", now);
            _db.Set<Bank>().Add(bank);
            await _db.SaveChangesAsync(ct);
        }

        var exam = await _db.Set<Exam>()
            .FirstOrDefaultAsync(e => e.Name == DemoMarkers.ExamName, ct);
        if (exam is null)
        {
            // CreatedById=0 is fine for seed; Publish so IsOpenAt works if needed.
            exam = Exam.Create(
                DemoMarkers.ExamName,
                "Examen teórico oficial DEMO para CEAs de demostración",
                bank.Id,
                questionCount: 40,
                timeMinutes: 60,
                allowedAttempts: 10,
                randomize: false,
                createdById: 0,
                startsAt: null,
                endsAt: null,
                utcNow: now);
            exam.SetPublished(true, now);
            _db.Set<Exam>().Add(exam);
            await _db.SaveChangesAsync(ct);
        }
        else if (!exam.Published)
        {
            exam.SetPublished(true, now);
            await _db.SaveChangesAsync(ct);
        }

        return (bank.Id, exam.Id);
    }

    private async Task<DemoCredentialSet> SeedSchoolAsync(
        DemoSchoolSpec spec,
        int bankId,
        int examId,
        DateOnly today,
        DateTime now,
        CancellationToken ct)
    {
        var ownerPassword = DemoCredentials.OwnerPasswordForMode();
        var owner = await EnsureOwnerAsync(spec, ownerPassword, now, ct);
        var profile = await EnsureSchoolProfileAsync(owner, spec, now, ct);

        var instructors = await EnsureInstructorsAsync(owner.Id, spec, now, ct);
        var catalog = await EnsureTheoryCatalogAsync(owner.Id, examId, now, ct);
        var vehicles = await EnsureVehiclesAsync(owner.Id, spec, now, ct);

        var theorySessions = await EnsureTheorySessionsAsync(
            owner.Id, catalog.TheoryTopicId, catalog.WorkshopTopicId, catalog.ClassroomId,
            instructors[0].User.Id, today, now, ct);

        var set = new DemoCredentialSet
        {
            SchoolName = spec.LegalName,
            OwnerEmail = spec.OwnerEmail,
            OwnerPassword = ownerPassword,
            Instructors = instructors
                .Select(i => new DemoUserCredential
                {
                    Scenario = 0,
                    Role = "Instructor",
                    Name = i.User.Name,
                    Email = i.User.Email,
                    Password = i.Password
                })
                .ToList(),
            Students = []
        };

        for (var scenario = 1; scenario <= 14; scenario++)
        {
            var studentCred = await SeedStudentScenarioAsync(
                owner.Id,
                spec,
                scenario,
                bankId,
                examId,
                instructors.Select(x => x.User).ToList(),
                vehicles,
                theorySessions,
                today,
                now,
                ct);
            set.Students.Add(studentCred);
        }

        _ = profile; // profile already persisted with courtesy
        await _db.SaveChangesAsync(ct);
        return set;
    }

    private async Task<User> EnsureOwnerAsync(
        DemoSchoolSpec spec,
        string password,
        DateTime now,
        CancellationToken ct)
    {
        var user = await _db.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == spec.OwnerEmail, ct);
        if (user is null)
        {
            user = User.RegisterSchool(spec.OwnerName, spec.OwnerEmail, _hasher.Hash(password), now);
            user.MarkEmailConfirmed();
            _db.Set<User>().Add(user);
            await _db.SaveChangesAsync(ct);
            return user;
        }

        user.ChangePassword(_hasher.Hash(password));
        if (!user.EmailConfirmed)
        {
            user.MarkEmailConfirmed();
        }

        if (!user.IsActive)
        {
            user.Activate();
        }

        await _db.SaveChangesAsync(ct);
        return user;
    }

    private async Task<SchoolProfile> EnsureSchoolProfileAsync(
        User owner,
        DemoSchoolSpec spec,
        DateTime now,
        CancellationToken ct)
    {
        var plan = SchoolPlans.Find(SchoolPlans.Monthly)!;
        var profile = await _db.Set<SchoolProfile>()
            .FirstOrDefaultAsync(p => p.UserId == owner.Id, ct);

        if (profile is null)
        {
            profile = SchoolProfile.Create(
                owner.Id,
                spec.LegalName,
                spec.TaxId,
                spec.OwnerEmail,
                spec.Phone,
                spec.Address,
                spec.City,
                spec.Department,
                plan,
                now);
            _db.Set<SchoolProfile>().Add(profile);
            await _db.SaveChangesAsync(ct);
        }

        // Operational demo access without commercial confirmation / directory.
        // PaymentAdminStatus stays None (Create default) — never Confirmed.
        if (!profile.IsCourtesyActive(now))
        {
            profile.GrantCourtesy(now.AddDays(120), DemoMarkers.CourtesyReason, now);
        }

        if (profile.IsPendingApproval)
        {
            profile.Approve(now);
        }

        await _db.SaveChangesAsync(ct);

        if (profile.PaymentAdminStatus == PaymentAdminStatuses.Confirmed)
        {
            profile.SetPaymentAdminStatus(PaymentAdminStatuses.None, now);
            await _db.SaveChangesAsync(ct);
        }

        return profile;
    }

    private sealed record InstructorSeed(User User, string Password);

    private async Task<List<InstructorSeed>> EnsureInstructorsAsync(
        int schoolId,
        DemoSchoolSpec spec,
        DateTime now,
        CancellationToken ct)
    {
        var list = new List<InstructorSeed>();
        for (var i = 0; i < 3; i++)
        {
            var email = DemoMarkers.InstructorEmail(spec.Key, i + 1);
            var name = $"{DemoMarkers.InstructorFirstNames[i]} {DemoMarkers.InstructorLastNames[i]} ({spec.Key.ToUpperInvariant()})";
            var password = DemoCredentials.GeneratePassword();
            var user = await _db.Set<User>().FirstOrDefaultAsync(u => u.Email == email, ct);
            if (user is null)
            {
                user = User.CreateTeacher(name, email, _hasher.Hash(password), now, schoolId, emailConfirmed: true);
                user.MarkCreatedBySchool(schoolId);
                _db.Set<User>().Add(user);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                user.ChangePassword(_hasher.Hash(password));
                user.AssignSchool(schoolId);
                user.MarkCreatedBySchool(schoolId);
                if (!user.EmailConfirmed)
                {
                    user.MarkEmailConfirmed();
                }

                await _db.SaveChangesAsync(ct);
            }

            list.Add(new InstructorSeed(user, password));
        }

        return list;
    }

    private sealed record TheoryCatalog(int TheoryTopicId, int WorkshopTopicId, int ClassroomId);

    private async Task<TheoryCatalog> EnsureTheoryCatalogAsync(
        int schoolId,
        int examId,
        DateTime now,
        CancellationToken ct)
    {
        async Task<TheoryTopic> TopicAsync(string name, string category)
        {
            var t = await _db.Set<TheoryTopic>()
                .FirstOrDefaultAsync(x => x.SchoolUserId == schoolId && x.Name == name, ct);
            if (t is not null)
            {
                return t;
            }

            t = new TheoryTopic
            {
                SchoolUserId = schoolId,
                Name = name,
                Description = $"{DemoMarkers.Tag} {name}",
                Color = category == TheoryTopicCategories.Workshop ? "#F59E0B" : "#16A34A",
                Category = category,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.Set<TheoryTopic>().Add(t);
            await _db.SaveChangesAsync(ct);
            return t;
        }

        var theory = await TopicAsync("Normas y señales (DEMO)", TheoryTopicCategories.Theory);
        var workshop = await TopicAsync("Taller mecánico (DEMO)", TheoryTopicCategories.Workshop);

        var room = await _db.Set<TheoryClassroom>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolId && x.Name == "Aula DEMO 1", ct);
        if (room is null)
        {
            room = new TheoryClassroom
            {
                SchoolUserId = schoolId,
                Name = "Aula DEMO 1",
                Identifier = "DEMO-A1",
                Capacity = 20,
                Location = "Planta baja",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.Set<TheoryClassroom>().Add(room);
            await _db.SaveChangesAsync(ct);
        }

        var settings = await _db.Set<TheoryTrainingSettings>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolId, ct);
        if (settings is null)
        {
            settings = new TheoryTrainingSettings
            {
                SchoolUserId = schoolId,
                DefaultDurationMinutes = 120,
                RequiredTheoryHours = TheoryHourStandards.DefaultTheoryHours,
                RequiredWorkshopHours = TheoryHourStandards.DefaultWorkshopHours,
                TheoryExamId = examId,
                WeekdaysEnabled = true,
                SaturdayEnabled = true,
                UpdatedAt = now
            };
            _db.Set<TheoryTrainingSettings>().Add(settings);
        }
        else
        {
            settings.TheoryExamId = examId;
            settings.UpdatedAt = now;
        }

        // Recurring theory-exam slots: Tue/Thu 09:00 capacity 4.
        foreach (var dow in new[] { (int)DayOfWeek.Tuesday, (int)DayOfWeek.Thursday })
        {
            var tmpl = await _db.Set<TheoryExamScheduleTemplate>()
                .FirstOrDefaultAsync(x => x.SchoolUserId == schoolId
                    && x.DayOfWeek == dow
                    && x.StartTime == new TimeOnly(9, 0), ct);
            if (tmpl is null)
            {
                _db.Set<TheoryExamScheduleTemplate>().Add(new TheoryExamScheduleTemplate
                {
                    SchoolUserId = schoolId,
                    DayOfWeek = dow,
                    StartTime = new TimeOnly(9, 0),
                    Capacity = 4,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return new TheoryCatalog(theory.Id, workshop.Id, room.Id);
    }

    private async Task<List<PracticalVehicle>> EnsureVehiclesAsync(
        int schoolId,
        DemoSchoolSpec spec,
        DateTime now,
        CancellationToken ct)
    {
        var labels = new[]
        {
            ($"Moto DEMO {spec.Key.ToUpperInvariant()}", $"DEM{spec.DocPrefix[^2..]}A"),
            ($"Auto DEMO {spec.Key.ToUpperInvariant()}", $"DEM{spec.DocPrefix[^2..]}B"),
            ($"Camión DEMO {spec.Key.ToUpperInvariant()}", $"DEM{spec.DocPrefix[^2..]}C")
        };

        var list = new List<PracticalVehicle>();
        foreach (var (label, plate) in labels)
        {
            var v = await _db.Set<PracticalVehicle>()
                .FirstOrDefaultAsync(x => x.SchoolUserId == schoolId && x.Label == label, ct);
            if (v is null)
            {
                v = new PracticalVehicle
                {
                    SchoolUserId = schoolId,
                    Label = label,
                    Plate = plate,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.Set<PracticalVehicle>().Add(v);
                await _db.SaveChangesAsync(ct);
            }

            list.Add(v);
        }

        return list;
    }

    private sealed record TheorySessionSeed(TheoryClassSession Session, bool IsWorkshop, bool IsPast);

    private async Task<List<TheorySessionSeed>> EnsureTheorySessionsAsync(
        int schoolId,
        int theoryTopicId,
        int workshopTopicId,
        int classroomId,
        int instructorId,
        DateOnly today,
        DateTime now,
        CancellationToken ct)
    {
        // Wipe school theory sessions then recreate (purge already ran, but keep idempotent).
        var existing = await _db.Set<TheoryClassSession>()
            .Where(x => x.SchoolUserId == schoolId)
            .ToListAsync(ct);
        if (existing.Count > 0)
        {
            var ids = existing.Select(x => x.Id).ToList();
            var att = await _db.Set<TheoryAttendanceRecord>()
                .Where(x => ids.Contains(x.ClassSessionId)).ToListAsync(ct);
            var res = await _db.Set<TheoryClassReservation>()
                .Where(x => ids.Contains(x.ClassSessionId)).ToListAsync(ct);
            _db.Set<TheoryAttendanceRecord>().RemoveRange(att);
            _db.Set<TheoryClassReservation>().RemoveRange(res);
            _db.Set<TheoryClassSession>().RemoveRange(existing);
            await _db.SaveChangesAsync(ct);
        }

        var seeds = new List<TheorySessionSeed>();
        // 10 past theory (2h) + 3 past workshop (2h) + 4 future theory + 2 future workshop
        for (var i = 0; i < 10; i++)
        {
            var date = PreviousWeekday(today, i + 1);
            seeds.Add(await AddTheorySessionAsync(
                schoolId, theoryTopicId, classroomId, instructorId, date,
                new TimeOnly(8, 0), new TimeOnly(10, 0), past: true, workshop: false, now, ct));
        }

        for (var i = 0; i < 3; i++)
        {
            var date = PreviousWeekday(today, i + 12);
            seeds.Add(await AddTheorySessionAsync(
                schoolId, workshopTopicId, classroomId, instructorId, date,
                new TimeOnly(10, 0), new TimeOnly(12, 0), past: true, workshop: true, now, ct));
        }

        for (var i = 1; i <= 4; i++)
        {
            var date = NextWeekday(today, i);
            seeds.Add(await AddTheorySessionAsync(
                schoolId, theoryTopicId, classroomId, instructorId, date,
                new TimeOnly(14, 0), new TimeOnly(16, 0), past: false, workshop: false, now, ct));
        }

        for (var i = 1; i <= 2; i++)
        {
            var date = NextWeekday(today, i + 4);
            seeds.Add(await AddTheorySessionAsync(
                schoolId, workshopTopicId, classroomId, instructorId, date,
                new TimeOnly(14, 0), new TimeOnly(16, 0), past: false, workshop: true, now, ct));
        }

        return seeds;
    }

    private async Task<TheorySessionSeed> AddTheorySessionAsync(
        int schoolId,
        int topicId,
        int classroomId,
        int instructorId,
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        bool past,
        bool workshop,
        DateTime now,
        CancellationToken ct)
    {
        var (open, close) = ColombiaTime.ComputeReservationWindow(date, start, 0, 2);
        var session = new TheoryClassSession
        {
            SchoolUserId = schoolId,
            TopicId = topicId,
            ClassroomId = classroomId,
            InstructorUserId = instructorId,
            SessionDate = date,
            StartTime = start,
            EndTime = end,
            Capacity = 20,
            Status = past ? TheoryClassStatuses.Completed : TheoryClassStatuses.Scheduled,
            ReservationOpenAt = open,
            ReservationCloseAt = close,
            Notes = $"{DemoMarkers.Tag} {(workshop ? "taller" : "teoría")}",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Set<TheoryClassSession>().Add(session);
        await _db.SaveChangesAsync(ct);
        return new TheorySessionSeed(session, workshop, past);
    }

    private async Task<DemoUserCredential> SeedStudentScenarioAsync(
        int schoolId,
        DemoSchoolSpec spec,
        int scenario,
        int bankId,
        int examId,
        IReadOnlyList<User> instructors,
        IReadOnlyList<PracticalVehicle> vehicles,
        IReadOnlyList<TheorySessionSeed> theorySessions,
        DateOnly today,
        DateTime now,
        CancellationToken ct)
    {
        var nameInfo = DemoMarkers.StudentNames[scenario - 1];
        var email = DemoMarkers.StudentEmail(spec.Key, scenario);
        var password = DemoCredentials.GeneratePassword();
        var doc = $"{spec.DocPrefix}{scenario:0000}";
        var fullName = $"{nameInfo.First} {nameInfo.Last}";
        var license = nameInfo.License;
        var scenarioEnum = (DemoScenario)scenario;

        var user = await _db.Set<User>().FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            user = User.RegisterStudent(fullName, email, _hasher.Hash(password), now, schoolId);
            user.MarkEmailConfirmed();
            user.MarkCreatedBySchool(schoolId);
            _db.Set<User>().Add(user);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            user.ChangePassword(_hasher.Hash(password));
            user.AssignSchool(schoolId);
            user.MarkCreatedBySchool(schoolId);
            if (!user.EmailConfirmed)
            {
                user.MarkEmailConfirmed();
            }

            await _db.SaveChangesAsync(ct);
        }

        var enrollmentStatus = scenarioEnum == DemoScenario.NewlyEnrolled
            ? StudentEnrollmentStatuses.Pending
            : scenarioEnum == DemoScenario.PracticalExamPassed
                ? StudentEnrollmentStatuses.Completed
                : StudentEnrollmentStatuses.Active;

        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolId && x.StudentUserId == user.Id, ct);
        if (enrollment is null)
        {
            enrollment = new SchoolStudentEnrollment
            {
                SchoolUserId = schoolId,
                StudentUserId = user.Id,
                Status = enrollmentStatus,
                AttendanceDayType = StudentAttendanceDayTypes.Weekday,
                LicenseCategories = license,
                CreatedAt = now,
                UpdatedAt = now,
                AcceptedAt = enrollmentStatus == StudentEnrollmentStatuses.Pending ? null : now.AddDays(-30)
            };
            _db.Set<SchoolStudentEnrollment>().Add(enrollment);
        }
        else
        {
            enrollment.Status = enrollmentStatus;
            enrollment.LicenseCategories = license;
            enrollment.AttendanceDayType = StudentAttendanceDayTypes.Weekday;
            enrollment.UpdatedAt = now;
        }

        // Theory/practical authorization gates
        var needTheoryAuth = scenario >= (int)DemoScenario.TheoryDonePendingExam;
        var needPracticalAuth = scenario >= (int)DemoScenario.PracticalEnabled;
        enrollment.TheoryExamAuthorized = needTheoryAuth;
        enrollment.TheoryExamAuthorizedAt = needTheoryAuth ? now.AddDays(-10) : null;
        enrollment.PracticalAuthorized = needPracticalAuth;
        enrollment.PracticalAuthorizedAt = needPracticalAuth ? now.AddDays(-5) : null;
        await _db.SaveChangesAsync(ct);

        await UpsertApprenticeProfileAsync(schoolId, user.Id, spec, scenario, doc, email, today, now, ct);
        await ApplyTheoryProgressAsync(schoolId, user.Id, scenarioEnum, theorySessions, instructors[0].Id, now, ct);
        await ApplyTheoryExamAsync(schoolId, user.Id, bankId, examId, scenarioEnum, today, now, ct);
        await ApplyPracticalProgressAsync(
            schoolId, user.Id, scenarioEnum, license, instructors, vehicles, today, now, ct);

        return new DemoUserCredential
        {
            Scenario = scenario,
            Role = "Student",
            Name = fullName,
            Email = email,
            Password = password,
            Document = doc,
            License = license,
            Notes = ScenarioLabel(scenarioEnum)
        };
    }

    private async Task UpsertApprenticeProfileAsync(
        int schoolId,
        int studentId,
        DemoSchoolSpec spec,
        int scenario,
        string doc,
        string email,
        DateOnly today,
        DateTime now,
        CancellationToken ct)
    {
        const decimal courseFee = 1_800_000m;
        var (paid, method, notes) = CarteraForScenario(scenario, courseFee);

        var profile = await _db.Set<SchoolApprenticeProfile>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolId && x.StudentUserId == studentId, ct);
        if (profile is null)
        {
            profile = new SchoolApprenticeProfile
            {
                SchoolUserId = schoolId,
                StudentUserId = studentId,
                CreatedAt = now
            };
            _db.Set<SchoolApprenticeProfile>().Add(profile);
        }

        profile.DocumentType = "CC";
        profile.DocumentNumber = doc;
        profile.Phone = $"300{spec.DocPrefix[^3..]}{scenario:00}";
        profile.Address = $"{spec.Address} — aprendiz DEMO {scenario}";
        profile.ContactEmail = email;
        profile.EnrollmentDate = today.AddDays(-40 + scenario);
        profile.EnrollmentMonth = profile.EnrollmentDate.Value.ToString("yyyy-MM");
        profile.OrderNumber = scenario;
        profile.ScheduleSlot = "Semana AM";
        profile.ReceiptNumber = $"DEMO-{spec.Key.ToUpperInvariant()}-{scenario:00}";
        profile.AmountDue = courseFee;
        profile.AmountPaid = paid;
        profile.BalanceDue = Math.Max(0, courseFee - paid);
        profile.AccountsReceivable = profile.BalanceDue;
        profile.PaymentMethod = method;
        profile.IsEnrolled = scenario > 1;
        profile.RuntRegistered = scenario >= 9;
        profile.Notes = $"{DemoMarkers.Tag} {notes}";
        profile.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        // Clear prior abonos then recreate coherent movements
        var old = await _db.Set<ApprenticePaymentAbono>()
            .Where(x => x.SchoolUserId == schoolId && x.StudentUserId == studentId)
            .ToListAsync(ct);
        _db.Set<ApprenticePaymentAbono>().RemoveRange(old);

        if (paid > 0)
        {
            if (paid >= courseFee)
            {
                _db.Set<ApprenticePaymentAbono>().Add(new ApprenticePaymentAbono
                {
                    SchoolUserId = schoolId,
                    StudentUserId = studentId,
                    PaymentDate = today.AddDays(-20),
                    Amount = courseFee,
                    PaymentMethod = method,
                    ReceiptNumber = $"R-{spec.Key}-{scenario}-FULL",
                    Kind = "Pago",
                    Notes = DemoMarkers.Tag,
                    RecordedByUserId = schoolId,
                    CreatedAt = now
                });
            }
            else
            {
                _db.Set<ApprenticePaymentAbono>().Add(new ApprenticePaymentAbono
                {
                    SchoolUserId = schoolId,
                    StudentUserId = studentId,
                    PaymentDate = today.AddDays(-25),
                    Amount = paid,
                    PaymentMethod = method,
                    ReceiptNumber = $"R-{spec.Key}-{scenario}-AB1",
                    Kind = "Abono",
                    Notes = $"{DemoMarkers.Tag} abono parcial",
                    RecordedByUserId = schoolId,
                    CreatedAt = now
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private static (decimal Paid, string Method, string Notes) CarteraForScenario(int scenario, decimal fee) =>
        scenario switch
        {
            1 => (0, "", "Sin pagos — recién inscrito"),
            2 or 3 => (fee * 0.3m, "Transferencia", "Abono parcial 30%"),
            4 or 5 or 6 => (fee, "Efectivo", "Al día"),
            7 => (fee, "Transferencia", "Al día — teórico aprobado"),
            8 => (fee * 0.4m, "Transferencia", "Cartera vencida / mora"),
            9 or 10 or 11 or 12 or 13 => (fee, "Transferencia", "Al día"),
            14 => (fee * 0.5m, "Efectivo", "Saldo pendiente tras examen práctico"),
            _ => (fee, "Transferencia", "Al día")
        };

    private async Task ApplyTheoryProgressAsync(
        int schoolId,
        int studentId,
        DemoScenario scenario,
        IReadOnlyList<TheorySessionSeed> sessions,
        int markerUserId,
        DateTime now,
        CancellationToken ct)
    {
        // Clear adjustments for this student
        var oldAdj = await _db.Set<TrainingHoursAdjustment>()
            .Where(x => x.SchoolUserId == schoolId && x.StudentUserId == studentId)
            .ToListAsync(ct);
        _db.Set<TrainingHoursAdjustment>().RemoveRange(oldAdj);

        var pastTheory = sessions.Where(s => s.IsPast && !s.IsWorkshop).OrderBy(s => s.Session.SessionDate).ToList();
        var pastWorkshop = sessions.Where(s => s.IsPast && s.IsWorkshop).OrderBy(s => s.Session.SessionDate).ToList();
        var future = sessions.Where(s => !s.IsPast).OrderBy(s => s.Session.SessionDate).ToList();

        var (theoryPresents, workshopPresents, futureReserves, oneAbsent) = scenario switch
        {
            DemoScenario.NewlyEnrolled => (0, 0, 0, false),
            DemoScenario.TheoryStarted => (1, 0, 1, false),
            DemoScenario.TheoryPartial => (4, 1, 1, true),
            DemoScenario.TheoryFinishing => (8, 2, 1, false),
            DemoScenario.TheoryDonePendingExam
                or DemoScenario.TheoryExamScheduled
                or DemoScenario.TheoryExamPassed
                or DemoScenario.TheoryExamFailed
                or DemoScenario.PracticalEnabled
                or DemoScenario.PracticalPartial
                or DemoScenario.PracticalDonePendingExam
                or DemoScenario.PracticalExamScheduled
                or DemoScenario.PracticalExamPassed
                or DemoScenario.PracticalExamFailed => (10, 3, 0, false),
            _ => (0, 0, 0, false)
        };

        async Task BookAndMarkAsync(TheorySessionSeed seed, string attendanceStatus)
        {
            var reservation = new TheoryClassReservation
            {
                ClassSessionId = seed.Session.Id,
                StudentUserId = studentId,
                Status = attendanceStatus == TheoryAttendanceStatuses.Pending
                    ? TheoryReservationStatuses.Reserved
                    : TheoryReservationStatuses.Attended,
                ReservedAt = now.AddDays(-15),
                CreatedAt = now,
                UpdatedAt = now
            };
            if (attendanceStatus == TheoryAttendanceStatuses.Absent)
            {
                reservation.Status = TheoryReservationStatuses.NoShow;
            }

            if (!seed.IsPast)
            {
                reservation.Status = TheoryReservationStatuses.Reserved;
            }

            _db.Set<TheoryClassReservation>().Add(reservation);

            if (seed.IsPast)
            {
                _db.Set<TheoryAttendanceRecord>().Add(new TheoryAttendanceRecord
                {
                    ClassSessionId = seed.Session.Id,
                    StudentUserId = studentId,
                    Status = attendanceStatus == TheoryAttendanceStatuses.Pending
                        ? TheoryAttendanceStatuses.Present
                        : attendanceStatus,
                    MarkedByUserId = markerUserId,
                    MarkedAt = now.AddDays(-10),
                    Notes = DemoMarkers.Tag,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        for (var i = 0; i < theoryPresents && i < pastTheory.Count; i++)
        {
            var status = oneAbsent && i == 1
                ? TheoryAttendanceStatuses.Absent
                : TheoryAttendanceStatuses.Present;
            await BookAndMarkAsync(pastTheory[i], status);
        }

        for (var i = 0; i < workshopPresents && i < pastWorkshop.Count; i++)
        {
            await BookAndMarkAsync(pastWorkshop[i], TheoryAttendanceStatuses.Present);
        }

        for (var i = 0; i < futureReserves && i < future.Count; i++)
        {
            await BookAndMarkAsync(future[i], TheoryAttendanceStatuses.Pending);
        }

        // Top-up hours with adjustments when attendance alone is short of requirements.
        // 10 theory * 2h = 20; 3 workshop * 2h = 6 (>=5). Partial scenarios get smaller attendance.
        if (scenario is DemoScenario.TheoryFinishing)
        {
            // 8*2=16 theory → need +4; 2*2=4 workshop → need +1
            AddHourAdjustment(schoolId, studentId, TheoryTopicCategories.Theory, 4m, 16m, now);
            AddHourAdjustment(schoolId, studentId, TheoryTopicCategories.Workshop, 1m, 4m, now);
        }

        await _db.SaveChangesAsync(ct);
    }

    private void AddHourAdjustment(
        int schoolId,
        int studentId,
        string category,
        decimal delta,
        decimal previous,
        DateTime now)
    {
        _db.Set<TrainingHoursAdjustment>().Add(new TrainingHoursAdjustment
        {
            SchoolUserId = schoolId,
            StudentUserId = studentId,
            Category = category,
            DeltaHours = delta,
            PreviousHours = previous,
            NewHours = previous + delta,
            Reason = $"{DemoMarkers.Tag} ajuste demostración",
            PerformedByUserId = schoolId,
            CreatedAt = now
        });
    }

    private async Task ApplyTheoryExamAsync(
        int schoolId,
        int studentId,
        int bankId,
        int examId,
        DemoScenario scenario,
        DateOnly today,
        DateTime now,
        CancellationToken ct)
    {
        var oldAttempts = await _db.Set<Attempt>()
            .Where(a => a.UserId == studentId && a.ExamId == examId)
            .ToListAsync(ct);
        _db.Set<Attempt>().RemoveRange(oldAttempts);

        var oldAppts = await _db.Set<TheoryExamAppointment>()
            .Where(x => x.SchoolUserId == schoolId && x.StudentUserId == studentId)
            .ToListAsync(ct);
        _db.Set<TheoryExamAppointment>().RemoveRange(oldAppts);
        await _db.SaveChangesAsync(ct);

        var seat = Math.Clamp((int)scenario, 1, 20);

        if (scenario is DemoScenario.TheoryExamScheduled or DemoScenario.TheoryExamFailed)
        {
            var examDate = NextWeekday(today, scenario == DemoScenario.TheoryExamFailed ? 5 : 2);
            _db.Set<TheoryExamAppointment>().Add(new TheoryExamAppointment
            {
                SchoolUserId = schoolId,
                ExamDate = examDate,
                SlotTime = new TimeOnly(9, 0),
                StudentUserId = studentId,
                StudentLabel = DemoMarkers.Tag,
                Status = TheoryExamBookingStatuses.Active,
                SeatNumber = seat,
                BookedByUserId = schoolId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (scenario is DemoScenario.TheoryExamPassed
            || scenario >= DemoScenario.PracticalEnabled)
        {
            var attempt = Attempt.Start(studentId, bankId, examId, "exam", 40, 60, now.AddDays(-7));
            attempt.Finish(correctCount: 38, utcNow: now.AddDays(-7).AddMinutes(45), passed: true);
            _db.Set<Attempt>().Add(attempt);

            // Unique (school, date, time, seat): stagger seats; one student per seat on shared past slot.
            _db.Set<TheoryExamAppointment>().Add(new TheoryExamAppointment
            {
                SchoolUserId = schoolId,
                ExamDate = today.AddDays(-7),
                SlotTime = new TimeOnly(9, 0),
                StudentUserId = studentId,
                StudentLabel = DemoMarkers.Tag,
                Status = TheoryExamBookingStatuses.Completed,
                SeatNumber = seat,
                CheckedInAt = now.AddDays(-7),
                BookedByUserId = schoolId,
                CreatedAt = now.AddDays(-8),
                UpdatedAt = now.AddDays(-7)
            });
        }

        if (scenario is DemoScenario.TheoryExamFailed)
        {
            var attempt = Attempt.Start(studentId, bankId, examId, "exam", 40, 60, now.AddDays(-3));
            attempt.Finish(correctCount: 20, utcNow: now.AddDays(-3).AddMinutes(50), passed: false);
            _db.Set<Attempt>().Add(attempt);

            _db.Set<TheoryExamAppointment>().Add(new TheoryExamAppointment
            {
                SchoolUserId = schoolId,
                ExamDate = today.AddDays(-3),
                SlotTime = new TimeOnly(9, 0),
                StudentUserId = studentId,
                StudentLabel = $"{DemoMarkers.Tag} reprobado",
                Status = TheoryExamBookingStatuses.Completed,
                SeatNumber = seat,
                CheckedInAt = now.AddDays(-3),
                BookedByUserId = schoolId,
                CreatedAt = now.AddDays(-4),
                UpdatedAt = now.AddDays(-3)
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task ApplyPracticalProgressAsync(
        int schoolId,
        int studentId,
        DemoScenario scenario,
        string license,
        IReadOnlyList<User> instructors,
        IReadOnlyList<PracticalVehicle> vehicles,
        DateOnly today,
        DateTime now,
        CancellationToken ct)
    {
        if (scenario < DemoScenario.PracticalPartial
            && scenario is not DemoScenario.PracticalEnabled)
        {
            return;
        }

        var practiceRequired = PracticalLessonRequirements.GetPracticeRequired(license);
        var instructor = instructors[scenario == DemoScenario.PracticalPartial ? 1 : 0];
        var vehicle = vehicles[license.Contains("A2", StringComparison.OrdinalIgnoreCase) ? 0
            : license.Contains("C1", StringComparison.OrdinalIgnoreCase) ? 2 : 1];

        var attendedTarget = scenario switch
        {
            DemoScenario.PracticalEnabled => 0,
            DemoScenario.PracticalPartial => Math.Max(3, practiceRequired / 3),
            DemoScenario.PracticalDonePendingExam
                or DemoScenario.PracticalExamScheduled
                or DemoScenario.PracticalExamPassed
                or DemoScenario.PracticalExamFailed => practiceRequired,
            _ => 0
        };

        // Future reserved lesson for partial/enabled
        var futureLessons = scenario is DemoScenario.PracticalEnabled or DemoScenario.PracticalPartial ? 2 : 0;

        for (var i = 0; i < attendedTarget; i++)
        {
            var date = PreviousWeekday(today, i + 1);
            var start = new TimeOnly(7 + (i % 4), 0);
            await AddPracticalAsync(
                schoolId, studentId, instructor.Id, vehicle.Id, date, start,
                PracticalSessionKinds.Lesson, PracticalReservationStatuses.Attended, past: true, now, ct);
        }

        for (var i = 1; i <= futureLessons; i++)
        {
            var date = NextWeekday(today, i);
            var start = new TimeOnly(8, 0);
            await AddPracticalAsync(
                schoolId, studentId, instructor.Id, vehicle.Id, date, start,
                PracticalSessionKinds.Lesson, PracticalReservationStatuses.Reserved, past: false, now, ct);
        }

        if (scenario is DemoScenario.PracticalExamScheduled)
        {
            await AddPracticalAsync(
                schoolId, studentId, instructor.Id, vehicle.Id, NextWeekday(today, 3),
                new TimeOnly(10, 0), PracticalSessionKinds.Exam, PracticalReservationStatuses.Reserved,
                past: false, now, ct);
        }

        if (scenario is DemoScenario.PracticalExamPassed)
        {
            await AddPracticalAsync(
                schoolId, studentId, instructor.Id, vehicle.Id, today.AddDays(-2),
                new TimeOnly(10, 0), PracticalSessionKinds.Exam, PracticalReservationStatuses.Attended,
                past: true, now, ct, notes: $"{DemoMarkers.Tag} examen presentado (aprobado en demo)");
        }

        if (scenario is DemoScenario.PracticalExamFailed)
        {
            // Domain has no fail flag — model as NoShow + new reservation.
            await AddPracticalAsync(
                schoolId, studentId, instructor.Id, vehicle.Id, today.AddDays(-4),
                new TimeOnly(10, 0), PracticalSessionKinds.Exam, PracticalReservationStatuses.NoShow,
                past: true, now, ct, notes: $"{DemoMarkers.Tag} reprobado/no apto — requiere nueva programación");
            await AddPracticalAsync(
                schoolId, studentId, instructor.Id, vehicle.Id, NextWeekday(today, 6),
                new TimeOnly(11, 0), PracticalSessionKinds.Exam, PracticalReservationStatuses.Reserved,
                past: false, now, ct, notes: $"{DemoMarkers.Tag} recuperación");
        }
    }

    private async Task AddPracticalAsync(
        int schoolId,
        int studentId,
        int instructorId,
        int vehicleId,
        DateOnly date,
        TimeOnly start,
        string kind,
        string reservationStatus,
        bool past,
        DateTime now,
        CancellationToken ct,
        string? notes = null)
    {
        // Avoid instructor hour conflicts: nudge start if slot taken.
        var end = start.AddHours(1).AddMinutes(-1); // platform uses ~1h slots
        var collision = await _db.Set<PracticalLessonSession>().AnyAsync(x =>
            x.SchoolUserId == schoolId
            && x.InstructorUserId == instructorId
            && x.SessionDate == date
            && x.StartTime == start, ct);
        if (collision)
        {
            start = start.AddHours(1);
            end = start.AddHours(1).AddMinutes(-1);
        }

        var session = new PracticalLessonSession
        {
            SchoolUserId = schoolId,
            SessionDate = date,
            StartTime = start,
            EndTime = end,
            InstructorUserId = instructorId,
            VehicleId = vehicleId,
            Capacity = 1,
            Status = past ? PracticalLessonStatuses.Completed : PracticalLessonStatuses.Scheduled,
            Kind = kind,
            Notes = notes ?? DemoMarkers.Tag,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Set<PracticalLessonSession>().Add(session);
        await _db.SaveChangesAsync(ct);

        _db.Set<PracticalLessonReservation>().Add(new PracticalLessonReservation
        {
            LessonSessionId = session.Id,
            StudentUserId = studentId,
            Status = reservationStatus,
            ReservedAt = now.AddDays(-2),
            CreatedAt = now,
            UpdatedAt = now
        });
        await _db.SaveChangesAsync(ct);
    }

    private static string ScenarioLabel(DemoScenario s) => s switch
    {
        DemoScenario.NewlyEnrolled => "Recién inscrito",
        DemoScenario.TheoryStarted => "Teoría iniciada",
        DemoScenario.TheoryPartial => "Teoría avance parcial",
        DemoScenario.TheoryFinishing => "Finalizando teoría",
        DemoScenario.TheoryDonePendingExam => "Teoría completa, pendiente examen",
        DemoScenario.TheoryExamScheduled => "Examen teórico programado",
        DemoScenario.TheoryExamPassed => "Examen teórico aprobado",
        DemoScenario.TheoryExamFailed => "Examen teórico reprobado",
        DemoScenario.PracticalEnabled => "Habilitado para prácticas",
        DemoScenario.PracticalPartial => "Prácticas parciales",
        DemoScenario.PracticalDonePendingExam => "Prácticas completas, pendiente examen",
        DemoScenario.PracticalExamScheduled => "Examen de manejo programado",
        DemoScenario.PracticalExamPassed => "Examen de manejo aprobado / proceso terminado",
        DemoScenario.PracticalExamFailed => "Examen de manejo reprobado + reprogramación",
        _ => s.ToString()
    };

    private static DateOnly PreviousWeekday(DateOnly from, int n)
    {
        var d = from;
        var count = 0;
        while (count < n)
        {
            d = d.AddDays(-1);
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                count++;
            }
        }

        return d;
    }

    private static DateOnly NextWeekday(DateOnly from, int n)
    {
        var d = from;
        var count = 0;
        while (count < n)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                count++;
            }
        }

        return d;
    }
}
