using Cale.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cale.Api.Persistence.Migrations;

/// <summary>
/// Temporary B2B commercial columns, plan catalog and assistant usage ledger.
/// Also applied idempotently by <see cref="CommercialSchemaGuard"/>.
/// </summary>
[DbContext(typeof(CaleDbContext))]
[Migration("20261023000000_AddCommercialSubscription")]
public partial class AddCommercialSubscription : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "SchoolProfiles" ADD COLUMN IF NOT EXISTS "PaymentAdminStatus" character varying(32) NOT NULL DEFAULT 'None';
            ALTER TABLE "SchoolProfiles" ADD COLUMN IF NOT EXISTS "AgreedPriceCop" numeric(18,2) NULL;
            ALTER TABLE "SchoolProfiles" ADD COLUMN IF NOT EXISTS "CourtesyUntil" timestamp with time zone NULL;
            ALTER TABLE "SchoolProfiles" ADD COLUMN IF NOT EXISTS "CourtesyReason" character varying(500) NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "SchoolProfiles" DROP COLUMN IF EXISTS "PaymentAdminStatus";
            ALTER TABLE "SchoolProfiles" DROP COLUMN IF EXISTS "AgreedPriceCop";
            ALTER TABLE "SchoolProfiles" DROP COLUMN IF EXISTS "CourtesyUntil";
            ALTER TABLE "SchoolProfiles" DROP COLUMN IF EXISTS "CourtesyReason";
            """);
    }
}
