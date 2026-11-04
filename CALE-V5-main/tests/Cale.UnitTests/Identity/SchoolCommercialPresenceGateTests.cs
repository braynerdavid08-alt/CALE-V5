using Cale.BuildingBlocks.Domain.Access;
using Cale.Modules.Identity.Application.Services;
using Cale.Modules.Identity.Domain;

namespace Cale.UnitTests.Identity;

public sealed class SchoolCommercialPresenceGateTests
{
    private static readonly DateTime T0 = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private readonly SchoolCommercialPresenceGate _gate = new();

    [Fact]
    public void Eligible_when_monthly_confirmed_covered_complete_and_published()
    {
        var previous = FreeAccessPolicy.Enabled;
        try
        {
            FreeAccessPolicy.Enabled = true; // must not affect commercial gate
            var school = EligibleSchool(SchoolPlans.Monthly);
            var commercial = CompletePublishedCommercial(school.UserId);

            var decision = _gate.Evaluate(school, commercial, T0);

            Assert.True(decision.Eligible);
            Assert.Null(decision.DenyReason);
        }
        finally
        {
            FreeAccessPolicy.Enabled = previous;
        }
    }

    [Fact]
    public void Eligible_when_annual_confirmed_covered_complete_and_published()
    {
        var school = EligibleSchool(SchoolPlans.Annual);
        var commercial = CompletePublishedCommercial(school.UserId);

        Assert.True(_gate.Evaluate(school, commercial, T0).Eligible);
    }

    [Fact]
    public void Denies_legacy_semestral_plan()
    {
        var school = EligibleSchool(SchoolPlans.Semestral);

        var decision = _gate.Evaluate(school, CompletePublishedCommercial(school.UserId), T0);

        Assert.False(decision.Eligible);
        Assert.Equal(SchoolCommercialPresenceDenyReasons.PlanNotEligible, decision.DenyReason);
    }

    [Fact]
    public void FreeAccess_alone_never_grants_presence()
    {
        var previous = FreeAccessPolicy.Enabled;
        try
        {
            FreeAccessPolicy.Enabled = true;
            var school = SchoolProfile.Create(
                10,
                "CEA Libre",
                "900111222",
                "cea@test.com",
                "3001112233",
                "Calle 1",
                "Bogotá",
                "Cundinamarca",
                SchoolPlans.Find(SchoolPlans.Monthly)!,
                T0);
            Assert.True(school.CanOperateProduct(T0));
            Assert.False(school.IsCommerciallyActive(T0));

            var decision = _gate.Evaluate(school, CompletePublishedCommercial(10), T0);

            Assert.False(decision.Eligible);
            Assert.Equal(SchoolCommercialPresenceDenyReasons.PaymentNotConfirmed, decision.DenyReason);
        }
        finally
        {
            FreeAccessPolicy.Enabled = previous;
        }
    }

    [Fact]
    public void Courtesy_alone_never_grants_presence()
    {
        var previous = FreeAccessPolicy.Enabled;
        try
        {
            FreeAccessPolicy.Enabled = false;
            var school = SchoolProfile.Create(
                11,
                "CEA Cortesía",
                "900111223",
                "cortesia@test.com",
                "3001112244",
                "Calle 2",
                "Medellín",
                "Antioquia",
                SchoolPlans.Find(SchoolPlans.Monthly)!,
                T0);
            school.GrantCourtesy(T0.AddDays(20), "Transición", T0);
            Assert.True(school.CanOperateProduct(T0));

            var decision = _gate.Evaluate(school, CompletePublishedCommercial(11), T0);

            Assert.False(decision.Eligible);
            Assert.Equal(SchoolCommercialPresenceDenyReasons.PaymentNotConfirmed, decision.DenyReason);
        }
        finally
        {
            FreeAccessPolicy.Enabled = previous;
        }
    }

    [Fact]
    public void Denies_when_payment_not_confirmed()
    {
        var school = EligibleSchool(SchoolPlans.Monthly);
        school.SetPaymentAdminStatus(PaymentAdminStatuses.Pending, T0);

        var decision = _gate.Evaluate(school, CompletePublishedCommercial(school.UserId), T0);

        Assert.False(decision.Eligible);
        Assert.Equal(SchoolCommercialPresenceDenyReasons.PaymentNotConfirmed, decision.DenyReason);
    }

    [Fact]
    public void Denies_when_coverage_expired()
    {
        var school = EligibleSchool(SchoolPlans.Monthly);
        school.AdminOverrideMembership(
            SchoolPlans.Find(SchoolPlans.Monthly),
            SchoolSubscriptionStatus.Expired,
            null,
            false,
            T0);

        var decision = _gate.Evaluate(school, CompletePublishedCommercial(school.UserId), T0);

        Assert.False(decision.Eligible);
        Assert.Equal(SchoolCommercialPresenceDenyReasons.CoverageInactive, decision.DenyReason);
    }

