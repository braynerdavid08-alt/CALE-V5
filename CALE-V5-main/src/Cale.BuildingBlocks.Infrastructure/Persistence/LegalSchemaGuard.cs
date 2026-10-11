using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Idempotent creation of the legal document versions and acceptance evidence tables.
/// Runs even when FeatureSchema is disabled.
/// </summary>
public static class LegalSchemaGuard
{
    private static readonly string[] PostgresStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "LegalVersionesDocumento" (
            "Id" serial PRIMARY KEY,
            "DocumentKey" varchar(40) NOT NULL,
            "Version" varchar(40) NOT NULL,
            "Title" varchar(200) NOT NULL,
            "Content" text NOT NULL,
            "ContentHash" varchar(64) NOT NULL,
            "Status" varchar(20) NOT NULL,
            "RequiresReacceptance" boolean NOT NULL DEFAULT true,
            "ChangeSummary" varchar(500) NULL,
            "CreatedByUserId" integer NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "PublishedByUserId" integer NULL,
            "PublishedAt" timestamp with time zone NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_LegalVersionesDocumento_DocumentKey_Version" ON "LegalVersionesDocumento" ("DocumentKey", "Version");""",
        """
        CREATE TABLE IF NOT EXISTS "LegalAceptaciones" (
            "Id" serial PRIMARY KEY,
            "UserId" integer NOT NULL,
            "DocumentKey" varchar(40) NOT NULL,
            "DocumentVersionId" integer NOT NULL,
            "Version" varchar(40) NOT NULL,
            "ContentHash" varchar(64) NOT NULL,
            "Action" varchar(20) NOT NULL,
            "Method" varchar(20) NOT NULL,
            "IpAddress" varchar(64) NULL,
            "UserAgent" varchar(300) NULL,
            "CreatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_LegalAceptaciones_UserId_DocumentKey" ON "LegalAceptaciones" ("UserId", "DocumentKey");""",
        """CREATE INDEX IF NOT EXISTS "IX_LegalAceptaciones_DocumentVersionId" ON "LegalAceptaciones" ("DocumentVersionId");""",
        """
        CREATE TABLE IF NOT EXISTS "DeclaracionesEdad" (
            "Id" serial PRIMARY KEY,
            "UserId" integer NOT NULL,
            "BirthDate" date NOT NULL,
            "IsMinorAtDeclaration" boolean NOT NULL,
            "GuardianName" varchar(150) NULL,
            "GuardianRelationship" varchar(30) NULL,
            "GuardianEmail" varchar(254) NULL,
            "GuardianStatus" varchar(20) NOT NULL,
            "GuardianTokenHash" varchar(64) NULL,
            "GuardianLinkExpiresAt" timestamp with time zone NULL,
            "GuardianDecidedAt" timestamp with time zone NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_DeclaracionesEdad_UserId" ON "DeclaracionesEdad" ("UserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_DeclaracionesEdad_GuardianTokenHash" ON "DeclaracionesEdad" ("GuardianTokenHash");""",
        """
        CREATE TABLE IF NOT EXISTS "AutorizacionesMenoresEventos" (
            "Id" serial PRIMARY KEY,
            "AgeDeclarationId" integer NOT NULL,
            "UserId" integer NOT NULL,
            "Action" varchar(30) NOT NULL,
            "DocumentVersions" varchar(300) NULL,
            "IpAddress" varchar(64) NULL,
            "UserAgent" varchar(300) NULL,
            "Note" varchar(300) NULL,
            "CreatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_AutorizacionesMenoresEventos_UserId" ON "AutorizacionesMenoresEventos" ("UserId");""",
        """
        CREATE TABLE IF NOT EXISTS "SolicitudesPqr" (
            "Id" serial PRIMARY KEY,
            "Kind" varchar(30) NOT NULL,
            "Status" varchar(20) NOT NULL,
            "UserId" integer NULL,
            "ContactName" varchar(150) NOT NULL,
            "ContactEmail" varchar(254) NOT NULL,
            "Subject" varchar(200) NOT NULL,
            "Message" varchar(4000) NOT NULL,
            "Response" varchar(4000) NULL,
            "TrackingCodeHash" varchar(64) NOT NULL,
            "DueAt" timestamp with time zone NOT NULL,
            "Extended" boolean NOT NULL DEFAULT false,
            "AssignedAdminId" integer NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL,
            "ClosedAt" timestamp with time zone NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_SolicitudesPqr_UserId" ON "SolicitudesPqr" ("UserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_SolicitudesPqr_TrackingCodeHash" ON "SolicitudesPqr" ("TrackingCodeHash");""",
        """CREATE INDEX IF NOT EXISTS "IX_SolicitudesPqr_Status" ON "SolicitudesPqr" ("Status");""",
        """
        CREATE TABLE IF NOT EXISTS "SolicitudesPqrEventos" (
            "Id" serial PRIMARY KEY,
            "PqrRequestId" integer NOT NULL,
            "Action" varchar(30) NOT NULL,
            "FromStatus" varchar(20) NULL,
            "ToStatus" varchar(20) NULL,
            "Note" varchar(4000) NULL,
            "ActorUserId" integer NULL,
            "VisibleToRequester" boolean NOT NULL DEFAULT true,
            "CreatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_SolicitudesPqrEventos_PqrRequestId" ON "SolicitudesPqrEventos" ("PqrRequestId");"""
    ];

