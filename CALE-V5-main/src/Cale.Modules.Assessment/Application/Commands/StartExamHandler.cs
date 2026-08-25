using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Assessment;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Application.DTOs;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Domain;

namespace Cale.Modules.Assessment.Application.Commands;

public sealed class StartExamHandler
{
    private readonly IAttemptStore _attempts;
    private readonly ICatalogStore _catalog;
    private readonly IGroupAccess _groups;
    private readonly IClock _clock;

    public StartExamHandler(
        IAttemptStore attempts,
        ICatalogStore catalog,
        IGroupAccess groups,
        IClock clock)
    {
        _attempts = attempts;
        _catalog = catalog;
        _groups = groups;
        _clock = clock;
    }

    public async Task<StartExamResponse> HandleAsync(
        StartExamRequest request,
        int userId,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (bankId, exam, timeMinutes, count) =
            await ResolveTarget(request, userId, now, ct);

        var pool = await LoadPool(bankId, exam?.Id, ct);
        if (pool.Count == 0)
        {
            throw new DomainException(
                "There are no active questions to take.",
                400,
                "empty_bank");
        }

        var take = Math.Min(count, pool.Count);
        var selected = exam is { Randomize: false }
            ? pool.Take(take).ToList()
            : pool.OrderBy(_ => Guid.NewGuid()).Take(take).ToList();

        var attempt = Attempt.Start(
            userId,
            bankId,
            exam?.Id,
            string.IsNullOrWhiteSpace(request.Mode)
                ? AttemptModes.Practice
                : request.Mode,
            selected.Count,
            timeMinutes,
            now);

        await _attempts.AddAsync(attempt, ct);
        await _attempts.SaveChangesAsync(ct);

        var snapshot = selected
            .Select((q, i) => AttemptQuestion.Create(attempt.Id, q.Id, i + 1))
            .ToList();
        await _attempts.AddQuestionsAsync(attempt.Id, snapshot, ct);
        await _attempts.SaveChangesAsync(ct);

        var questions = selected.Select((q, i) => new TakeQuestionDto(
            q.Id,
            i + 1,
            q.Text,
            q.Type,
            q.ImageUrl,
            q.Options.Select(o => new TakeOptionDto(o.Id, o.Text, o.ImageUrl))
                .ToList())).ToList();

        return new StartExamResponse(
            attempt.Id,
            attempt.StartedAt,
            attempt.ExpiresAt,
            timeMinutes,
            questions);
    }

    private async Task<(int BankId, Exam? Exam, int TimeMinutes, int Count)>
        ResolveTarget(
            StartExamRequest request,
            int userId,
            DateTime now,
            CancellationToken ct)
    {
        if (request.ExamId is > 0)
        {
            var exam = await _catalog.GetExamAsync(request.ExamId.Value, ct)
                ?? throw new NotFoundException("Exam not found.", "exam_not_found");
            await EnsureExamAccess(exam, userId, now, ct);
            var bankId = exam.BankId
                ?? throw new DomainException(
                    "Exam has no bank.",
                    400,
                    "exam_without_bank");
            return (bankId, exam, exam.TimeMinutes, exam.QuestionCount);
        }

        if (request.BankId is not > 0)
        {
            throw new DomainException(
                "Bank or exam is required.",
                400,
                "missing_target");
        }

        var bank = await _catalog.GetBankAsync(request.BankId.Value, ct)
            ?? throw new NotFoundException("Bank not found.", "bank_not_found");
        if (!bank.IsActive)
        {
            throw new ForbiddenException("Bank is inactive.");
        }

        var minutes = request.TimeMinutes < 1 ? 20 : request.TimeMinutes;
        var count = request.QuestionCount < 1 ? 10 : request.QuestionCount;
        return (bank.Id, null, minutes, count);
    }

    private async Task EnsureExamAccess(
        Exam exam,
        int userId,
        DateTime now,
        CancellationToken ct)
    {
        if (!exam.IsOpenAt(now))
        {
            throw new ForbiddenException(
                "Exam is not available now.",
                "exam_closed");
        }

        var used = await _attempts.CountFinishedByUserAndExamAsync(
            userId,
            exam.Id,
            ct);
        if (used >= exam.AllowedAttempts)
        {
            throw new ForbiddenException(
                "No attempts left.",
                "attempts_exhausted");
        }

        var links = await _catalog.ListExamGroupsAsync(exam.Id, ct);
        if (links.Count == 0)
        {
            return;
        }

        var groupIds = await _groups.GetActiveGroupIdsAsync(userId, ct);
        var match = links.FirstOrDefault(x => groupIds.Contains(x.GroupId));
        if (match is null)
        {
            throw new ForbiddenException(
                "You are not in a group for this exam.",
                "exam_not_assigned");
        }

        if (match.StartsAt is { } start && now < start)
        {
            throw new ForbiddenException("Exam has not started.", "exam_closed");
        }

        if (match.EndsAt is { } end && now > end)
        {
            throw new ForbiddenException("Exam window expired.", "exam_closed");
        }
    }

    private async Task<List<Question>> LoadPool(
        int bankId,
        int? examId,
        CancellationToken ct)
    {
        var pool = (await _catalog.ListActiveQuestionsInBankAsync(bankId, ct))
            .ToList();
        if (examId is null)
        {
            return pool;
        }

        var ids = await _catalog.ListExamQuestionIdsAsync(examId.Value, ct);
        if (ids.Count == 0)
        {
            return pool;
        }

        var map = pool.ToDictionary(x => x.Id);
        return ids.Where(map.ContainsKey).Select(id => map[id]).ToList();
    }
}
