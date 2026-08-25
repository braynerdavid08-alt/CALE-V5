using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;

namespace Cale.Modules.Identity.Application.Queries;

public sealed class GetCurrentUserHandler
{
    private readonly IUserStore _users;

    public GetCurrentUserHandler(IUserStore users) => _users = users;

    public async Task<MeResponse> HandleAsync(int userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found.", "user_not_found");

        return new MeResponse(
            user.Id,
            user.Name,
            user.Email,
            Roles.Normalize(user.Role),
            user.IsActive);
    }
}
