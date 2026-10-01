using Cale.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cale.Api.Persistence.Migrations;

/// <summary>
/// Weekly theory-exam schedules, date overrides, seat-based bookings (cancel = status change),
/// manual hour adjustments and the school audit log.
/// Idempotent SQL shared with <see cref="ExamScheduleSchemaGuard"/> — the snapshot drifted from
/// production, and the guard may already have created these objects at startup.
/// </summary>
public partial class AddExamScheduling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var sql in ExamScheduleSchemaGuard.PostgresStatements)
        {
            migrationBuilder.Sql(sql);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP TABLE IF EXISTS "SchoolAuditEntries";""");
        migrationBuilder.Sql("""DROP TABLE IF EXISTS "TrainingHoursAdjustments";""");
        migrationBuilder.Sql("""DROP TABLE IF EXISTS "TheoryExamScheduleOverrides";""");
        migrationBuilder.Sql("""DROP TABLE IF EXISTS "TheoryExamScheduleTemplates";""");
        migrationBuilder.Sql("""DROP INDEX IF EXISTS "UX_TheoryExamAppointments_Seat";""");
        migrationBuilder.Sql("""DROP INDEX IF EXISTS "UX_TheoryExamAppointments_Student";""");
        migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_TheoryExamAppointments_SchoolUserId_StudentUserId_Status";""");
        // Appointment columns are kept: dropping Status would resurrect cancelled bookings.
    }
}
