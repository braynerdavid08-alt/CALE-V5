using Cale.BuildingBlocks.Domain.Engagement;
using Cale.Modules.Engagement.Application;
using Cale.Modules.Engagement.Domain;

namespace Cale.UnitTests;

public sealed class NotificationDomainTests
{
    [Fact]
    public void Create_and_mark_read_sets_ReadAt()
    {
        var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
        var n = AppNotification.Create(
            7,
            "Título",
            "Mensaje",
            NotificationTypes.Exam,
            3,
            "exam",
            9,
            now,
            "/student/group/3",
            NotificationPriorities.High,
            "exam:9:user:7");

        Assert.False(n.IsRead);
        Assert.Null(n.ReadAt);

        var readAt = now.AddMinutes(2);
        n.MarkRead(readAt);

        Assert.True(n.IsRead);
        Assert.Equal(readAt, n.ReadAt);
        n.MarkRead(now.AddHours(1));
        Assert.Equal(readAt, n.ReadAt);
    }

    [Fact]
    public void Archive_hides_from_active_inbox()
    {
        var n = AppNotification.Create(
            1, "a", "b", NotificationTypes.Admin, null, null, null,
            DateTime.UtcNow);
        Assert.False(n.IsArchived);
        n.Archive();
        Assert.True(n.IsArchived);
    }

    [Fact]
    public void Link_resolver_uses_real_routes()
    {
        Assert.Equal(
            "/student/group/5",
            NotificationLinkResolver.Resolve(
                NotificationTypes.Exam, 5, "exam", 1, null));
        Assert.Equal(
            "/teacher/groups/5",
            NotificationLinkResolver.Resolve(
                NotificationTypes.Submission, 5, "activity", 1, null));
        Assert.Equal(
            "/admin/memberships",
            NotificationLinkResolver.Resolve(
                NotificationTypes.Membership, null, "admin_review", 2, null));
        Assert.Equal(
            "/school/membership",
            NotificationLinkResolver.Resolve(
                NotificationTypes.Membership, null, "membership", 2, null));
        Assert.Null(
            NotificationLinkResolver.Resolve(
                NotificationTypes.Admin, null, null, null, "https://evil.example"));
        Assert.Equal(
            "/student",
            NotificationLinkResolver.Resolve(
                NotificationTypes.System, null, null, null, "/student"));
    }

    [Fact]
    public void Preference_defaults_allow_all_categories()
    {
        var p = NotificationPreference.Defaults(10);
        Assert.True(p.AllowsCategory(NotificationCategories.Academic));
        p.Update(academic: false, membership: true, admin: true, system: true);
        Assert.False(p.AllowsCategory(NotificationCategories.Academic));
        Assert.True(p.AllowsCategory(NotificationCategories.Membership));
    }

    [Fact]
    public void CategoryOf_maps_known_types()
    {
        Assert.Equal(
            NotificationCategories.Academic,
            NotificationTypes.CategoryOf(NotificationTypes.ExamResult));
        Assert.Equal(
            NotificationCategories.Membership,
            NotificationTypes.CategoryOf(NotificationTypes.Membership));
        Assert.Equal(
            NotificationCategories.Admin,
            NotificationTypes.CategoryOf(NotificationTypes.Admin));
    }
}
