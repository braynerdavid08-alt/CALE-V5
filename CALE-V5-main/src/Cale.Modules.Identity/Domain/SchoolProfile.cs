using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Identity.Domain;

public sealed class SchoolProfile
{
    public int Id { get; private set; }
    public int UserId { get; private set; }
    public string LegalName { get; private set; } = "";
    public string TaxId { get; private set; } = "";
    public string BillingEmail { get; private set; } = "";
    public string Phone { get; private set; } = "";
    public string Address { get; private set; } = "";
    public string City { get; private set; } = "";
    public string Department { get; private set; } = "";
    public string PlanCode { get; private set; } = "";
    public decimal PlanPriceCop { get; private set; }
    public string SubscriptionStatus { get; private set; } = SchoolSubscriptionStatus.PendingPayment;
    public DateTime? MembershipStartsAt { get; private set; }
    public DateTime? MembershipEndsAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SchoolProfile()
    {
    }

    public static SchoolProfile Create(
        int userId,
        string legalName,
        string taxId,
        string billingEmail,
        string phone,
        string address,
        string city,
        string department,
        SchoolPlanInfo plan,
        DateTime utcNow)
    {
        return new SchoolProfile
        {
            UserId = userId,
            LegalName = legalName.Trim(),
            TaxId = taxId.Trim(),
            BillingEmail = billingEmail.Trim().ToLowerInvariant(),
            Phone = phone.Trim(),
            Address = address.Trim(),
            City = city.Trim(),
            Department = department.Trim(),
            PlanCode = plan.Code,
            PlanPriceCop = plan.PriceCop,
            SubscriptionStatus = SchoolSubscriptionStatus.PendingPayment,
            CreatedAt = utcNow
        };
    }

    /// <summary>
    /// Minimal profile for School accounts created without billing data (e.g. role change).
    /// </summary>
    public static SchoolProfile CreateDraft(
        int userId,
        string contactName,
        string email,
        SchoolPlanInfo plan,
        DateTime utcNow)
    {
        var name = string.IsNullOrWhiteSpace(contactName) ? "Escuela" : contactName.Trim();
        return new SchoolProfile
        {
            UserId = userId,
            LegalName = name,
            TaxId = "PENDIENTE",
            BillingEmail = email.Trim().ToLowerInvariant(),
            Phone = "Sin registrar",
            Address = "Sin registrar",
            City = "Sin registrar",
            Department = "Sin registrar",
            PlanCode = plan.Code,
            PlanPriceCop = plan.PriceCop,
            SubscriptionStatus = SchoolSubscriptionStatus.PendingPayment,
            CreatedAt = utcNow
        };
    }

    public void SelectPlan(SchoolPlanInfo plan)
    {
        PlanCode = plan.Code;
        PlanPriceCop = plan.PriceCop;
        if (SubscriptionStatus == SchoolSubscriptionStatus.Active
            && MembershipEndsAt is { } end
            && end > DateTime.UtcNow)
        {
            // Keep current window; only change commercial plan / seats.
            return;
        }

        SubscriptionStatus = SchoolSubscriptionStatus.PendingPayment;
        MembershipStartsAt = null;
        MembershipEndsAt = null;
    }

    public void ActivateOrRenew(SchoolPlanInfo plan, DateTime utcNow)
    {
        PlanCode = plan.Code;
        PlanPriceCop = plan.PriceCop;

        var baseDate = utcNow;
        if (SubscriptionStatus == SchoolSubscriptionStatus.Active
            && MembershipEndsAt is { } end
            && end > utcNow)
        {
            baseDate = end;
        }

        MembershipStartsAt ??= utcNow;
        MembershipEndsAt = baseDate.AddMonths(plan.DurationMonths);
        SubscriptionStatus = SchoolSubscriptionStatus.Active;
    }

    public void RefreshStatus(DateTime utcNow)
    {
        if (SubscriptionStatus == SchoolSubscriptionStatus.Active
            && MembershipEndsAt is { } end
            && end <= utcNow)
        {
            SubscriptionStatus = SchoolSubscriptionStatus.Expired;
        }
    }

    public int DaysRemaining(DateTime utcNow)
    {
        RefreshStatus(utcNow);
        if (MembershipEndsAt is null)
        {
            return 0;
        }

        var days = (int)Math.Ceiling((MembershipEndsAt.Value - utcNow).TotalDays);
        return Math.Max(0, days);
    }

    public void UpdateBilling(
        string legalName,
        string taxId,
        string billingEmail,
        string phone,
        string address,
        string city,
        string department)
    {
        if (string.IsNullOrWhiteSpace(legalName))
        {
            throw new DomainException("Legal name is required.", 400, "invalid_legal_name");
        }

        if (string.IsNullOrWhiteSpace(taxId))
        {
            throw new DomainException("Tax ID (NIT) is required.", 400, "invalid_tax_id");
        }

        LegalName = legalName.Trim();
        TaxId = taxId.Trim();
        BillingEmail = billingEmail.Trim().ToLowerInvariant();
        Phone = phone.Trim();
        Address = address.Trim();
        City = city.Trim();
        Department = department.Trim();
    }
}

public static class SchoolSubscriptionStatus
{
    public const string PendingPayment = "PendingPayment";
    public const string Active = "Active";
    public const string Expired = "Expired";
}
