using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Infrastructure;

public sealed class CatalogAccessGuard : ICatalogAccessGuard
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly IClock _clock;

    public CatalogAccessGuard(
        IUserStore users,
        ISchoolProfileStore profiles,
        IClock clock)
    {
        _users = users;
        _profiles = profiles;
        _clock = clock;
    }

    public async Task EnsureCatalogReadAsync(
        int userId,
        string role,
        CancellationToken ct = default)
    {
        role = Roles.Normalize(role);
        if (role == Roles.Admin)
        {
            return;
        }

        if (role == Roles.School)
        {
            await EnsureSchoolMembershipActiveAsync(userId, ct);
            return;
        }

        if (role == Roles.Teacher)
        {
            await EnsureLinkedSchoolMembershipActiveAsync(
                userId,
                "No estás vinculado a una escuela con plan activo. " +
                "Pide a tu escuela que te agregue cuando tenga membresía pagada.",
                ct);
            return;
        }

        throw new ForbiddenException(
            "No tienes acceso al catálogo de preguntas.",
            "catalog_access_denied");
    }

    public async Task EnsureSimulacroAsync(
        int userId,
        string role,
        CancellationToken ct = default)
    {
        role = Roles.Normalize(role);
        if (role == Roles.Admin)
        {
            return;
        }

        if (role == Roles.School)
        {
            await EnsureSchoolMembershipActiveAsync(userId, ct);
            return;
        }

        if (role is Roles.Teacher or Roles.Student)
        {
            await EnsureLinkedSchoolMembershipActiveAsync(
                userId,
                "Debes estar vinculado a una escuela con plan activo para usar simulacros.",
                ct);
            return;
        }

        throw new ForbiddenException(
            "No tienes acceso a simulacros.",
            "simulacro_access_denied");
    }

    private async Task EnsureSchoolMembershipActiveAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var profile = await _profiles.GetByUserIdAsync(schoolUserId, ct);
        if (profile is null || !IsCommerciallyActive(profile))
        {
            throw new ForbiddenException(
                "Tu escuela no tiene un plan activo. Contrata un plan y espera la activación del administrador.",
                "membership_inactive");
        }
    }

    private async Task EnsureLinkedSchoolMembershipActiveAsync(
        int userId,
        string inactiveMessage,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");

        if (user.SchoolId is null)
        {
            throw new ForbiddenException(
                "No estás vinculado a una escuela.",
                "school_not_linked");
        }

        var profile = await _profiles.GetByUserIdAsync(user.SchoolId.Value, ct);
        if (profile is null || !IsCommerciallyActive(profile))
        {
            throw new ForbiddenException(inactiveMessage, "membership_inactive");
        }
    }

    private bool IsCommerciallyActive(SchoolProfile profile)
    {
        profile.RefreshStatus(_clock.UtcNow);
        return profile.IsCommerciallyActive(_clock.UtcNow);
    }
}
