using Cale.BuildingBlocks.Domain.Assessment;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Application.DTOs;

namespace Cale.Modules.Assessment.Application.Queries;

public sealed class StudentProgressHandler
{
    public const string ModeAll = "all";
    public const string ModeExam = "exam";
    public const string ModePractice = "practice";

    /// <summary>Upper bound of attempts read for the charts; older ones still count in <see cref="ProgressModeCountsDto"/>.</summary>
    public const int MaxRows = 500;

    private static readonly string[] ExamModes = [AttemptModes.Exam];
    private static readonly string[] PracticeModes = [AttemptModes.Practice, AttemptModes.MixedPractice, AttemptModes.Official];

    private readonly IAttemptStore _attempts;

    public StudentProgressHandler(IAttemptStore attempts) => _attempts = attempts;

    public async Task<StudentProgressDto> HandleAsync(
        int userId,
        string? mode,
        int? take,
        CancellationToken ct)
    {
        var normalizedMode = NormalizeMode(mode);
        int? normalizedTake = take is 5 or 10 ? take : null;

        var byMode = await _attempts.CountFinishedByModeAsync(userId, ct);
        var counts = new ProgressModeCountsDto(
            byMode.Values.Sum(),
            ExamModes.Sum(m => byMode.GetValueOrDefault(m)),
            PracticeModes.Sum(m => byMode.GetValueOrDefault(m)));
        var totalForMode = normalizedMode switch
        {
            ModeExam => counts.Exam,
            ModePractice => counts.Practice,
            _ => counts.All
        };

        var modes = normalizedMode switch
        {
            ModeExam => ExamModes,
            ModePractice => PracticeModes,
            _ => null
        };
        var rows = totalForMode == 0
            ? Array.Empty<AttemptProgressRow>()
            : await _attempts.ListFinishedForProgressAsync(userId, modes, MaxRows, ct);

        return StudentProgressCalculator.Build(rows, totalForMode, normalizedMode, normalizedTake, counts);
    }

    public static string NormalizeMode(string? mode) =>
        (mode ?? "").Trim().ToLowerInvariant() switch
        {
            ModeExam => ModeExam,
            ModePractice => ModePractice,
            _ => ModeAll
        };
}
