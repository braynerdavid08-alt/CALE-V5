using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Idempotent schema for recurring theory-exam schedules, date overrides, exam booking status/seat columns,
/// manual hours adjustments and the school audit log. Runs even when FeatureSchema is disabled.
/// Mirrors migration <c>AddExamScheduling</c>.
/// </summary>
public static class ExamScheduleSchemaGuard
{
    public const string SeatIndexName = "UX_TheoryExamAppointments_Seat";

    public static readonly string[] PostgresStatements =
    [
        """ALTER TABLE "TheoryExamAppointments" ADD COLUMN IF NOT EXISTS "Status" varchar(16) NOT NULL DEFAULT 'Active';""",
        """ALTER TABLE "TheoryExamAppointments" ADD COLUMN IF NOT EXISTS "SeatNumber" integer NOT NULL DEFAULT 1;""",
        """ALTER TABLE "TheoryExamAppointments" ADD COLUMN IF NOT EXISTS "CancelledAt" timestamp with time zone NULL;""",
        """ALTER TABLE "TheoryExamAppointments" ADD COLUMN IF NOT EXISTS "CancelledByUserId" integer NULL;""",
        """ALTER TABLE "TheoryExamAppointments" ADD COLUMN IF NOT EXISTS "BookedByUserId" integer NULL;""",
        """
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'UX_TheoryExamAppointments_Seat') THEN
                UPDATE "TheoryExamAppointments" t
                SET "SeatNumber" = s.rn
                FROM (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "SchoolUserId", "ExamDate", "SlotTime" ORDER BY "Id") AS rn
                    FROM "TheoryExamAppointments"
                    WHERE "Status" <> 'Cancelled'
                ) s
                WHERE t."Id" = s."Id" AND t."SeatNumber" <> s.rn;
            END IF;
        END $$;
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "UX_TheoryExamAppointments_Seat"
            ON "TheoryExamAppointments" ("SchoolUserId", "ExamDate", "SlotTime", "SeatNumber")
            WHERE "Status" <> 'Cancelled';
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "UX_TheoryExamAppointments_Student"
            ON "TheoryExamAppointments" ("SchoolUserId", "StudentUserId", "ExamDate", "SlotTime")
            WHERE "Status" <> 'Cancelled' AND "StudentUserId" IS NOT NULL;
        """,
        """CREATE INDEX IF NOT EXISTS "IX_TheoryExamAppointments_SchoolUserId_StudentUserId_Status" ON "TheoryExamAppointments" ("SchoolUserId", "StudentUserId", "Status");""",
        """
        CREATE TABLE IF NOT EXISTS "TheoryExamScheduleTemplates" (
            "Id" serial PRIMARY KEY,
            "SchoolUserId" integer NOT NULL,
            "DayOfWeek" integer NOT NULL,
            "StartTime" time without time zone NOT NULL,
            "Capacity" integer NOT NULL DEFAULT 1,
            "IsActive" boolean NOT NULL DEFAULT TRUE,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TheoryExamScheduleTemplates_SchoolUserId_DayOfWeek_StartTime" ON "TheoryExamScheduleTemplates" ("SchoolUserId", "DayOfWeek", "StartTime");""",
        """
        CREATE TABLE IF NOT EXISTS "TheoryExamScheduleOverrides" (
            "Id" serial PRIMARY KEY,
            "SchoolUserId" integer NOT NULL,
            "Date" date NOT NULL,
            "StartTime" time without time zone NOT NULL,
            "IsWholeDay" boolean NOT NULL DEFAULT FALSE,
            "IsClosed" boolean NOT NULL DEFAULT FALSE,
            "Capacity" integer NULL,
            "Note" varchar(200) NULL,
            "CreatedByUserId" integer NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TheoryExamScheduleOverrides_SchoolUserId_Date_StartTime" ON "TheoryExamScheduleOverrides" ("SchoolUserId", "Date", "StartTime");""",
        """
        CREATE TABLE IF NOT EXISTS "TrainingHoursAdjustments" (
            "Id" serial PRIMARY KEY,
            "SchoolUserId" integer NOT NULL,
            "StudentUserId" integer NOT NULL,
            "Category" varchar(16) NOT NULL,
            "DeltaHours" numeric(6,1) NOT NULL,
            "PreviousHours" numeric(6,1) NOT NULL,
            "NewHours" numeric(6,1) NOT NULL,
            "Reason" varchar(300) NOT NULL,
            "PerformedByUserId" integer NULL,
            "CreatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_TrainingHoursAdjustments_SchoolUserId_StudentUserId" ON "TrainingHoursAdjustments" ("SchoolUserId", "StudentUserId");""",
        """
        CREATE TABLE IF NOT EXISTS "SchoolAuditEntries" (
            "Id" bigserial PRIMARY KEY,
            "SchoolUserId" integer NOT NULL,
            "ActorUserId" integer NULL,
            "Area" varchar(32) NOT NULL,
            "Action" varchar(48) NOT NULL,
            "EntityType" varchar(48) NULL,
            "EntityId" integer NULL,
            "StudentUserId" integer NULL,
            "Summary" varchar(400) NULL,
            "OldValue" varchar(200) NULL,
            "NewValue" varchar(200) NULL,
            "Reason" varchar(300) NULL,
            "CreatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_SchoolAuditEntries_SchoolUserId_CreatedAt" ON "SchoolAuditEntries" ("SchoolUserId", "CreatedAt");""",
        """CREATE INDEX IF NOT EXISTS "IX_SchoolAuditEntries_SchoolUserId_StudentUserId" ON "SchoolAuditEntries" ("SchoolUserId", "StudentUserId");"""
    ];

