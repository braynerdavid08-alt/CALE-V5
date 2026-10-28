using Cale.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cale.Api.Persistence.Migrations;

/// <summary>
/// Lesson vs Exam on practical sessions. Idempotent SQL shared with
/// <see cref="PracticalSessionKindSchemaGuard"/>.
/// </summary>
[DbContext(typeof(CaleDbContext))]
[Migration("20261028000000_AddPracticalSessionKind")]
public partial class AddPracticalSessionKind : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(PracticalSessionKindSchemaGuard.PostgresStatement);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""ALTER TABLE "PracticalLessonSessions" DROP COLUMN IF EXISTS "Kind";""");
    }
}
