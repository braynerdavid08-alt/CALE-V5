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
    string PlanCode);

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
    DateTime CreatedAt,
    DateTime? MembershipStartsAt,
    DateTime? MembershipEndsAt,
    int DaysRemaining,
    bool IsMembershipActive,
    int TeachersUsed,
    int TeachersMax,
    int StudentsUsed,
    int StudentsMax);

public sealed record ChangeSchoolPlanRequest(string PlanCode);

public sealed record ActivateSchoolPlanRequest(string? PlanCode);

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
    string Role);

public sealed record MeResponse(
    int Id,
    string Name,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
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

public sealed record UpdateMyProfileRequest(string Name);

public sealed record UserListItemDto(
    int Id,
    string Name,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt);

public sealed record CreateTeacherRequest(
    string Name,
    string Email,
    string Password);

public sealed record UpdateUserRequest(
    string Name,
    string Email,
    string Role,
    string? NewPassword);

public sealed record SetUserActiveRequest(bool IsActive);
