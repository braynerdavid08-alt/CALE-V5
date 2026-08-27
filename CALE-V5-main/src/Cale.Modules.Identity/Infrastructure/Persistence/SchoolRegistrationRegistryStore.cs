using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class SchoolRegistrationRegistryStore : ISchoolRegistrationRegistryStore
{
    private readonly CaleDbContext _db;

    public SchoolRegistrationRegistryStore(CaleDbContext db) => _db = db;

    public async Task<bool> HasSimilarRegistrationAsync(
        string taxIdKey,
        string billingEmailKey,
        string accessEmailKey,
        string phoneKey,
        string legalNameKey,
        string cityKey,
        CancellationToken ct = default)
    {
        var set = _db.Set<SchoolRegistrationRegistry>().AsNoTracking();
        if (!string.IsNullOrEmpty(taxIdKey)
            && await set.AnyAsync(x => x.TaxIdKey == taxIdKey, ct))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(billingEmailKey)
            && await set.AnyAsync(x => x.BillingEmailKey == billingEmailKey, ct))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(accessEmailKey)
            && await set.AnyAsync(x => x.AccessEmailKey == accessEmailKey, ct))
        {
            return true;
        }

        if (phoneKey.Length >= 7
            && await set.AnyAsync(x => x.PhoneKey == phoneKey, ct))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(legalNameKey)
            && !string.IsNullOrEmpty(cityKey)
            && await set.AnyAsync(
                x => x.LegalNameKey == legalNameKey && x.CityKey == cityKey,
                ct))
        {
            return true;
        }

        return false;
    }

    public async Task RecordAsync(
        SchoolRegistrationRegistry entry,
        CancellationToken ct = default)
    {
        await _db.Set<SchoolRegistrationRegistry>().AddAsync(entry, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task TouchExistingAsync(
        string taxIdKey,
        string billingEmailKey,
        string accessEmailKey,
        string phoneKey,
        string legalNameKey,
        string cityKey,
        bool freeTrialUsed,
        int userId,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        var set = _db.Set<SchoolRegistrationRegistry>();
        var existing = await set.FirstOrDefaultAsync(
            x => x.TaxIdKey == taxIdKey
                || x.BillingEmailKey == billingEmailKey
                || x.AccessEmailKey == accessEmailKey,
            ct);

        if (existing is null)
        {
            await RecordAsync(
                SchoolRegistrationRegistry.Create(
                    taxIdKey,
                    billingEmailKey,
                    accessEmailKey,
                    phoneKey,
                    legalNameKey,
                    cityKey,
                    freeTrialUsed,
                    userId,
                    utcNow),
                ct);
            return;
        }

        existing.Touch(userId, freeTrialUsed, utcNow);
        await _db.SaveChangesAsync(ct);
    }
}
