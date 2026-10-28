using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Ensures SchedulingReportRuns / SchedulingReportSnapshots exist (same style as shop/legal guards).
/// </summary>
public static class SchedulingReportsSchemaGuard
{
    public static async Task EnsureAsync(DbContext db, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "SchedulingReportRuns" (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "SchoolUserId" integer NOT NULL,
                    "Type" character varying(32) NOT NULL,
                    "TargetDate" date NOT NULL,
                    "Status" character varying(32) NOT NULL,
                    "FileName" character varying(256) NOT NULL,
                    "ByteLength" integer NOT NULL DEFAULT 0,
                    "Error" character varying(1024) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL
                );
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_SchedulingReportRuns_School_Date_Type"
                    ON "SchedulingReportRuns" ("SchoolUserId", "TargetDate", "Type", "CreatedAt");
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "SchedulingReportSnapshots" (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "SchoolUserId" integer NOT NULL,
                    "CutKind" character varying(32) NOT NULL,
                    "TargetDate" date NOT NULL,
                    "PayloadJson" text NOT NULL,
                    "PayloadHash" character varying(64) NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL
                );
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SchedulingReportSnapshots_School_Date_Cut"
                    ON "SchedulingReportSnapshots" ("SchoolUserId", "TargetDate", "CutKind");
                """,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SchedulingReportsSchemaGuard could not ensure tables (non-fatal).");
        }
    }
}
