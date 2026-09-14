using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DogisAnaliz.Migrations
{
    /// <inheritdoc />
    public partial class AddStandingsTeamStatsPredictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApiTeamId",
                table: "Teams",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FixturePredictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApiFixtureId = table.Column<int>(type: "integer", nullable: false),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    HomeTeamId = table.Column<int>(type: "integer", nullable: false),
                    AwayTeamId = table.Column<int>(type: "integer", nullable: false),
                    WinnerTeamId = table.Column<int>(type: "integer", nullable: true),
                    WinnerComment = table.Column<string>(type: "text", nullable: true),
                    WinOrDraw = table.Column<bool>(type: "boolean", nullable: false),
                    UnderOver = table.Column<string>(type: "text", nullable: true),
                    GoalsHome = table.Column<string>(type: "text", nullable: true),
                    GoalsAway = table.Column<string>(type: "text", nullable: true),
                    Advice = table.Column<string>(type: "text", nullable: true),
                    PercentHome = table.Column<double>(type: "double precision", nullable: false),
                    PercentDraw = table.Column<double>(type: "double precision", nullable: false),
                    PercentAway = table.Column<double>(type: "double precision", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixturePredictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixturePredictions_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixturePredictions_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixturePredictions_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixturePredictions_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Standings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    GoalsDiff = table.Column<int>(type: "integer", nullable: false),
                    Form = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Played = table.Column<int>(type: "integer", nullable: false),
                    Win = table.Column<int>(type: "integer", nullable: false),
                    Draw = table.Column<int>(type: "integer", nullable: false),
                    Lose = table.Column<int>(type: "integer", nullable: false),
                    GoalsFor = table.Column<int>(type: "integer", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "integer", nullable: false),
                    HomePlayed = table.Column<int>(type: "integer", nullable: false),
                    HomeWin = table.Column<int>(type: "integer", nullable: false),
                    HomeDraw = table.Column<int>(type: "integer", nullable: false),
                    HomeLose = table.Column<int>(type: "integer", nullable: false),
                    HomeGoalsFor = table.Column<int>(type: "integer", nullable: false),
                    HomeGoalsAgainst = table.Column<int>(type: "integer", nullable: false),
                    AwayPlayed = table.Column<int>(type: "integer", nullable: false),
                    AwayWin = table.Column<int>(type: "integer", nullable: false),
                    AwayDraw = table.Column<int>(type: "integer", nullable: false),
                    AwayLose = table.Column<int>(type: "integer", nullable: false),
                    AwayGoalsFor = table.Column<int>(type: "integer", nullable: false),
                    AwayGoalsAgainst = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Standings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Standings_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Standings_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Standings_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamSeasonStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    Form = table.Column<string>(type: "text", nullable: false),
                    GoalsForAvgHome = table.Column<double>(type: "double precision", nullable: false),
                    GoalsForAvgAway = table.Column<double>(type: "double precision", nullable: false),
                    GoalsForAvgTotal = table.Column<double>(type: "double precision", nullable: false),
                    GoalsAgainstAvgHome = table.Column<double>(type: "double precision", nullable: false),
                    GoalsAgainstAvgAway = table.Column<double>(type: "double precision", nullable: false),
                    GoalsAgainstAvgTotal = table.Column<double>(type: "double precision", nullable: false),
                    CleanSheetsHome = table.Column<int>(type: "integer", nullable: false),
                    CleanSheetsAway = table.Column<int>(type: "integer", nullable: false),
                    CleanSheetsTotal = table.Column<int>(type: "integer", nullable: false),
                    FailedToScoreHome = table.Column<int>(type: "integer", nullable: false),
                    FailedToScoreAway = table.Column<int>(type: "integer", nullable: false),
                    FailedToScoreTotal = table.Column<int>(type: "integer", nullable: false),
                    PenaltyScored = table.Column<int>(type: "integer", nullable: false),
                    PenaltyMissed = table.Column<int>(type: "integer", nullable: false),
                    CardsYellowTotal = table.Column<int>(type: "integer", nullable: false),
                    CardsRedTotal = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamSeasonStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamSeasonStats_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamSeasonStats_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamSeasonStats_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FixturePredictions_ApiFixtureId",
                table: "FixturePredictions",
                column: "ApiFixtureId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixturePredictions_AwayTeamId",
                table: "FixturePredictions",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_FixturePredictions_HomeTeamId",
                table: "FixturePredictions",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_FixturePredictions_LeagueId",
                table: "FixturePredictions",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_FixturePredictions_SeasonId",
                table: "FixturePredictions",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Standings_LeagueId_SeasonId_TeamId",
                table: "Standings",
                columns: new[] { "LeagueId", "SeasonId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Standings_SeasonId",
                table: "Standings",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Standings_TeamId",
                table: "Standings",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamSeasonStats_LeagueId_SeasonId_TeamId",
                table: "TeamSeasonStats",
                columns: new[] { "LeagueId", "SeasonId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamSeasonStats_SeasonId",
                table: "TeamSeasonStats",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamSeasonStats_TeamId",
                table: "TeamSeasonStats",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FixturePredictions");

            migrationBuilder.DropTable(
                name: "Standings");

            migrationBuilder.DropTable(
                name: "TeamSeasonStats");

            migrationBuilder.DropColumn(
                name: "ApiTeamId",
                table: "Teams");
        }
    }
}
