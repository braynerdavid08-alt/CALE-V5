using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cale.Api.Persistence.Migrations;

/// <summary>
/// Repairs attempt columns that FeatureSchema used to patch at runtime.
/// After EF baseline stamping those ALTERs no longer run in Production,
/// which breaks simulacro/practice start with db_error.
/// Idempotent SQL — safe if columns already exist.
/// </summary>
public partial class RepairAttemptSchemaForSimulacros : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "IntentosPreguntas"
            ADD COLUMN IF NOT EXISTS "SnapshotJson" text NOT NULL DEFAULT '{}';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE "Intentos"
            ADD COLUMN IF NOT EXISTS "ExpiresAt" timestamp with time zone NULL;
            """);

        migrationBuilder.Sql(
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
            """);

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Intentos_OpenExam"
                ON "Intentos" ("UsuarioId", "ExamenId")
                WHERE "FinEn" IS NULL AND "ExamenId" IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Non-destructive repair — keep columns/index.
    }
}
