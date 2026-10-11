using System.Data.Common;
using System.Reflection;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Domain;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Admin;

/// <summary>
/// Deletes a school account only when nothing depends on it. Any table with a SchoolId,
/// SchoolUserId or CreatedBySchoolId column pointing at the school (users, courses, decks, game
/// shows, schedules…) blocks the delete, so students are never left attached to a missing school.
/// Pending join requests and auto-created settings go with it; membership/payment history and
/// the school audit log are kept for the record.
/// </summary>
public sealed class SchoolDeletionService
{
    private static readonly string[] LinkColumns = ["SchoolId", "SchoolUserId", "CreatedBySchoolId"];

    private static readonly HashSet<Type> OwnedBySchool =
    [
        typeof(SchoolProfile),
        typeof(MembershipEvent),
        typeof(SchoolJoinRequest),
        typeof(TheoryTrainingSettings),
        typeof(SchoolAuditEntry)
    ];

    private static readonly Dictionary<string, string> FriendlyNames = new()
    {
        [nameof(Cale.Modules.Courses.Domain.Course)] = "cursos",
        [nameof(Cale.Modules.Presentation.Domain.PresentationDeck)] = "presentaciones",
        [nameof(TheoryClassroom)] = "salones",
        [nameof(TheoryClassSession)] = "clases teóricas",
        [nameof(SchoolStudentEnrollment)] = "matrículas",
        [nameof(SchoolApprenticeProfile)] = "fichas de aprendices",
        [nameof(PracticalVehicle)] = "vehículos",
        [nameof(PracticalLessonSession)] = "clases prácticas",
        [nameof(TheoryExamScheduleTemplate)] = "horarios de examen",
        [nameof(TheoryExamScheduleOverride)] = "excepciones de horario",
        [nameof(TheoryExamAppointment)] = "citas de examen",
    };

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

        var other = new Dictionary<string, int>();
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
                    var count = await (Task<int>)method.MakeGenericMethod(clr)
                        .Invoke(null, [_db, column, schoolUserId, ct])!;
                    if (count > 0)
                    {
                        var label = FriendlyNames.GetValueOrDefault(clr.Name, clr.Name);
                        other[label] = other.GetValueOrDefault(label) + count;
                    }
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
            var parts = new List<string>();
            if (links.Users > 0)
            {
                parts.Add($"{links.Users} usuario(s) vinculados");
            }

            parts.AddRange(links.Other.OrderBy(x => x.Key).Select(x => $"{x.Value} {x.Key}"));
            throw new DomainException(
                $"No se puede borrar: la escuela tiene {string.Join(", ", parts)}. "
                + "Suspéndela o mueve primero sus usuarios y contenido.",
                409,
                "school_not_empty");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.Set<SchoolJoinRequest>().Where(r => r.SchoolUserId == schoolUserId).ExecuteDeleteAsync(ct);
        await DeleteSettingsAsync(schoolUserId, ct);
        await _deleteUser.HandleAsync(actorUserId, schoolUserId, ct);
        await tx.CommitAsync(ct);

        _homepage.InvalidatePublicCache();
        _logger.LogWarning("Audit: admin {ActorId} deleted empty school {SchoolUserId}", actorUserId, schoolUserId);
    }

    private async Task DeleteSettingsAsync(int schoolUserId, CancellationToken ct)
    {
        try
        {
            await _db.Set<TheoryTrainingSettings>().Where(s => s.SchoolUserId == schoolUserId).ExecuteDeleteAsync(ct);
        }
        catch (DbException ex) when (IsMissingTable(ex))
        {
            _logger.LogWarning("School settings delete skipped: table missing");
        }
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

public sealed record SchoolLinks(int Users, IReadOnlyDictionary<string, int> Other)
{
    public int Total => Users + Other.Values.Sum();
}
