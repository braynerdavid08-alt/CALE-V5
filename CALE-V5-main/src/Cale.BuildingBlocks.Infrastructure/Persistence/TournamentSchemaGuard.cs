using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Idempotent creation of tournament tables (bracket, matches, answers, trophies)
/// and SourceKey on PartidasJuego. Runs even when FeatureSchema is disabled.
/// </summary>
public static class TournamentSchemaGuard
{
    private static readonly string[] PostgresStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "Torneos" (
            "Id" serial PRIMARY KEY,
            "SchoolUserId" integer NOT NULL,
            "CreatedByUserId" integer NOT NULL,
            "Name" varchar(120) NOT NULL,
            "Description" varchar(1000) NULL,
            "ScheduledAt" timestamp with time zone NOT NULL,
            "Capacity" integer NOT NULL,
            "QuestionsPerMatch" integer NOT NULL,
            "SecondsPerQuestion" integer NOT NULL,
            "QuestionMode" varchar(20) NOT NULL,
            "CategoryFilterJson" varchar(2000) NULL,
            "ManualQuestionIdsJson" varchar(4000) NULL,
            "Status" varchar(40) NOT NULL,
            "BracketSize" integer NOT NULL,
            "CurrentRoundNumber" integer NOT NULL,
            "ChampionUserId" integer NULL,
            "RowVersion" integer NOT NULL DEFAULT 0,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_Torneos_SchoolUserId_Status" ON "Torneos" ("SchoolUserId", "Status");""",
        """CREATE INDEX IF NOT EXISTS "IX_Torneos_ScheduledAt" ON "Torneos" ("ScheduledAt");""",
        """
        CREATE TABLE IF NOT EXISTS "TorneoParticipantes" (
            "Id" serial PRIMARY KEY,
            "TournamentId" integer NOT NULL,
            "UserId" integer NOT NULL,
            "DisplayNameSnapshot" varchar(120) NOT NULL,
            "JoinedAt" timestamp with time zone NOT NULL,
            "IsConnected" boolean NOT NULL,
            "ConnectionId" varchar(80) NULL,
            "DisconnectedAt" timestamp with time zone NULL,
            "IsEliminated" boolean NOT NULL,
            "EliminatedInRound" integer NULL,
            "BracketSlot" integer NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TorneoParticipantes_TournamentId_UserId" ON "TorneoParticipantes" ("TournamentId", "UserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_TorneoParticipantes_UserId" ON "TorneoParticipantes" ("UserId");""",
        """
        CREATE TABLE IF NOT EXISTS "TorneoRondas" (
            "Id" serial PRIMARY KEY,
            "TournamentId" integer NOT NULL,
            "RoundNumber" integer NOT NULL,
            "Name" varchar(60) NOT NULL,
            "Status" varchar(20) NOT NULL,
            "StartedAt" timestamp with time zone NULL,
            "FinishedAt" timestamp with time zone NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TorneoRondas_TournamentId_RoundNumber" ON "TorneoRondas" ("TournamentId", "RoundNumber");""",
        """
        CREATE TABLE IF NOT EXISTS "TorneoEnfrentamientos" (
            "Id" serial PRIMARY KEY,
            "TournamentId" integer NOT NULL,
            "RoundId" integer NOT NULL,
            "SlotIndex" integer NOT NULL,
            "Player1UserId" integer NULL,
            "Player2UserId" integer NULL,
            "Player1IsBye" boolean NOT NULL,
            "Player2IsBye" boolean NOT NULL,
            "Status" varchar(20) NOT NULL,
            "WinnerUserId" integer NULL,
            "FinishReason" varchar(30) NULL,
            "Player1Correct" integer NOT NULL,
            "Player2Correct" integer NOT NULL,
            "Player1TimeMs" integer NOT NULL,
            "Player2TimeMs" integer NOT NULL,
            "CurrentQuestionIndex" integer NOT NULL,
            "QuestionStartedAt" timestamp with time zone NULL,
            "Player1DisconnectedAt" timestamp with time zone NULL,
            "Player2DisconnectedAt" timestamp with time zone NULL,
            "StartedAt" timestamp with time zone NULL,
            "FinishedAt" timestamp with time zone NULL,
            "RowVersion" integer NOT NULL DEFAULT 0
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TorneoEnfrentamientos_TournamentId_RoundId_SlotIndex" ON "TorneoEnfrentamientos" ("TournamentId", "RoundId", "SlotIndex");""",
        """CREATE INDEX IF NOT EXISTS "IX_TorneoEnfrentamientos_Status" ON "TorneoEnfrentamientos" ("Status");""",
        """
        CREATE TABLE IF NOT EXISTS "TorneoEnfrentamientoPreguntas" (
            "Id" serial PRIMARY KEY,
            "MatchId" integer NOT NULL,
            "Ordinal" integer NOT NULL,
            "QuestionId" integer NOT NULL,
            "CorrectOptionId" integer NOT NULL,
            "IsSuddenDeath" boolean NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TorneoEnfrentamientoPreguntas_MatchId_Ordinal" ON "TorneoEnfrentamientoPreguntas" ("MatchId", "Ordinal");""",
        """
        CREATE TABLE IF NOT EXISTS "TorneoRespuestas" (
            "Id" serial PRIMARY KEY,
            "MatchId" integer NOT NULL,
            "MatchQuestionId" integer NOT NULL,
            "UserId" integer NOT NULL,
            "SelectedOptionId" integer NULL,
            "IsCorrect" boolean NOT NULL,
            "ResponseTimeMs" integer NOT NULL,
            "AnsweredAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TorneoRespuestas_MatchQuestionId_UserId" ON "TorneoRespuestas" ("MatchQuestionId", "UserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_TorneoRespuestas_MatchId" ON "TorneoRespuestas" ("MatchId");""",
        """
        CREATE TABLE IF NOT EXISTS "TrofeosTorneo" (
            "Id" serial PRIMARY KEY,
            "UserId" integer NOT NULL,
            "TournamentId" integer NOT NULL,
            "TournamentNameSnapshot" varchar(120) NOT NULL,
            "ObtainedAt" timestamp with time zone NOT NULL,
            "ChampionResult" varchar(200) NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TrofeosTorneo_TournamentId_UserId" ON "TrofeosTorneo" ("TournamentId", "UserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_TrofeosTorneo_UserId_ObtainedAt" ON "TrofeosTorneo" ("UserId", "ObtainedAt");""",
        """ALTER TABLE "PartidasJuego" ADD COLUMN IF NOT EXISTS "SourceKey" varchar(120) NULL;""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_PartidasJuego_SourceKey" ON "PartidasJuego" ("SourceKey") WHERE "SourceKey" IS NOT NULL;"""
    ];

