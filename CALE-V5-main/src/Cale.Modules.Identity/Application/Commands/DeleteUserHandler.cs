using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Identity.Application.Abstractions;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class DeleteUserHandler
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;

    public DeleteUserHandler(IUserStore users, ISchoolProfileStore profiles)
    {
        _users = users;
        _profiles = profiles;
    }

    public async Task HandleAsync(
        int actorUserId,
        int targetUserId,
        CancellationToken ct)
    {
        if (actorUserId == targetUserId)
        {
            throw new DomainException(
                "You cannot delete your own account.",
                400,
                "cannot_delete_self");
        }

        var user = await _users.GetByIdAsync(targetUserId, ct)
            ?? throw new NotFoundException("User not found.", "user_not_found");

        var profile = await _profiles.GetTrackedByUserIdAsync(targetUserId, ct);
        if (profile is not null)
        {
            _profiles.Remove(profile);
        }

        _users.Remove(user);
        await _users.SaveChangesAsync(ct);
    }
}
