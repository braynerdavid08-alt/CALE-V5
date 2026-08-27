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
        // On Render, DATABASE_URL from "Connect to service" is the reliable internal URL.
        var onRender = !string.IsNullOrWhiteSpace(config["RENDER"]);
        string? raw = onRender ? config["DATABASE_URL"] : null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = config.GetConnectionString("Cale");
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = config["DATABASE_URL"];
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "Missing ConnectionStrings:Cale (or DATABASE_URL). " +
                "In Render: Postgres → Connect → Connect to MICALE (sets DATABASE_URL), " +
                "or paste the Internal Database URL into ConnectionStrings__Cale.");
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
            return ApplyPostgresSslMode(raw);
        }

        if (raw.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            && raw.Contains("Username=", StringComparison.OrdinalIgnoreCase))
        {
            return ApplyPostgresSslModeToKeyValue(raw);
        }

        return raw;
    }

    /// <summary>
    /// Render internal: <c>dpg-xxxx-a</c> (no SSL). External: <c>*.render.com</c> (SSL required).
    /// </summary>
    private static string ApplyPostgresSslMode(string postgresUri)
    {
        if (postgresUri.Contains("sslmode=", StringComparison.OrdinalIgnoreCase))
        {
            return postgresUri;
        }

        if (!Uri.TryCreate(postgresUri, UriKind.Absolute, out var uri))
        {
            return postgresUri;
        }

        var sslMode = ResolveSslMode(uri.Host);
        var separator = postgresUri.Contains('?') ? '&' : '?';
        return $"{postgresUri}{separator}sslmode={sslMode}";
    }

    private static string ApplyPostgresSslModeToKeyValue(string connection)
    {
        if (connection.Contains("SSL Mode=", StringComparison.OrdinalIgnoreCase)
            || connection.Contains("Ssl Mode=", StringComparison.OrdinalIgnoreCase))
        {
            return connection;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connection);
            builder.SslMode = ResolveSslMode(builder.Host) switch
            {
                "disable" => SslMode.Disable,
                "require" => SslMode.Require,
                _ => SslMode.Prefer
            };
            return builder.ConnectionString;
        }
        catch
        {
            return connection;
        }
    }

    private static string ResolveSslMode(string host)
    {
        if (host.Contains("render.com", StringComparison.OrdinalIgnoreCase))
        {
            return "require";
        }

        if (host.StartsWith("dpg-", StringComparison.Ordinal) && !host.Contains('.'))
        {
            return "disable";
        }

        return "prefer";
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
