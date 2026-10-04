using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Queries;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class UpdateMyProfileHandler
{
    private readonly IUserStore _users;
    private readonly GetCurrentUserHandler _me;

    public UpdateMyProfileHandler(IUserStore users, GetCurrentUserHandler me)
    {
        _users = users;
        _me = me;
    }

    public async Task<MeResponse> HandleAsync(
        int userId,
        UpdateMyProfileRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("El nombre es obligatorio.", 400, "invalid_name");
        }

        if (request.Name.Trim().Length > 200)
        {
            throw new DomainException(
                "El nombre es demasiado largo.",
                400,
                "invalid_name");
        }

        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");

        if (!string.IsNullOrWhiteSpace(request.Email)
            && !string.Equals(EmailAddress.Normalize(request.Email), user.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                "El correo de acceso no se puede cambiar.",
                400,
                "email_change_disabled");
        }

        user.UpdateProfile(request.Name.Trim(), user.Email);
        await _users.SaveChangesAsync(ct);
        return await _me.HandleAsync(userId, ct);
    }

    /// <summary>Sets or clears the profile photo; returns the previous URL so its blob can be removed.</summary>
    public async Task<(MeResponse Me, string? PreviousUrl)> SetPhotoAsync(
        int userId,
        string? photoUrl,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");

        var previous = user.PhotoUrl;
        user.SetPhoto(photoUrl);
        await _users.SaveChangesAsync(ct);
        return (await _me.HandleAsync(userId, ct), previous);
    }
}
