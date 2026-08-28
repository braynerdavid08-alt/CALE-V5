namespace Cale.Modules.Catalog.Application.DTOs;

public sealed record BankThemeDto(string Name, int QuestionCount);

public sealed record BankDto(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    int QuestionCount,
    string? ThemeLabel = null,
    IReadOnlyList<BankThemeDto>? Themes = null,
    IReadOnlyList<BankThemeDto>? Difficulties = null);

public sealed record BlockDto(int Id, string Name);

public sealed record OptionDto(
    int Id,
    string Text,
    bool IsCorrect,
    string? ImageUrl);

public sealed record OptionInput(string Text, bool IsCorrect, string? ImageUrl);

public sealed record QuestionListDto(
    int Id,
    string Text,
    string Type,
    int BankId,
    string BankName,
    string? Topic,
    bool IsActive,
    int? CreatedById);

public sealed record QuestionDetailDto(
    int Id,
    string Text,
    string Type,
    int BankId,
    int BlockId,
    string? Topic,
    string? ImageUrl,
    string? Explanation,
    bool IsActive,
    int? CreatedById,
    IReadOnlyList<OptionDto> Options);

public sealed record SaveQuestionRequest(
    int BankId,
    int BlockId,
    string Text,
    string Type,
    string? Topic,
    string? ImageUrl,
    string? Explanation,
    bool IsActive,
    IReadOnlyList<OptionInput> Options);

public sealed record SaveBankRequest(string Name, string? Description, bool IsActive);

public sealed record ExamDto(
    int Id,
    string Name,
    string? Description,
    int? BankId,
    int QuestionCount,
    int TimeMinutes,
    int AllowedAttempts,
    bool Randomize,
    bool Published,
    bool IsActive,
    int CreatedById,
    DateTime? StartsAt,
    DateTime? EndsAt);

public sealed record SaveExamRequest(
    string Name,
    string? Description,
    int? BankId,
    int QuestionCount,
    int TimeMinutes,
    int AllowedAttempts,
    bool Randomize,
    DateTime? StartsAt,
    DateTime? EndsAt);

public sealed record AssignExamToGroupRequest(
    int GroupId,
    DateTime? StartsAt,
    DateTime? EndsAt);
