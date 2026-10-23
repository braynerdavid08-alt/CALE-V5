using Cale.BuildingBlocks.Domain.Access;
using Cale.Modules.Identity.Domain;

namespace Cale.UnitTests.Identity;

public sealed class CommercialSubscriptionTests
{
    [Fact]
    public void Commercial_offer_defaults_match_temporary_pricing()
    {
        var monthly = SchoolPlans.Find(SchoolPlans.Monthly)!;
        var annual = SchoolPlans.Find(SchoolPlans.Annual)!;

        Assert.Equal(500_000m, monthly.PriceCop);
        Assert.Equal(5_000_000m, annual.PriceCop);
        Assert.Equal(0, monthly.MaxStudents);
        Assert.False(SchoolPlans.HasSeatCap(monthly.MaxStudents));
    }

    [Fact]
    public void Courtesy_grants_access_when_not_commercially_active()
    {
        var previous = FreeAccessPolicy.Enabled;
        try
        {
            FreeAccessPolicy.Enabled = false;
            var now = DateTime.UtcNow;
            var plan = SchoolPlans.Find(SchoolPlans.Monthly)!;
            var profile = SchoolProfile.CreateDraft(1, "CEA Demo", "cea@test.com", plan, now);
            Assert.False(profile.CanOperateProduct(now));

            profile.GrantCourtesy(now.AddDays(10), "Transición", now);
            Assert.True(profile.IsCourtesyActive(now));
            Assert.True(profile.CanOperateProduct(now));
        }
        finally
        {
            FreeAccessPolicy.Enabled = previous;
        }
    }

    [Fact]
    public void Assign_subscription_requires_confirmed_payment_to_activate()
    {
        var now = DateTime.UtcNow;
        var plan = SchoolPlans.Find(SchoolPlans.Annual)!;
        var profile = SchoolProfile.CreateDraft(2, "CEA", "a@b.com", plan, now);

        var ex = Assert.Throws<Cale.BuildingBlocks.Domain.Exceptions.DomainException>(() =>
            profile.AdminAssignSubscription(
                plan,
                5_000_000m,
                now,
                now.AddYears(1),
                PaymentAdminStatuses.Pending,
                activateNow: true,
                now));
        Assert.Equal("payment_not_confirmed", ex.ErrorCode);

        profile.AdminAssignSubscription(
            plan,
            5_000_000m,
            now,
            now.AddYears(1),
            PaymentAdminStatuses.Confirmed,
            activateNow: true,
            now);
        Assert.Equal(SchoolSubscriptionStatus.Active, profile.SubscriptionStatus);
        Assert.Equal(PaymentAdminStatuses.Confirmed, profile.PaymentAdminStatus);
        Assert.Equal(5_000_000m, profile.AgreedPriceCop);
    }
}
