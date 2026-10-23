using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;

namespace Cale.Modules.Identity.Infrastructure;

public sealed class UserLookupService : IUserLookup
{
    private readonly IUserStore _users;

    public UserLookupService(IUserStore users) => _users = users;

    public async Task<string?> GetNameAsync(int userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        return user?.Name;
    }

    public Task<IReadOnlyDictionary<int, string>> GetNamesAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken ct) =>
        _users.GetNamesAsync(userIds, ct);

    public async Task<int?> FindIdByEmailAsync(string email, CancellationToken ct)
    {
        var normalized = EmailAddress.Normalize(email);
        var user = await _users.FindByEmailAsync(normalized, ct);
        return user?.Id;
    }

    public async Task<string?> GetRoleAsync(int userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        return user?.Role;
    }

    public async Task<int?> GetSchoolIdAsync(int userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        return user?.SchoolId;
    }

    public async Task<IReadOnlyList<int>> ListSchoolCatalogOwnerIdsAsync(
        int userId,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return [userId];
        }

        var role = Roles.Normalize(user.Role);
        // School account id is the anchor teachers link via SchoolId.
        var schoolAnchor = role == Roles.School ? user.Id : user.SchoolId;
        if (schoolAnchor is null)
        {
            return [userId];
        }

        var members = await _users.ListBySchoolAsync(schoolAnchor.Value, ct);
        var ids = new HashSet<int> { userId, schoolAnchor.Value };
        foreach (var member in members)
        {
            var memberRole = Roles.Normalize(member.Role);
            if (memberRole is Roles.Teacher or Roles.School)
            {
                ids.Add(member.Id);
            }
        }

        return ids.ToList();
    }

    public Task<int> CountAsync(CancellationToken ct) => _users.CountAsync(ct);
}
