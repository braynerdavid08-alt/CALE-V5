namespace Cale.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Gates access to the admin-owned question catalog and simulacros by paid school plan.
/// </summary>
public interface ICatalogAccessGuard
{
    /// <summary>Admin, active school, or teacher linked to an active school.</summary>
    Task EnsureCatalogReadAsync(int userId, string role, CancellationToken ct = default);

    /// <summary>Admin, active school, or member linked to a school with active plan.</summary>
    Task EnsureSimulacroAsync(int userId, string role, CancellationToken ct = default);
}
