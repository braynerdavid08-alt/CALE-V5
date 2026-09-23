using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cale.Api.Persistence.Migrations;

/// <summary>
/// Adds GameShowSettings singleton + Session.SettingsJson snapshot.
/// Also applied idempotently by GameShowPackSchemaGuard in production.
/// </summary>
public partial class AddGameShowSettingsAndSessionSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH(N'dbo.GameShowSessions', N'SettingsJson') IS NULL
                ALTER TABLE dbo.GameShowSessions ADD SettingsJson nvarchar(max) NOT NULL CONSTRAINT DF_GSS_SettingsJson_Mig DEFAULT(N'');
            IF OBJECT_ID(N'dbo.GameShowSettings', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.GameShowSettings (
                    Id int NOT NULL PRIMARY KEY,
                    PayloadJson nvarchar(max) NOT NULL,
                    UpdatedAt datetimeoffset NOT NULL,
                    UpdatedByUserId int NULL
                );
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.GameShowSettings', N'U') IS NOT NULL
                DROP TABLE dbo.GameShowSettings;
            """);
    }
}
