using System.Net.Mail;
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

    /// <summary>
    /// Stricter check for public self-registration: real mailbox format, public domain.
    /// </summary>
    public static string NormalizeForRegistration(string? value)
    {
        var email = Normalize(value);

        try
        {
            _ = new MailAddress(email);
        }
        catch (FormatException)
        {
            throw new DomainException(
                "Email is not valid.",
                400,
                "invalid_email");
        }

        var at = email.LastIndexOf('@');
        var domain = at >= 0 ? email[(at + 1)..] : "";
        if (string.IsNullOrWhiteSpace(domain)
            || !domain.Contains('.')
            || domain.StartsWith('.')
            || domain.EndsWith('.')
            || domain.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || domain.EndsWith(".test", StringComparison.OrdinalIgnoreCase)
            || domain.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                "Use a real email address (Gmail, Outlook, institutional, etc.).",
                400,
                "email_not_public");
        }

        return email;
    }
}
