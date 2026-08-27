namespace Cale.Modules.Identity.Application.DTOs;

public sealed record ImportPreviewDto(
    Guid PreviewId,
    string FileName,
    int TotalRows,
    int CreateCount,
    int AttachCount,
    int SkipCount,
    int ErrorCount,
    bool CanCommit,
    string? BlockingReason,
    IReadOnlyList<ImportRowPreviewDto> Rows);

public sealed record ImportRowPreviewDto(
    int LineNumber,
    string Name,
    string Email,
    string Role,
    string Action,
    string Severity,
    string? Code,
    string? Message);

public sealed record ImportCommitResultDto(
    Guid PreviewId,
    int Created,
    int Attached,
    int Skipped,
    int Failed,
    IReadOnlyList<ImportCredentialDto> Credentials,
    IReadOnlyList<ImportRowPreviewDto> Results,
    string CredentialsCsv);

public sealed record ImportCredentialDto(
    string Name,
    string Email,
    string Role,
    string TemporaryPassword);
