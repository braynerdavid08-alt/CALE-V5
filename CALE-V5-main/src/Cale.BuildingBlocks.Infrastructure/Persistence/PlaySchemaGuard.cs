using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Idempotent creation of the student gamification tables (daily challenge, mistakes review,
/// achievements, game results, player profile). Runs even when FeatureSchema is disabled.
/// </summary>
public static class PlaySchemaGuard
{
    private static readonly string[] PostgresStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "RetosDiarios" (
            "Id" serial PRIMARY KEY,
            "UserId" integer NOT NULL,
            "ChallengeDate" date NOT NULL,
            "QuestionIdsJson" text NOT NULL,
            "AnswersJson" text NOT NULL,
            "CorrectCount" integer NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "CompletedAt" timestamp with time zone NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_RetosDiarios_UserId_ChallengeDate" ON "RetosDiarios" ("UserId", "ChallengeDate");""",
        """
        CREATE TABLE IF NOT EXISTS "RepasoErrores" (
            "Id" serial PRIMARY KEY,
            "UserId" integer NOT NULL,
            "QuestionId" integer NOT NULL,
            "Box" integer NOT NULL,
            "NextDueAt" timestamp with time zone NOT NULL,
            "LastWrongAt" timestamp with time zone NOT NULL,
            "Mastered" boolean NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_RepasoErrores_UserId_QuestionId" ON "RepasoErrores" ("UserId", "QuestionId");""",
        """CREATE INDEX IF NOT EXISTS "IX_RepasoErrores_UserId_NextDueAt" ON "RepasoErrores" ("UserId", "NextDueAt");""",
        """
        CREATE TABLE IF NOT EXISTS "LogrosUsuario" (
            "Id" serial PRIMARY KEY,
            "UserId" integer NOT NULL,
            "Code" varchar(60) NOT NULL,
            "EarnedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_LogrosUsuario_UserId_Code" ON "LogrosUsuario" ("UserId", "Code");""",
        """
        CREATE TABLE IF NOT EXISTS "PartidasJuego" (
            "Id" serial PRIMARY KEY,
            "UserId" integer NOT NULL,
            "Game" varchar(20) NOT NULL,
            "Score" integer NOT NULL,
            "Correct" integer NOT NULL,
            "Total" integer NOT NULL,
            "Won" boolean NOT NULL,
            "OpponentId" integer NULL,
            "PlayedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_PartidasJuego_UserId_Game" ON "PartidasJuego" ("UserId", "Game");""",
        """CREATE INDEX IF NOT EXISTS "IX_PartidasJuego_PlayedAt" ON "PartidasJuego" ("PlayedAt");""",
        """
        CREATE TABLE IF NOT EXISTS "PerfilJuego" (
            "UserId" integer NOT NULL PRIMARY KEY,
            "ShowInRanking" boolean NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """
    ];

    private static readonly string[] SqliteStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "RetosDiarios" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "UserId" INTEGER NOT NULL,
            "ChallengeDate" TEXT NOT NULL,
            "QuestionIdsJson" TEXT NOT NULL,
            "AnswersJson" TEXT NOT NULL,
            "CorrectCount" INTEGER NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "CompletedAt" TEXT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_RetosDiarios_UserId_ChallengeDate" ON "RetosDiarios" ("UserId", "ChallengeDate");""",
        """
        CREATE TABLE IF NOT EXISTS "RepasoErrores" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "UserId" INTEGER NOT NULL,
            "QuestionId" INTEGER NOT NULL,
            "Box" INTEGER NOT NULL,
            "NextDueAt" TEXT NOT NULL,
            "LastWrongAt" TEXT NOT NULL,
            "Mastered" INTEGER NOT NULL,
            "UpdatedAt" TEXT NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_RepasoErrores_UserId_QuestionId" ON "RepasoErrores" ("UserId", "QuestionId");""",
        """
        CREATE TABLE IF NOT EXISTS "LogrosUsuario" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "UserId" INTEGER NOT NULL,
            "Code" TEXT NOT NULL,
            "EarnedAt" TEXT NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_LogrosUsuario_UserId_Code" ON "LogrosUsuario" ("UserId", "Code");""",
        """
        CREATE TABLE IF NOT EXISTS "PartidasJuego" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "UserId" INTEGER NOT NULL,
            "Game" TEXT NOT NULL,
            "Score" INTEGER NOT NULL,
            "Correct" INTEGER NOT NULL,
            "Total" INTEGER NOT NULL,
            "Won" INTEGER NOT NULL,
            "OpponentId" INTEGER NULL,
            "PlayedAt" TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "PerfilJuego" (
            "UserId" INTEGER NOT NULL PRIMARY KEY,
            "ShowInRanking" INTEGER NOT NULL,
            "UpdatedAt" TEXT NOT NULL
        );
        """
    ];

    public static async Task EnsureAsync(
        CaleDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
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
                logger?.LogWarning(ex, "PlaySchemaGuard statement failed; gamification features may be unavailable.");
            }
        }

        if (failures == 0)
        {
            logger?.LogInformation("PlaySchemaGuard applied (RetosDiarios/RepasoErrores/LogrosUsuario/PartidasJuego/PerfilJuego).");
        }
    }
}
