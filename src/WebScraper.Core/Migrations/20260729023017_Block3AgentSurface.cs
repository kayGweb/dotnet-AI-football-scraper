using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebScraper.Migrations
{
    /// <inheritdoc />
    public partial class Block3AgentSurface : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataCorrections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntityType = table.Column<string>(type: "TEXT", nullable: false),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Field = table.Column<string>(type: "TEXT", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: false),
                    Rationale = table.Column<string>(type: "TEXT", nullable: false),
                    ProposedBy = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResolvedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataCorrections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataCorrections_EntityType_EntityId_Field_Status",
                table: "DataCorrections",
                columns: new[] { "EntityType", "EntityId", "Field", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DataCorrections_Status_CreatedAt",
                table: "DataCorrections",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataCorrections");
        }
    }
}
