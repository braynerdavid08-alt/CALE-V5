namespace Cale.Modules.TheoreticalTraining.Application.DTOs;

public sealed record ApprenticeDto(
    int Id,
    int StudentUserId,
    string StudentName,
    string? StudentEmail,
    string? DocumentType,
    string? DocumentNumber,
    string? Phone,
    string? Address,
    string? ContactEmail,
    string? EnrollmentMonth,
    DateOnly? EnrollmentDate,
    int? OrderNumber,
    string? LicenseCategories,
    string? AttendanceDayType,
    string? ScheduleSlot,
    string? ReceiptNumber,
    decimal AmountDue,
    decimal AmountPaid,
    decimal BalanceDue,
    string? PaymentMethod,
    decimal? BalancePaymentAmount,
    decimal AccountsReceivable,
    DateOnly? BalancePaymentDate,
    string? BalancePaymentMethod,
    string? BalanceReceiptNumber,
    string? EnrollmentPin,
    bool RuntRegistered,
    bool IsEnrolled,
    string EnrollmentStatus,
    bool TheoryExamAuthorized,
    bool PracticalAuthorized,
    string? Notes);

public sealed record SaveApprenticeRequest(
    string? DocumentType,
    string? DocumentNumber,
    string? Phone,
    string? Address,
    string? ContactEmail,
    string? EnrollmentMonth,
    DateOnly? EnrollmentDate,
    int? OrderNumber,
    string? LicenseCategories,
    string? AttendanceDayType,
    string? ScheduleSlot,
    string? ReceiptNumber,
    decimal AmountDue,
    decimal AmountPaid,
    string? PaymentMethod,
    decimal? BalancePaymentAmount,
    decimal AccountsReceivable,
    DateOnly? BalancePaymentDate,
    string? BalancePaymentMethod,
    string? BalanceReceiptNumber,
    string? EnrollmentPin,
    bool RuntRegistered,
    bool IsEnrolled,
    string? Notes);

public sealed record ExcelImportRowPreviewDto(
    int LineNumber,
    string Label,
    string Action,
    string Severity,
    string? Message);

public sealed record ExcelImportPreviewDto(
    Guid PreviewId,
    string FileName,
    string ImportType,
    int TotalRows,
    int CreateCount,
    int UpdateCount,
    int SkipCount,
    int ErrorCount,
    bool CanCommit,
    string? BlockingReason,
    IReadOnlyList<ExcelImportRowPreviewDto> Rows);

public sealed record ExcelImportCredentialDto(
    string Name,
    string Email,
    string TemporaryPassword);

public sealed record ExcelImportCommitResultDto(
    Guid PreviewId,
    int Created,
    int Updated,
    int Skipped,
    int Failed,
    IReadOnlyList<ExcelImportCredentialDto> Credentials,
    IReadOnlyList<ExcelImportRowPreviewDto> Results,
    string CredentialsCsv);

public sealed record ApprenticePracticalSummaryDto(
    int CompletedLessons,
    int RequiredLessons,
    int ScheduledLessons,
    string? NextLessonDate,
    string? NextLessonTime);

public sealed record ApprenticeExamSummaryDto(
    int Id,
    string ExamDate,
    string SlotTime);

public sealed record ApprenticeDetailDto(
    ApprenticeDto Profile,
    PracticalEligibilityDto Training,
    ApprenticePracticalSummaryDto Practical,
    ApprenticeExamSummaryDto? NextExam);

public sealed record SchoolDashboardBalanceRowDto(
    int StudentUserId,
    string StudentName,
    decimal BalanceDue);

public sealed record SchoolDashboardStudentRowDto(
    int StudentUserId,
    string StudentName);

public sealed record SchoolOperationsDashboardDto(
    int ApprenticeCount,
    int BalancePendingCount,
    decimal BalancePendingTotal,
    int ExamsNext7Days,
    int PendingEnrollmentCount,
    int ReadyForExamCount,
    int ReadyForPracticalCount,
    int NoExamAppointmentCount,
    IReadOnlyList<SchoolDashboardBalanceRowDto> TopBalanceDue,
    IReadOnlyList<SchoolDashboardStudentRowDto> TopReadyForExam,
    IReadOnlyList<SchoolDashboardStudentRowDto> TopNoExamAppointment,
    IReadOnlyList<TheoryExamSlotDto> UpcomingExams);

public sealed record TheoryExamSlotDto(
    int Id,
    string ExamDate,
    string SlotTime,
    int? StudentUserId,
    string? StudentLabel,
    string? StudentName,
    string? Notes);

public sealed record SaveTheoryExamSlotRequest(
    DateOnly ExamDate,
    string SlotTime,
    int? StudentUserId,
    string? StudentLabel,
    string? Notes);
