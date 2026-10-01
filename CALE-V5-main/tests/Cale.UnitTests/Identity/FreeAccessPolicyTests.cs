using Cale.BuildingBlocks.Domain.Access;
using Cale.Modules.Identity.Domain;

namespace Cale.UnitTests.Identity;

[Collection(nameof(FreeAccessPolicyTests))]
public sealed class FreeAccessPolicyTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static SchoolProfile DraftProfile() =>
        SchoolProfile.CreateDraft(1, "CEA Prueba", "cea@example.com", SchoolPlans.Find(SchoolPlans.Monthly)!, T0);

    [Fact]
    public void Free_mode_lets_a_school_without_membership_operate_without_seat_caps()
    {
        var previous = FreeAccessPolicy.Enabled;
        try
        {
            FreeAccessPolicy.Enabled = true;
            var profile = DraftProfile();

            Assert.False(profile.IsCommerciallyActive(T0));
            Assert.True(profile.CanOperateProduct(T0));
            Assert.False(SchoolProfile.SeatLimitsEnforced);
        }
        finally
        {
            FreeAccessPolicy.Enabled = previous;
        }
    }

    [Fact]
    public void Paid_mode_requires_an_active_membership_and_enforces_seats()
    {
        var previous = FreeAccessPolicy.Enabled;
        try
        {
            FreeAccessPolicy.Enabled = false;
            var profile = DraftProfile();

            Assert.False(profile.CanOperateProduct(T0));
            Assert.True(SchoolProfile.SeatLimitsEnforced);
        }
        finally
        {
            FreeAccessPolicy.Enabled = previous;
        }
    }
}
