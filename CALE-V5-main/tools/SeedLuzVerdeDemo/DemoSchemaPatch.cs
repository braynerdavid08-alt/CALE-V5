using System.Data;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SeedLuzVerdeDemo;

/// <summary>
/// Lightweight SQLite column patches for local DBs created before recent model fields.
/// Only adds missing nullable/defaulted columns; never drops data.
/// </summary>
internal static class DemoSchemaPatch
{
    public static async Task EnsureAsync(CaleDbContext db, CancellationToken ct = default)
    {
        if (!db.Database.IsSqlite())
        {
            return;
        }

        await EnsureColumnAsync(db, "Usuarios", "FotoUrl", "TEXT NULL", ct);
        await EnsureColumnAsync(db, "Usuarios", "EscuelaCreadoraId", "INTEGER NULL", ct);
        await EnsureColumnAsync(db, "SchoolProfiles", "PaymentAdminStatus", "TEXT NOT NULL DEFAULT 'None'", ct);
        await EnsureColumnAsync(db, "SchoolProfiles", "CourtesyUntil", "TEXT NULL", ct);
        await EnsureColumnAsync(db, "SchoolProfiles", "CourtesyReason", "TEXT NULL", ct);
        await EnsureColumnAsync(db, "SchoolProfiles", "AgreedPriceCop", "TEXT NULL", ct);
        await EnsureColumnAsync(db, "SchoolProfiles", "PendingApprovalSince", "TEXT NULL", ct);
        await EnsureColumnAsync(db, "PracticalLessonSessions", "Kind", "TEXT NOT NULL DEFAULT 'Lesson'", ct);
        await EnsureColumnAsync(db, "TheoryExamAppointments", "Status", "TEXT NOT NULL DEFAULT 'Active'", ct);
        await EnsureColumnAsync(db, "TheoryExamAppointments", "SeatNumber", "INTEGER NOT NULL DEFAULT 1", ct);
    }

    private static async Task EnsureColumnAsync(
        CaleDbContext db,
        string table,
        string column,
        string sqlType,
        CancellationToken ct)
    {
        if (await ScalarAsync(db, $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'", ct) == 0)
        {
            return;
        }

        var exists = await ScalarAsync(
            db,
            $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{column}'",
            ct);
        if (exists > 0)
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync(
            $"""ALTER TABLE "{table}" ADD COLUMN "{column}" {sqlType};""",
            ct);
    }

    private static async Task<long> ScalarAsync(CaleDbContext db, string sql, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt64(result ?? 0);
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }
}