    private static readonly (string Column, string Ddl)[] SqliteAppointmentColumns =
    [
        ("Status", "\"Status\" TEXT NOT NULL DEFAULT 'Active'"),
        ("SeatNumber", "\"SeatNumber\" INTEGER NOT NULL DEFAULT 1"),
        ("CancelledAt", "\"CancelledAt\" TEXT NULL"),
        ("CancelledByUserId", "\"CancelledByUserId\" INTEGER NULL"),
        ("BookedByUserId", "\"BookedByUserId\" INTEGER NULL")
    ];

    private static readonly string[] SqliteStatements =
    [
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "UX_TheoryExamAppointments_Student"
            ON "TheoryExamAppointments" ("SchoolUserId", "StudentUserId", "ExamDate", "SlotTime")
            WHERE "Status" <> 'Cancelled' AND "StudentUserId" IS NOT NULL;
        """,
        """CREATE INDEX IF NOT EXISTS "IX_TheoryExamAppointments_SchoolUserId_StudentUserId_Status" ON "TheoryExamAppointments" ("SchoolUserId", "StudentUserId", "Status");""",
        """
        CREATE TABLE IF NOT EXISTS "TheoryExamScheduleTemplates" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "SchoolUserId" INTEGER NOT NULL,
            "DayOfWeek" INTEGER NOT NULL,
            "StartTime" TEXT NOT NULL,
            "Capacity" INTEGER NOT NULL DEFAULT 1,
            "IsActive" INTEGER NOT NULL DEFAULT 1,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TheoryExamScheduleTemplates_SchoolUserId_DayOfWeek_StartTime" ON "TheoryExamScheduleTemplates" ("SchoolUserId", "DayOfWeek", "StartTime");""",
        """
        CREATE TABLE IF NOT EXISTS "TheoryExamScheduleOverrides" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "SchoolUserId" INTEGER NOT NULL,
            "Date" TEXT NOT NULL,
            "StartTime" TEXT NOT NULL,
            "IsWholeDay" INTEGER NOT NULL DEFAULT 0,
            "IsClosed" INTEGER NOT NULL DEFAULT 0,
            "Capacity" INTEGER NULL,
            "Note" TEXT NULL,
            "CreatedByUserId" INTEGER NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TheoryExamScheduleOverrides_SchoolUserId_Date_StartTime" ON "TheoryExamScheduleOverrides" ("SchoolUserId", "Date", "StartTime");""",
        """
        CREATE TABLE IF NOT EXISTS "TrainingHoursAdjustments" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "SchoolUserId" INTEGER NOT NULL,
            "StudentUserId" INTEGER NOT NULL,
            "Category" TEXT NOT NULL,
            "DeltaHours" TEXT NOT NULL,
            "PreviousHours" TEXT NOT NULL,
            "NewHours" TEXT NOT NULL,
            "Reason" TEXT NOT NULL,
            "PerformedByUserId" INTEGER NULL,
            "CreatedAt" TEXT NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_TrainingHoursAdjustments_SchoolUserId_StudentUserId" ON "TrainingHoursAdjustments" ("SchoolUserId", "StudentUserId");""",
        """
        CREATE TABLE IF NOT EXISTS "SchoolAuditEntries" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "SchoolUserId" INTEGER NOT NULL,
            "ActorUserId" INTEGER NULL,
            "Area" TEXT NOT NULL,
            "Action" TEXT NOT NULL,
            "EntityType" TEXT NULL,
            "EntityId" INTEGER NULL,
            "StudentUserId" INTEGER NULL,
            "Summary" TEXT NULL,
            "OldValue" TEXT NULL,
            "NewValue" TEXT NULL,
            "Reason" TEXT NULL,
            "CreatedAt" TEXT NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_SchoolAuditEntries_SchoolUserId_CreatedAt" ON "SchoolAuditEntries" ("SchoolUserId", "CreatedAt");""",
        """CREATE INDEX IF NOT EXISTS "IX_SchoolAuditEntries_SchoolUserId_StudentUserId" ON "SchoolAuditEntries" ("SchoolUserId", "StudentUserId");"""
    ];

