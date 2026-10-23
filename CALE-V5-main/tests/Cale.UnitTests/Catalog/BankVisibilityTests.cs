using Cale.Modules.Catalog.Domain;

namespace Cale.UnitTests.Catalog;

public sealed class BankVisibilityTests
{
    [Fact]
    public void Official_banks_are_visible_to_any_teacher()
    {
        var bank = Bank.Create("Oficial", null, DateTime.UtcNow, createdById: null);
        Assert.True(bank.IsVisibleTo(42, isAdmin: false));
        Assert.True(bank.IsVisibleTo(99, isAdmin: false, schoolOwnerIds: [99, 7]));
    }

    [Fact]
    public void Private_bank_is_visible_to_school_peers()
    {
        var bank = Bank.Create("Importado Word", null, DateTime.UtcNow, createdById: 7);
        Assert.False(bank.IsVisibleTo(42, isAdmin: false));
        Assert.True(bank.IsVisibleTo(42, isAdmin: false, schoolOwnerIds: [42, 7, 3]));
        Assert.True(bank.IsVisibleTo(7, isAdmin: false));
        Assert.True(bank.IsVisibleTo(1, isAdmin: true));
    }
}
