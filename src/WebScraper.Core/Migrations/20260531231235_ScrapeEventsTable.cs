using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebScraper.Migrations
{
    /// <inheritdoc />
    public partial class ScrapeEventsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScrapeJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Season = table.Column<int>(type: "INTEGER", nullable: true),
                    Week = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordsProcessed = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordsFailed = table.Column<int>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RequestedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapeJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrapeJobs_Status_CreatedAt",
                table: "ScrapeJobs",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScrapeJobs");
        }
    }
}
