namespace Cale.BuildingBlocks.Domain.Engagement;

public static class NotificationTypes
{
    public const string Announcement = "announcement";
    public const string Material = "material";
    public const string Activity = "activity";
    public const string Exam = "exam";
    public const string ExamResult = "exam_result";
    public const string Grade = "grade";
    public const string Submission = "submission";
    public const string Membership = "membership";
    public const string System = "system";
    public const string Admin = "admin";

    public static string CategoryOf(string type) => type switch
    {
        Announcement or Material or Activity or Exam or ExamResult or Grade or Submission
            => NotificationCategories.Academic,
        Membership => NotificationCategories.Membership,
        Admin => NotificationCategories.Admin,
        _ => NotificationCategories.System
    };
}

public static class NotificationCategories
{
    public const string Academic = "academic";
    public const string Membership = "membership";
    public const string Admin = "admin";
    public const string System = "system";
}

public static class NotificationPriorities
{
    public const string Low = "low";
    public const string Normal = "normal";
    public const string High = "high";
}
