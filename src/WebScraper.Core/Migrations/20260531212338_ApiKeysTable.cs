using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebScraper.Migrations
{
    /// <inheritdoc />
    public partial class ApiKeysTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KeyId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    HashedKey = table.Column<string>(type: "TEXT", nullable: false),
                    Scopes = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
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
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_HashedKey",
                table: "ApiKeys",
                column: "HashedKey");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyId",
                table: "ApiKeys",
                column: "KeyId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeys");
        }
    }
}
