using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Critical, idempotent repairs for attempt tables required to start simulacros.
/// Runs even when FeatureSchema / EF migrations are disabled in Production,
/// because those paths previously left SnapshotJson / ExpiresAt missing after baseline stamp.
/// </summary>
public static class AttemptSchemaGuard
{
    public static async Task EnsureAsync(
        CaleDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (!db.Database.IsRelational())
        {
            return;
        }

        try
        {
            if (db.Database.IsNpgsql())
            {
                await db.Database.ExecuteSqlRawAsync(
                    """
                    ALTER TABLE IF EXISTS "IntentosPreguntas"
                    ADD COLUMN IF NOT EXISTS "SnapshotJson" text NOT NULL DEFAULT '{}';
                    """,
                    ct);

                await db.Database.ExecuteSqlRawAsync(
                    """
                    ALTER TABLE IF EXISTS "Intentos"
                    ADD COLUMN IF NOT EXISTS "ExpiresAt" timestamp with time zone NULL;
                    """,
                    ct);

                await db.Database.ExecuteSqlRawAsync(
                    """
                    DO $$
                    BEGIN
                      IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'Intentos'
                          AND column_name = 'Modo'
                          AND data_type = 'character varying'
                      ) THEN
                        ALTER TABLE "Intentos" ALTER COLUMN "Modo" TYPE text;
                      END IF;
                    END $$;
                    """,
                    ct);

                await db.Database.ExecuteSqlRawAsync(
                    """
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_Intentos_OpenExam"
                        ON "Intentos" ("UsuarioId", "ExamenId")
                        WHERE "FinEn" IS NULL AND "ExamenId" IS NOT NULL;
                    """,
                    ct);
            }
            else if (db.Database.IsSqlServer())
            {
                await db.Database.ExecuteSqlRawAsync(
                    """
                    IF OBJECT_ID(N'dbo.IntentosPreguntas', N'U') IS NOT NULL
                       AND COL_LENGTH(N'dbo.IntentosPreguntas', N'SnapshotJson') IS NULL
                        ALTER TABLE dbo.IntentosPreguntas
                        ADD SnapshotJson nvarchar(max) NOT NULL
                            CONSTRAINT DF_IntentosPreguntas_SnapshotJson DEFAULT N'{}';
                    """,
                    ct);

                await db.Database.ExecuteSqlRawAsync(
                    """
                    IF OBJECT_ID(N'dbo.Intentos', N'U') IS NOT NULL
                       AND COL_LENGTH(N'dbo.Intentos', N'ExpiresAt') IS NULL
                        ALTER TABLE dbo.Intentos ADD ExpiresAt datetime2 NULL;
                    """,
                    ct);
            }

            logger?.LogInformation("AttemptSchemaGuard applied (SnapshotJson/ExpiresAt/Modo/open-exam index).");
        }
        catch (Exception ex)
        {
            // Do not fail boot if tables are temporarily locked; start will surface a clear error.
            logger?.LogWarning(ex, "AttemptSchemaGuard could not complete; simulacro start may fail until repaired.");
        }
    }
}