    private static readonly string[] SqliteStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "LegalVersionesDocumento" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "DocumentKey" TEXT NOT NULL,
            "Version" TEXT NOT NULL,
            "Title" TEXT NOT NULL,
            "Content" TEXT NOT NULL,
            "ContentHash" TEXT NOT NULL,
            "Status" TEXT NOT NULL,
            "RequiresReacceptance" INTEGER NOT NULL DEFAULT 1,
            "ChangeSummary" TEXT NULL,
            "CreatedByUserId" INTEGER NULL,
            "CreatedAt" TEXT NOT NULL,
            "PublishedByUserId" INTEGER NULL,
            "PublishedAt" TEXT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_LegalVersionesDocumento_DocumentKey_Version" ON "LegalVersionesDocumento" ("DocumentKey", "Version");""",
        """
        CREATE TABLE IF NOT EXISTS "LegalAceptaciones" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "UserId" INTEGER NOT NULL,
            "DocumentKey" TEXT NOT NULL,
            "DocumentVersionId" INTEGER NOT NULL,
            "Version" TEXT NOT NULL,
            "ContentHash" TEXT NOT NULL,
            "Action" TEXT NOT NULL,
            "Method" TEXT NOT NULL,
            "IpAddress" TEXT NULL,
            "UserAgent" TEXT NULL,
            "CreatedAt" TEXT NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_LegalAceptaciones_UserId_DocumentKey" ON "LegalAceptaciones" ("UserId", "DocumentKey");""",
        """CREATE INDEX IF NOT EXISTS "IX_LegalAceptaciones_DocumentVersionId" ON "LegalAceptaciones" ("DocumentVersionId");""",
        """
        CREATE TABLE IF NOT EXISTS "DeclaracionesEdad" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "UserId" INTEGER NOT NULL,
            "BirthDate" TEXT NOT NULL,
            "IsMinorAtDeclaration" INTEGER NOT NULL,
            "GuardianName" TEXT NULL,
            "GuardianRelationship" TEXT NULL,
            "GuardianEmail" TEXT NULL,
            "GuardianStatus" TEXT NOT NULL,
            "GuardianTokenHash" TEXT NULL,
            "GuardianLinkExpiresAt" TEXT NULL,
            "GuardianDecidedAt" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_DeclaracionesEdad_UserId" ON "DeclaracionesEdad" ("UserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_DeclaracionesEdad_GuardianTokenHash" ON "DeclaracionesEdad" ("GuardianTokenHash");""",
        """
        CREATE TABLE IF NOT EXISTS "AutorizacionesMenoresEventos" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "AgeDeclarationId" INTEGER NOT NULL,
            "UserId" INTEGER NOT NULL,
            "Action" TEXT NOT NULL,
            "DocumentVersions" TEXT NULL,
            "IpAddress" TEXT NULL,
            "UserAgent" TEXT NULL,
            "Note" TEXT NULL,
            "CreatedAt" TEXT NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_AutorizacionesMenoresEventos_UserId" ON "AutorizacionesMenoresEventos" ("UserId");""",
        """
        CREATE TABLE IF NOT EXISTS "SolicitudesPqr" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "Kind" TEXT NOT NULL,
            "Status" TEXT NOT NULL,
            "UserId" INTEGER NULL,
            "ContactName" TEXT NOT NULL,
            "ContactEmail" TEXT NOT NULL,
            "Subject" TEXT NOT NULL,
            "Message" TEXT NOT NULL,
            "Response" TEXT NULL,
            "TrackingCodeHash" TEXT NOT NULL,
            "DueAt" TEXT NOT NULL,
            "Extended" INTEGER NOT NULL DEFAULT 0,
            "AssignedAdminId" INTEGER NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL,
            "ClosedAt" TEXT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_SolicitudesPqr_UserId" ON "SolicitudesPqr" ("UserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_SolicitudesPqr_TrackingCodeHash" ON "SolicitudesPqr" ("TrackingCodeHash");""",
        """CREATE INDEX IF NOT EXISTS "IX_SolicitudesPqr_Status" ON "SolicitudesPqr" ("Status");""",
        """
        CREATE TABLE IF NOT EXISTS "SolicitudesPqrEventos" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "PqrRequestId" INTEGER NOT NULL,
            "Action" TEXT NOT NULL,
            "FromStatus" TEXT NULL,
            "ToStatus" TEXT NULL,
            "Note" TEXT NULL,
            "ActorUserId" INTEGER NULL,
            "VisibleToRequester" INTEGER NOT NULL DEFAULT 1,
            "CreatedAt" TEXT NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_SolicitudesPqrEventos_PqrRequestId" ON "SolicitudesPqrEventos" ("PqrRequestId");"""
    ];

    public static async Task EnsureAsync(CaleDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational())
        {
            return;
        }

        string[] statements;
        if (db.Database.IsNpgsql())
        {
            statements = PostgresStatements;
        }
        else if (db.Database.IsSqlite())
        {
            statements = SqliteStatements;
        }
        else
        {
            return;
        }

        var failures = 0;
        foreach (var sql in statements)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(sql, ct);
            }
            catch (Exception ex)
            {
                failures++;
                logger?.LogWarning(ex, "LegalSchemaGuard statement failed; legal documents may be unavailable.");
            }
        }

        if (failures == 0)
        {
            logger?.LogInformation("LegalSchemaGuard applied (LegalVersionesDocumento, LegalAceptaciones, DeclaracionesEdad, AutorizacionesMenoresEventos, SolicitudesPqr, SolicitudesPqrEventos).");
        }
    }
}
