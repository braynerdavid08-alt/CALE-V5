using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Ensures GruposUsuarios has a unique (GrupoId, UsuarioId) so concurrent joins cannot duplicate rows.
/// </summary>
public static class GroupMemberSchemaGuard
{
    public static async Task EnsureAsync(CaleDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational())
        {
            return;
        }

        try
        {
            if (db.Database.IsNpgsql())
            {
                if (await ScalarAsync(
                        db,
                        "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'GruposUsuarios';",
                        ct) == 0)
                {
                    return;
                }

                await db.Database.ExecuteSqlRawAsync("""
                    DELETE FROM "GruposUsuarios" a
                    USING "GruposUsuarios" b
                    WHERE a."Id" > b."Id"
                      AND a."GrupoId" = b."GrupoId"
                      AND a."UsuarioId" = b."UsuarioId";
                    """, ct);

                await db.Database.ExecuteSqlRawAsync("""
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_GruposUsuarios_GrupoId_UsuarioId"
                    ON "GruposUsuarios" ("GrupoId", "UsuarioId");
                    """, ct);
            }
            else if (db.Database.IsSqlite())
            {
                if (await ScalarAsync(
                        db,
                        "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'GruposUsuarios';",
                        ct) == 0)
                {
                    return;
                }

                await db.Database.ExecuteSqlRawAsync("""
                    DELETE FROM GruposUsuarios
                    WHERE Id NOT IN (
                      SELECT MIN(Id) FROM GruposUsuarios GROUP BY GrupoId, UsuarioId
                    );
                    """, ct);

                await db.Database.ExecuteSqlRawAsync("""
                    CREATE UNIQUE INDEX IF NOT EXISTS IX_GruposUsuarios_GrupoId_UsuarioId
                    ON GruposUsuarios (GrupoId, UsuarioId);
                    """, ct);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "GroupMemberSchemaGuard failed; duplicate group memberships may still be possible until it succeeds.");
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
