using Cale.BuildingBlocks.Domain.Privacy;
using Cale.Modules.Identity.Application.Services;
using Xunit;

namespace Cale.UnitTests;

public sealed class PersonalDataTests
{
    [Theory]
    [InlineData("brayner@gmail.com", "br***@gmail.com")]
    [InlineData("a@x.co", "a***@x.co")]
    [InlineData("  Ana@Escuela.com ", "An***@Escuela.com")]
    [InlineData("sin-arroba", "***")]
    [InlineData("", "(vacío)")]
    [InlineData(null, "(vacío)")]
    public void Mask_email_never_reveals_the_full_address(string? email, string expected)
    {
        Assert.Equal(expected, PersonalData.MaskEmail(email));
    }

    [Fact]
    public void School_payment_data_is_hidden_until_the_account_is_configured()
    {
        Assert.False(new SchoolPaymentOptions().IsConfigured);
        Assert.False(new SchoolPaymentOptions { BankName = "Banco", AccountNumber = "123" }.IsConfigured);
        Assert.True(new SchoolPaymentOptions
        {
            BankName = "Banco",
            AccountNumber = "123",
            AccountHolder = "Titular",
            HolderTaxId = "NIT",
        }.IsConfigured);
    }
}
