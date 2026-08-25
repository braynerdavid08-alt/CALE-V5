using Cale.BuildingBlocks.Infrastructure.Security;

namespace Cale.UnitTests;

public class PasswordHasherTests
{
    private const string LegacyAdminHash =
        "afPYtigqOhHQDj3ZVQC1sw==.funFjV1mernSMi4XjQPGZaLZtIJvOl3ms3pCpGuFNGE=";

    [Fact]
    public void Hash_And_Verify_RoundTrip()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("Secret123!");
        Assert.True(hasher.Verify("Secret123!", hash));
        Assert.False(hasher.Verify("Wrong", hash));
        Assert.False(hasher.NeedsRehash(hash));
    }

    [Fact]
    public void Verify_LegacyPbkdf2_FromV4()
    {
        var hasher = new PasswordHasher();
        Assert.True(hasher.Verify("Admin123!", LegacyAdminHash));
        Assert.False(hasher.Verify("WrongPass1", LegacyAdminHash));
        Assert.True(hasher.NeedsRehash(LegacyAdminHash));
    }

    [Fact]
    public void Verify_InvalidBase64_ReturnsFalse()
    {
        var hasher = new PasswordHasher();
        Assert.False(hasher.Verify("Admin123!", "not-a-valid-hash!!!"));
    }
}
