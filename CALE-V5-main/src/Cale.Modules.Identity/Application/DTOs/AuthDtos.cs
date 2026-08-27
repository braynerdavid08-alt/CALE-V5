namespace Cale.Modules.Identity.Application.DTOs;

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(string Name, string Email, string Password);

public sealed record RegisterSchoolRequest(
    string ContactName,
    string Email,
    string Password,
    string LegalName,
    string TaxId,
    string BillingEmail,
    string Phone,
    string Address,
    string City,
    string Department,
    string PlanCode,
    bool ClaimFreeTrial = false);

public sealed record SchoolPlanDto(
    string Code,
    string Label,
    decimal PriceCop,
    decimal MonthlyEquivalentCop,
    int DurationMonths,
    int MaxTeachers,
    int MaxStudents);

public sealed record SchoolProfileDto(
    int UserId,
    string ContactName,
    string Email,
    string LegalName,
    string TaxId,
    string BillingEmail,
    string Phone,
    string Address,
    string City,
    string Department,
    string PlanCode,
    string PlanLabel,
    decimal PlanPriceCop,
    decimal MonthlyEquivalentCop,
    int PlanDurationMonths,
    string SubscriptionStatus,
    string DisplayStatus,
    string RenewalStatus,
    DateTime CreatedAt,
    DateTime? MembershipStartsAt,
    DateTime? MembershipEndsAt,
    int DaysRemaining,
    bool IsMembershipActive,
    string? RequestedPlanCode,
    string? RequestedPlanLabel,
    bool HasPendingRequest,
    bool NeedsPaymentProof,
    bool AwaitingAdminReview,
    string? PaymentProofUrl,
    string? PaymentReference,
    string? RejectionReason,
    string? SuspensionReason,
    DateTime? RequestedAt,
    DateTime? ProofSubmittedAt,
    DateTime? LastDecisionAt,
    SchoolPaymentInstructionsDto PaymentInstructions,
    int TeachersUsed,
    int TeachersMax,
    int StudentsUsed,
    int StudentsMax);

public sealed record SchoolPaymentInstructionsDto(
    string BankName,
    string AccountType,
    string AccountNumber,
    string AccountHolder,
    string HolderTaxId,
    string WhatsApp,
    string SupportEmail,
    string Notes,
    string PaymentReferenceHint);

public sealed record ChangeSchoolPlanRequest(string PlanCode);

public sealed record RequestSchoolMembershipRequest(string? PlanCode);

public sealed record SubmitPaymentProofRequest(
    string PaymentProofUrl,
    string? PaymentReference);

public sealed record ActivateSchoolPlanRequest(
    string? PlanCode,
    bool ForceWithoutProof = false);

public sealed record RejectSchoolMembershipRequest(string? Note);

public sealed record CancelSchoolMembershipRequest(string? Note);

public sealed record SuspendSchoolMembershipRequest(string? Note);

public sealed record AdminSetSchoolSeatsRequest(
    int? TeachersMax,
    int? StudentsMax,
    string? Note);

public sealed record AdminOverrideSchoolMembershipRequest(
    string? PlanCode,
    string? SubscriptionStatus,
    DateTime? MembershipEndsAt,
    bool ClearRejection = true,
    string? Note = null);

public sealed record AdminReopenSchoolRequest(
    string? PlanCode,
    string? Note);

public sealed record SchoolMembershipRequestDto(
    int UserId,
    string ContactName,
    string Email,
    string LegalName,
    string TaxId,
    string BillingEmail,
    string Phone,
    string City,
    string Department,
    string PlanCode,
    string PlanLabel,
    decimal PlanPriceCop,
    int PlanDurationMonths,
    string SubscriptionStatus,
    string DisplayStatus,
    string RenewalStatus,
    string? RequestedPlanCode,
    bool IsRenewalRequest,
    bool HasPaymentProof,
    string? PaymentProofUrl,
    string? PaymentReference,
    DateTime? RequestedAt,
    DateTime? ProofSubmittedAt,
    DateTime CreatedAt,
    DateTime? MembershipStartsAt,
    DateTime? MembershipEndsAt,
    int TeachersUsed = 0,
    int TeachersMax = 0,
    int StudentsUsed = 0,
    int StudentsMax = 0,
    int? TeachersMaxOverride = null,
    int? StudentsMaxOverride = null,
    string? RejectionReason = null,
    string? SuspensionReason = null);

public sealed record AdminSchoolSummaryDto(
    int UserId,
    string ContactName,
    string Email,
    string LegalName,
    string TaxId,
    string PlanCode,
    string PlanLabel,
    string SubscriptionStatus,
    string DisplayStatus,
    string RenewalStatus,
    bool IsMembershipActive,
    int DaysRemaining,
    DateTime? MembershipEndsAt,
    int TeachersUsed,
    int TeachersMax,
    int StudentsUsed,
    int StudentsMax,
    bool HasSeatOverrides,
    bool HasOpenRequest,
    DateTime CreatedAt);

