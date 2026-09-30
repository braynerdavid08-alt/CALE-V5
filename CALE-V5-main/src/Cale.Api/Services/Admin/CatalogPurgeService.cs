using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Assessment.Domain.Gamification;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.LiveClassroom.Domain;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Admin;

/// <summary>
/// Admin-only hard delete of banks and exams, including everything that points at them:
/// questions and options, exam links, student attempts (answers, questions, ratings),
/// mistakes review, live classes that used the bank. Runs in a single transaction.
/// </summary>
public sealed class CatalogPurgeService
{
    private readonly CaleDbContext _db;
    private readonly ILogger<CatalogPurgeService> _logger;

    public CatalogPurgeService(CaleDbContext db, ILogger<CatalogPurgeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task PurgeBankAsync(int bankId, CancellationToken ct)
    {
        if (!await _db.Set<Bank>().AnyAsync(b => b.Id == bankId, ct))
        {
            throw new NotFoundException("Bank not found.", "bank_not_found");
        }

        await InTransactionAsync(async () =>
        {
            var examIds = await _db.Set<Exam>().Where(e => e.BankId == bankId).Select(e => e.Id).ToListAsync(ct);
            var questionIds = await _db.Set<Question>().Where(q => q.BankId == bankId).Select(q => q.Id).ToListAsync(ct);
            var attemptIds = await _db.Set<Attempt>()
                .Where(a => a.BankId == bankId || (a.ExamId != null && examIds.Contains(a.ExamId.Value)))
                .Select(a => a.Id)
                .ToListAsync(ct);

            await DeleteAttemptsAsync(attemptIds, ct);
            await _db.Set<AttemptRating>().Where(r => r.BankId == bankId).ExecuteDeleteAsync(ct);
            await DeleteExamsAsync(examIds, ct);
            await DeleteQuestionsAsync(questionIds, ct);

            var liveIds = await _db.Set<LiveSession>().Where(s => s.BankId == bankId).Select(s => s.Id).ToListAsync(ct);
            await DeleteLiveSessionsAsync(liveIds, ct);

            await _db.Set<Bank>().Where(b => b.Id == bankId).ExecuteDeleteAsync(ct);

            _logger.LogWarning(
                "Admin purged bank {BankId}: exams={Exams} questions={Questions} attempts={Attempts} liveSessions={Live}",
                bankId, examIds.Count, questionIds.Count, attemptIds.Count, liveIds.Count);
        }, ct);
    }

    public async Task PurgeExamAsync(int examId, CancellationToken ct)
    {
        if (!await _db.Set<Exam>().AnyAsync(e => e.Id == examId, ct))
        {
            throw new NotFoundException("Exam not found.", "exam_not_found");
        }

        await InTransactionAsync(async () =>
        {
            var attemptIds = await _db.Set<Attempt>().Where(a => a.ExamId == examId).Select(a => a.Id).ToListAsync(ct);
            await DeleteAttemptsAsync(attemptIds, ct);
            await DeleteExamsAsync([examId], ct);

            _logger.LogWarning("Admin purged exam {ExamId}: attempts={Attempts}", examId, attemptIds.Count);
        }, ct);
    }

    private async Task DeleteAttemptsAsync(List<int> attemptIds, CancellationToken ct)
    {
        if (attemptIds.Count == 0) return;
        await _db.Set<AttemptAnswer>().Where(x => attemptIds.Contains(x.AttemptId)).ExecuteDeleteAsync(ct);
        await _db.Set<AttemptQuestion>().Where(x => attemptIds.Contains(x.AttemptId)).ExecuteDeleteAsync(ct);
        await _db.Set<AttemptRating>()
            .Where(x => x.AttemptId != null && attemptIds.Contains(x.AttemptId.Value))
            .ExecuteDeleteAsync(ct);
        await _db.Set<Attempt>().Where(x => attemptIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
    }

    private async Task DeleteExamsAsync(List<int> examIds, CancellationToken ct)
    {
        if (examIds.Count == 0) return;
        await _db.Set<ExamQuestion>().Where(x => examIds.Contains(x.ExamId)).ExecuteDeleteAsync(ct);
        await _db.Set<ExamGroupLink>().Where(x => examIds.Contains(x.ExamId)).ExecuteDeleteAsync(ct);
        await _db.Set<TheoryTrainingSettings>()
            .Where(x => x.TheoryExamId != null && examIds.Contains(x.TheoryExamId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TheoryExamId, (int?)null), ct);
        await _db.Set<Exam>().Where(x => examIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
    }

    private async Task DeleteQuestionsAsync(List<int> questionIds, CancellationToken ct)
    {
        if (questionIds.Count == 0) return;
        await _db.Set<AttemptAnswer>().Where(x => questionIds.Contains(x.QuestionId)).ExecuteDeleteAsync(ct);
        await _db.Set<AttemptQuestion>().Where(x => questionIds.Contains(x.QuestionId)).ExecuteDeleteAsync(ct);
        await _db.Set<ExamQuestion>().Where(x => questionIds.Contains(x.QuestionId)).ExecuteDeleteAsync(ct);
        await _db.Set<MistakeReview>().Where(x => questionIds.Contains(x.QuestionId)).ExecuteDeleteAsync(ct);

        var liveQuestionIds = await _db.Set<LiveSessionQuestion>()
            .Where(x => questionIds.Contains(x.QuestionId))
            .Select(x => x.Id)
            .ToListAsync(ct);
        if (liveQuestionIds.Count > 0)
        {
            await _db.Set<LiveAnswer>().Where(x => liveQuestionIds.Contains(x.SessionQuestionId)).ExecuteDeleteAsync(ct);
            await _db.Set<LiveSessionQuestion>().Where(x => liveQuestionIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
        }

        await _db.Set<QuestionOption>().Where(x => questionIds.Contains(x.QuestionId)).ExecuteDeleteAsync(ct);
        await _db.Set<Question>().Where(x => questionIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
    }

    private async Task DeleteLiveSessionsAsync(List<int> sessionIds, CancellationToken ct)
    {
        if (sessionIds.Count == 0) return;
        var questionIds = await _db.Set<LiveSessionQuestion>()
            .Where(x => sessionIds.Contains(x.SessionId))
            .Select(x => x.Id)
            .ToListAsync(ct);
        var doubtIds = await _db.Set<LiveDoubt>()
            .Where(x => sessionIds.Contains(x.SessionId))
            .Select(x => x.Id)
            .ToListAsync(ct);

        await _db.Set<LiveAnswer>().Where(x => questionIds.Contains(x.SessionQuestionId)).ExecuteDeleteAsync(ct);
        await _db.Set<LiveSessionQuestion>().Where(x => sessionIds.Contains(x.SessionId)).ExecuteDeleteAsync(ct);
        await _db.Set<LiveDoubtVote>().Where(x => doubtIds.Contains(x.DoubtId)).ExecuteDeleteAsync(ct);
        await _db.Set<LiveDoubt>().Where(x => sessionIds.Contains(x.SessionId)).ExecuteDeleteAsync(ct);
        await _db.Set<LiveParticipant>().Where(x => sessionIds.Contains(x.SessionId)).ExecuteDeleteAsync(ct);
        await _db.Set<LiveSession>().Where(x => sessionIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
    }

    private async Task InTransactionAsync(Func<Task> work, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await work();
            await tx.CommitAsync(ct);
        });
    }
}
