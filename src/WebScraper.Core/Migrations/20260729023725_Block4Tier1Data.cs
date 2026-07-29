using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebScraper.Migrations
{
    /// <inheritdoc />
    public partial class Block4Tier1Data : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GamesWithOdds",
                table: "SeasonCoverages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BroadcastNetworks",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameDrives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    EspnDriveId = table.Column<string>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamSeasonId = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    StartPeriod = table.Column<int>(type: "INTEGER", nullable: true),
                    EndPeriod = table.Column<int>(type: "INTEGER", nullable: true),
                    TimeElapsed = table.Column<string>(type: "TEXT", nullable: true),
                    Yards = table.Column<int>(type: "INTEGER", nullable: false),
                    OffensivePlays = table.Column<int>(type: "INTEGER", nullable: false),
                    IsScore = table.Column<bool>(type: "INTEGER", nullable: false),
                    Result = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayResult = table.Column<string>(type: "TEXT", nullable: true),
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
                    table.PrimaryKey("PK_GameDrives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameDrives_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameDrives_TeamSeasons_TeamSeasonId",
                        column: x => x.TeamSeasonId,
                        principalTable: "TeamSeasons",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GameOdds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sportsbook = table.Column<string>(type: "TEXT", nullable: false),
                    Spread = table.Column<double>(type: "REAL", nullable: true),
                    OverUnder = table.Column<double>(type: "REAL", nullable: true),
                    HomeMoneyline = table.Column<int>(type: "INTEGER", nullable: true),
                    AwayMoneyline = table.Column<int>(type: "INTEGER", nullable: true),
                    SnapshotType = table.Column<int>(type: "INTEGER", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
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
                    table.PrimaryKey("PK_GameOdds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameOdds_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameOfficials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
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
                    table.PrimaryKey("PK_GameOfficials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameOfficials_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameWeathers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    TemperatureF = table.Column<int>(type: "INTEGER", nullable: true),
                    HighTemperatureF = table.Column<int>(type: "INTEGER", nullable: true),
                    Condition = table.Column<string>(type: "TEXT", nullable: true),
                    WindSpeedMph = table.Column<int>(type: "INTEGER", nullable: true),
                    WindDirection = table.Column<string>(type: "TEXT", nullable: true),
                    HumidityPercent = table.Column<int>(type: "INTEGER", nullable: true),
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
                    table.PrimaryKey("PK_GameWeathers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameWeathers_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScoringPlays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    EspnPlayId = table.Column<string>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamSeasonId = table.Column<int>(type: "INTEGER", nullable: true),
                    Period = table.Column<int>(type: "INTEGER", nullable: false),
                    Clock = table.Column<string>(type: "TEXT", nullable: true),
                    PlayType = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    HomeScore = table.Column<int>(type: "INTEGER", nullable: false),
                    AwayScore = table.Column<int>(type: "INTEGER", nullable: false),
                    ScoringType = table.Column<string>(type: "TEXT", nullable: true),
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
                    table.PrimaryKey("PK_ScoringPlays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoringPlays_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScoringPlays_TeamSeasons_TeamSeasonId",
                        column: x => x.TeamSeasonId,
                        principalTable: "TeamSeasons",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameDrives_GameId_EspnDriveId",
                table: "GameDrives",
                columns: new[] { "GameId", "EspnDriveId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameDrives_TeamSeasonId",
                table: "GameDrives",
                column: "TeamSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_GameOdds_GameId_Sportsbook_SnapshotType_CapturedAt",
                table: "GameOdds",
                columns: new[] { "GameId", "Sportsbook", "SnapshotType", "CapturedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameOfficials_GameId",
                table: "GameOfficials",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameWeathers_GameId",
                table: "GameWeathers",
                column: "GameId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoringPlays_GameId_EspnPlayId",
                table: "ScoringPlays",
                columns: new[] { "GameId", "EspnPlayId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoringPlays_TeamSeasonId",
                table: "ScoringPlays",
                column: "TeamSeasonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameDrives");

            migrationBuilder.DropTable(
                name: "GameOdds");

            migrationBuilder.DropTable(
                name: "GameOfficials");

            migrationBuilder.DropTable(
                name: "GameWeathers");

            migrationBuilder.DropTable(
                name: "ScoringPlays");

            migrationBuilder.DropColumn(
                name: "GamesWithOdds",
                table: "SeasonCoverages");

            migrationBuilder.DropColumn(
                name: "BroadcastNetworks",
                table: "Games");
        }
    }
}
