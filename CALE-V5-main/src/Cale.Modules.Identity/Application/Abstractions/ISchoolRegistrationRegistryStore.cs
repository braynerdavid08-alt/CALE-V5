using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Abstractions;

public interface ISchoolRegistrationRegistryStore
{
    Task<bool> HasSimilarRegistrationAsync(
        string taxIdKey,
        string billingEmailKey,
        string accessEmailKey,
        string phoneKey,
        string legalNameKey,
        string cityKey,
        CancellationToken ct = default);

    Task RecordAsync(SchoolRegistrationRegistry entry, CancellationToken ct = default);

    Task TouchExistingAsync(
        string taxIdKey,
        string billingEmailKey,
        string accessEmailKey,
        string phoneKey,
        string legalNameKey,
        string cityKey,
        bool freeTrialUsed,
        int userId,
        DateTime utcNow,
        CancellationToken ct = default);
}
