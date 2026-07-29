using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebScraper.Migrations
{
    /// <inheritdoc />
    public partial class Block1IdentitySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_Teams_AwayTeamId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Teams_HomeTeamId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamGameStats_Teams_TeamId",
                table: "TeamGameStats");

            migrationBuilder.DropIndex(
                name: "IX_Games_HomeTeamId",
                table: "Games");

            migrationBuilder.RenameColumn(
                name: "TeamId",
                table: "TeamGameStats",
                newName: "TeamSeasonId");

            migrationBuilder.RenameIndex(
                name: "IX_TeamGameStats_TeamId",
                table: "TeamGameStats",
                newName: "IX_TeamGameStats_TeamSeasonId");

            migrationBuilder.RenameIndex(
                name: "IX_TeamGameStats_GameId_TeamId",
                table: "TeamGameStats",
                newName: "IX_TeamGameStats_GameId_TeamSeasonId");

            migrationBuilder.RenameColumn(
                name: "HomeTeamId",
                table: "Games",
                newName: "SeasonType");

            migrationBuilder.RenameColumn(
                name: "AwayTeamId",
                table: "Games",
                newName: "HomeTeamSeasonId");

            migrationBuilder.RenameIndex(
                name: "IX_Games_AwayTeamId",
                table: "Games",
                newName: "IX_Games_HomeTeamSeasonId");

            migrationBuilder.AddColumn<int>(
                name: "ParentJobId",
                table: "ScrapeJobs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeasonType",
                table: "ScrapeJobs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayTeamSeasonId",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Franchises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CanonicalAbbreviation = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    DataSource = table.Column<string>(type: "TEXT", nullable: true),
                    DataSourceFetchedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataSourceRecordId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    DeleteReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Franchises", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamSeasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FranchiseId = table.Column<int>(type: "INTEGER", nullable: false),
                    Season = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Abbreviation = table.Column<string>(type: "TEXT", nullable: false),
                    City = table.Column<string>(type: "TEXT", nullable: false),
                    Conference = table.Column<string>(type: "TEXT", nullable: false),
                    Division = table.Column<string>(type: "TEXT", nullable: false),
                    DataSource = table.Column<string>(type: "TEXT", nullable: true),
                    DataSourceFetchedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataSourceRecordId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    DeleteReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamSeasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamSeasons_Franchises_FranchiseId",
                        column: x => x.FranchiseId,
                        principalTable: "Franchises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerTeamSeasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamSeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Season = table.Column<int>(type: "INTEGER", nullable: false),
                    DataSource = table.Column<string>(type: "TEXT", nullable: true),
                    DataSourceFetchedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataSourceRecordId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    DeleteReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerTeamSeasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerTeamSeasons_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerTeamSeasons_TeamSeasons_TeamSeasonId",
                        column: x => x.TeamSeasonId,
                        principalTable: "TeamSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrapeJobs_ParentJobId",
                table: "ScrapeJobs",
                column: "ParentJobId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_EspnId",
                table: "Players",
                column: "EspnId",
                unique: true,
                filter: "\"EspnId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Games_AwayTeamSeasonId",
                table: "Games",
                column: "AwayTeamSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Season_SeasonType_Week_HomeTeamSeasonId_AwayTeamSeasonId",
                table: "Games",
                columns: new[] { "Season", "SeasonType", "Week", "HomeTeamSeasonId", "AwayTeamSeasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Franchises_CanonicalAbbreviation",
                table: "Franchises",
                column: "CanonicalAbbreviation",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTeamSeasons_PlayerId_TeamSeasonId",
                table: "PlayerTeamSeasons",
                columns: new[] { "PlayerId", "TeamSeasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTeamSeasons_TeamSeasonId",
                table: "PlayerTeamSeasons",
                column: "TeamSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamSeasons_FranchiseId_Season",
                table: "TeamSeasons",
                columns: new[] { "FranchiseId", "Season" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_TeamSeasons_AwayTeamSeasonId",
                table: "Games",
                column: "AwayTeamSeasonId",
                principalTable: "TeamSeasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_TeamSeasons_HomeTeamSeasonId",
                table: "Games",
                column: "HomeTeamSeasonId",
                principalTable: "TeamSeasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamGameStats_TeamSeasons_TeamSeasonId",
                table: "TeamGameStats",
                column: "TeamSeasonId",
                principalTable: "TeamSeasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_TeamSeasons_AwayTeamSeasonId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_TeamSeasons_HomeTeamSeasonId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamGameStats_TeamSeasons_TeamSeasonId",
                table: "TeamGameStats");

            migrationBuilder.DropTable(
                name: "PlayerTeamSeasons");

            migrationBuilder.DropTable(
                name: "TeamSeasons");

            migrationBuilder.DropTable(
                name: "Franchises");

            migrationBuilder.DropIndex(
                name: "IX_ScrapeJobs_ParentJobId",
                table: "ScrapeJobs");

            migrationBuilder.DropIndex(
                name: "IX_Players_EspnId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Games_AwayTeamSeasonId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_Season_SeasonType_Week_HomeTeamSeasonId_AwayTeamSeasonId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ParentJobId",
                table: "ScrapeJobs");

            migrationBuilder.DropColumn(
                name: "SeasonType",
                table: "ScrapeJobs");

            migrationBuilder.DropColumn(
                name: "AwayTeamSeasonId",
                table: "Games");

            migrationBuilder.RenameColumn(
                name: "TeamSeasonId",
                table: "TeamGameStats",
                newName: "TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_TeamGameStats_TeamSeasonId",
                table: "TeamGameStats",
                newName: "IX_TeamGameStats_TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_TeamGameStats_GameId_TeamSeasonId",
                table: "TeamGameStats",
                newName: "IX_TeamGameStats_GameId_TeamId");

            migrationBuilder.RenameColumn(
                name: "SeasonType",
                table: "Games",
                newName: "HomeTeamId");

            migrationBuilder.RenameColumn(
                name: "HomeTeamSeasonId",
                table: "Games",
                newName: "AwayTeamId");

            migrationBuilder.RenameIndex(
                name: "IX_Games_HomeTeamSeasonId",
                table: "Games",
                newName: "IX_Games_AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_HomeTeamId",
                table: "Games",
                column: "HomeTeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Teams_AwayTeamId",
                table: "Games",
                column: "AwayTeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Teams_HomeTeamId",
                table: "Games",
                column: "HomeTeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamGameStats_Teams_TeamId",
                table: "TeamGameStats",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