    private static readonly string[] SqliteStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "Torneos" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "SchoolUserId" INTEGER NOT NULL,
            "CreatedByUserId" INTEGER NOT NULL,
            "Name" TEXT NOT NULL,
            "Description" TEXT NULL,
            "ScheduledAt" TEXT NOT NULL,
            "Capacity" INTEGER NOT NULL,
            "QuestionsPerMatch" INTEGER NOT NULL,
            "SecondsPerQuestion" INTEGER NOT NULL,
            "QuestionMode" TEXT NOT NULL,
            "CategoryFilterJson" TEXT NULL,
            "ManualQuestionIdsJson" TEXT NULL,
            "Status" TEXT NOT NULL,
            "BracketSize" INTEGER NOT NULL,
            "CurrentRoundNumber" INTEGER NOT NULL,
            "ChampionUserId" INTEGER NULL,
            "RowVersion" INTEGER NOT NULL DEFAULT 0,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_Torneos_SchoolUserId_Status" ON "Torneos" ("SchoolUserId", "Status");""",
        """CREATE INDEX IF NOT EXISTS "IX_Torneos_ScheduledAt" ON "Torneos" ("ScheduledAt");""",
        """
        CREATE TABLE IF NOT EXISTS "TorneoParticipantes" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "TournamentId" INTEGER NOT NULL,
            "UserId" INTEGER NOT NULL,
            "DisplayNameSnapshot" TEXT NOT NULL,
            "JoinedAt" TEXT NOT NULL,
            "IsConnected" INTEGER NOT NULL,
            "ConnectionId" TEXT NULL,
            "DisconnectedAt" TEXT NULL,
            "IsEliminated" INTEGER NOT NULL,
            "EliminatedInRound" INTEGER NULL,
            "BracketSlot" INTEGER NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TorneoParticipantes_TournamentId_UserId" ON "TorneoParticipantes" ("TournamentId", "UserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_TorneoParticipantes_UserId" ON "TorneoParticipantes" ("UserId");""",
        """
        CREATE TABLE IF NOT EXISTS "TorneoRondas" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "TournamentId" INTEGER NOT NULL,
            "RoundNumber" INTEGER NOT NULL,
            "Name" TEXT NOT NULL,
            "Status" TEXT NOT NULL,
            "StartedAt" TEXT NULL,
            "FinishedAt" TEXT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TorneoRondas_TournamentId_RoundNumber" ON "TorneoRondas" ("TournamentId", "RoundNumber");""",
        """
        CREATE TABLE IF NOT EXISTS "TorneoEnfrentamientos" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "TournamentId" INTEGER NOT NULL,
            "RoundId" INTEGER NOT NULL,
            "SlotIndex" INTEGER NOT NULL,
            "Player1UserId" INTEGER NULL,
            "Player2UserId" INTEGER NULL,
            "Player1IsBye" INTEGER NOT NULL,
            "Player2IsBye" INTEGER NOT NULL,
            "Status" TEXT NOT NULL,
            "WinnerUserId" INTEGER NULL,
            "FinishReason" TEXT NULL,
            "Player1Correct" INTEGER NOT NULL,
            "Player2Correct" INTEGER NOT NULL,
            "Player1TimeMs" INTEGER NOT NULL,
            "Player2TimeMs" INTEGER NOT NULL,
            "CurrentQuestionIndex" INTEGER NOT NULL,
            "QuestionStartedAt" TEXT NULL,
            "Player1DisconnectedAt" TEXT NULL,
            "Player2DisconnectedAt" TEXT NULL,
            "StartedAt" TEXT NULL,
            "FinishedAt" TEXT NULL,
            "RowVersion" INTEGER NOT NULL DEFAULT 0
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TorneoEnfrentamientos_TournamentId_RoundId_SlotIndex" ON "TorneoEnfrentamientos" ("TournamentId", "RoundId", "SlotIndex");""",
        """CREATE INDEX IF NOT EXISTS "IX_TorneoEnfrentamientos_Status" ON "TorneoEnfrentamientos" ("Status");""",
        """
        CREATE TABLE IF NOT EXISTS "TorneoEnfrentamientoPreguntas" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "MatchId" INTEGER NOT NULL,
            "Ordinal" INTEGER NOT NULL,
            "QuestionId" INTEGER NOT NULL,
            "CorrectOptionId" INTEGER NOT NULL,
            "IsSuddenDeath" INTEGER NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TorneoEnfrentamientoPreguntas_MatchId_Ordinal" ON "TorneoEnfrentamientoPreguntas" ("MatchId", "Ordinal");""",
        """
        CREATE TABLE IF NOT EXISTS "TorneoRespuestas" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "MatchId" INTEGER NOT NULL,
            "MatchQuestionId" INTEGER NOT NULL,
            "UserId" INTEGER NOT NULL,
            "SelectedOptionId" INTEGER NULL,
            "IsCorrect" INTEGER NOT NULL,
            "ResponseTimeMs" INTEGER NOT NULL,
            "AnsweredAt" TEXT NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TorneoRespuestas_MatchQuestionId_UserId" ON "TorneoRespuestas" ("MatchQuestionId", "UserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_TorneoRespuestas_MatchId" ON "TorneoRespuestas" ("MatchId");""",
        """
        CREATE TABLE IF NOT EXISTS "TrofeosTorneo" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "UserId" INTEGER NOT NULL,
            "TournamentId" INTEGER NOT NULL,
            "TournamentNameSnapshot" TEXT NOT NULL,
            "ObtainedAt" TEXT NOT NULL,
            "ChampionResult" TEXT NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TrofeosTorneo_TournamentId_UserId" ON "TrofeosTorneo" ("TournamentId", "UserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_TrofeosTorneo_UserId_ObtainedAt" ON "TrofeosTorneo" ("UserId", "ObtainedAt");""",
        """ALTER TABLE "PartidasJuego" ADD COLUMN "SourceKey" TEXT NULL;""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_PartidasJuego_SourceKey" ON "PartidasJuego" ("SourceKey") WHERE "SourceKey" IS NOT NULL;"""
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
                logger?.LogWarning(ex, "TournamentSchemaGuard statement failed; tournament features may be unavailable.");
            }
        }

        if (failures == 0)
        {
            logger?.LogInformation("TournamentSchemaGuard applied (Torneos/TorneoParticipantes/TorneoRondas/TorneoEnfrentamientos/TrofeosTorneo/PartidasJuego.SourceKey).");
        }
    }
}