    public static async Task EnsureAsync(CaleDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational())
        {
            return;
        }

        var failures = 0;
        if (db.Database.IsNpgsql())
        {
            foreach (var sql in PostgresStatements)
            {
                failures += await TryExecuteAsync(db, sql, logger, ct);
            }
        }
        else if (db.Database.IsSqlite())
        {
            failures += await EnsureSqliteAppointmentColumnsAsync(db, logger, ct);
            foreach (var sql in SqliteStatements)
            {
                failures += await TryExecuteAsync(db, sql, logger, ct);
            }
        }
        else
        {
            return;
        }

        if (failures == 0)
        {
            logger?.LogInformation("ExamScheduleSchemaGuard applied (exam schedules, overrides, bookings, hours, audit).");
        }
    }

    private static async Task<int> EnsureSqliteAppointmentColumnsAsync(
        CaleDbContext db,
        ILogger? logger,
        CancellationToken ct)
    {
        try
        {
            if (await ScalarAsync(db, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'TheoryExamAppointments';", ct) == 0)
            {
                return 0;
            }

            foreach (var (column, ddl) in SqliteAppointmentColumns)
            {
                var exists = await ScalarAsync(
                    db,
                    $"SELECT COUNT(*) FROM pragma_table_info('TheoryExamAppointments') WHERE name = '{column}';",
                    ct);
                if (exists == 0)
                {
                    // ddl comes from the SqliteAppointmentColumns constant table, never from user input.
                    var sql = "ALTER TABLE \"TheoryExamAppointments\" ADD COLUMN " + ddl + ";";
                    await db.Database.ExecuteSqlRawAsync(sql, ct);
                }
            }

            var hasSeatIndex = await ScalarAsync(
                db,
                $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = '{SeatIndexName}';",
                ct);
            if (hasSeatIndex == 0)
            {
                await db.Database.ExecuteSqlRawAsync(
                    """
                    UPDATE "TheoryExamAppointments"
                    SET "SeatNumber" = (
                        SELECT COUNT(*) FROM "TheoryExamAppointments" b
                        WHERE b."SchoolUserId" = "TheoryExamAppointments"."SchoolUserId"
                          AND b."ExamDate" = "TheoryExamAppointments"."ExamDate"
                          AND b."SlotTime" = "TheoryExamAppointments"."SlotTime"
                          AND b."Status" <> 'Cancelled'
                          AND b."Id" <= "TheoryExamAppointments"."Id")
                    WHERE "Status" <> 'Cancelled';
                    """,
                    ct);
                await db.Database.ExecuteSqlRawAsync(
                    """
                    CREATE UNIQUE INDEX IF NOT EXISTS "UX_TheoryExamAppointments_Seat"
                        ON "TheoryExamAppointments" ("SchoolUserId", "ExamDate", "SlotTime", "SeatNumber")
                        WHERE "Status" <> 'Cancelled';
                    """,
                    ct);
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "ExamScheduleSchemaGuard could not upgrade TheoryExamAppointments (SQLite).");
            return 1;
        }
    }

    private static async Task<int> TryExecuteAsync(CaleDbContext db, string sql, ILogger? logger, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, ct);
            return 0;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "ExamScheduleSchemaGuard statement failed; exam scheduling may be unavailable.");
            return 1;
        }
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
