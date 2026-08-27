using Cale.Modules.LiveClassroom.Domain;

namespace Cale.UnitTests.Live;

public sealed class LiveScoringAndTimerTests
{
    [Fact]
    public void ComputePoints_Wrong_IsZero()
    {
        Assert.Equal(0, LiveAnswer.ComputePoints(false, 100, 30));
    }

    [Fact]
    public void ComputePoints_FastCorrect_NearMax()
    {
        var pts = LiveAnswer.ComputePoints(true, 0, 30);
        Assert.Equal(1000, pts);
    }

    [Fact]
    public void ComputePoints_SlowCorrect_StillAtLeastFloor()
    {
        var pts = LiveAnswer.ComputePoints(true, 60_000, 30);
        Assert.True(pts >= 150);
        Assert.True(pts < 1000);
    }

    [Fact]
    public void IsQuestionOpen_False_WhenClosedAtElapsed()
    {
        var session = LiveSession.Create(
            hostUserId: 1,
            title: "T",
            joinCode: "ABC123",
            mode: LiveSessionModes.Competitive,
            bankId: 1,
            configJson: "{}",
            utcNow: new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        session.MarkRunning(session.CreatedAt);
        session.OpenQuestion(
            0,
            session.CreatedAt,
            session.CreatedAt.AddSeconds(30));

        Assert.True(session.IsQuestionOpen(session.CreatedAt.AddSeconds(10)));
        Assert.False(session.IsQuestionOpen(session.CreatedAt.AddSeconds(31)));
    }

    [Fact]
    public void CloseCurrentQuestion_BlocksAnswers()
    {
        var opened = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var session = LiveSession.Create(
            1, "T", "ABC123", LiveSessionModes.Exam, 1, "{}", opened);
        session.MarkRunning(opened);
        session.OpenQuestion(0, opened, opened.AddMinutes(5));
        session.CloseCurrentQuestion(opened.AddSeconds(5));

        Assert.False(session.IsQuestionOpen(opened.AddSeconds(6)));
    }

    [Fact]
    public void InsertQuestionAfterCurrent_Reorders()
    {
        var session = LiveSession.Create(
            1, "T", "ABC123", LiveSessionModes.Pedagogical, 1, "{}", DateTime.UtcNow);
        var q0 = LiveSessionQuestion.Create(session.Id, 10, 0, "{}",', null, null);
        var q1 = LiveSessionQuestion.Create(session.Id, 11, 1, "{}", null, null);
        session.SetQuestions([q0, q1]);
        session.OpenQuestion(0, DateTime.UtcNow, null);

        var surprise = LiveSessionQuestion.Create(
            session.Id, 99, 99, "{}",', "T", null, isSurprise: true);
        session.InsertQuestionAfterCurrent(surprise);

        var ordered = session.Questions.OrderBy(q => q.SortOrder).ToList();
        Assert.Equal(3, ordered.Count);
        Assert.Equal(10, ordered[0].QuestionId);
        Assert.Equal(99, ordered[1].QuestionId);
        Assert.True(ordered[1].IsSurprise);
        Assert.Equal(11, ordered[2].QuestionId);
    }
}
