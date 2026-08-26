using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DogisAnaliz.Migrations
{
    /// <inheritdoc />
    public partial class RefactorSurpriseArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Leagues_LeagueId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Teams_LeagueId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "LeagueId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AdditionalDataJson",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "IsSurprise",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ResultCode",
                table: "Matches");

            migrationBuilder.RenameColumn(
                name: "Season",
                table: "Matches",
                newName: "SeasonId");

            migrationBuilder.CreateTable(
                name: "MatchDetails",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "integer", nullable: false),
                    Referee = table.Column<string>(type: "text", nullable: true),
                    AdditionalDataJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchDetails", x => x.MatchId);
                    table.ForeignKey(
                        name: "FK_MatchDetails_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchSurprises",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "integer", nullable: false),
                    IyMsCode = table.Column<string>(type: "text", nullable: false),
                    TotalGoals = table.Column<int>(type: "integer", nullable: false),
                    IsTurnaround = table.Column<bool>(type: "boolean", nullable: false),
                    IsHighGoal = table.Column<bool>(type: "boolean", nullable: false),
                    IsDoubleSurprise = table.Column<bool>(type: "boolean", nullable: false),
                    SurpriseType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSurprises", x => x.MatchId);
                    table.ForeignKey(
                        name: "FK_MatchSurprises_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeasonName = table.Column<string>(type: "text", nullable: false),
                    StartYear = table.Column<int>(type: "integer", nullable: false),
                    EndYear = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_SeasonId",
                table: "Matches",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchSurprises_IsHighGoal",
                table: "MatchSurprises",
                column: "IsHighGoal");

            migrationBuilder.CreateIndex(
                name: "IX_MatchSurprises_IsTurnaround",
                table: "MatchSurprises",
                column: "IsTurnaround");

            migrationBuilder.CreateIndex(
                name: "IX_MatchSurprises_SurpriseType",
                table: "MatchSurprises",
                column: "SurpriseType");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Seasons_SeasonId",
                table: "Matches",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Seasons_SeasonId",
                table: "Matches");

            migrationBuilder.DropTable(
                name: "MatchDetails");

            migrationBuilder.DropTable(
                name: "MatchSurprises");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_Matches_SeasonId",
                table: "Matches");

            migrationBuilder.RenameColumn(
                name: "SeasonId",
                table: "Matches",
                newName: "Season");

            migrationBuilder.AddColumn<int>(
                name: "LeagueId",
                table: "Teams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AdditionalDataJson",
                table: "Matches",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSurprise",
                table: "Matches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ResultCode",
                table: "Matches",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_LeagueId",
                table: "Teams",
                column: "LeagueId");

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Leagues_LeagueId",
                table: "Teams",
                column: "LeagueId",
                principalTable: "Leagues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
