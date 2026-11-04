using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Identity.Application.Abstractions;

namespace Cale.Modules.Identity.Application.Commands;

internal static class EmailRegistrationGuard
{
    /// <summary>
    /// Registration creates the user before email confirmation. If the address already
    /// exists but is still pending, steer the client to /verify-email instead of a dead end.
    /// </summary>
    public static async Task EnsureAvailableOrPendingAsync(
        IUserStore users,
        string email,
        CancellationToken ct)
    {
        var existing = await users.FindByEmailAsync(email, ct);
        if (existing is null)
        {
            return;
        }

        if (!existing.EmailConfirmed)
        {
            throw new ConflictException(
                "Email pending confirmation.",
                "email_pending_confirmation");
        }

        throw new ConflictException(
            "Email already registered.",
            "email_taken");
    }
}
