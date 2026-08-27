using Cale.BuildingBlocks.Domain.Engagement;

namespace Cale.Modules.Engagement.Application;

public static class NotificationLinkResolver
{
    public static string? Resolve(
        string type,
        int? groupId,
        string? relatedEntity,
        int? relatedId,
        string? explicitLink)
    {
        if (!string.IsNullOrWhiteSpace(explicitLink))
        {
            return Normalize(explicitLink);
        }

        return type switch
        {
            NotificationTypes.Submission when groupId is > 0 =>
                $"/teacher/groups/{groupId.Value}",
            NotificationTypes.Announcement or NotificationTypes.Material
                or NotificationTypes.Activity or NotificationTypes.Exam
                or NotificationTypes.Grade when groupId is > 0 =>
                $"/student/group/{groupId.Value}",
            NotificationTypes.ExamResult => "/student",
            NotificationTypes.Membership when relatedEntity == "admin_review" =>
                "/admin/memberships",
            NotificationTypes.Membership when relatedEntity == "school_join_request" =>
                "/school/users",
            NotificationTypes.Membership => "/school/membership",
            NotificationTypes.Admin or NotificationTypes.System => null,
            _ when groupId is > 0 => $"/student/group/{groupId.Value}",
            _ => null
        };
    }

    private static string? Normalize(string link)
    {
        var value = link.Trim();
        if (value.Length == 0)
        {
            return null;
        }

        // Only allow in-app relative paths — never open arbitrary URLs.
        if (!value.StartsWith('/') || value.StartsWith("//"))
        {
            return null;
        }

        return value;
    }
}
