using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Identity.Domain;

/// <summary>
/// Membership FSM (persisted):
///   None → PendingPayment → UnderReview → Active → Expired
///   PendingPayment|UnderReview → Rejected|Cancelled
///   Active → Suspended → Active|Expired
///
/// EXPIRING is a read projection of Active when daysRemaining ≤ <see cref="ExpiringWithinDays"/>.
///
/// Renewal while Active uses <see cref="RenewalStatus"/> (sub-machine), so membershipStatus
/// does not lie about access.
/// </summary>
public sealed class SchoolProfile
{
    public const int ExpiringWithinDays = 14;

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
    public string? RequestedPlanCode { get; private set; }
    public string SubscriptionStatus { get; private set; } = SchoolSubscriptionStatus.None;
    public string RenewalStatus { get; private set; } = SchoolRenewalStatus.None;
    public string? PaymentProofUrl { get; private set; }
    public string? PaymentReference { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? SuspensionReason { get; private set; }
    public DateTime? RequestedAt { get; private set; }
    public DateTime? ProofSubmittedAt { get; private set; }
    public DateTime? LastDecisionAt { get; private set; }
    public DateTime? MembershipStartsAt { get; private set; }
    public DateTime? MembershipEndsAt { get; private set; }
    public int? TeachersMaxOverride { get; private set; }
    public int? StudentsMaxOverride { get; private set; }
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
            RequestedPlanCode = plan.Code,
            SubscriptionStatus = SchoolSubscriptionStatus.PendingPayment,
            RenewalStatus = SchoolRenewalStatus.None,
            RequestedAt = utcNow,
            CreatedAt = utcNow
        };
    }

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
            RequestedPlanCode = null,
            SubscriptionStatus = SchoolSubscriptionStatus.None,
            RenewalStatus = SchoolRenewalStatus.None,
            CreatedAt = utcNow
        };
    }

    public string EffectiveRequestedPlanCode =>
        RequestedPlanCode ?? PlanCode;

    /// <summary>Product access gate: only Active (persisted) with future end date.</summary>
    public bool IsCommerciallyActive(DateTime utcNow)
    {
        RefreshStatus(utcNow);
        return SubscriptionStatus == SchoolSubscriptionStatus.Active
            && MembershipEndsAt is { } end
            && end > utcNow;
    }

    /// <summary>
    /// Read model including EXPIRING projection. Prefer this for UI/métricas.
    /// </summary>
    public string DisplayStatus(DateTime utcNow)
    {
        RefreshStatus(utcNow);
        if (SubscriptionStatus == SchoolSubscriptionStatus.Active
            && DaysRemaining(utcNow) is > 0 and <= ExpiringWithinDays)
        {
            return SchoolSubscriptionStatus.Expiring;
        }

        return SubscriptionStatus;
    }

    public bool CanOperateProduct(DateTime utcNow) =>
        IsCommerciallyActive(utcNow);

    /// <summary>School starts or updates a plan request (never self-activates).</summary>
    public void RequestMembership(SchoolPlanInfo plan, DateTime utcNow)
    {
        RefreshStatus(utcNow);

        if (SubscriptionStatus == SchoolSubscriptionStatus.Suspended)
        {
            throw new DomainException(
                "La membresía está suspendida. Contacta soporte o un administrador.",
                403,
                "membership_suspended");
        }

        if (SubscriptionStatus is SchoolSubscriptionStatus.UnderReview)
        {
            throw new DomainException(
                "Ya hay un comprobante en revisión. Espera la decisión del administrador.",
                400,
                "membership_under_review");
        }

        RequestedPlanCode = plan.Code;
        RequestedAt = utcNow;
        PaymentProofUrl = null;
        PaymentReference = null;
        ProofSubmittedAt = null;
        RejectionReason = null;

        if (IsCommerciallyActive(utcNow))
        {
            RenewalStatus = SchoolRenewalStatus.PendingPayment;
            return;
        }

        if (SubscriptionStatus is SchoolSubscriptionStatus.None
            or SchoolSubscriptionStatus.PendingPayment
            or SchoolSubscriptionStatus.Rejected
            or SchoolSubscriptionStatus.Cancelled
            or SchoolSubscriptionStatus.Expired)
        {
            PlanCode = plan.Code;
            PlanPriceCop = plan.PriceCop;
            SubscriptionStatus = SchoolSubscriptionStatus.PendingPayment;
            RenewalStatus = SchoolRenewalStatus.None;
            return;
        }

        throw new DomainException(
            "No se puede solicitar membresía desde el estado actual.",
            400,
            "invalid_membership_transition");
    }

    /// <summary>School uploads payment proof → UnderReview (main or renewal).</summary>
    public void SubmitPaymentProof(string proofUrl, string? paymentReference, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(proofUrl))
        {
            throw new DomainException(
                "Debes adjuntar el comprobante de pago.",
                400,
                "payment_proof_required");
        }

        RefreshStatus(utcNow);
        if (!HasOpenCommercialRequest(utcNow))
        {
            throw new DomainException(
                "No hay una solicitud de membresía abierta.",
                400,
                "no_membership_request");
        }

        if (SubscriptionStatus == SchoolSubscriptionStatus.Suspended)
        {
            throw new DomainException(
                "La membresía está suspendida.",
                403,
                "membership_suspended");
        }

        PaymentProofUrl = proofUrl.Trim();
        PaymentReference = string.IsNullOrWhiteSpace(paymentReference)
            ? null
            : paymentReference.Trim();
        ProofSubmittedAt = utcNow;
        RejectionReason = null;

        if (IsCommerciallyActive(utcNow))
        {
            if (RenewalStatus is not (SchoolRenewalStatus.PendingPayment
                or SchoolRenewalStatus.UnderReview
                or SchoolRenewalStatus.Rejected))
            {
                throw new DomainException(
                    "No hay renovación pendiente de pago.",
                    400,
                    "no_renewal_request");
            }

            RenewalStatus = SchoolRenewalStatus.UnderReview;
            return;
        }

        if (SubscriptionStatus is SchoolSubscriptionStatus.PendingPayment
            or SchoolSubscriptionStatus.UnderReview)
        {
            SubscriptionStatus = SchoolSubscriptionStatus.UnderReview;
            RenewalStatus = SchoolRenewalStatus.None;
            return;
        }

        throw new DomainException(
            "No se puede subir comprobante desde el estado actual.",
            400,
            "invalid_membership_transition");
    }

    public void ActivateOrRenew(SchoolPlanInfo plan, DateTime utcNow)
    {
        RefreshStatus(utcNow);

        if (SubscriptionStatus == SchoolSubscriptionStatus.Suspended)
        {
            throw new DomainException(
                "Quita la suspensión antes de activar o renovar.",
                400,
                "membership_suspended");
        }

        var operating = IsCommerciallyActive(utcNow);
        if (operating)
        {
            if (RenewalStatus != SchoolRenewalStatus.UnderReview
                && string.IsNullOrWhiteSpace(PaymentProofUrl))
            {
                throw new DomainException(
                    "No hay renovación en revisión para aprobar.",
                    400,
                    "no_renewal_request");
            }
        }
        else if (SubscriptionStatus is not (
                     SchoolSubscriptionStatus.UnderReview
                     or SchoolSubscriptionStatus.PendingPayment
                     or SchoolSubscriptionStatus.Expired
                     or SchoolSubscriptionStatus.Rejected))
        {
            throw new DomainException(
                "No hay solicitud de membresía para activar.",
                400,
                "no_membership_request");
        }

        PlanCode = plan.Code;
        PlanPriceCop = plan.PriceCop;

        var baseDate = utcNow;
        if (MembershipEndsAt is { } end && end > utcNow)
        {
            baseDate = end;
        }

        MembershipStartsAt ??= utcNow;
        MembershipEndsAt = baseDate.AddMonths(plan.DurationMonths);
        SubscriptionStatus = SchoolSubscriptionStatus.Active;
        SuspensionReason = null;
        ClearRequestFields();
        RejectionReason = null;
        LastDecisionAt = utcNow;
    }

    public void RejectRequest(string reason, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(
                "Indica el motivo del rechazo.",
                400,
                "rejection_reason_required");
        }

        RefreshStatus(utcNow);
        if (!HasOpenCommercialRequest(utcNow))
        {
            throw new DomainException(
                "No hay solicitud pendiente para rechazar.",
                400,
                "no_membership_request");
        }

        RejectionReason = reason.Trim();
        LastDecisionAt = utcNow;

        if (IsCommerciallyActive(utcNow))
        {
            PaymentProofUrl = null;
            PaymentReference = null;
            ProofSubmittedAt = null;
            RenewalStatus = SchoolRenewalStatus.Rejected;
            return;
        }

        ClearRequestFields(keepRejection: true);
        SubscriptionStatus = SchoolSubscriptionStatus.Rejected;
        RenewalStatus = SchoolRenewalStatus.None;
    }

    /// <summary>School or admin abandons a pre-activation or renewal request.</summary>
    public void CancelRequest(string? note, DateTime utcNow)
    {
        RefreshStatus(utcNow);

        if (IsCommerciallyActive(utcNow)
            && RenewalStatus is SchoolRenewalStatus.PendingPayment
                or SchoolRenewalStatus.UnderReview
                or SchoolRenewalStatus.Rejected)
        {
            ClearRequestFields();
            RenewalStatus = SchoolRenewalStatus.None;
            LastDecisionAt = utcNow;
            return;
        }

        if (SubscriptionStatus is SchoolSubscriptionStatus.PendingPayment
            or SchoolSubscriptionStatus.UnderReview)
        {
            ClearRequestFields();
            SubscriptionStatus = SchoolSubscriptionStatus.Cancelled;
            RenewalStatus = SchoolRenewalStatus.None;
            LastDecisionAt = utcNow;
            _ = note;
            return;
        }

        throw new DomainException(
            "No hay solicitud cancelable en el estado actual.",
            400,
            "no_membership_request");
    }

    public void Suspend(string reason, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(
                "Indica el motivo de la suspensión.",
                400,
                "suspension_reason_required");
        }

        RefreshStatus(utcNow);
        if (SubscriptionStatus != SchoolSubscriptionStatus.Active)
        {
            throw new DomainException(
                "Solo se puede suspender una membresía activa.",
                400,
                "invalid_membership_transition");
        }

        SuspensionReason = reason.Trim();
        SubscriptionStatus = SchoolSubscriptionStatus.Suspended;
        LastDecisionAt = utcNow;
    }

    public void Unsuspend(DateTime utcNow)
    {
        RefreshStatus(utcNow);
        if (SubscriptionStatus != SchoolSubscriptionStatus.Suspended)
        {
            throw new DomainException(
                "La membresía no está suspendida.",
                400,
                "not_suspended");
        }

        SuspensionReason = null;
        LastDecisionAt = utcNow;

        if (MembershipEndsAt is { } end && end > utcNow)
        {
            SubscriptionStatus = SchoolSubscriptionStatus.Active;
            return;
        }

        SubscriptionStatus = SchoolSubscriptionStatus.Expired;
        if (!string.IsNullOrWhiteSpace(RequestedPlanCode)
            || RenewalStatus != SchoolRenewalStatus.None)
        {
            AbsorbRenewalIntoMainRequest(utcNow);
        }
    }

    public bool HasOpenCommercialRequest(DateTime utcNow)
    {
        RefreshStatus(utcNow);
        if (IsCommerciallyActive(utcNow))
        {
            return RenewalStatus is SchoolRenewalStatus.PendingPayment
                or SchoolRenewalStatus.UnderReview
                or SchoolRenewalStatus.Rejected;
        }

        return SubscriptionStatus is SchoolSubscriptionStatus.PendingPayment
            or SchoolSubscriptionStatus.UnderReview;
    }

    public bool AwaitingAdminReview(DateTime utcNow)
    {
        RefreshStatus(utcNow);
        if (IsCommerciallyActive(utcNow))
        {
            return RenewalStatus == SchoolRenewalStatus.UnderReview
                && !string.IsNullOrWhiteSpace(PaymentProofUrl);
        }

        return SubscriptionStatus == SchoolSubscriptionStatus.UnderReview;
    }

    public bool NeedsPaymentProof(DateTime utcNow)
    {
        RefreshStatus(utcNow);
        if (IsCommerciallyActive(utcNow))
        {
            return RenewalStatus is SchoolRenewalStatus.PendingPayment
                or SchoolRenewalStatus.Rejected;
        }

        return SubscriptionStatus == SchoolSubscriptionStatus.PendingPayment;
    }

    public void RefreshStatus(DateTime utcNow)
    {
        NormalizeLegacyStatuses();

        if (SubscriptionStatus == SchoolSubscriptionStatus.Active
            && MembershipEndsAt is { } end
            && end <= utcNow)
        {
            SubscriptionStatus = SchoolSubscriptionStatus.Expired;
            if (RenewalStatus != SchoolRenewalStatus.None
                || !string.IsNullOrWhiteSpace(RequestedPlanCode))
            {
                AbsorbRenewalIntoMainRequest(utcNow);
            }
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

    public int EffectiveMaxTeachers(SchoolPlanInfo? plan) =>
        TeachersMaxOverride ?? plan?.MaxTeachers ?? 0;

    public int EffectiveMaxStudents(SchoolPlanInfo? plan) =>
        StudentsMaxOverride ?? plan?.MaxStudents ?? 0;

    public bool HasSeatOverrides =>
        TeachersMaxOverride is not null || StudentsMaxOverride is not null;

    /// <summary>Admin sets custom seat caps (null clears override → plan default).</summary>
    public void SetSeatOverrides(int? teachersMax, int? studentsMax, DateTime utcNow)
    {
        if (teachersMax is < 0 or > 100_000)
        {
            throw new DomainException(
                "El cupo de docentes debe estar entre 0 y 100000.",
                400,
                "invalid_teachers_max");
        }

        if (studentsMax is < 0 or > 100_000)
        {
            throw new DomainException(
                "El cupo de estudiantes debe estar entre 0 y 100000.",
                400,
                "invalid_students_max");
        }

        TeachersMaxOverride = teachersMax;
        StudentsMaxOverride = studentsMax;
        LastDecisionAt = utcNow;
    }

    /// <summary>
    /// Admin full control: force plan/status/dates without commercial request flow.
    /// </summary>
    public void AdminOverrideMembership(
        SchoolPlanInfo? plan,
        string? targetStatus,
        DateTime? membershipEndsAt,
        bool clearRejection,
        DateTime utcNow)
    {
        if (plan is not null)
        {
            PlanCode = plan.Code;
            PlanPriceCop = plan.PriceCop;
        }

        if (membershipEndsAt is not null)
        {
            if (membershipEndsAt.Value <= utcNow.AddMinutes(-1))
            {
                throw new DomainException(
                    "La fecha de fin debe ser futura.",
                    400,
                    "invalid_membership_end");
            }

            MembershipStartsAt ??= utcNow;
            MembershipEndsAt = membershipEndsAt.Value.ToUniversalTime();
        }

        if (!string.IsNullOrWhiteSpace(targetStatus))
        {
            var status = NormalizeAdminStatus(targetStatus);
            SubscriptionStatus = status;
            if (status == SchoolSubscriptionStatus.Active)
            {
                MembershipStartsAt ??= utcNow;
                if (MembershipEndsAt is null || MembershipEndsAt <= utcNow)
                {
                    var duration = plan?.DurationMonths
                        ?? SchoolPlans.Find(PlanCode)?.DurationMonths
                        ?? 1;
                    MembershipEndsAt = utcNow.AddMonths(duration);
                }

                SuspensionReason = null;
                RenewalStatus = SchoolRenewalStatus.None;
                ClearRequestFields();
            }
            else if (status == SchoolSubscriptionStatus.Suspended)
            {
                // keep endsAt
            }
            else if (status is SchoolSubscriptionStatus.PendingPayment
                or SchoolSubscriptionStatus.UnderReview
                or SchoolSubscriptionStatus.Rejected
                or SchoolSubscriptionStatus.Cancelled
                or SchoolSubscriptionStatus.None
                or SchoolSubscriptionStatus.Expired)
            {
                RenewalStatus = SchoolRenewalStatus.None;
            }
        }

        if (clearRejection)
        {
            RejectionReason = null;
        }

        LastDecisionAt = utcNow;
    }

    /// <summary>Re-open a closed commercial decision into PendingPayment for re-review.</summary>
    public void AdminReopenRequest(SchoolPlanInfo? plan, DateTime utcNow)
    {
        RefreshStatus(utcNow);
        if (plan is not null)
        {
            PlanCode = plan.Code;
            PlanPriceCop = plan.PriceCop;
            RequestedPlanCode = plan.Code;
        }
        else
        {
            RequestedPlanCode = PlanCode;
        }

        RequestedAt = utcNow;
        PaymentProofUrl = null;
        PaymentReference = null;
        ProofSubmittedAt = null;
        RejectionReason = null;
        SuspensionReason = null;

        if (IsCommerciallyActive(utcNow))
        {
            RenewalStatus = SchoolRenewalStatus.PendingPayment;
        }
        else
        {
            SubscriptionStatus = SchoolSubscriptionStatus.PendingPayment;
            RenewalStatus = SchoolRenewalStatus.None;
        }

        LastDecisionAt = utcNow;
    }

    private static string NormalizeAdminStatus(string status) => status.Trim() switch
    {
        SchoolSubscriptionStatus.None => SchoolSubscriptionStatus.None,
        SchoolSubscriptionStatus.PendingPayment => SchoolSubscriptionStatus.PendingPayment,
        SchoolSubscriptionStatus.UnderReview
            or SchoolSubscriptionStatus.PaymentSubmittedLegacy => SchoolSubscriptionStatus.UnderReview,
        SchoolSubscriptionStatus.Active => SchoolSubscriptionStatus.Active,
        SchoolSubscriptionStatus.Expired => SchoolSubscriptionStatus.Expired,
        SchoolSubscriptionStatus.Rejected => SchoolSubscriptionStatus.Rejected,
        SchoolSubscriptionStatus.Cancelled => SchoolSubscriptionStatus.Cancelled,
        SchoolSubscriptionStatus.Suspended => SchoolSubscriptionStatus.Suspended,
        _ => throw new DomainException(
            "Estado de membresía inválido.",
            400,
            "invalid_membership_status")
    };

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

    private void AbsorbRenewalIntoMainRequest(DateTime utcNow)
    {
        if (!string.IsNullOrWhiteSpace(RequestedPlanCode))
        {
            PlanCode = RequestedPlanCode;
            var plan = SchoolPlans.Find(RequestedPlanCode);
            if (plan is not null)
            {
                PlanPriceCop = plan.PriceCop;
            }
        }

        SubscriptionStatus = RenewalStatus == SchoolRenewalStatus.UnderReview
            || !string.IsNullOrWhiteSpace(PaymentProofUrl)
            ? SchoolSubscriptionStatus.UnderReview
            : SchoolSubscriptionStatus.PendingPayment;
        RenewalStatus = SchoolRenewalStatus.None;
        RequestedAt ??= utcNow;
    }

    private void NormalizeLegacyStatuses()
    {
        if (SubscriptionStatus == SchoolSubscriptionStatus.PaymentSubmittedLegacy)
        {
            SubscriptionStatus = SchoolSubscriptionStatus.UnderReview;
        }

        if (string.IsNullOrWhiteSpace(RenewalStatus))
        {
            RenewalStatus = SchoolRenewalStatus.None;
        }

        // Legacy overlay: Active + RequestedPlanCode without RenewalStatus
        if (SubscriptionStatus == SchoolSubscriptionStatus.Active
            && RenewalStatus == SchoolRenewalStatus.None
            && !string.IsNullOrWhiteSpace(RequestedPlanCode))
        {
            RenewalStatus = string.IsNullOrWhiteSpace(PaymentProofUrl)
                ? SchoolRenewalStatus.PendingPayment
                : SchoolRenewalStatus.UnderReview;
        }
    }

    private void ClearRequestFields(bool keepRejection = false)
    {
        RequestedPlanCode = null;
        PaymentProofUrl = null;
        PaymentReference = null;
        ProofSubmittedAt = null;
        RequestedAt = null;
        RenewalStatus = SchoolRenewalStatus.None;
        if (!keepRejection)
        {
            RejectionReason = null;
        }
    }
}

public static class SchoolSubscriptionStatus
{
    public const string None = "None";
    public const string PendingPayment = "PendingPayment";
    public const string UnderReview = "UnderReview";
    /// <summary>Legacy alias persisted in older DBs; normalized to UnderReview.</summary>
    public const string PaymentSubmittedLegacy = "PaymentSubmitted";
    public const string Active = "Active";
    /// <summary>Projection only — not persisted.</summary>
    public const string Expiring = "Expiring";
    public const string Expired = "Expired";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
    public const string Suspended = "Suspended";
}

public static class SchoolRenewalStatus
{
    public const string None = "None";
    public const string PendingPayment = "PendingPayment";
    public const string UnderReview = "UnderReview";
    public const string Rejected = "Rejected";
}

public static class SchoolPaymentInstructions
{
    public const string BankName = "Bancolombia";
    public const string AccountType = "Ahorros";
    public const string AccountNumber = "ACCT-000019";
    public const string AccountHolder = "CALE Formación Vial SAS";
    public const string HolderTaxId = "901.000.000-1";
    public const string WhatsApp = "+57 300 000 0000";
    public const string SupportEmail = "pagos@cale.local";
    public const string Notes =
        "Usa como referencia tu NIT o el correo de la escuela. Sube el comprobante en Membresía para que un administrador verifique el pago.";
}
