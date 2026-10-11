using System.Data.Common;
using System.Reflection;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Admin;

/// <summary>
/// Deletes a school account only when nothing depends on it. Any table with a SchoolId,
/// SchoolUserId or CreatedBySchoolId column pointing at the school (users, courses, decks, game
/// shows, schedules…) blocks the delete, so students are never left attached to a missing school.
/// Pending join requests go with it; membership/payment history is kept for the record.
/// </summary>
public sealed class SchoolDeletionService
{
    private static readonly string[] LinkColumns = ["SchoolId", "SchoolUserId", "CreatedBySchoolId"];

    private static readonly HashSet<Type> OwnedBySchool =
    [
        typeof(SchoolProfile),
        typeof(MembershipEvent),
        typeof(SchoolJoinRequest)
    ];

    private static readonly MethodInfo CountIntMethod = typeof(SchoolDeletionService)
        .GetMethod(nameof(CountIntAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo CountNullableIntMethod = typeof(SchoolDeletionService)
        .GetMethod(nameof(CountNullableIntAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly CaleDbContext _db;
    private readonly DeleteUserHandler _deleteUser;
    private readonly HomepageService _homepage;
    private readonly ILogger<SchoolDeletionService> _logger;

    public SchoolDeletionService(
        CaleDbContext db,
        DeleteUserHandler deleteUser,
        HomepageService homepage,
        ILogger<SchoolDeletionService> logger)
    {
        _db = db;
        _deleteUser = deleteUser;
        _homepage = homepage;
        _logger = logger;
    }

    public Task<bool> IsSchoolAsync(int userId, CancellationToken ct) =>
        _db.Set<User>().AnyAsync(u => u.Id == userId && u.Role == Roles.School, ct);

    /// <summary>Linked users and other records per table, excluding the school's own requests/profile.</summary>
    public async Task<SchoolLinks> CountLinksAsync(int schoolUserId, CancellationToken ct)
    {
        var users = await _db.Set<User>().CountAsync(
            u => u.Id != schoolUserId && (u.SchoolId == schoolUserId || u.CreatedBySchoolId == schoolUserId),
            ct);

        var other = 0;
        foreach (var entityType in _db.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            if (entityType.IsOwned() || entityType.HasSharedClrType || entityType.FindPrimaryKey() is null
                || clr == typeof(User) || OwnedBySchool.Contains(clr))
            {
                continue;
            }

            foreach (var column in LinkColumns)
            {
                var property = entityType.FindProperty(column);
                if (property is null)
                {
                    continue;
                }

                var method = property.ClrType == typeof(int)
                    ? CountIntMethod
                    : property.ClrType == typeof(int?) ? CountNullableIntMethod : null;
                if (method is null)
                {
                    continue;
                }

                try
                {
                    other += await (Task<int>)method.MakeGenericMethod(clr)
                        .Invoke(null, [_db, column, schoolUserId, ct])!;
                }
                catch (DbException ex) when (IsMissingTable(ex))
                {
                    // A feature table missing on this database cannot hold rows for the school.
                    _logger.LogWarning("School link count skipped for {Entity}.{Column}: table missing", clr.Name, column);
                }
            }
        }

        return new SchoolLinks(users, other);
    }

    public async Task DeleteAsync(int actorUserId, int schoolUserId, CancellationToken ct)
    {
        if (!await IsSchoolAsync(schoolUserId, ct))
        {
            throw new NotFoundException("Escuela no encontrada.", "school_not_found");
        }

        var links = await CountLinksAsync(schoolUserId, ct);
        if (links.Total > 0)
        {
            throw new DomainException(
                $"No se puede borrar: la escuela tiene {links.Users} usuario(s) vinculados y {links.Other} "
                + "registro(s) propios (cursos, horarios, presentaciones, concursos…). Suspéndela o mueve primero a sus usuarios.",
                409,
                "school_not_empty");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.Set<SchoolJoinRequest>().Where(r => r.SchoolUserId == schoolUserId).ExecuteDeleteAsync(ct);
        await _deleteUser.HandleAsync(actorUserId, schoolUserId, ct);
        await tx.CommitAsync(ct);

        _homepage.InvalidatePublicCache();
        _logger.LogWarning("Audit: admin {ActorId} deleted empty school {SchoolUserId}", actorUserId, schoolUserId);
    }

    private static bool IsMissingTable(DbException ex) =>
        ex.SqlState == "42P01"
        || ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase);

    private static Task<int> CountIntAsync<TEntity>(CaleDbContext db, string column, int id, CancellationToken ct)
        where TEntity : class =>
        db.Set<TEntity>().CountAsync(e => EF.Property<int>(e, column) == id, ct);

    private static Task<int> CountNullableIntAsync<TEntity>(CaleDbContext db, string column, int id, CancellationToken ct)
        where TEntity : class =>
        db.Set<TEntity>().CountAsync(e => EF.Property<int?>(e, column) == id, ct);
}

public sealed record SchoolLinks(int Users, int Other)
{
    public int Total => Users + Other;
}
