namespace Cale.Modules.Engagement.Domain;

public sealed class NotificationPreference
{
    public int UserId { get; private set; }
    public bool AcademicEnabled { get; private set; } = true;
    public bool MembershipEnabled { get; private set; } = true;
    public bool AdminEnabled { get; private set; } = true;
    public bool SystemEnabled { get; private set; } = true;

    private NotificationPreference()
    {
    }

    public static NotificationPreference Defaults(int userId) => new()
    {
        UserId = userId,
        AcademicEnabled = true,
        MembershipEnabled = true,
        AdminEnabled = true,
        SystemEnabled = true
    };

    public void Update(
        bool academic,
        bool membership,
        bool admin,
        bool system)
    {
        AcademicEnabled = academic;
        MembershipEnabled = membership;
        AdminEnabled = admin;
        SystemEnabled = system;
    }

    public bool AllowsCategory(string category) => category switch
    {
        "academic" => AcademicEnabled,
        "membership" => MembershipEnabled,
        "admin" => AdminEnabled,
        "system" => SystemEnabled,
        _ => true
    };
}
