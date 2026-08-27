namespace Cale.Modules.Identity.Domain;

/// <summary>
/// Permanent fingerprint of school sign-ups. Never deleted — blocks repeat free trials.
/// </summary>
public sealed class SchoolRegistrationRegistry
{
    public int Id { get; private set; }
    public string TaxIdKey { get; private set; } = "";
    public string BillingEmailKey { get; private set; } = "";
    public string AccessEmailKey { get; private set; } = "";
    public string PhoneKey { get; private set; } = "";
    public string LegalNameKey { get; private set; } = "";
    public string CityKey { get; private set; } = "";
    public bool FreeTrialUsed { get; private set; }
    public DateTime FirstRegisteredAt { get; private set; }
    public DateTime LastRegisteredAt { get; private set; }
    public int? LastUserId { get; private set; }

    private SchoolRegistrationRegistry()
    {
    }

    public static SchoolRegistrationRegistry Create(
        string taxIdKey,
        string billingEmailKey,
        string accessEmailKey,
        string phoneKey,
        string legalNameKey,
        string cityKey,
        bool freeTrialUsed,
        int userId,
        DateTime utcNow) =>
        new()
        {
            TaxIdKey = taxIdKey,
            BillingEmailKey = billingEmailKey,
            AccessEmailKey = accessEmailKey,
            PhoneKey = phoneKey,
            LegalNameKey = legalNameKey,
            CityKey = cityKey,
            FreeTrialUsed = freeTrialUsed,
            FirstRegisteredAt = utcNow,
            LastRegisteredAt = utcNow,
            LastUserId = userId
        };

    public void Touch(int userId, bool freeTrialUsed, DateTime utcNow)
    {
        LastRegisteredAt = utcNow;
        LastUserId = userId;
        if (freeTrialUsed)
        {
            FreeTrialUsed = true;
        }
    }
}
