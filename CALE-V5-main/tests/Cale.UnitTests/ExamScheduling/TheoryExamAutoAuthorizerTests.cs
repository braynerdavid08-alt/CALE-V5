using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.UnitTests.ExamScheduling;

public sealed class TheoryExamAutoAuthorizerTests : IDisposable
{
    private readonly ExamSchedulingFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    private async Task PrepareAsync(int student, decimal balanceDue = 0)
    {
        await using var db = _fx.NewDb();
        var enrollment = await db.Set<SchoolStudentEnrollment>()
            .SingleAsync(x => x.SchoolUserId == _fx.SchoolA && x.StudentUserId == student);
        enrollment.TheoryExamAuthorized = false;
        if (!await db.Set<TheoryTrainingSettings>().AnyAsync(x => x.SchoolUserId == _fx.SchoolA))
        {
            db.Set<TheoryTrainingSettings>().Add(new TheoryTrainingSettings
            {
                SchoolUserId = _fx.SchoolA,
                TheoryExamId = 77
            });
        }

        db.Set<SchoolApprenticeProfile>().Add(new SchoolApprenticeProfile
        {
            SchoolUserId = _fx.SchoolA,
            StudentUserId = student,
            BalanceDue = balanceDue,
            CreatedAt = _fx.Clock.UtcNow,
            UpdatedAt = _fx.Clock.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task CompleteHoursAsync(int student)
    {
        await using var db = _fx.NewDb();
        var hours = _fx.Hours(db);
        await hours.AdjustAsync(_fx.SchoolA, _fx.SchoolA, student,
            new AdjustStudentHoursRequest("Theory", 20, "Horas completas de teoría"), default);
        await hours.AdjustAsync(_fx.SchoolA, _fx.SchoolA, student,
            new AdjustStudentHoursRequest("Workshop", 5, "Horas completas de taller"), default);
    }

    private async Task<bool> IsAuthorizedAsync(int student)
    {
        await using var db = _fx.NewDb();
        return await db.Set<SchoolStudentEnrollment>()
            .Where(x => x.SchoolUserId == _fx.SchoolA && x.StudentUserId == student)
            .Select(x => x.TheoryExamAuthorized)
            .SingleAsync();
    }

    [Fact]
    public async Task Completing_theory_and_workshop_hours_authorizes_and_notifies()
    {
        await PrepareAsync(_fx.Juan);

        await CompleteHoursAsync(_fx.Juan);

        Assert.True(await IsAuthorizedAsync(_fx.Juan));
        await using var db = _fx.NewDb();
        var evt = await db.Set<EnrollmentAuthorizationEvent>().SingleAsync(x => x.StudentUserId == _fx.Juan);
        Assert.Equal(EnrollmentAuthorizationActions.Granted, evt.Action);
        Assert.Null(evt.PerformedByUserId);
        Assert.Contains(_fx.Notifications.Sent, n => n.UserId == _fx.Juan && n.Title.Contains("agendar"));
    }

    [Fact]
    public async Task Authorizes_even_without_official_exam_configured()
    {
        await PrepareAsync(_fx.Juan);
        await using (var db = _fx.NewDb())
        {
            var settings = await db.Set<TheoryTrainingSettings>().SingleAsync(x => x.SchoolUserId == _fx.SchoolA);
            settings.TheoryExamId = null;
            await db.SaveChangesAsync();
        }

        await CompleteHoursAsync(_fx.Juan);

        Assert.True(await IsAuthorizedAsync(_fx.Juan));
    }

    [Fact]
    public async Task Theory_hours_alone_do_not_authorize()
    {
        await PrepareAsync(_fx.Juan);
        await using var db = _fx.NewDb();
        await _fx.Hours(db).AdjustAsync(_fx.SchoolA, _fx.SchoolA, _fx.Juan,
            new AdjustStudentHoursRequest("Theory", 20, "Horas completas de teoría"), default);

        Assert.False(await IsAuthorizedAsync(_fx.Juan));
    }

    [Fact]
    public async Task Pending_balance_blocks_until_paid()
    {
        await PrepareAsync(_fx.Juan, balanceDue: 150_000);
        await CompleteHoursAsync(_fx.Juan);
        Assert.False(await IsAuthorizedAsync(_fx.Juan));

        await using (var db = _fx.NewDb())
        {
            var profile = await db.Set<SchoolApprenticeProfile>().SingleAsync(x => x.StudentUserId == _fx.Juan);
            profile.BalanceDue = 0;
            await db.SaveChangesAsync();
            Assert.True(await _fx.AutoAuthorizer(db).TryAuthorizeAsync(_fx.SchoolA, _fx.Juan, default));
        }

        Assert.True(await IsAuthorizedAsync(_fx.Juan));
    }

    [Fact]
    public async Task Manual_school_decision_is_never_overridden()
    {
        await PrepareAsync(_fx.Juan);
        await using (var db = _fx.NewDb())
        {
            db.Set<EnrollmentAuthorizationEvent>().Add(new EnrollmentAuthorizationEvent
            {
                SchoolUserId = _fx.SchoolA,
                StudentUserId = _fx.Juan,
                AuthorizationType = EnrollmentAuthorizationTypes.TheoryExam,
                Action = EnrollmentAuthorizationActions.Revoked,
                PerformedByUserId = _fx.SchoolA,
                CreatedAt = _fx.Clock.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await CompleteHoursAsync(_fx.Juan);

        Assert.False(await IsAuthorizedAsync(_fx.Juan));
    }
}
