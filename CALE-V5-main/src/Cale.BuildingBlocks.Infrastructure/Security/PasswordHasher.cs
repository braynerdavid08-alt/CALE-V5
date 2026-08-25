using System.Security.Cryptography;
using Cale.BuildingBlocks.Domain.Security;
using Microsoft.AspNetCore.Identity;

namespace Cale.BuildingBlocks.Infrastructure.Security;

public sealed class PasswordHasher : IPasswordHasher
{
    // v4 stored PBKDF2 as "salt.hash" (SHA256, 100k, 16+32 bytes).
    private const int LegacyIterations = 100_000;
    private readonly PasswordHasher<object> _identity = new();

    public string Hash(string password) =>
        _identity.HashPassword(null!, password);

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        if (IsLegacy(hash))
        {
            return VerifyLegacy(password, hash);
        }

        try
        {
            return _identity.VerifyHashedPassword(null!, hash, password)
                != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool NeedsRehash(string hash) => IsLegacy(hash);

    private static bool IsLegacy(string hash) => hash.Contains('.');

    private static bool VerifyLegacy(string password, string stored)
    {
        var parts = stored.Split('.', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[0]);
            expected = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            LegacyIterations,
            HashAlgorithmName.SHA256,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
