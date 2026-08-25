using System.Security.Claims;
using Cale.BuildingBlocks.Domain.Auth;

namespace Cale.Api.Extensions;

public static class CurrentUser
{
    public static int GetId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(value, out var id))
        {
            throw new UnauthorizedAccessException("Missing user id claim.");
        }

        return id;
    }

    public static string GetRole(ClaimsPrincipal user) =>
        Roles.Normalize(user.FindFirstValue(ClaimTypes.Role));

    public static bool IsAdmin(ClaimsPrincipal user) =>
        GetRole(user) == Roles.Admin;

    public static bool IsStaff(ClaimsPrincipal user) =>
        Roles.IsStaff(GetRole(user));
}
