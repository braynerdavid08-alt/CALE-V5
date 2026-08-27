using Microsoft.Extensions.Configuration;
using Npgsql;

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
                "Missing ConnectionStrings:Cale (or DATABASE_URL). " +
                "In Render: Postgres → Connect → copy Internal Database URL into " +
                "ConnectionStrings__Cale (full URL, not just the hostname).");
        }

        return Normalize(raw, config["RENDER_REGION"]);
    }

    public static string Normalize(string raw, string? renderRegion = null)
    {
        raw = raw.Trim().Trim('"').Trim('\'');

        // Common mistake: entire URL pasted as Host= value.
        if (raw.StartsWith("Host=postgresql://", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("Host=postgres://", StringComparison.OrdinalIgnoreCase))
        {
            var idx = raw.IndexOf('=');
            raw = raw[(idx + 1)..].Trim();
        }

        if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            // Npgsql parses postgres:// URIs natively — do not manual-convert to Host=...
            return ExpandRenderInternalHost(raw, renderRegion);
        }

        return raw;
    }

    /// <summary>
    /// Render internal URLs use host <c>dpg-xxxx-a</c> (no domain). That is valid on Render private network.
    /// External tools need <c>dpg-xxxx-a.region-postgres.render.com</c>.
    /// </summary>
    private static string ExpandRenderInternalHost(string postgresUri, string? renderRegion)
    {
        if (!Uri.TryCreate(postgresUri, UriKind.Absolute, out var uri))
        {
            return postgresUri;
        }

        if (uri.Host.Contains('.') || !uri.Host.StartsWith("dpg-", StringComparison.Ordinal))
        {
            return postgresUri;
        }

        // Short internal hostname — keep URI as-is for services running on Render.
        return postgresUri;
    }

    public static DatabaseProviderKind Detect(string connection)
    {
        var c = connection.Trim();
        if (c.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
            || c.Contains("Filename=", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseProviderKind.Sqlite;
        }

        if (c.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || c.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            || c.Contains("Host=", StringComparison.OrdinalIgnoreCase)
                && (c.Contains("Username=", StringComparison.OrdinalIgnoreCase)
                    || c.Contains("User ID=", StringComparison.OrdinalIgnoreCase)))
        {
            return DatabaseProviderKind.PostgreSql;
        }

        return DatabaseProviderKind.SqlServer;
    }

    /// <summary>Validate without exposing secrets (for startup logs).</summary>
    public static string Describe(string connection)
    {
        try
        {
            if (connection.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(connection);
                return $"PostgreSQL host={uri.Host} db={uri.AbsolutePath.Trim('/')}";
            }

            var b = new NpgsqlConnectionStringBuilder(connection);
            return $"PostgreSQL host={b.Host} db={b.Database}";
        }
        catch
        {
            return "PostgreSQL (connection string could not be parsed)";
        }
    }
}
