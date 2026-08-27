using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text.RegularExpressions;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

public enum DatabaseProviderKind
{
    Sqlite,
    SqlServer,
    PostgreSql
}

public static class DatabaseConnection
{
    private static readonly Regex PostgresUriRegex = new(
        @"^postgres(?:ql)?://(?:(?<user>[^:@/]+)(?::(?<password>[^@]*))?@)?(?<host>[^:/?#]+)(?::(?<port>\d+))?/(?<database>[^?#]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

        return Normalize(raw);
    }

    public static string Normalize(string raw)
    {
        raw = raw.Trim().Trim('"').Trim('\'');

        // Common mistake: entire URL pasted as Host= value.
        if (raw.StartsWith("Host=postgresql://", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("Host=postgres://", StringComparison.OrdinalIgnoreCase))
        {
            var idx = raw.IndexOf('=');
            raw = raw[(idx + 1)..].Trim();
        }

        // Render/env parsers sometimes truncate at '=' and leave a broken "?sslmode" suffix.
        raw = StripBrokenSslModeQuery(raw);

        if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return PostgresUriToNpgsql(raw);
        }

        if (raw.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            && (raw.Contains("Username=", StringComparison.OrdinalIgnoreCase)
                || raw.Contains("User ID=", StringComparison.OrdinalIgnoreCase)))
        {
            return ApplyPostgresSslModeToKeyValue(raw);
        }

        return raw;
    }

    private static string StripBrokenSslModeQuery(string raw)
    {
        var qIdx = raw.IndexOf('?', StringComparison.Ordinal);
        if (qIdx < 0)
        {
            return raw;
        }

        var query = raw[(qIdx + 1)..];
        if (string.IsNullOrWhiteSpace(query)
            || query.Equals("sslmode", StringComparison.OrdinalIgnoreCase)
            || query.StartsWith("sslmode&", StringComparison.OrdinalIgnoreCase))
        {
            return raw[..qIdx];
        }

        return raw;
    }

    /// <summary>
    /// Convert postgres:// URI to Npgsql key=value format.
    /// Avoids ?sslmode= in URLs — Render env vars truncate at '='.
    /// </summary>
    private static string PostgresUriToNpgsql(string postgresUri)
    {
        var match = PostgresUriRegex.Match(StripBrokenSslModeQuery(postgresUri));
        if (!match.Success)
        {
            throw new InvalidOperationException(
                "Invalid PostgreSQL URL. Use: postgresql://USER:PASSWORD@HOST/DATABASE");
        }

        var host = match.Groups["host"].Value;
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = match.Groups["port"].Success
                ? int.Parse(match.Groups["port"].Value)
                : 5432,
            Database = Uri.UnescapeDataString(match.Groups["database"].Value),
            Username = Uri.UnescapeDataString(match.Groups["user"].Value),
            Password = match.Groups["password"].Success
                ? Uri.UnescapeDataString(match.Groups["password"].Value)
                : string.Empty,
            SslMode = ResolveSslMode(host) switch
            {
                "disable" => SslMode.Disable,
                "require" => SslMode.Require,
                _ => SslMode.Prefer
            }
        };

        return builder.ConnectionString;
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
                var converted = PostgresUriToNpgsql(connection);
                var b = new NpgsqlConnectionStringBuilder(converted);
                return $"PostgreSQL host={b.Host} db={b.Database}";
            }

            var builder = new NpgsqlConnectionStringBuilder(connection);
            return $"PostgreSQL host={builder.Host} db={builder.Database}";
        }
        catch
        {
            return "PostgreSQL (connection string could not be parsed)";
        }
    }
}
