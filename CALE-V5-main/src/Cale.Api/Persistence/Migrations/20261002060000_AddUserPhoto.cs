using Cale.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cale.Api.Persistence.Migrations;

/// <summary>
/// Profile photo URL on "Usuarios". Idempotent SQL shared with <see cref="UserPhotoSchemaGuard"/>,
/// which may already have added the column at startup.
/// </summary>
[DbContext(typeof(CaleDbContext))]
[Migration("20261002060000_AddUserPhoto")]
public partial class AddUserPhoto : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(UserPhotoSchemaGuard.PostgresStatement);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""ALTER TABLE "Usuarios" DROP COLUMN IF EXISTS "FotoUrl";""");
    }
}
