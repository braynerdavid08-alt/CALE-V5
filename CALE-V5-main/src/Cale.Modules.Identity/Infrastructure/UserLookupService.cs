using Cale.BuildingBlocks.Domain.Abstractions;
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

    public Task<int> CountAsync(CancellationToken ct) => _users.CountAsync(ct);
}
