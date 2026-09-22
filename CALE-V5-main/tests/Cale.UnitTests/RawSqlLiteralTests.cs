using Cale.BuildingBlocks.Infrastructure.Persistence;

namespace Cale.UnitTests;

public sealed class RawSqlLiteralTests
{
    [Fact]
    public void Escape_doubles_braces_so_ExecuteSqlRaw_format_keeps_json_default()
    {
        var sql = """ALTER TABLE "T" ADD COLUMN "J" text NOT NULL DEFAULT '{}';""";
        var escaped = RawSqlLiteral.Escape(sql);

        Assert.Equal("""ALTER TABLE "T" ADD COLUMN "J" text NOT NULL DEFAULT '{{}}';""", escaped);

        // Simulate EF RawSqlCommandBuilder formatting with zero args.
        var formatted = string.Format(escaped);
        Assert.Equal(sql, formatted);
    }

    [Fact]
    public void Unescaped_json_default_throws_FormatException()
    {
        var sql = """ADD COLUMN "SnapshotJson" text NOT NULL DEFAULT '{}';""";
        Assert.Throws<FormatException>(() => string.Format(sql));
    }
}
