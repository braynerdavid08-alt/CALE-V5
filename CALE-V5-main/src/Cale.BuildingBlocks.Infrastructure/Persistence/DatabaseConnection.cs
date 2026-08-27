using Microsoft.Extensions.Configuration;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

public enum DatabaseProviderKind
{
    Sqlite,
    SqlServer,
    PostgreSql
}

public static class DatabaseConnection
{
    public static string Resolve(IConfiguration config)
    {
        var raw = config.GetConnectionString("Cale");
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = config["DATABASE_URL"];
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "Missing ConnectionStrings:Cale (or DATABASE_URL for Postgres). " +
                "In Render set ConnectionStrings__Cale to the Internal Database URL.");
        }

        return Normalize(raw);
    }

    public static string Normalize(string raw)
    {
        raw = raw.Trim().Trim('"');
        if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(raw);
            var userInfo = uri.UserInfo.Split(':', 2);
            var user = Uri.UnescapeDataString(userInfo[0]);
            var pass = userInfo.Length > 1
                ? Uri.UnescapeDataString(userInfo[1])
                : "";
            var database = uri.AbsolutePath.Trim('/');
            var port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port;

            // Prefer works with Render internal + external; Require often breaks internal links.
            return
                $"Host={uri.Host};Port={port};Database={database};Username={user};Password={pass};SSL Mode=Prefer;Trust Server Certificate=true;Timeout=30";
        }

        return raw;
    }

    public static DatabaseProviderKind Detect(string connection)
    {
        if (connection.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
            || connection.Contains("Filename=", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseProviderKind.Sqlite;
        }

        if (connection.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || connection.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            || (connection.Contains("Host=", StringComparison.OrdinalIgnoreCase)
                && (connection.Contains("Username=", StringComparison.OrdinalIgnoreCase)
                    || connection.Contains("User ID=", StringComparison.OrdinalIgnoreCase))))
        {
            return DatabaseProviderKind.PostgreSql;
        }

        return DatabaseProviderKind.SqlServer;
    }
}
