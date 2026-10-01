using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.TheoreticalTraining.Domain;

namespace Cale.Modules.TheoreticalTraining.Application;

/// <summary>Adds a <see cref="SchoolAuditEntry"/> to the current unit of work (saved with the change it describes).</summary>
public static class SchoolAudit
{
    public static void Add(
        CaleDbContext db,
        DateTime nowUtc,
        int schoolUserId,
        int? actorUserId,
        string area,
        string action,
        string summary,
        string? entityType = null,
        int? entityId = null,
        int? studentUserId = null,
        string? oldValue = null,
        string? newValue = null,
        string? reason = null)
    {
        db.Set<SchoolAuditEntry>().Add(new SchoolAuditEntry
        {
            SchoolUserId = schoolUserId,
            ActorUserId = actorUserId,
            Area = area,
            Action = action,
            Summary = Truncate(summary, 400),
            EntityType = entityType,
            EntityId = entityId,
            StudentUserId = studentUserId,
            OldValue = Truncate(oldValue, 200),
            NewValue = Truncate(newValue, 200),
            Reason = Truncate(reason, 300),
            CreatedAt = nowUtc
        });
    }

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
