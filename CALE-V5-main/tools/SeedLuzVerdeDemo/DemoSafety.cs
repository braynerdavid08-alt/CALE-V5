using System.Globalization;
using System.Text.RegularExpressions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;

namespace SeedLuzVerdeDemo;

internal static class DemoSafety
{
    private static readonly Regex ProdName = new(
        @"\b(prod|production|live|render)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static void AssertSafeToWrite(
        IConfiguration config,
        string connection,
        bool promoteProduction,
        string? confirmHost,
        string? confirmDatabase,
        string? confirmBackup,
        bool additiveAcknowledged)
    {
        if (promoteProduction)
        {
            AssertPromoteProductionSafe(
                connection,
                confirmHost,
                confirmDatabase,
                confirmBackup,
                additiveAcknowledged);
            return;
        }

        var env = config["ASPNETCORE_ENVIRONMENT"]
            ?? config["DOTNET_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "";

        if (env.Equals("Production", StringComparison.OrdinalIgnoreCase)
            || env.Equals("Prod", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "ABORT: ASPNETCORE_ENVIRONMENT/DOTNET_ENVIRONMENT indica Production. " +
                "Usa --promote-production con confirmaciones explícitas.");
        }

        if (!string.IsNullOrWhiteSpace(config["RENDER"])
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER")))
        {
            throw new InvalidOperationException(
                "ABORT: variable RENDER detectada. Usa --promote-production con confirmaciones explícitas.");
        }

        var provider = DatabaseConnection.Detect(connection);
        if (provider == DatabaseProviderKind.Sqlite)
        {
            AssertSqliteSafe(connection);
            return;
        }

        if (provider == DatabaseProviderKind.PostgreSql)
        {
            AssertPostgresLocalSafe(connection);
            return;
        }

        throw new InvalidOperationException(
            $"ABORT: proveedor no permitido en modo local ({provider}).");
    }

    private static void AssertPromoteProductionSafe(
        string connection,
        string? confirmHost,
        string? confirmDatabase,
        string? confirmBackup,
        bool additiveAcknowledged)
    {
        if (!additiveAcknowledged)
        {
            throw new InvalidOperationException(
                "ABORT promote: falta --i-understand-additive-only.");
        }

        if (string.IsNullOrWhiteSpace(confirmHost)
            || string.IsNullOrWhiteSpace(confirmDatabase)
            || string.IsNullOrWhiteSpace(confirmBackup))
        {
            throw new InvalidOperationException(
                "ABORT promote: requiere --confirm-host, --confirm-database y --confirm-backup YYYY-MM-DD.");
        }

        if (!DateOnly.TryParseExact(
                confirmBackup.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var backupDate))
        {
            throw new InvalidOperationException(
                "ABORT promote: --confirm-backup debe ser YYYY-MM-dd (fecha del backup reciente).");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (backupDate > today)
        {
            throw new InvalidOperationException("ABORT promote: la fecha de backup no puede ser futura.");
        }

        if (backupDate < today.AddDays(-7))
        {
            throw new InvalidOperationException(
                $"ABORT promote: backup {backupDate:yyyy-MM-dd} tiene más de 7 días. Toma uno reciente y vuelve a confirmar.");
        }

        if (DatabaseConnection.Detect(connection) != DatabaseProviderKind.PostgreSql)
        {
            throw new InvalidOperationException(
                "ABORT promote: la conexión debe ser PostgreSQL de producción (no SQLite).");
        }

        string host;
        string database;
        try
        {
            var described = DatabaseConnection.Describe(connection);
            host = Extract(described, "host=");
            database = Extract(described, "db=");
        }
        catch
        {
            throw new InvalidOperationException(
                "ABORT promote: no se pudo inspeccionar la cadena PostgreSQL sin exponer secretos.");
        }

        if (!HostMatches(host, confirmHost.Trim()))
        {
            throw new InvalidOperationException(
                $"ABORT promote: host real '{host}' no coincide con --confirm-host '{confirmHost}'.");
        }

        if (!database.Equals(confirmDatabase.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"ABORT promote: database real '{database}' no coincide con --confirm-database '{confirmDatabase}'.");
        }

        // Production hosts we expect for Luz Verde (Render).
        var looksProd = host.Contains("render.com", StringComparison.OrdinalIgnoreCase)
            || host.StartsWith("dpg-", StringComparison.OrdinalIgnoreCase);
        if (!looksProd)
        {
            throw new InvalidOperationException(
                $"ABORT promote: host '{host}' no parece Postgres de producción Render. Revisa la cadena.");
        }

        DemoRuntime.ConfirmedHost = host;
        DemoRuntime.ConfirmedDatabase = database;
        DemoRuntime.ConfirmedBackupDate = backupDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static bool HostMatches(string actual, string confirmed)
    {
        if (actual.Equals(confirmed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Allow confirming with a suffix (e.g. oregon-postgres.render.com) if actual ends with it.
        if (confirmed.StartsWith("*.", StringComparison.Ordinal))
        {
            var suffix = confirmed[1..]; // ".oregon-..."
            return actual.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        return actual.EndsWith(confirmed, StringComparison.OrdinalIgnoreCase)
            || confirmed.EndsWith(actual, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertSqliteSafe(string connection)
    {
        var lower = connection.ToLowerInvariant();
        if (lower.Contains("demo")
            || lower.Contains("dev")
            || lower.Contains("test")
            || lower.Contains("local")
            || lower.Contains(":memory:"))
        {
            return;
        }

        throw new InvalidOperationException(
            "ABORT: SQLite no parece de pruebas (el path debe contener demo/dev/test/local).");
    }

    private static void AssertPostgresLocalSafe(string connection)
    {
        string host;
        string database;
        try
        {
            var described = DatabaseConnection.Describe(connection);
            host = Extract(described, "host=");
            database = Extract(described, "db=");
        }
        catch
        {
            throw new InvalidOperationException(
                "ABORT: no se pudo inspeccionar la cadena PostgreSQL sin exponer secretos.");
        }

        if (host.Contains("render.com", StringComparison.OrdinalIgnoreCase)
            || host.StartsWith("dpg-", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"ABORT: host PostgreSQL parece de producción ({host}). Usa --promote-production.");
        }

        var isLoopback = host is "localhost" or "127.0.0.1" or "::1"
            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

        if (!isLoopback)
        {
            throw new InvalidOperationException(
                $"ABORT: PostgreSQL remoto no permitido en modo local (host={host}).");
        }

        if (ProdName.IsMatch(database) && !database.Contains("demo", StringComparison.OrdinalIgnoreCase)
            && !database.Contains("dev", StringComparison.OrdinalIgnoreCase)
            && !database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"ABORT: nombre de base PostgreSQL parece de producción ({database}).");
        }
    }

    private static string Extract(string described, string key)
    {
        var idx = described.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return "";
        }

        var start = idx + key.Length;
        var end = described.IndexOf(' ', start);
        return end < 0 ? described[start..] : described[start..end];
    }
}
