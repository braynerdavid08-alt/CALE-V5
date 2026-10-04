using System.Globalization;
using System.Text.Json;
using Cale.Api.Services.Courses;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Classroom;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Assessment.Domain.Gamification;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.Classroom.Domain;
using Cale.Modules.Identity.Domain;
using Cale.Modules.TheoreticalTraining.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cale.Api.Services.Play;

public sealed partial class PlayService
{
    public const int DailySize = 5;
    private static readonly int[] BoxIntervalsDays = [0, 1, 3, 7];
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly CaleDbContext _db;
    private readonly IClock _clock;
    private readonly INotificationPublisher _notifications;
    private readonly PlayContent _content;
    private readonly IMemoryCache _cache;

    public PlayService(
        CaleDbContext db,
        IClock clock,
        INotificationPublisher notifications,
        PlayContent content,
        IMemoryCache cache)
    {
        _db = db;
        _clock = clock;
        _notifications = notifications;
        _content = content;
        _cache = cache;
    }

    // ───────────────────────── Daily challenge ─────────────────────────

    public async Task<DailyChallengeDto> GetDailyAsync(int userId, CancellationToken ct)
    {
        var row = await EnsureDailyAsync(userId, ct);
        var ids = ParseIds(row.QuestionIdsJson);
        var questions = await LoadQuestionsAsync(ids, ct);
        var answers = ParseAnswers(row.AnswersJson);
        var seed = DailySeed(userId, row.ChallengeDate);

        var dtos = ids
            .Where(questions.ContainsKey)
            .Select(id => ToDto(questions[id], seed))
            .ToList();

        var answered = answers
            .Where(kv => questions.ContainsKey(kv.Key))
            .Select(kv => new AnsweredQuestionDto(
                kv.Key,
                kv.Value.OptionId,
                kv.Value.Correct,
                CorrectOptionId(questions[kv.Key]),
                questions[kv.Key].Explanation))
            .ToList();

        return new DailyChallengeDto(
            row.ChallengeDate,
            dtos,
            answered,
            row.CorrectCount,
            row.CompletedAt is not null,
            await GetStreakAsync(userId, ct));
    }

    public async Task<DailyAnswerResultDto> AnswerDailyAsync(
        int userId,
        PlayAnswerRequest request,
        CancellationToken ct)
    {
        var today = ColombiaTime.TodayInColombia();
        var row = await _db.Set<DailyChallenge>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ChallengeDate == today, ct)
            ?? throw new DomainException("Daily challenge not started.", 404, "daily_not_found");

        var ids = ParseIds(row.QuestionIdsJson);
        if (!ids.Contains(request.QuestionId))
        {
            throw new DomainException("Question is not part of today's challenge.", 400, "daily_question_invalid");
        }

        var answers = ParseAnswers(row.AnswersJson);
        if (answers.ContainsKey(request.QuestionId))
        {
            throw new DomainException("Question already answered.", 409, "daily_already_answered");
        }

        await EnsureNotInOpenAttemptAsync(userId, request.QuestionId, ct);
        var question = await LoadQuestionAsync(request.QuestionId, ct);
        var correctId = CorrectOptionId(question);
        var correct = correctId == request.OptionId;
        var now = _clock.UtcNow;

        answers[request.QuestionId] = new StoredAnswer(request.OptionId, correct);
        row.AnswersJson = JsonSerializer.Serialize(answers, JsonOpts);
        if (correct)
        {
            row.CorrectCount++;
        }
        else
        {
            await UpsertMistakeAsync(userId, request.QuestionId, now, ct);
        }

        if (answers.Count >= ids.Count && row.CompletedAt is null)
        {
            row.CompletedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        var newBadges = await CheckAchievementsAsync(userId, ct);

        return new DailyAnswerResultDto(
            correct,
            correctId,
            question.Explanation,
            row.CompletedAt is not null,
            row.CorrectCount,
            ids.Count,
            await GetStreakAsync(userId, ct),
            newBadges);
    }

    private async Task<DailyChallenge> EnsureDailyAsync(int userId, CancellationToken ct)
    {
        var today = ColombiaTime.TodayInColombia();
        var row = await _db.Set<DailyChallenge>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ChallengeDate == today, ct);
        if (row is not null)
        {
            return row;
        }

        var official = await OfficialQuestionIdsAsync(ct);
        var school = await SchoolQuestionIdsAsync(userId, ct);
        if (official.Count == 0 && school.Count == 0)
        {
            throw new DomainException("There are no questions available yet.", 404, "no_questions");
        }

