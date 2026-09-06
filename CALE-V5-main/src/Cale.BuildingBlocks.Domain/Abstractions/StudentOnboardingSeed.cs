namespace Cale.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Optional CRM / enrollment fields collected when a school creates a student.
/// </summary>
public sealed record StudentOnboardingSeed(
    string? DocumentType = null,
    string? DocumentNumber = null,
    string? Phone = null,
    string? Address = null,
    string? ContactEmail = null,
    string? LicenseCategories = null,
    string? AttendanceDayType = null,
    string? ScheduleSlot = null,
    string? EnrollmentPin = null,
    decimal? AmountDue = null,
    decimal? AmountPaid = null,
    string? PaymentMethod = null,
    string? ReceiptNumber = null,
    bool RuntRegistered = false,
    bool IsEnrolled = false,
    string? Notes = null);
