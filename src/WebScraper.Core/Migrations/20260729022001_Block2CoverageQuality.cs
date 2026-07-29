using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebScraper.Migrations
{
    /// <inheritdoc />
    public partial class Block2CoverageQuality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DependsOnJobId",
                table: "ScrapeJobs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DataQualityFindings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuleType = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", nullable: false),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: true),
                    Season = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonType = table.Column<int>(type: "INTEGER", nullable: true),
                    Week = table.Column<int>(type: "INTEGER", nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: true),
                    RepairJobId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataQualityFindings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeasonCoverages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Season = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonType = table.Column<int>(type: "INTEGER", nullable: false),
                    Week = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpectedGames = table.Column<int>(type: "INTEGER", nullable: true),
                    ActualGames = table.Column<int>(type: "INTEGER", nullable: false),
                    GamesWithPlayerStats = table.Column<int>(type: "INTEGER", nullable: false),
                    GamesWithTeamStats = table.Column<int>(type: "INTEGER", nullable: false),
                    GamesWithInjuries = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastVerifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonCoverages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrapeJobs_DependsOnJobId",
                table: "ScrapeJobs",
                column: "DependsOnJobId");

            migrationBuilder.CreateIndex(
                name: "IX_DataQualityFindings_RuleType_EntityType_EntityId",
                table: "DataQualityFindings",
                columns: new[] { "RuleType", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_DataQualityFindings_Status_Severity",
                table: "DataQualityFindings",
                columns: new[] { "Status", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonCoverages_Season",
                table: "SeasonCoverages",
                column: "Season");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonCoverages_Season_SeasonType_Week",
                table: "SeasonCoverages",
                columns: new[] { "Season", "SeasonType", "Week" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataQualityFindings");

            migrationBuilder.DropTable(
                name: "SeasonCoverages");

            migrationBuilder.DropIndex(
                name: "IX_ScrapeJobs_DependsOnJobId",
                table: "ScrapeJobs");

            migrationBuilder.DropColumn(
                name: "DependsOnJobId",
                table: "ScrapeJobs");
        }
    }
}
