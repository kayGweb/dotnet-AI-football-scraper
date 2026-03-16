using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebScraper.Migrations
{
    /// <inheritdoc />
    public partial class ExpandedSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Teams",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Division",
                table: "Teams",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Conference",
                table: "Teams",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Teams",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Abbreviation",
                table: "Teams",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Teams",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<int>(
                name: "Weight",
                table: "Players",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TeamId",
                table: "Players",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Position",
                table: "Players",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Players",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "JerseyNumber",
                table: "Players",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Height",
                table: "Players",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "College",
                table: "Players",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<string>(
                name: "EspnId",
                table: "Players",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RushYards",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "RushTouchdowns",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "RushAttempts",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Receptions",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "ReceivingYards",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "ReceivingTouchdowns",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "PlayerId",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "PassYards",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "PassTouchdowns",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "PassCompletions",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "PassAttempts",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Interceptions",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "GameId",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<double>(
                name: "AdjQBR",
                table: "PlayerGameStats",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DefensiveSacks",
                table: "PlayerGameStats",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "DefensiveTouchdowns",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExtraPointAttempts",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExtraPointsMade",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FieldGoalAttempts",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FieldGoalsMade",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Fumbles",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FumblesLost",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FumblesRecovered",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "GrossAvgPuntYards",
                table: "PlayerGameStats",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "InterceptionTouchdowns",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InterceptionYards",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InterceptionsCaught",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KickReturnTouchdowns",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KickReturnYards",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KickReturns",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LongFieldGoal",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LongKickReturn",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LongPunt",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LongPuntReturn",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LongReception",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LongRushing",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PassesDefended",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PuntReturnTouchdowns",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PuntReturnYards",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PuntReturns",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PuntTouchbacks",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PuntYards",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Punts",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PuntsInside20",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QBHits",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "QBRating",
                table: "PlayerGameStats",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceivingTargets",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SackYardsLost",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Sacks",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SoloTackles",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TacklesForLoss",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalKickingPoints",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalTackles",
                table: "PlayerGameStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "YardsPerReception",
                table: "PlayerGameStats",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AlterColumn<int>(
                name: "Week",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Season",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "HomeTeamId",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "HomeScore",
                table: "Games",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "GameDate",
                table: "Games",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<int>(
                name: "AwayTeamId",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "AwayScore",
                table: "Games",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<int>(
                name: "Attendance",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayOT",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayQ1",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayQ2",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayQ3",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayQ4",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EspnEventId",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameStatus",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeOT",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeQ1",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeQ2",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeQ3",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeQ4",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HomeWinner",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeutralSite",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VenueId",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApiLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    EndpointType = table.Column<string>(type: "TEXT", nullable: false),
                    RelationType = table.Column<string>(type: "TEXT", nullable: false),
                    GameId = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: true),
                    Season = table.Column<int>(type: "INTEGER", nullable: true),
                    Week = table.Column<int>(type: "INTEGER", nullable: true),
                    EspnEventId = table.Column<string>(type: "TEXT", nullable: true),
                    DiscoveredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiLinks_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ApiLinks_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Injuries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: true),
                    EspnAthleteId = table.Column<string>(type: "TEXT", nullable: false),
                    PlayerName = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    InjuryType = table.Column<string>(type: "TEXT", nullable: false),
                    BodyLocation = table.Column<string>(type: "TEXT", nullable: false),
                    Side = table.Column<string>(type: "TEXT", nullable: false),
                    Detail = table.Column<string>(type: "TEXT", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReportDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Injuries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Injuries_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Injuries_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TeamGameStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstDowns = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstDownsPassing = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstDownsRushing = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstDownsPenalty = table.Column<int>(type: "INTEGER", nullable: false),
                    ThirdDownMade = table.Column<int>(type: "INTEGER", nullable: false),
                    ThirdDownAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    FourthDownMade = table.Column<int>(type: "INTEGER", nullable: false),
                    FourthDownAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPlays = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalYards = table.Column<int>(type: "INTEGER", nullable: false),
                    NetPassingYards = table.Column<int>(type: "INTEGER", nullable: false),
                    PassCompletions = table.Column<int>(type: "INTEGER", nullable: false),
                    PassAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    YardsPerPass = table.Column<double>(type: "REAL", nullable: false),
                    InterceptionsThrown = table.Column<int>(type: "INTEGER", nullable: false),
                    SacksAgainst = table.Column<int>(type: "INTEGER", nullable: false),
                    SackYardsLost = table.Column<int>(type: "INTEGER", nullable: false),
                    RushingYards = table.Column<int>(type: "INTEGER", nullable: false),
                    RushingAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    YardsPerRush = table.Column<double>(type: "REAL", nullable: false),
                    RedZoneMade = table.Column<int>(type: "INTEGER", nullable: false),
                    RedZoneAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    Turnovers = table.Column<int>(type: "INTEGER", nullable: false),
                    FumblesLost = table.Column<int>(type: "INTEGER", nullable: false),
                    Penalties = table.Column<int>(type: "INTEGER", nullable: false),
                    PenaltyYards = table.Column<int>(type: "INTEGER", nullable: false),
                    DefensiveTouchdowns = table.Column<int>(type: "INTEGER", nullable: false),
                    PossessionTime = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamGameStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamGameStats_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamGameStats_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Venues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EspnId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    City = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    Country = table.Column<string>(type: "TEXT", nullable: false),
                    IsGrass = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsIndoor = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_VenueId",
                table: "Games",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiLinks_GameId",
                table: "ApiLinks",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiLinks_TeamId",
                table: "ApiLinks",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiLinks_Url",
                table: "ApiLinks",
                column: "Url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Injuries_GameId_EspnAthleteId",
                table: "Injuries",
                columns: new[] { "GameId", "EspnAthleteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Injuries_PlayerId",
                table: "Injuries",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamGameStats_GameId_TeamId",
                table: "TeamGameStats",
                columns: new[] { "GameId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamGameStats_TeamId",
                table: "TeamGameStats",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Venues_EspnId",
                table: "Venues",
                column: "EspnId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Venues_VenueId",
                table: "Games",
                column: "VenueId",
                principalTable: "Venues",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_Venues_VenueId",
                table: "Games");

            migrationBuilder.DropTable(
                name: "ApiLinks");

            migrationBuilder.DropTable(
                name: "Injuries");

            migrationBuilder.DropTable(
                name: "TeamGameStats");

            migrationBuilder.DropTable(
                name: "Venues");

            migrationBuilder.DropIndex(
                name: "IX_Games_VenueId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "EspnId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "AdjQBR",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "DefensiveSacks",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "DefensiveTouchdowns",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "ExtraPointAttempts",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "ExtraPointsMade",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "FieldGoalAttempts",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "FieldGoalsMade",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "Fumbles",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "FumblesLost",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "FumblesRecovered",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "GrossAvgPuntYards",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "InterceptionTouchdowns",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "InterceptionYards",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "InterceptionsCaught",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "KickReturnTouchdowns",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "KickReturnYards",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "KickReturns",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "LongFieldGoal",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "LongKickReturn",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "LongPunt",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "LongPuntReturn",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "LongReception",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "LongRushing",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "PassesDefended",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "PuntReturnTouchdowns",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "PuntReturnYards",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "PuntReturns",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "PuntTouchbacks",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "PuntYards",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "Punts",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "PuntsInside20",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "QBHits",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "QBRating",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "ReceivingTargets",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "SackYardsLost",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "Sacks",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "SoloTackles",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "TacklesForLoss",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "TotalKickingPoints",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "TotalTackles",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "YardsPerReception",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "Attendance",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AwayOT",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AwayQ1",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AwayQ2",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AwayQ3",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AwayQ4",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "EspnEventId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "GameStatus",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HomeOT",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HomeQ1",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HomeQ2",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HomeQ3",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HomeQ4",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HomeWinner",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "NeutralSite",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "VenueId",
                table: "Games");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Teams",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Division",
                table: "Teams",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Conference",
                table: "Teams",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Teams",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Abbreviation",
                table: "Teams",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Teams",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<int>(
                name: "Weight",
                table: "Players",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TeamId",
                table: "Players",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Position",
                table: "Players",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Players",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "JerseyNumber",
                table: "Players",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Height",
                table: "Players",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "College",
                table: "Players",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Players",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<int>(
                name: "RushYards",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "RushTouchdowns",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "RushAttempts",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "Receptions",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ReceivingYards",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ReceivingTouchdowns",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "PlayerId",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "PassYards",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "PassTouchdowns",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "PassCompletions",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "PassAttempts",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "Interceptions",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "GameId",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<int>(
                name: "Week",
                table: "Games",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "Season",
                table: "Games",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "HomeTeamId",
                table: "Games",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "HomeScore",
                table: "Games",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "GameDate",
                table: "Games",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "AwayTeamId",
                table: "Games",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "AwayScore",
                table: "Games",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Games",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);
        }
    }
}
