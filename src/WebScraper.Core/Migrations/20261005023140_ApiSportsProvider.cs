using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebScraper.Migrations
{
    /// <inheritdoc />
    public partial class ApiSportsProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InjuryReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamSeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: true),
                    ExternalPlayerId = table.Column<string>(type: "TEXT", nullable: false),
                    PlayerName = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SnapshotAt = table.Column<DateTime>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_InjuryReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InjuryReports_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InjuryReports_TeamSeasons_TeamSeasonId",
                        column: x => x.TeamSeasonId,
                        principalTable: "TeamSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Players_DataSource_DataSourceRecordId",
                table: "Players",
                columns: new[] { "DataSource", "DataSourceRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_Games_DataSource_DataSourceRecordId",
                table: "Games",
                columns: new[] { "DataSource", "DataSourceRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_InjuryReports_ExternalPlayerId_SnapshotAt",
                table: "InjuryReports",
                columns: new[] { "ExternalPlayerId", "SnapshotAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InjuryReports_PlayerId",
                table: "InjuryReports",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_InjuryReports_TeamSeasonId",
                table: "InjuryReports",
                column: "TeamSeasonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InjuryReports");

            migrationBuilder.DropIndex(
                name: "IX_Players_DataSource_DataSourceRecordId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Games_DataSource_DataSourceRecordId",
                table: "Games");
        }
    }
}
