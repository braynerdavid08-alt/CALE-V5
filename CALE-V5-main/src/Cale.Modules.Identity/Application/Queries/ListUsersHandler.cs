using Cale.BuildingBlocks.Domain.Auth;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;

namespace Cale.Modules.Identity.Application.Queries;

public sealed class ListUsersHandler
{
    private readonly IUserStore _users;

    public ListUsersHandler(IUserStore users) => _users = users;

    public async Task<IReadOnlyList<UserListItemDto>> HandleAsync(
        CancellationToken ct)
    {
        var users = await _users.ListAsync(ct);
        return users
            .Select(user => new UserListItemDto(
                user.Id,
                user.Name,
                user.Email,
                Roles.Normalize(user.Role),
                user.IsActive,
                user.CreatedAt))
            .ToList();
    }
}
