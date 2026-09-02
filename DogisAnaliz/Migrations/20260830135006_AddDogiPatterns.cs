using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DogisAnaliz.Migrations
{
    /// <inheritdoc />
    public partial class AddDogiPatterns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DogiPatterns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    TriggerMatch1Id = table.Column<int>(type: "integer", nullable: false),
                    TriggerMatch2Id = table.Column<int>(type: "integer", nullable: false),
                    AlertMatchId = table.Column<int>(type: "integer", nullable: true),
                    PatternType = table.Column<string>(type: "text", nullable: false),
                    AlertResult = table.Column<string>(type: "text", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DogiPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DogiPatterns_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DogiPatterns_Matches_AlertMatchId",
                        column: x => x.AlertMatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DogiPatterns_Matches_TriggerMatch1Id",
                        column: x => x.TriggerMatch1Id,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DogiPatterns_Matches_TriggerMatch2Id",
                        column: x => x.TriggerMatch2Id,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DogiPatterns_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DogiPatterns_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DogiPatterns_AlertMatchId",
                table: "DogiPatterns",
                column: "AlertMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_DogiPatterns_AlertResult",
                table: "DogiPatterns",
                column: "AlertResult");

            migrationBuilder.CreateIndex(
                name: "IX_DogiPatterns_LeagueId",
                table: "DogiPatterns",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_DogiPatterns_SeasonId",
                table: "DogiPatterns",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_DogiPatterns_TeamId",
                table: "DogiPatterns",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_DogiPatterns_TriggerMatch1Id_TriggerMatch2Id_TeamId",
                table: "DogiPatterns",
                columns: new[] { "TriggerMatch1Id", "TriggerMatch2Id", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DogiPatterns_TriggerMatch2Id",
                table: "DogiPatterns",
                column: "TriggerMatch2Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DogiPatterns");
        }
    }
}