        var seed = DailySeed(userId, today);
        var fromSchool = school
            .OrderBy(id => StableHash(seed, id))
            .Take(DailySchoolShare)
            .ToList();
        var fromOfficial = official
            .Where(id => !fromSchool.Contains(id))
            .OrderBy(id => StableHash(seed, id))
            .Take(DailySize - fromSchool.Count)
            .ToList();
        var picked = fromSchool
            .Concat(fromOfficial)
            .Concat(school.Where(id => !fromSchool.Contains(id)).OrderBy(id => StableHash(seed, id)))
            .Take(DailySize)
            .OrderBy(id => StableHash(seed ^ 0x5bd1e995, id))
            .ToList();

        row = new DailyChallenge
        {
            UserId = userId,
            ChallengeDate = today,
            QuestionIdsJson = JsonSerializer.Serialize(picked, JsonOpts),
            AnswersJson = "{}",
            CreatedAt = _clock.UtcNow
        };
        _db.Set<DailyChallenge>().Add(row);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two tabs created it at once: keep the stored row.
            _db.Entry(row).State = EntityState.Detached;
            row = await _db.Set<DailyChallenge>()
                .FirstAsync(x => x.UserId == userId && x.ChallengeDate == today, ct);
        }

        return row;
    }

    // ───────────────────────── Streak ─────────────────────────

    public async Task<StreakDto> GetStreakAsync(int userId, CancellationToken ct)
    {
        var dates = await ActivityDatesAsync(userId, ct);
        var today = ColombiaTime.TodayInColombia();

        var current = 0;
        var cursor = dates.Contains(today) ? today : today.AddDays(-1);
        while (dates.Contains(cursor))
        {
            current++;
            cursor = cursor.AddDays(-1);
        }

        var best = 0;
        var run = 0;
        DateOnly? prev = null;
        foreach (var d in dates.OrderBy(x => x))
        {
            run = prev is { } p && d == p.AddDays(1) ? run + 1 : 1;
            best = Math.Max(best, run);
            prev = d;
        }

        return new StreakDto(current, Math.Max(best, current), dates.Contains(today));
    }

    private async Task<HashSet<DateOnly>> ActivityDatesAsync(int userId, CancellationToken ct)
    {
        var since = _clock.UtcNow.AddDays(-400);
        var dailyDates = await _db.Set<DailyChallenge>().AsNoTracking()
            .Where(x => x.UserId == userId && x.CompletedAt != null)
            .Select(x => x.ChallengeDate)
            .ToListAsync(ct);
        var attemptTimes = await _db.Set<Attempt>().AsNoTracking()
            .Where(x => x.UserId == userId && x.FinishedAt != null && x.FinishedAt >= since)
            .Select(x => x.FinishedAt!.Value)
            .ToListAsync(ct);

        var set = new HashSet<DateOnly>(dailyDates);
        foreach (var t in attemptTimes)
        {
            set.Add(ToColombiaDate(t));
        }

        return set;
    }

    // ───────────────────────── Mistakes review ─────────────────────────

    public async Task<MistakesDto> GetMistakesAsync(int userId, CancellationToken ct)
    {
        await SyncMistakesAsync(userId, ct);
        var now = _clock.UtcNow;
        var rows = await ReviewableMistakesAsync(userId, ct);

        var pending = rows.Where(x => !x.Mastered).ToList();
        var dueRows = pending
            .Where(x => x.NextDueAt <= now)
            .OrderBy(x => x.Box)
            .ThenBy(x => x.NextDueAt)
            .Take(10)
            .ToList();

        var questions = await LoadQuestionsAsync(dueRows.Select(x => x.QuestionId).ToList(), ct, includeInactive: true);
        var seed = unchecked(userId * 7919 + (int)(now.Ticks / TimeSpan.TicksPerHour));
        var dtos = dueRows
            .Where(x => questions.ContainsKey(x.QuestionId))
            .Select(x => ToDto(questions[x.QuestionId], seed))
            .ToList();

        var nextDue = pending
            .Where(x => x.NextDueAt > now)
            .Select(x => (DateTime?)x.NextDueAt)
            .DefaultIfEmpty(null)
            .Min();

        return new MistakesDto(
            pending.Count(x => x.NextDueAt <= now),
            pending.Count,
            rows.Count(x => x.Mastered),
            nextDue,
            dtos);
    }

    public async Task<MistakeAnswerResultDto> AnswerMistakeAsync(
        int userId,
        PlayAnswerRequest request,
        CancellationToken ct)
    {
        var row = await _db.Set<MistakeReview>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.QuestionId == request.QuestionId, ct)
            ?? throw new DomainException("Question is not in your review list.", 404, "mistake_not_found");

        await EnsureNotInOpenAttemptAsync(userId, request.QuestionId, ct);
        var question = await LoadQuestionAsync(request.QuestionId, ct);
        var correctId = CorrectOptionId(question);
        var correct = correctId == request.OptionId;
        var now = _clock.UtcNow;

        if (correct)
        {
            row.Box = Math.Min(row.Box + 1, BoxIntervalsDays.Length);
            if (row.Box >= BoxIntervalsDays.Length - 1)
            {
                row.Mastered = true;
                row.NextDueAt = now;
            }
            else
            {
                row.NextDueAt = now.AddDays(BoxIntervalsDays[row.Box]);
            }
        }
        else
        {
            row.Box = 0;
            row.Mastered = false;
            row.NextDueAt = now.AddMinutes(10);
            row.LastWrongAt = now;
        }

        row.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        var newBadges = await CheckAchievementsAsync(userId, ct);

        return new MistakeAnswerResultDto(
            correct,
            correctId,
            question.Explanation,
            row.Mastered,
            row.Box,
            newBadges,
            correct ? null : await LessonForAsync(question, userId, ct));
    }

    private async Task SyncMistakesAsync(int userId, CancellationToken ct)
    {
        var wrong = await (
                from ans in _db.Set<AttemptAnswer>().AsNoTracking()
                join at in _db.Set<Attempt>().AsNoTracking() on ans.AttemptId equals at.Id
                join q in _db.Set<Question>().AsNoTracking() on ans.QuestionId equals q.Id
                where at.UserId == userId && at.FinishedAt != null && !ans.IsCorrect && ans.OptionId != null
                select new { ans.QuestionId, When = at.FinishedAt!.Value })
            .ToListAsync(ct);
        if (wrong.Count == 0)
        {
            return;
        }

        var latest = wrong
            .GroupBy(x => x.QuestionId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.When));
        var existing = await _db.Set<MistakeReview>()
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.QuestionId, ct);

        var now = _clock.UtcNow;
        foreach (var (questionId, when) in latest)
        {
            if (!existing.TryGetValue(questionId, out var row))
            {
                _db.Set<MistakeReview>().Add(new MistakeReview
                {
                    UserId = userId,
                    QuestionId = questionId,
                    Box = 0,
                    NextDueAt = now,
                    LastWrongAt = when,
                    UpdatedAt = now
                });
            }
            else if (when > row.LastWrongAt.AddSeconds(1))
            {
                row.Box = 0;
                row.Mastered = false;
                row.NextDueAt = now;
                row.LastWrongAt = when;
                row.UpdatedAt = now;
            }
        }

        if (_db.ChangeTracker.HasChanges())
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<(int Due, int Pending)> MistakeCountsAsync(int userId, CancellationToken ct)
    {
        await SyncMistakesAsync(userId, ct);
        var now = _clock.UtcNow;
        var dueDates = (await ReviewableMistakesAsync(userId, ct))
            .Where(x => !x.Mastered)
            .Select(x => x.NextDueAt)
            .ToList();
        return (dueDates.Count(d => d <= now), dueDates.Count);
    }

    /// <summary>
    /// Mistake rows whose question can still be practised: it exists, has exactly one correct
    /// option and is not an unkeyed Word import. Rows of deleted questions are pruned.
    /// </summary>
    private async Task<List<MistakeReview>> ReviewableMistakesAsync(int userId, CancellationToken ct)
    {
        var rows = await _db.Set<MistakeReview>().AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
        if (rows.Count == 0)
        {
            return rows;
        }

        var ids = rows.Select(x => x.QuestionId).Distinct().ToList();
        var existing = await _db.Set<Question>().AsNoTracking()
            .Where(q => ids.Contains(q.Id))
            .Select(q => new
            {
                q.Id,
                Reviewable = q.Options.Count(o => o.IsCorrect) == 1
                    && (q.Explanation == null || !q.Explanation.Contains("Importada sin clave"))
            })
            .ToListAsync(ct);

        var orphanIds = ids.Except(existing.Select(x => x.Id)).ToList();
        if (orphanIds.Count > 0)
        {
            await _db.Set<MistakeReview>()
                .Where(x => x.UserId == userId && orphanIds.Contains(x.QuestionId))
                .ExecuteDeleteAsync(ct);
        }

        var reviewable = existing.Where(x => x.Reviewable).Select(x => x.Id).ToHashSet();
        return rows.Where(x => reviewable.Contains(x.QuestionId)).ToList();
    }

    private async Task UpsertMistakeAsync(int userId, int questionId, DateTime now, CancellationToken ct)
    {
        var row = await _db.Set<MistakeReview>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.QuestionId == questionId, ct);
        if (row is null)
        {
            _db.Set<MistakeReview>().Add(new MistakeReview
            {
                UserId = userId,
                QuestionId = questionId,
                Box = 0,
                NextDueAt = now,
                LastWrongAt = now,
                UpdatedAt = now
            });
            return;
        }

        row.Box = 0;
        row.Mastered = false;
        row.NextDueAt = now;
        row.LastWrongAt = now;
        row.UpdatedAt = now;
    }

    // ───────────────────────── Readiness ─────────────────────────

    public async Task<ReadinessDto> GetReadinessAsync(int userId, CancellationToken ct)
    {
        var answers = await (
                from ans in _db.Set<AttemptAnswer>().AsNoTracking()
                join at in _db.Set<Attempt>().AsNoTracking() on ans.AttemptId equals at.Id
                join q in _db.Set<Question>().AsNoTracking() on ans.QuestionId equals q.Id
                where at.UserId == userId && at.FinishedAt != null
                orderby ans.Id descending
                select new { q.BlockId, ans.IsCorrect, q.Subject, q.Topic, q.Subtopic })
            .Take(400)
            .ToListAsync(ct);

        var recent = await _db.Set<Attempt>().AsNoTracking()
            .Where(x => x.UserId == userId && x.FinishedAt != null && x.TotalQuestions > 0)
            .OrderByDescending(x => x.FinishedAt)
            .Select(x => x.Percent)
            .Take(5)
            .ToListAsync(ct);

        var blockIds = await OfficialBlockIdsAsync(ct);
        var practiced = answers.Select(a => a.BlockId).Distinct().ToList();
        var allBlockIds = blockIds.Union(practiced).ToList();
        var names = await _db.Set<Block>().AsNoTracking()
            .Where(b => allBlockIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        var byBlock = answers
            .GroupBy(a => a.BlockId)
            .ToDictionary(g => g.Key, g => (Answered: g.Count(), Correct: g.Count(x => x.IsCorrect)));

        var topics = allBlockIds
            .Where(names.ContainsKey)
            .Select(id =>
            {
                byBlock.TryGetValue(id, out var s);
                var percent = s.Answered == 0 ? 0 : (int)Math.Round(100.0 * s.Correct / s.Answered);
                var level = s.Answered == 0 ? "sin_datos" : percent >= 90 ? "alto" : percent >= 70 ? "medio" : "bajo";
                return new ReadinessTopicDto(id, names[id], s.Answered, s.Correct, percent, level, s.Answered < 10);
            })
            .OrderBy(t => t.Answered == 0 ? 1 : 0)
            .ThenBy(t => t.Percent)
            .ToList();

        var recentAvg = recent.Count == 0 ? 0 : (int)Math.Round((double)recent.Average());
        var measured = topics.Where(t => t.Answered >= 5).ToList();
        var topicAvg = measured.Count > 0
            ? measured.Average(t => t.Percent)
            : answers.Count == 0 ? 0 : 100.0 * answers.Count(a => a.IsCorrect) / answers.Count;
        var coverage = topics.Count == 0 ? 0 : (double)topics.Count(t => t.Answered > 0) / topics.Count;

        var overall = answers.Count == 0
            ? 0
            : (int)Math.Round(Math.Clamp(
                (recent.Count > 0 ? 0.55 * recentAvg + 0.35 * topicAvg : 0.9 * topicAvg) + 10 * coverage,
                0,
                100));

        var label = answers.Count == 0 ? "Sin datos todavía"
            : overall >= 92 ? "Listo para el examen"
            : overall >= 80 ? "Casi listo"
            : overall >= 60 ? "Vas por buen camino"
            : "Todavía no";

        var byLesson = answers
            .Select(a => (Answer: a, Subtopic: CurriculumTree.Find(a.Subject, a.Topic, a.Subtopic)))
            .Where(x => x.Subtopic is { CourseSlug: not null, LessonTitle: not null })
            .GroupBy(x => (Slug: x.Subtopic!.CourseSlug!, Title: x.Subtopic!.LessonTitle!))
            .Select(g => (g.Key, Answered: g.Count(), Wrong: g.Count(x => !x.Answer.IsCorrect)))
            .Where(x => x.Wrong >= StudyMinWrong && 100 * (x.Answered - x.Wrong) < 90 * x.Answered)
            .OrderByDescending(x => x.Wrong)
            .ThenBy(x => (double)(x.Answered - x.Wrong) / x.Answered)
            .ToList();
        var links = await LessonLinks.ResolveAsync(_db, byLesson.Select(x => x.Key), userId, ct);
        var study = byLesson
            .Where(x => links.ContainsKey(x.Key))
            .Take(StudyLessonsShown)
            .Select(x => new StudyLessonDto(
                links[x.Key],
                x.Answered,
                x.Wrong,
                (int)Math.Round(100.0 * (x.Answered - x.Wrong) / x.Answered)))
            .ToList();

        var weakest = topics.FirstOrDefault(t => t.Answered >= 5 && t.Percent < 90);
        var unpracticed = topics.FirstOrDefault(t => t.Answered == 0);
        var recommendation = answers.Count == 0
            ? "Presenta tu primer simulacro para medir en qué temas vas bien."
            : study.Count > 0
                ? $"Repasa la lección \"{study[0].Lesson.LessonTitle}\": fallaste {study[0].Wrong} de {study[0].Answered} preguntas de ese tema."
                : weakest is not null
                    ? $"Repasa \"{weakest.Name}\": llevas {weakest.Percent} % de aciertos."
                    : unpracticed is not null
                        ? $"Practica \"{unpracticed.Name}\": todavía no tienes respuestas en ese tema."
                        : "Vas muy bien. Presenta un simulacro completo para confirmarlo.";

        return new ReadinessDto(overall, label, recommendation, recentAvg, answers.Count, topics, study);
    }

    private const int StudyMinWrong = 2;
    private const int StudyLessonsShown = 3;

    /// <summary>The platform lesson that teaches a question's curriculum subtopic, if any.</summary>
    private async Task<LessonLinkDto?> LessonForAsync(Question question, int userId, CancellationToken ct)
    {
        if (CurriculumTree.Find(question.Subject, question.Topic, question.Subtopic) is not { CourseSlug: { } slug, LessonTitle: { } title })
        {
            return null;
        }

        var links = await LessonLinks.ResolveAsync(_db, [(slug, title)], userId, ct);
        return links.GetValueOrDefault((slug, title));
    }

    // ───────────────────────── Signs game ─────────────────────────

    public IReadOnlyList<SignDto> GetSigns() => _content.Signs;

    public async Task<GameSavedDto> SaveSignsResultAsync(
        int userId,
        SignsResultRequest request,
        CancellationToken ct)
    {
        if (request.Total < 0 || request.Total > 300 || request.Correct < 0 || request.Correct > request.Total)
        {
            throw new DomainException("Invalid game result.", 400, "invalid_result");
        }

        var previousBest = await _db.Set<GameResult>().AsNoTracking()
            .Where(x => x.UserId == userId && x.Game == GameKinds.Signs)
            .Select(x => (int?)x.Score)
            .MaxAsync(ct) ?? 0;

        _db.Set<GameResult>().Add(new GameResult
        {
            UserId = userId,
            Game = GameKinds.Signs,
            Score = request.Correct,
            Correct = request.Correct,
            Total = request.Total,
            Won = request.Correct > previousBest,
            PlayedAt = _clock.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        var newBadges = await CheckAchievementsAsync(userId, ct);

        return new GameSavedDto(
            request.Correct,
            Math.Max(previousBest, request.Correct),
            request.Correct > previousBest && request.Correct > 0,
            newBadges);
    }

    // ───────────────────────── Summary ─────────────────────────

    public async Task<PlaySummaryDto> GetSummaryAsync(int userId, CancellationToken ct)
    {
        var user = await _db.Set<User>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct);
        var firstName = user?.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

        var today = ColombiaTime.TodayInColombia();
        var daily = await _db.Set<DailyChallenge>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ChallengeDate == today, ct);
        var dailyAnswered = daily is null ? 0 : ParseAnswers(daily.AnswersJson).Count;
        var dailyTotal = daily is null ? DailySize : ParseIds(daily.QuestionIdsJson).Count;

        var (stats, streak) = await ComputeStatsCoreAsync(userId, ct);
        var newBadges = await CheckAchievementsAsync(userId, stats, ct);
        var (mistakesDue, mistakesPending) = await MistakeCountsAsync(userId, ct);
        var readiness = await GetReadinessAsync(userId, ct);
        var earned = await _db.Set<UserAchievement>().AsNoTracking().CountAsync(x => x.UserId == userId, ct);

        RankingDto? ranking = null;
        try
        {
            ranking = await GetRankingAsync(userId, null, null, ct);
        }
        catch (DomainException)
        {
        }

        var last = await _db.Set<Attempt>().AsNoTracking()
            .Where(x => x.UserId == userId && x.FinishedAt != null)
            .OrderByDescending(x => x.FinishedAt)
            .Select(x => new { x.Percent, x.Passed })
            .FirstOrDefaultAsync(ct);

        return new PlaySummaryDto(
            firstName,
            streak,
            BuildLevel(stats.Xp),
            dailyAnswered,
            dailyTotal,
            daily?.CompletedAt is not null,
            mistakesDue,
            mistakesPending,
            readiness.Overall,
            readiness.Label,
            readiness.Topics.FirstOrDefault(t => t.Answered >= 5 && t.Percent < 90)?.Name,
            ranking?.MyPosition,
            ranking?.MyXp ?? 0,
            earned,
            BadgeCatalog.All.Count,
            last?.Percent,
            last?.Passed,
            newBadges);
    }

    // ───────────────────────── Helpers ─────────────────────────

    internal async Task<Dictionary<int, Question>> LoadQuestionsAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken ct,
        bool includeInactive = false)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _db.Set<Question>().AsNoTracking()
            .Include(q => q.Options)
            .Where(q => ids.Contains(q.Id) && (includeInactive || q.IsActive))
            .ToDictionaryAsync(q => q.Id, ct);
    }

    // Practice games reveal the correct option; never for a question the student still has to answer
    // in an unfinished exam attempt (they could read the key and then change their exam answer).
    internal async Task EnsureNotInOpenAttemptAsync(int userId, int questionId, CancellationToken ct)
    {
        var cutoff = _clock.UtcNow.AddMinutes(-1);
        var inOpenAttempt = await (
                from aq in _db.Set<AttemptQuestion>().AsNoTracking()
                join at in _db.Set<Attempt>().AsNoTracking() on aq.AttemptId equals at.Id
                where at.UserId == userId
                    && aq.QuestionId == questionId
                    && at.FinishedAt == null
                    && (at.ExpiresAt == null || at.ExpiresAt > cutoff)
                select aq.Id)
            .AnyAsync(ct);
        if (inOpenAttempt)
        {
            throw new DomainException(
                "Finish your open exam before practising this question.",
                409,
                "question_in_open_attempt");
        }
    }

    private async Task<Question> LoadQuestionAsync(int id, CancellationToken ct) =>
        await _db.Set<Question>().AsNoTracking()
            .Include(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == id, ct)
        ?? throw new NotFoundException("Question not found.");

    internal static PlayQuestionDto ToDto(Question q, int seed) =>
        new(
            q.Id,
            q.Text,
            q.Type,
            q.ImageUrl,
            q.Topic,
            q.Options
                .OrderBy(o => StableHash(seed ^ q.Id, o.Id))
                .Select(o => new PlayOptionDto(o.Id, o.Text, o.ImageUrl))
                .ToList());

    internal static int? CorrectOptionId(Question q) =>
        q.Options.FirstOrDefault(o => o.IsCorrect)?.Id;

    private static int DailySeed(int userId, DateOnly date) =>
        unchecked(userId * 397 ^ date.DayNumber * 7919);

    internal static uint StableHash(int seed, int value)
    {
        unchecked
        {
            var h = (uint)seed * 2654435761u ^ (uint)value * 2246822519u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            h *= 3266489917u;
            h ^= h >> 16;
            return h;
        }
    }

    private static DateOnly ToColombiaDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            ColombiaTime.TimeZone));

    internal static DateOnly WeekStart()
    {
        var today = ColombiaTime.TodayInColombia();
        var offset = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-offset);
    }

    private static List<int> ParseIds(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<int>>(json, JsonOpts) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Dictionary<int, StoredAnswer> ParseAnswers(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<int, StoredAnswer>>(json, JsonOpts) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal static string PublicName(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "Estudiante",
            1 => parts[0],
            _ => $"{parts[0]} {char.ToUpper(parts[^1][0], CultureInfo.InvariantCulture)}."
        };
    }

    private sealed record StoredAnswer(int? OptionId, bool Correct);
}