    [Fact]
    public void Denies_when_membership_suspended()
    {
        var school = EligibleSchool(SchoolPlans.Monthly);
        school.Suspend("Incumplimiento", T0);

        var decision = _gate.Evaluate(school, CompletePublishedCommercial(school.UserId), T0);

        Assert.False(decision.Eligible);
        Assert.Equal(SchoolCommercialPresenceDenyReasons.MembershipSuspended, decision.DenyReason);
    }

    [Fact]
    public void Denies_when_pending_approval()
    {
        var school = EligibleSchool(SchoolPlans.Monthly);
        school.RequireApproval(T0);

        var decision = _gate.Evaluate(school, CompletePublishedCommercial(school.UserId), T0);

        Assert.False(decision.Eligible);
        Assert.Equal(SchoolCommercialPresenceDenyReasons.PendingApproval, decision.DenyReason);
    }

    [Fact]
    public void Denies_when_commercial_sheet_incomplete()
    {
        var school = EligibleSchool(SchoolPlans.Monthly);
        var commercial = SchoolCommercialProfile.Create(school.UserId, T0);

        var decision = _gate.Evaluate(school, commercial, T0);

        Assert.False(decision.Eligible);
        Assert.Equal(SchoolCommercialPresenceDenyReasons.CommercialSheetIncomplete, decision.DenyReason);
    }

    [Fact]
    public void Denies_when_not_voluntarily_published()
    {
        var school = EligibleSchool(SchoolPlans.Monthly);
        var commercial = CompletePublishedCommercial(school.UserId);
        commercial.Unpublish(T0);

        var decision = _gate.Evaluate(school, commercial, T0);

        Assert.False(decision.Eligible);
        Assert.Equal(SchoolCommercialPresenceDenyReasons.NotPublished, decision.DenyReason);
    }

    [Fact]
    public void Denies_when_commercially_suspended()
    {
        var school = EligibleSchool(SchoolPlans.Monthly);
        var commercial = CompletePublishedCommercial(school.UserId);
        commercial.SuspendCommercial("Abuso de directorio", T0);

        var decision = _gate.Evaluate(school, commercial, T0);

        Assert.False(decision.Eligible);
        Assert.Equal(SchoolCommercialPresenceDenyReasons.CommerciallySuspended, decision.DenyReason);
    }

    [Fact]
    public void Denies_when_school_contact_sheet_incomplete()
    {
        var school = EligibleSchool(SchoolPlans.Monthly);
        school.UpdateBilling(
            "CEA Sin Teléfono",
            "900123456-8",
            "a@b.com",
            SchoolProfile.NotRegistered,
            "Calle 9",
            "Cali",
            "Valle");

        var decision = _gate.Evaluate(school, CompletePublishedCommercial(school.UserId), T0);

        Assert.False(decision.Eligible);
        Assert.Equal(SchoolCommercialPresenceDenyReasons.SchoolSheetIncomplete, decision.DenyReason);
    }

    [Fact]
    public async Task Owner_only_access_policy_blocks_other_schools()
    {
        var policy = new OwnerOnlySchoolCommercialAccessPolicy();
        Assert.True(await policy.CanManageAsync(5, 5));
        Assert.False(await policy.CanManageAsync(5, 9));
        Assert.False(await policy.CanManageAsync(0, 0));
    }

    private static SchoolProfile EligibleSchool(string planCode)
    {
        var plan = SchoolPlans.Find(planCode) ?? SchoolPlans.Find(SchoolPlans.Monthly)!;
        var school = SchoolProfile.Create(
            42,
            "CEA Elegible",
            "900123456",
            "cea@elegible.com",
            "3001234567",
            "Cra 7 # 12-34",
            "Bogotá",
            "Cundinamarca",
            plan,
            T0);
        school.AdminAssignSubscription(
            plan,
            plan.PriceCop,
            T0.AddDays(-1),
            T0.AddMonths(plan.DurationMonths),
            PaymentAdminStatuses.Confirmed,
            activateNow: true,
            T0);
        return school;
    }

    private static SchoolCommercialProfile CompletePublishedCommercial(int schoolUserId)
    {
        var commercial = SchoolCommercialProfile.Create(schoolUserId, T0);
        commercial.UpdateSheet(
            "Formamos conductores responsables con instructores certificados y flota propia en la ciudad.",
            "A2, B1",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            null,
            T0);
        commercial.SetSlug("cea-elegible", T0);
        commercial.Publish(T0);
        return commercial;
    }
}
