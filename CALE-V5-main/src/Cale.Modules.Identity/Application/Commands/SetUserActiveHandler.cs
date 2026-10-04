using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class SetUserActiveHandler
{
    private readonly IUserStore _users;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly ILogger<SetUserActiveHandler> _logger;

    public SetUserActiveHandler(
        IUserStore users,
        IRefreshTokenStore refreshTokens,
        ILogger<SetUserActiveHandler> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _logger = logger;
    }

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
        if (!request.IsActive)
        {
            await _refreshTokens.RevokeAllForUserAsync(user.Id, ct);
        }

        _logger.LogWarning(
            "Audit: admin {ActorId} set user {TargetId} active={Active}",
            actorUserId,
            user.Id,
            request.IsActive);

        return new UserListItemDto(
            user.Id,
            user.Name,
            user.Email,
            Roles.Normalize(user.Role),
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt);
    }
}
