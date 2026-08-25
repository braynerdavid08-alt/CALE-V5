using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class SetUserActiveHandler
{
    private readonly IUserStore _users;

    public SetUserActiveHandler(IUserStore users) => _users = users;

    public async Task<UserListItemDto> HandleAsync(
        int actorUserId,
        int targetUserId,
        SetUserActiveRequest request,
        CancellationToken ct)
    {
        if (actorUserId == targetUserId && !request.IsActive)
        {
            throw new DomainException(
                "You cannot deactivate your own account.",
                400,
                "cannot_deactivate_self");
        }

        var user = await _users.GetByIdAsync(targetUserId, ct)
            ?? throw new NotFoundException("User not found.", "user_not_found");

        if (request.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await _users.SaveChangesAsync(ct);

        return new UserListItemDto(
            user.Id,
            user.Name,
            user.Email,
            Roles.Normalize(user.Role),
            user.IsActive,
            user.CreatedAt);
    }
}
