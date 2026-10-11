using Cale.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cale.Api.Persistence.Migrations;

/// <summary>
/// Admin approval for self-registered schools on "SchoolProfiles". Idempotent SQL shared with
/// <see cref="SchoolApprovalSchemaGuard"/>; existing schools stay NULL (approved).
/// </summary>
[DbContext(typeof(CaleDbContext))]
[Migration("20261011060000_AddSchoolApproval")]
public partial class AddSchoolApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(SchoolApprovalSchemaGuard.PostgresStatement);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""ALTER TABLE "SchoolProfiles" DROP COLUMN IF EXISTS "PendingApprovalSince";""");
    }
}
