using Cale.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.Courses.Infrastructure.Persistence;

/// <summary>Idempotent creation of the course tables; runs at every startup.</summary>
public static class CourseSchemaGuard
{
    private static readonly string[] PostgresStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "Cursos" (
            "Id" serial PRIMARY KEY,
            "SchoolUserId" integer NULL,
            "OwnerUserId" integer NOT NULL,
            "Slug" varchar(80) NULL,
            "Title" varchar(160) NOT NULL,
            "Description" varchar(1000) NULL,
            "Category" varchar(80) NOT NULL,
            "CoverUrl" varchar(500) NULL,
            "IsPublished" boolean NOT NULL DEFAULT false,
            "IsActive" boolean NOT NULL DEFAULT true,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_Cursos_SchoolUserId" ON "Cursos" ("SchoolUserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_Cursos_Slug" ON "Cursos" ("Slug");""",
        """
        CREATE TABLE IF NOT EXISTS "CursoLecciones" (
            "Id" serial PRIMARY KEY,
            "CourseId" integer NOT NULL,
            "Position" integer NOT NULL,
            "Title" varchar(160) NOT NULL,
            "Summary" varchar(500) NULL,
            "EstimatedMinutes" integer NOT NULL DEFAULT 10,
            "ContentJson" text NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_CursoLecciones_CourseId_Position" ON "CursoLecciones" ("CourseId", "Position");""",
        """
        CREATE TABLE IF NOT EXISTS "CursoProgresoLecciones" (
            "Id" serial PRIMARY KEY,
            "CourseId" integer NOT NULL,
            "LessonId" integer NOT NULL,
            "StudentUserId" integer NOT NULL,
            "Score" integer NOT NULL,
            "Attempts" integer NOT NULL,
            "CompletedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_CursoProgresoLecciones_LessonId_StudentUserId" ON "CursoProgresoLecciones" ("LessonId", "StudentUserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_CursoProgresoLecciones_CourseId_StudentUserId" ON "CursoProgresoLecciones" ("CourseId", "StudentUserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_CursoProgresoLecciones_StudentUserId" ON "CursoProgresoLecciones" ("StudentUserId");"""
    ];

    private static readonly string[] SqliteStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "Cursos" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "SchoolUserId" INTEGER NULL,
            "OwnerUserId" INTEGER NOT NULL,
            "Slug" TEXT NULL,
            "Title" TEXT NOT NULL,
            "Description" TEXT NULL,
            "Category" TEXT NOT NULL,
            "CoverUrl" TEXT NULL,
            "IsPublished" INTEGER NOT NULL DEFAULT 0,
            "IsActive" INTEGER NOT NULL DEFAULT 1,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_Cursos_SchoolUserId" ON "Cursos" ("SchoolUserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_Cursos_Slug" ON "Cursos" ("Slug");""",
        """
        CREATE TABLE IF NOT EXISTS "CursoLecciones" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "CourseId" INTEGER NOT NULL,
            "Position" INTEGER NOT NULL,
            "Title" TEXT NOT NULL,
            "Summary" TEXT NULL,
            "EstimatedMinutes" INTEGER NOT NULL DEFAULT 10,
            "ContentJson" TEXT NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_CursoLecciones_CourseId_Position" ON "CursoLecciones" ("CourseId", "Position");""",
        """
        CREATE TABLE IF NOT EXISTS "CursoProgresoLecciones" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "CourseId" INTEGER NOT NULL,
            "LessonId" INTEGER NOT NULL,
            "StudentUserId" INTEGER NOT NULL,
            "Score" INTEGER NOT NULL,
            "Attempts" INTEGER NOT NULL,
            "CompletedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_CursoProgresoLecciones_LessonId_StudentUserId" ON "CursoProgresoLecciones" ("LessonId", "StudentUserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_CursoProgresoLecciones_CourseId_StudentUserId" ON "CursoProgresoLecciones" ("CourseId", "StudentUserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_CursoProgresoLecciones_StudentUserId" ON "CursoProgresoLecciones" ("StudentUserId");"""
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
                logger?.LogWarning(ex, "CourseSchemaGuard statement failed; courses may be unavailable.");
            }
        }

        if (failures == 0)
        {
            logger?.LogInformation("CourseSchemaGuard applied (Cursos/CursoLecciones/CursoProgresoLecciones).");
        }
    }
}
