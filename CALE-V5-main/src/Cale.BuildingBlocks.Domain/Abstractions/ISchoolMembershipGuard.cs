namespace Cale.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Ensures a school account has an active commercial membership before write operations.
/// </summary>
public interface ISchoolMembershipGuard
{
    Task EnsureActiveAsync(int schoolUserId, CancellationToken ct = default);
}