public sealed record AdminSchoolDetailDto(
    int UserId,
    string ContactName,
    string Email,
    string LegalName,
    string TaxId,
    string BillingEmail,
    string Phone,
    string Address,
    string City,
    string Department,
    string PlanCode,
    string PlanLabel,
    decimal PlanPriceCop,
    string SubscriptionStatus,
    string DisplayStatus,
    string RenewalStatus,
    bool IsMembershipActive,
    int DaysRemaining,
    DateTime? MembershipStartsAt,
    DateTime? MembershipEndsAt,
    int TeachersUsed,
    int TeachersMax,
    int StudentsUsed,
    int StudentsMax,
    bool HasSeatOverrides,
    bool HasOpenRequest,
    DateTime CreatedAt,
    IReadOnlyList<UserListItemDto> Members,
    IReadOnlyList<MembershipEventDto> History);

public sealed record MembershipEventDto(
    int Id,
    string EventType,
    string? PlanCode,
    decimal? PlanPriceCop,
    string? Note,
    DateTime CreatedAt);

public sealed record MonthlyRegistrationPointDto(
    string Label,
    int Year,
    int Month,
    int Students,
    int Teachers,
    int Schools);

public sealed record PilotMetricsDto(
    int DailyActiveUsers,
    int WeeklyActiveUsers,
    int MonthlyActiveUsers,
    int ActiveSchools,
    int PendingMembershipRequests,
    int MembershipRequests30d,
    int MembershipActivations30d,
    decimal MembershipConversionRate30d,
    int StudentsTotal,
    int StudentsActive7d,
    int StudentsInactive14d,
    int TeachersTotal,
    int TeachersActive7d,
    int ActiveGroups,
    int AttemptsStarted30d,
    int AttemptsFinished30d,
    decimal ExamCompletionRate30d,
    decimal ExamPassRate30d,
    decimal AvgAttemptsPerStudent30d,
    int QuestionsAnsweredTotal,
    decimal AvgExamTimeSeconds30d,
    int AbandonedAttempts30d,
    decimal SimulatorUsageShare30d,
    int ClassroomSubmissions30d,
    decimal UsersGrowth30d,
    decimal SchoolsGrowth30d,
    decimal TeachersGrowth30d,
    decimal StudentsGrowth30d,
    IReadOnlyList<MonthlyRegistrationPointDto> RegistrationsLast6Months);

public sealed record UpdateSchoolBillingRequest(
    string LegalName,
    string TaxId,
    string BillingEmail,
    string Phone,
    string Address,
    string City,
    string Department);

public sealed record CreateSchoolMemberRequest(
    string Name,
    string Email,
    string Password,
    string Role);

public sealed record AttachSchoolMemberRequest(
    string Email,
    string Role);

public sealed record UpdateSchoolMemberRequest(
    string Name,
    string Email,
    string? NewPassword);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public sealed record AuthResponse(
    string Token,
    int UserId,
    string Name,
    string Email,
    string Role,
    bool MustChangePassword);

public sealed record PendingEmailConfirmationResponse(
    string Email,
    string Message,
    bool RequiresEmailConfirmation = true,
    bool EmailSent = false,
    string? Token = null,
    int? UserId = null,
    string? Name = null,
    string? Role = null,
    bool MustChangePassword = false);

public sealed record ConfirmEmailRequest(string Email, string Code);

public sealed record ResendConfirmationRequest(string Email);

public sealed record MeResponse(
    int Id,
    string Name,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    bool MustChangePassword,
    MeSchoolContextDto? School);

public sealed record MeSchoolContextDto(
    int SchoolId,
    string LegalName,
    string PlanLabel,
    string City,
    string Department,
    string SubscriptionStatus,
    int DaysRemaining,
    bool IsMembershipActive);

public sealed record UpdateMyProfileRequest(string Name, string? Email = null);

public sealed record UserListItemDto(
    int Id,
    string Name,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public sealed record CreateTeacherRequest(
    string Name,
    string Email,
    string Password);

public sealed record CreateSchoolRequest(
    string LegalName,
    string Email,
    string Password,
    string? ContactName = null,
    string? TaxId = null,
    string? BillingEmail = null,
    string? Phone = null,
    string? Address = null,
    string? City = null,
    string? Department = null,
    string? PlanCode = null);

public sealed record UpdateUserRequest(
    string Name,
    string Email,
    string Role,
    string? NewPassword);

public sealed record SetUserActiveRequest(bool IsActive);
