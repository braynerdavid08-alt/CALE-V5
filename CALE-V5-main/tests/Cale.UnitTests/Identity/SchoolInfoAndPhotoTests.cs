using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Identity.Domain;

namespace Cale.UnitTests.Identity;

public sealed class SchoolInfoAndPhotoTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static SchoolProfile DraftProfile() =>
        SchoolProfile.CreateDraft(1, "vipcea", "cea@example.com", SchoolPlans.Find(SchoolPlans.Monthly)!, T0);

    [Fact]
    public void School_can_correct_its_city_and_department()
    {
        var profile = DraftProfile();

        profile.UpdateBilling(" vipcea ", "900123456", "CEA@Example.com", "3001234567", "Calle 72 # 45-10", " Barranquilla ", "Atlántico");

        Assert.Equal("vipcea", profile.LegalName);
        Assert.Equal("Barranquilla", profile.City);
        Assert.Equal("Atlántico", profile.Department);
        Assert.Equal("cea@example.com", profile.BillingEmail);
    }

    [Theory]
    [InlineData("", "900123456", "Barranquilla", "Atlántico", "invalid_legal_name")]
    [InlineData("vipcea", " ", "Barranquilla", "Atlántico", "invalid_tax_id")]
    [InlineData("vipcea", "900123456", "", "Atlántico", "invalid_city")]
    [InlineData("vipcea", "900123456", "Barranquilla", "", "invalid_department")]
    public void Required_school_fields_are_rejected_with_spanish_messages(
        string name, string taxId, string city, string department, string code)
    {
        var profile = DraftProfile();

        var ex = Assert.Throws<DomainException>(() =>
            profile.UpdateBilling(name, taxId, "cea@example.com", "", "", city, department));

        Assert.Equal(code, ex.ErrorCode);
        Assert.DoesNotContain("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Overlong_city_is_rejected()
    {
        var profile = DraftProfile();

        var ex = Assert.Throws<DomainException>(() =>
            profile.UpdateBilling("vipcea", "900123456", "cea@example.com", "", "", new string('a', 121), "Atlántico"));

        Assert.Equal("invalid_city", ex.ErrorCode);
    }

    [Fact]
    public void Photo_can_be_set_and_cleared()
    {
        var user = User.RegisterStudent("Ana Torres", "ana@test", "x", T0);

        user.SetPhoto(" /api/media/2f1c7a8e-5b1d-4c43-9a43-0e6f0c3b1a11 ");
        Assert.Equal("/api/media/2f1c7a8e-5b1d-4c43-9a43-0e6f0c3b1a11", user.PhotoUrl);

        user.SetPhoto(null);
        Assert.Null(user.PhotoUrl);
    }
}
