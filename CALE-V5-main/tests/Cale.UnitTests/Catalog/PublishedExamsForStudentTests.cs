using Cale.BuildingBlocks.Domain.Paging;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Application.Queries;
using Cale.Modules.Catalog.Domain;

namespace Cale.UnitTests.Catalog;

public sealed class PublishedExamsForStudentTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task School_student_sees_staff_published_exams_without_group()
    {
        var schoolExam = MakeExam("Escuela A", createdById: 10, id: 101, attempts: 3);
        var otherExam = MakeExam("Otra escuela", createdById: 99, id: 202, attempts: 3);
        var store = new FakeCatalogStore([schoolExam, otherExam]);
        var handler = new ListExamsHandler(store);

        var result = await handler.PublishedForStudentAsync(
            groupIds: [],
            schoolCatalogOwnerIds: [50, 10],
            Now,
            authorizedOfficialTheoryExamId: null,
            configuredOfficialTheoryExamId: null,
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(101, result[0].Id);
        Assert.True(result[0].CanMixInPractice);
    }

    [Fact]
    public async Task Group_student_sees_assigned_exam_even_if_creator_is_outside_staff()
    {
        var assigned = MakeExam("Asignado", createdById: 7, id: 11, attempts: 3);
        var store = new FakeCatalogStore(
            [assigned],
            links: [ExamGroupLink.Create(11, groupId: 3, null, null, Now)]);
        var handler = new ListExamsHandler(store);

        var result = await handler.PublishedForStudentAsync(
            groupIds: [3],
            schoolCatalogOwnerIds: [50],
            Now,
            authorizedOfficialTheoryExamId: null,
            configuredOfficialTheoryExamId: null,
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(11, result[0].Id);
    }

    [Fact]
    public async Task Authorized_official_theory_exam_is_listed_but_not_mixable()
    {
        var theory = MakeExam("Teórico oficial", createdById: 10, id: 55, attempts: 1);
        var store = new FakeCatalogStore([theory]);
        var handler = new ListExamsHandler(store);

        var result = await handler.PublishedForStudentAsync(
            groupIds: [],
            schoolCatalogOwnerIds: [50, 10],
            Now,
            authorizedOfficialTheoryExamId: 55,
            configuredOfficialTheoryExamId: 55,
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(55, result[0].Id);
        Assert.False(result[0].CanMixInPractice);
    }

    [Fact]
    public async Task Unauthorized_official_theory_exam_is_hidden_even_if_staff_created()
    {
        var theory = MakeExam("Teórico oficial", createdById: 10, id: 55, attempts: 1);
        var practice = MakeExam("Práctica escuela", createdById: 10, id: 56, attempts: 3);
        var store = new FakeCatalogStore([theory, practice]);
        var handler = new ListExamsHandler(store);

        var result = await handler.PublishedForStudentAsync(
            groupIds: [],
            schoolCatalogOwnerIds: [50, 10],
            Now,
            authorizedOfficialTheoryExamId: null,
            configuredOfficialTheoryExamId: 55,
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(56, result[0].Id);
    }

    [Fact]
    public async Task Independent_student_does_not_see_school_exams()
    {
        var schoolExam = MakeExam("Escuela", createdById: 10, id: 101, attempts: 3);
        var store = new FakeCatalogStore([schoolExam]);
        var handler = new ListExamsHandler(store);

        var result = await handler.PublishedForStudentAsync(
            groupIds: [],
            schoolCatalogOwnerIds: [],
            Now,
            authorizedOfficialTheoryExamId: null,
            configuredOfficialTheoryExamId: null,
            CancellationToken.None);

        Assert.Empty(result);
    }

    private static Exam MakeExam(string name, int createdById, int id, int attempts)
    {
        var exam = Exam.Create(
            name,
            null,
            bankId: 1,
            questionCount: 20,
            timeMinutes: 30,
            allowedAttempts: attempts,
            randomize: true,
            createdById,
            startsAt: null,
            endsAt: null,
            Now);
        exam.SetPublished(true, Now);
        typeof(Exam).GetProperty(nameof(Exam.Id))!
            .SetValue(exam, id);
        return exam;
    }

    private sealed class FakeCatalogStore : ICatalogStore
    {
        private readonly IReadOnlyList<Exam> _exams;
        private readonly IReadOnlyList<ExamGroupLink> _links;

        public FakeCatalogStore(
            IReadOnlyList<Exam> exams,
            IReadOnlyList<ExamGroupLink>? links = null)
        {
            _exams = exams;
            _links = links ?? [];
        }

        public Task<IReadOnlyList<Exam>> ListPublishedExamsAsync(CancellationToken ct) =>
            Task.FromResult(_exams);

        public Task<IReadOnlySet<int>> ListAllExamGroupExamIdsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<int>>(_links.Select(x => x.ExamId).ToHashSet());

        public Task<IReadOnlyList<ExamGroupLink>> ListExamGroupsForGroupsAsync(
            IReadOnlyList<int> groupIds,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ExamGroupLink>>(
                _links.Where(x => groupIds.Contains(x.GroupId)).ToList());

        public Task<Bank?> GetBankAsync(int id, CancellationToken ct) =>
            Task.FromResult<Bank?>(null);

        public Task<IReadOnlyList<Bank>> ListBanksAsync(
            bool activeOnly,
            CancellationToken ct,
            int? viewerUserId = null,
            bool isAdmin = false,
            IReadOnlyCollection<int>? schoolOwnerIds = null) => throw new NotImplementedException();

        public Task AddBankAsync(Bank bank, CancellationToken ct) => throw new NotImplementedException();
        public Task RemoveBankAsync(Bank bank, CancellationToken ct) => throw new NotImplementedException();
        public Task<int> CountQuestionsInBankAsync(int bankId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, int>> CountActiveQuestionsByBankIdsAsync(
            IReadOnlyList<int> bankIds,
            CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<QuestionThemeRow>> ListActiveThemeRowsAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<QuestionDifficultyRow>> ListActiveDifficultyRowsAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<Block>> ListBlocksAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<Block?> GetBlockAsync(int id, CancellationToken ct) => throw new NotImplementedException();
        public Task<Block?> GetBlockByNameAsync(string name, CancellationToken ct) => throw new NotImplementedException();
        public Task AddBlockAsync(Block block, CancellationToken ct) => throw new NotImplementedException();
        public Task<Bank?> GetBankByNameAsync(string name, CancellationToken ct) => throw new NotImplementedException();
        public Task<PagedResult<QuestionListDto>> ListQuestionsAsync(
            int page,
            int pageSize,
            int? bankId,
            string? search,
            bool? active,
            int? ownerId,
            CancellationToken ct,
            int? visibleToUserId = null) => throw new NotImplementedException();
        public Task<Question?> GetQuestionAsync(int id, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<Question>> ListQuestionsByIdsAsync(
            IReadOnlyList<int> ids,
            CancellationToken ct) => throw new NotImplementedException();
        public Task AddQuestionAsync(Question question, CancellationToken ct) => throw new NotImplementedException();
        public Task RemoveOptionsAsync(Question question, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<Exam>> ListExamsAsync(int? ownerId, CancellationToken ct) => throw new NotImplementedException();
        public Task<Exam?> GetExamAsync(int id, CancellationToken ct) => throw new NotImplementedException();
        public Task AddExamAsync(Exam exam, CancellationToken ct) => throw new NotImplementedException();
        public Task<int> CountActiveExamsForBankAsync(int bankId, int? excludingExamId, CancellationToken ct) => throw new NotImplementedException();
        public Task<int> CountExamsForBankAsync(int bankId, CancellationToken ct) => throw new NotImplementedException();
        public Task<ExamGroupLink?> FindExamGroupAsync(int examId, int groupId, CancellationToken ct) => throw new NotImplementedException();
        public Task AddExamGroupAsync(ExamGroupLink link, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<ExamGroupLink>> ListExamGroupsAsync(int examId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<Question>> ListActiveQuestionsInBankAsync(int bankId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<Question>> ListQuestionsForReviewAsync(int bankId, CancellationToken ct) => throw new NotImplementedException();
        public Task<int> CountQuestionsNeedingReviewAsync(int bankId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<int>> ListExamQuestionIdsAsync(int examId, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new NotImplementedException();
    }
}
