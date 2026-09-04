using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Identity.Application.Abstractions;

namespace Cale.Modules.Identity.Infrastructure;

public sealed class SchoolMembershipGuard : ISchoolMembershipGuard
{
    private readonly ISchoolProfileStore _profiles;
    private readonly IClock _clock;

    public SchoolMembershipGuard(ISchoolProfileStore profiles, IClock clock)
    {
        _profiles = profiles;
        _clock = clock;
    }

    public async Task EnsureActiveAsync(int schoolUserId, CancellationToken ct = default)
    {
        var profile = await _profiles.GetByUserIdAsync(schoolUserId, ct);
        if (profile is null || !profile.IsCommerciallyActive(_clock.UtcNow))
        {
            throw new DomainException(
                "La membresía de la escuela no está activa.",
                400,
                "membership_inactive");
        }
    }
}
