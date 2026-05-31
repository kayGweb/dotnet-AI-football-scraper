using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebScraper.Migrations
{
    /// <inheritdoc />
    public partial class AuditableAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Venues",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "Venues",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataSourceFetchedAt",
                table: "Venues",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceRecordId",
                table: "Venues",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeleteReason",
                table: "Venues",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Venues",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Venues",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Venues",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Venues",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Teams",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "Teams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataSourceFetchedAt",
                table: "Teams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceRecordId",
                table: "Teams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeleteReason",
                table: "Teams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Teams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Teams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Teams",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Teams",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TeamGameStats",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "TeamGameStats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataSourceFetchedAt",
                table: "TeamGameStats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceRecordId",
                table: "TeamGameStats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeleteReason",
                table: "TeamGameStats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "TeamGameStats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "TeamGameStats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TeamGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "TeamGameStats",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Players",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "Players",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataSourceFetchedAt",
                table: "Players",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceRecordId",
                table: "Players",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeleteReason",
                table: "Players",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Players",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Players",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Players",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "PlayerGameStats",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "PlayerGameStats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataSourceFetchedAt",
                table: "PlayerGameStats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceRecordId",
                table: "PlayerGameStats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeleteReason",
                table: "PlayerGameStats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "PlayerGameStats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "PlayerGameStats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PlayerGameStats",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Injuries",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "Injuries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataSourceFetchedAt",
                table: "Injuries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceRecordId",
                table: "Injuries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeleteReason",
                table: "Injuries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Injuries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Injuries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Injuries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Injuries",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Games",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataSourceFetchedAt",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceRecordId",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeleteReason",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Games",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ApiLinks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "ApiLinks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataSourceFetchedAt",
                table: "ApiLinks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceRecordId",
                table: "ApiLinks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeleteReason",
                table: "ApiLinks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ApiLinks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "ApiLinks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ApiLinks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ApiLinks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "ApiQueryLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApiKeyId = table.Column<string>(type: "TEXT", nullable: true),
                    ApiKeyName = table.Column<string>(type: "TEXT", nullable: true),
                    Method = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    QueryString = table.Column<string>(type: "TEXT", nullable: true),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponseBytes = table.Column<int>(type: "INTEGER", nullable: false),
                    UserAgent = table.Column<string>(type: "TEXT", nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiQueryLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiQueryLogs_ApiKeyId_Timestamp",
                table: "ApiQueryLogs",
                columns: new[] { "ApiKeyId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiQueryLogs_Timestamp",
                table: "ApiQueryLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiQueryLogs");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "DataSourceFetchedAt",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "DataSourceRecordId",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "DeleteReason",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "DataSourceFetchedAt",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "DataSourceRecordId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "DeleteReason",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TeamGameStats");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "TeamGameStats");

            migrationBuilder.DropColumn(
                name: "DataSourceFetchedAt",
                table: "TeamGameStats");

            migrationBuilder.DropColumn(
                name: "DataSourceRecordId",
                table: "TeamGameStats");

            migrationBuilder.DropColumn(
                name: "DeleteReason",
                table: "TeamGameStats");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "TeamGameStats");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "TeamGameStats");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TeamGameStats");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "TeamGameStats");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DataSourceFetchedAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DataSourceRecordId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DeleteReason",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "DataSourceFetchedAt",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "DataSourceRecordId",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "DeleteReason",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Injuries");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "Injuries");

            migrationBuilder.DropColumn(
                name: "DataSourceFetchedAt",
                table: "Injuries");

            migrationBuilder.DropColumn(
                name: "DataSourceRecordId",
                table: "Injuries");

            migrationBuilder.DropColumn(
                name: "DeleteReason",
                table: "Injuries");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Injuries");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Injuries");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Injuries");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Injuries");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "DataSourceFetchedAt",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "DataSourceRecordId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "DeleteReason",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ApiLinks");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "ApiLinks");

            migrationBuilder.DropColumn(
                name: "DataSourceFetchedAt",
                table: "ApiLinks");

            migrationBuilder.DropColumn(
                name: "DataSourceRecordId",
                table: "ApiLinks");

            migrationBuilder.DropColumn(
                name: "DeleteReason",
                table: "ApiLinks");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ApiLinks");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ApiLinks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ApiLinks");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ApiLinks");
        }
    }
}
