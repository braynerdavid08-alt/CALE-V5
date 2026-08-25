using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.BuildingBlocks.Domain.Validation;

public static class EmailAddress
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@'))
        {
            throw new DomainException(
                "Email is not valid.",
                400,
                "invalid_email");
        }

        return value.Trim().ToLowerInvariant();
    }
}
