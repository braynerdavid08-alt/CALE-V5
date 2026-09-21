using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cale.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameShowTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameShowSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HostUserId = table.Column<int>(type: "integer", nullable: false),
                    SchoolUserId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    JoinCode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TeamAName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TeamBName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TeamAScore = table.Column<int>(type: "integer", nullable: false),
                    TeamBScore = table.Column<int>(type: "integer", nullable: false),
                    CurrentRoundIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameShowSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameShowPlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Team = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    PlayerToken = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsConnected = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameShowPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameShowPlayers_GameShowSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "GameShowSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameShowRounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    QuestionText = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    SourceQuestionId = table.Column<int>(type: "integer", nullable: true),
                    Phase = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ControllingTeam = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    BuzzWinnerTeam = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Strikes = table.Column<int>(type: "integer", nullable: false),
                    RoundPointsForController = table.Column<int>(type: "integer", nullable: false),
                    StealSucceeded = table.Column<bool>(type: "boolean", nullable: false),
                    BuzzOpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameShowRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameShowRounds_GameShowSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "GameShowSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameShowAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoundId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: true),
                    Team = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    RawText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    IsSteal = table.Column<bool>(type: "boolean", nullable: false),
                    MatchedAnswerId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameShowAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameShowAttempts_GameShowRounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "GameShowRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameShowBoardAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoundId = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AliasesJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    IsRevealed = table.Column<bool>(type: "boolean", nullable: false),
                    RevealedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameShowBoardAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameShowBoardAnswers_GameShowRounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "GameShowRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameShowAttempts_RoundId",
                table: "GameShowAttempts",
                column: "RoundId");

            migrationBuilder.CreateIndex(
                name: "IX_GameShowBoardAnswers_RoundId_Rank",
                table: "GameShowBoardAnswers",
                columns: new[] { "RoundId", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameShowPlayers_PlayerToken",
                table: "GameShowPlayers",
                column: "PlayerToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameShowPlayers_SessionId",
                table: "GameShowPlayers",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameShowRounds_SessionId_SortOrder",
                table: "GameShowRounds",
                columns: new[] { "SessionId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameShowSessions_HostUserId",
                table: "GameShowSessions",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameShowSessions_JoinCode",
                table: "GameShowSessions",
                column: "JoinCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameShowSessions_SchoolUserId",
                table: "GameShowSessions",
                column: "SchoolUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameShowAttempts");

            migrationBuilder.DropTable(
                name: "GameShowBoardAnswers");

            migrationBuilder.DropTable(
                name: "GameShowPlayers");

            migrationBuilder.DropTable(
                name: "GameShowRounds");

            migrationBuilder.DropTable(
                name: "GameShowSessions");
        }
    }
}
