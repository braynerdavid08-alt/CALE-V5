namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// EF Core <c>ExecuteSqlRaw</c> runs SQL through <c>string.Format</c>-style placeholders.
/// Literal braces in DDL (e.g. <c>DEFAULT '{}'</c> for JSON) must be doubled.
/// </summary>
public static class RawSqlLiteral
{
    public static string Escape(string sql) =>
        sql.Replace("{", "{{", StringComparison.Ordinal)
           .Replace("}", "}}", StringComparison.Ordinal);
}
