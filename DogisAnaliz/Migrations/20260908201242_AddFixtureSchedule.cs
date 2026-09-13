using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DogisAnaliz.Migrations
{
    /// <inheritdoc />
    public partial class AddFixtureSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FixtureSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApiFixtureId = table.Column<int>(type: "integer", nullable: false),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    Week = table.Column<int>(type: "integer", nullable: false),
                    Round = table.Column<string>(type: "text", nullable: false),
                    KickoffUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HomeTeamId = table.Column<int>(type: "integer", nullable: false),
                    AwayTeamId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    HomeGoals = table.Column<int>(type: "integer", nullable: true),
                    AwayGoals = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixtureSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixtureSchedules_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixtureSchedules_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixtureSchedules_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixtureSchedules_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FixtureSchedules_ApiFixtureId",
                table: "FixtureSchedules",
                column: "ApiFixtureId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixtureSchedules_AwayTeamId",
                table: "FixtureSchedules",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_FixtureSchedules_HomeTeamId",
                table: "FixtureSchedules",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_FixtureSchedules_KickoffUtc",
                table: "FixtureSchedules",
                column: "KickoffUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FixtureSchedules_LeagueId_SeasonId_Week",
                table: "FixtureSchedules",
                columns: new[] { "LeagueId", "SeasonId", "Week" });

            migrationBuilder.CreateIndex(
                name: "IX_FixtureSchedules_SeasonId",
                table: "FixtureSchedules",
                column: "SeasonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FixtureSchedules");
        }
    }
}
